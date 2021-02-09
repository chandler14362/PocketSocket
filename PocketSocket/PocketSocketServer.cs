using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Krypton.Buffers;
using PocketSocket.Abstractions;
using PocketSocket.Abstractions.Delegates;
using PocketSocket.Abstractions.Enums;
using PocketSocket.Abstractions.Models;
using PocketSocket.Extensions;
using PocketSocket.Implementations;

namespace PocketSocket
{
    public class PocketSocketServer<TConnection> : IPocketSocketServer<TConnection> 
        where TConnection : class, ISocketConnection
    {
        private readonly PocketSocketConfig _config;

        private readonly ILogger _logger;
        private readonly IMessageRegistry _messageRegistry;
        private readonly ISerializationProvider _serializationProvider;
        private readonly StreamWriterFactoryDelegate _streamWriterFactory;
        private readonly StreamReaderFactoryDelegate _streamReaderFactory;
        private readonly IReadOnlyList<SocketInterfaceModel> _interfaceModels;
        
        private readonly ConcurrentDictionary<ISocketConnection, TcpClient> _activeConnections = new();

        private ConnectionFactoryDelegate<TConnection> _connectionFactory;
        private OnConnectionClosed<TConnection> _onConnectionClosed;
        
        private readonly Dictionary<uint, nint> _messageHandlerPtrs = new();
        private readonly Dictionary<uint, Delegate> _messageHandlers = new();
        
        private TcpListener _tcpListener;

        private readonly CancellationTokenSource _cts = new();
        private Task _processIncomingConnections;
        
        internal PocketSocketServer(
            PocketSocketConfig config,
            ILogger logger,
            IMessageRegistry messageRegistry, 
            ISerializationProvider serializationProvider,
            StreamWriterFactoryDelegate streamWriterFactory,
            StreamReaderFactoryDelegate streamReaderFactory,
            IReadOnlyList<SocketInterfaceModel> interfaceModels)
        {
            _config = config;
            _logger = logger;
            _messageRegistry = messageRegistry;
            _serializationProvider = serializationProvider;
            _streamWriterFactory = streamWriterFactory;
            _streamReaderFactory = streamReaderFactory;
            _interfaceModels = interfaceModels;
        }

        public void Bind(ISocketInterface socketInterface)
        {
            var implementedModels = _interfaceModels.GetModelsApplicableToType(socketInterface.GetType());
            foreach (var implementedModel in implementedModels)
                Bind(socketInterface, implementedModel);
        }

        private void Bind(ISocketInterface socketInterface, SocketInterfaceModel interfaceModel)
        {
            var bindRequestMethod = typeof(PocketSocketServer<TConnection>).GetMethods()
                .First(m => m.Name == "BindRequest");
            foreach (var request in interfaceModel.Requests)
            {
                var genericImpl =
                    bindRequestMethod.MakeGenericMethod(request.RequestType, request.ResponseType);
                genericImpl.Invoke(this, new object[] {socketInterface, request.MethodInfo});
            }

            var bindCommandMethod = typeof(PocketSocketServer<TConnection>).GetMethods()
                .First(m => m.Name == "BindCommand");
            foreach (var command in interfaceModel.Commands)
            {
                var genericImpl = bindCommandMethod.MakeGenericMethod(command.CommandType);
                genericImpl.Invoke(this, new object[] {socketInterface, command.MethodInfo});
            }
        }

        public unsafe void BindRequest<TRequest, TResponse>(ISocketInterface socketInterface, MethodInfo methodInfo)
        {
            var concreteMethodInfo =
                methodInfo.DeclaringType.MakeGenericType(typeof(TConnection)).GetMethod(methodInfo.Name);
            var connectionParameter = Expression.Parameter(typeof(TConnection));
            var requestParameter = Expression.Parameter(typeof(TRequest));
            var onRequest = (OnServerRequestDelegate<TConnection, TRequest, TResponse>)Expression.Lambda(
                    typeof(OnServerRequestDelegate<TConnection, TRequest, TResponse>), 
                    Expression.Call(
                        Expression.Constant(socketInterface), 
                        concreteMethodInfo, 
                        connectionParameter, requestParameter),
                    connectionParameter, requestParameter)
                .Compile();
            var messageId = _messageRegistry.GetMessageId(typeof(TRequest));
            _messageHandlers[messageId] = onRequest;
            delegate*<
                PocketSocketServer<TConnection>,
                TConnection, 
                ReadOnlyMemory<byte>,
                Task> 
                handlerPtr = &OnConnectionRequest<TRequest, TResponse>;
            _messageHandlerPtrs[messageId] = (nint) handlerPtr;
        }

        public unsafe void BindCommand<TCommand>(ISocketInterface socketInterface, MethodInfo methodInfo)
        {
            var concreteMethodInfo =
                methodInfo.DeclaringType.MakeGenericType(typeof(TConnection)).GetMethod(methodInfo.Name);
            var connectionParameter = Expression.Parameter(typeof(TConnection));
            var commandDelegate = Expression.Parameter(typeof(TCommand));
            var onCommand = (OnServerCommandDelegate<TConnection, TCommand>)Expression.Lambda(
                    typeof(OnServerCommandDelegate<TConnection, TCommand>), 
                    Expression.Call(
                        Expression.Constant(socketInterface), 
                        concreteMethodInfo, 
                        connectionParameter, commandDelegate),
                    connectionParameter, commandDelegate)
                .Compile();
            var messageId = _messageRegistry.GetMessageId(typeof(TCommand));
            _messageHandlers[messageId] = onCommand;
            delegate*<
                PocketSocketServer<TConnection>,
                TConnection,
                ReadOnlyMemory<byte>,
                Task>
                handlerPtr = &OnConnectionCommand<TCommand>;
            _messageHandlerPtrs[messageId] = (nint) handlerPtr;
        }
        
        public void Unbind(ISocketInterface socketInterface)
        {
            var implementedModels = _interfaceModels.GetModelsApplicableToType(socketInterface.GetType());
            foreach (var implementedModel in implementedModels)
                Unbind(implementedModel);
        }

        private void Unbind(SocketInterfaceModel interfaceModel)
        {
            foreach (var message in interfaceModel.GetMessageModels())
            {
                var messageId = _messageRegistry.GetMessageId(message);
                _messageHandlerPtrs.Remove(messageId);
                _messageHandlers.Remove(messageId);
            }
        }

        public Task Start(IPAddress ipAddress, int port, OnConnectionClosed<TConnection> onConnectionClosed)
        {
            if (this.GetType() != typeof(PocketSocketServer<ISocketConnection>))
                throw new NotImplementedException();
            return Start(ipAddress, port,
                (streamReader, streamWriter) => new BaseSocketConnection(streamReader, streamWriter) as TConnection,
                onConnectionClosed);
        }
        
        public Task Start(
            IPAddress ipAddress,
            int port,
            ConnectionFactoryDelegate<TConnection> connectionFactory,
            OnConnectionClosed<TConnection> onConnectionClosed)
        {
            _connectionFactory = connectionFactory;
            _onConnectionClosed = onConnectionClosed;
            _tcpListener = new TcpListener(ipAddress, port);
            _tcpListener.Start();
            _cts.Token.Register(() => _tcpListener.Stop());
            _processIncomingConnections = Task.Run(async () =>
            {
                try
                {
                    await ProcessIncomingConnections(_cts.Token);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Exception throw while processing incoming connections.");
                }
            }, _cts.Token);
            return Task.CompletedTask;
        }

        private async Task ProcessIncomingConnections(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _tcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (SocketException)
                {
                    break;
                }
                
                _logger.Debug($"Opened connection: {client.Client.RemoteEndPoint}");
                
                var stream = client.GetStream();
                var streamReader = _streamReaderFactory(stream);
                var streamWriter = _streamWriterFactory(stream);
                var connection = _connectionFactory(streamReader, streamWriter);
                _activeConnections[connection] = client;
                streamWriter.Start();
                streamReader.Start(data =>
                    Task.Run(async () =>
                    {
                        try
                        {
                            await OnConnectionPacket(connection, data).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            _logger.Error(e, "Exception thrown while handling a connection packet.");
                        }
                    }, cancellationToken), 
                    completionStatus => OnConnectionReaderComplete(connection, completionStatus));
            }
        }

        private void OnConnectionReaderComplete(TConnection connection, ReaderCompletionStatus completionStatus)
        {
            var closeStatus = completionStatus switch
            {
                ReaderCompletionStatus.Abrupt => ConnectionCloseStatus.Abrupt,
                ReaderCompletionStatus.Graceful => ConnectionCloseStatus.Graceful
            };
            Task.Run(() => DisposeConnection(connection));
            _onConnectionClosed(connection, closeStatus);
        }

        public async Task DisposeConnection(ISocketConnection connection)
        {
            if (!_activeConnections.TryRemove(connection, out var tcpClient))
                return;
            
            await connection.DisposeAsync().ConfigureAwait(false);
            _logger.Debug($"Closed connection: {tcpClient.Client.RemoteEndPoint}");
            tcpClient.Close();
        }

        private unsafe Task OnConnectionPacket(TConnection connection, ReadOnlyMemory<byte> data)
        {
            var messageId = BinaryPrimitives.ReadUInt32LittleEndian(data.Span);
            if (!_messageHandlerPtrs.TryGetValue(messageId, out var messageHandlerPtr))
                throw new Exception($"Got unknown message id: {messageId}");
            var handler = (delegate*<
                PocketSocketServer<TConnection>, 
                TConnection, 
                ReadOnlyMemory<byte>, 
                Task>) messageHandlerPtr;
            return handler(this, connection, data.Slice(sizeof(uint)));
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            await _processIncomingConnections.ConfigureAwait(false);
            
            foreach (var connection in _activeConnections.Keys)
                await DisposeConnection(connection).ConfigureAwait(false);

            _activeConnections.Clear();
            _cts.Dispose();
        }

        public void Write<TMessage>(ISocketConnection connection, TMessage message)
        {
            var messageId = _messageRegistry.GetMessageId<TMessage>();
            _serializationProvider.Serialize(message, (connection, messageId),
                (data, state) =>
                {
                    var buffer = new SpanBufferWriter(stackalloc byte[2048]);
                    var messageLength = data.Length + sizeof(uint);
                    buffer.WriteUInt16((ushort)messageLength);
                    buffer.WriteUInt32(state.messageId);
                    buffer.WriteBytes(data);
                    state.connection.Write(buffer.Data);
                });
        }

        private void Write<TMessage>(ISocketConnection connection, TMessage message, int requestId)
        {
            var messageId = _messageRegistry.GetMessageId<TMessage>();
            _serializationProvider.Serialize(message, (connection, messageId, requestId),
                (data, state) =>
                {
                    var buffer = new SpanBufferWriter(stackalloc byte[2048]);
                    var messageLength = data.Length + sizeof(uint) + sizeof(int);
                    buffer.WriteUInt16((ushort)messageLength);
                    buffer.WriteUInt32(state.messageId);
                    buffer.WriteInt32(state.requestId);
                    buffer.WriteBytes(data);
                    state.connection.Write(buffer.Data);
                });
        }
        
        private static async Task OnConnectionRequest<TRequest, TResponse>(
            PocketSocketServer<TConnection> server,
            TConnection connection, 
            ReadOnlyMemory<byte> data)
        {
            var messageId = server._messageRegistry.GetMessageId<TRequest>();
            if (!server._messageHandlers.TryGetValue(messageId, out var messageHandler))
                throw new Exception($"Got request {typeof(TRequest).FullName} with no bound handler.");
            var requestId = BinaryPrimitives.ReadInt32LittleEndian(data.Span);
            data = data.Slice(sizeof(int));
            var deserialized = server._serializationProvider.Deserialize<TRequest>(data);
            var resp = await ((OnServerRequestDelegate<TConnection, TRequest, TResponse>) messageHandler)(connection, deserialized).ConfigureAwait(false);
            server.Write(connection, resp, requestId);
        }

        private static Task OnConnectionCommand<TCommand>(
            PocketSocketServer<TConnection> server,
            TConnection connection,
            ReadOnlyMemory<byte> data)
        {
            var messageId = server._messageRegistry.GetMessageId<TCommand>();
            if (!server._messageHandlers.TryGetValue(messageId, out var messageHandler))
                throw new Exception($"Got command {typeof(TCommand).FullName} with no bound handler.");
            var deserialized = server._serializationProvider.Deserialize<TCommand>(data);
            return ((OnServerCommandDelegate<TConnection, TCommand>) messageHandler)(connection, deserialized);
        }
    }
}