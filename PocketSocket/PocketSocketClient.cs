using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
using PocketSocket.Models;
using PocketSocket.Providers;

namespace PocketSocket
{
    public class PocketSocketClient : IPocketSocketClient
    {
        private readonly PocketSocketConfig _config;
        private readonly IMessageRegistry _messageRegistry;
        private readonly ISerializationProvider _serializationProvider;
        private readonly ICorrelationIdProvider _correlationIdProvider;
        
        private readonly TcpClient _tcpClient;
        private readonly ILogger _logger;
        
        private readonly IReadOnlyList<SocketInterfaceModel> _interfaceModels;
        private readonly IReadOnlyList<SocketEventHandlerModel> _eventHandlerModels;

        private readonly Dictionary<uint, nint> _messageHandlerPtrs = new();
        private readonly Dictionary<uint, Delegate> _messageHandlers = new();
        
        private readonly ConcurrentDictionary<int, ServerRequestModel> _pendingRequests = new();

        private readonly CancellationTokenSource _cts = new();
        
        private OnConnectionClosed<IPocketSocketClient> _onConnectionClosed;
        private Task _processMessagesTask;

        internal PocketSocketClient(
            PocketSocketConfig config, 
            IMessageRegistry messageRegistry, 
            ISerializationProvider serializationProvider,
            ICorrelationIdProvider correlationIdProvider,
            ILogger logger,
            IReadOnlyList<SocketInterfaceModel> interfaceModels,
            IReadOnlyList<SocketEventHandlerModel> eventHandlerModels)
        {
            _config = config;
            _messageRegistry = messageRegistry;
            _serializationProvider = serializationProvider;
            _correlationIdProvider = correlationIdProvider;
            _tcpClient = new TcpClient();
            _logger = logger;
            _interfaceModels = interfaceModels;
            _eventHandlerModels = eventHandlerModels;
        }

        public async Task Start(string hostname, int port, OnConnectionClosed<IPocketSocketClient> onConnectionClosed)
        {
            _onConnectionClosed = onConnectionClosed;
            await _tcpClient.ConnectAsync(hostname, port, _cts.Token);
            _processMessagesTask = Task.Run(async () =>
            {
                try
                {
                    await ProcessIncomingMessages(_cts.Token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Exception thrown while processing incoming messages.");
                }
            }, _cts.Token);
        }

        private async Task ProcessIncomingMessages(CancellationToken cancellationToken)
        {
            static async ValueTask<ConnectionCloseStatus?> ReadAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
            {
                try
                {
                    var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    return read == buffer.Length ? null : ConnectionCloseStatus.Abrupt;
                }
                catch (ObjectDisposedException)
                {
                    return ConnectionCloseStatus.Graceful;
                }
                catch (Exception)
                {
                    return ConnectionCloseStatus.Abrupt;
                }
            }

            ConnectionCloseStatus? closeStatus = default;
            var messageHeader = new byte[2];
            var stream = _tcpClient.GetStream();
            while (!cancellationToken.IsCancellationRequested)
            {
                closeStatus = await ReadAsync(stream, messageHeader, cancellationToken).ConfigureAwait(false);
                if (closeStatus != null)
                    break;
                var messageLength = BinaryPrimitives.ReadUInt16LittleEndian(messageHeader);
                var pooledBuffer = ArrayPool<byte>.Shared.Rent(messageLength);
                var messageBuffer = pooledBuffer.AsMemory().Slice(0, messageLength);
                closeStatus = await ReadAsync(stream, messageBuffer, cancellationToken).ConfigureAwait(false);
                if (closeStatus != null)
                {
                    ArrayPool<byte>.Shared.Return(pooledBuffer);
                    break;
                }
                ProcessBulkedMessage(messageBuffer)
                    .ContinueWith(_ => ArrayPool<byte>.Shared.Return(pooledBuffer));
            }
            _onConnectionClosed(this, closeStatus!.Value);
        }

        private Task ProcessBulkedMessage(ReadOnlyMemory<byte> bulked)
        {
            var offset = 0;
            var handleTasks = new List<Task>();
            while (offset < bulked.Length)
            {
                var messageLength = BinaryPrimitives.ReadUInt16LittleEndian(bulked.Slice(offset).Span);
                offset += 2;
                handleTasks.Add(FireOffMessageHandler(bulked.Slice(offset, messageLength)));
                offset += messageLength;
            }
            return Task.WhenAll(handleTasks);
        }

        private Task FireOffMessageHandler(ReadOnlyMemory<byte> data) =>
            Task.Run(async () =>
            {
                try
                {
                    await OnMessage(data).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Exception thrown while handling message");
                }
            });

        private unsafe Task OnMessage(ReadOnlyMemory<byte> data)
        {
            var messageId = BinaryPrimitives.ReadUInt32LittleEndian(data.Span);
            data = data.Slice(sizeof(uint));
            
            if (!_messageRegistry.TryGetMessageModel(messageId, out var messageModel))
                throw new Exception($"Received unknown messageId: {messageId}");
            
            // Check if we are dealing with a response
            if (messageModel.Behavior is MessageBehavior.Response)
            {
                var requestId = BinaryPrimitives.ReadInt32LittleEndian(data.Span);
                data = data.Slice(sizeof(int));
                if (!_pendingRequests.TryRemove(requestId, out var requestModel))
                {
                    _logger.Warning($"Got unknown request id: {requestId}");
                    return Task.CompletedTask;
                }
                // We need to make an allocation here because the Task OnMessage returns disposes of data after it runs
                requestModel.CompletionSource.SetResult(data.ToArray());
                return Task.CompletedTask;
            }

            if (!_messageHandlerPtrs.TryGetValue(messageId, out var messageHandlerPtr))
                throw new Exception($"No bound handler for: {messageModel}");
            
            var handler = (delegate*<
                PocketSocketClient,
                ReadOnlyMemory<byte>, 
                Task>) messageHandlerPtr;
            return handler(this, data);
        }
        
        public async Task<TResponse> Publish<TRequest, TResponse>(TRequest request)
        {
            var requestModel = InitiateRequest(request);
            var responseMemory = await requestModel.CompletionSource.Task.ConfigureAwait(false);
            return _serializationProvider.Deserialize<TResponse>(responseMemory);
        }

        public void Publish<TCommand>(TCommand command) => WriteMessage(command);

        public void Bind(ISocketEventHandler eventHandler)
        {
            var models = _eventHandlerModels.GetModelsApplicableToType(eventHandler.GetType());
            var modelCount = 0;

            var bindEventMethod = typeof(PocketSocketClient).GetMethods()
                .First(m => m.Name == "BindEvent");
            foreach (var model in models)
            {
                foreach (var eventModel in model.Events)
                {
                    var genericImpl = bindEventMethod.MakeGenericMethod(eventModel.EventType);
                    genericImpl.Invoke(this, new object[] { eventHandler, eventModel.MethodInfo });
                }
                modelCount++;
            }

            if (modelCount == 0)
                throw new Exception();
        }

        public unsafe void BindEvent<TEvent>(ISocketEventHandler eventHandler, MethodInfo methodInfo)
        {
            var eventParameter = Expression.Parameter(typeof(TEvent));
            var onEvent = (OnClientEventDelegate<TEvent>)Expression.Lambda(
                    typeof(OnClientEventDelegate<TEvent>), 
                    Expression.Call(Expression.Constant(eventHandler), methodInfo, eventParameter),
                    eventParameter)
                .Compile();
            var messageId = _messageRegistry.GetMessageId<TEvent>();
            _messageHandlers[messageId] = onEvent;
            delegate*<
                PocketSocketClient,
                ReadOnlyMemory<byte>,
                Task>
                handlerPtr = &OnClientEvent<TEvent>;
            _messageHandlerPtrs[messageId] = (nint) handlerPtr;
        }

        private void WriteMessage<TMessage>(TMessage message)
        {
            var messageId = _messageRegistry.GetMessageId<TMessage>();
            _serializationProvider.Serialize(message, (stream: _tcpClient.GetStream(), messageId),
                (data, state) =>
                {
                    var buffer = new SpanBufferWriter(stackalloc byte[2048]);
                    var messageLength = data.Length + sizeof(uint);
                    var lengthHeader = buffer.ReserveBookmark(sizeof(ushort));
                    buffer.WriteUInt32(state.messageId);
                    buffer.WriteBytes(data);
                    buffer.WriteBookmark(lengthHeader, (ushort)messageLength, BinaryPrimitives.WriteUInt16LittleEndian);
                    state.stream.Write(buffer.Data);
                });
        }
        
        private void WriteMessage<TMessage>(TMessage message, int requestId)
        {
            var messageId = _messageRegistry.GetMessageId<TMessage>();
            _serializationProvider.Serialize(message, (stream: _tcpClient.GetStream(), messageId, requestId),
                (data, state) =>
                {
                    var buffer = new SpanBufferWriter(stackalloc byte[2048]);
                    var messageLength = data.Length + sizeof(uint) + sizeof(int);
                    var lengthHeader = buffer.ReserveBookmark(sizeof(ushort));
                    buffer.WriteUInt32(state.messageId);
                    buffer.WriteInt32(state.requestId);
                    buffer.WriteBytes(data);
                    buffer.WriteBookmark(lengthHeader, (ushort)messageLength, BinaryPrimitives.WriteUInt16LittleEndian);
                    state.stream.Write(buffer.Data);
                });
        }

        private ServerRequestModel InitiateRequest<TRequest>(TRequest request)
        {
            var requestModel = new ServerRequestModel(_correlationIdProvider.GetNextCorrelationId(), new());
            _pendingRequests[requestModel.RequestId] = requestModel;
            WriteMessage(request, requestModel.RequestId);
            return requestModel;
        }

        public void Unbind(ISocketEventHandler eventHandler)
        {
            var implementedModels = _eventHandlerModels.GetModelsApplicableToType(eventHandler.GetType());
            foreach (var implementedModel in implementedModels)
                Unbind(implementedModel);
        }

        private void Unbind(SocketEventHandlerModel eventHandlerModel)
        {
            foreach (var message in eventHandlerModel.GetMessageModels())
            {
                var messageId = _messageRegistry.GetMessageId(message);
                _messageHandlerPtrs.Remove(messageId);
                _messageHandlers.Remove(messageId);
            }
        }

        public TSocketInterface Open<TSocketInterface>() where TSocketInterface : ISocketInterface<IPocketSocketClient>
        {
            var models = _interfaceModels.GetModelsApplicableToType(typeof(TSocketInterface)).ToArray();
            if (models.Length != 1) throw new Exception();
            var type = SocketClientInterfaceTypeProvider.FromInterfaceModel(models[0]);
            return (TSocketInterface) Activator.CreateInstance(type);
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            await _processMessagesTask;
            _tcpClient.Close();
            _cts.Dispose();
        }
        
        private static Task OnClientEvent<TEvent>(PocketSocketClient client, ReadOnlyMemory<byte> data)
        {
            var messageId = client._messageRegistry.GetMessageId<TEvent>();
            if (!client._messageHandlers.TryGetValue(messageId, out var messageHandler))
                throw new Exception($"Got event {typeof(TEvent).FullName} with no bound handler.");    
            var deserialized = client._serializationProvider.Deserialize<TEvent>(data);
            return ((OnClientEventDelegate<TEvent>) messageHandler)(deserialized);
        }
    }
}