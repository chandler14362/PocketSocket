using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using PocketSocket.Abstractions;
using PocketSocket.Abstractions.Delegates;
using PocketSocket.Abstractions.Extensions;
using PocketSocket.Abstractions.Models;
using PocketSocket.Extensions;

namespace PocketSocket.Implementations
{
    public class PocketSocketBuilder : IPocketSocketBuilder
    {
        private readonly PocketSocketConfig _config = new();
        private ISerializationProvider? _serializationProvider;
        private ICorrelationIdProvider? _correlationIdProvider;
        private ILogger? _logger;
        private StreamWriterFactoryDelegate? _streamWriterFactory;
        private StreamReaderFactoryDelegate? _streamReaderFactory;
        private IMessageRegistry? _messageRegistry;
        private readonly List<TypedSocketModel> _typedModels = new();
        
        public IPocketSocketBuilder AddEventHandler<T>() where T : ISocketEventHandler
        {
            var model = SocketEventHandlerModel.FromInterface(typeof(T));
            _typedModels.Add(model);
            return this;
        }

        public IPocketSocketBuilder AddInterface(Type socketInterfaceType)
        {
            if (!socketInterfaceType.ImplementsInterface<ISocketInterface>() ||
                !socketInterfaceType.IsGenericType || 
                !socketInterfaceType.IsGenericTypeDefinition)
                throw new Exception();
            socketInterfaceType = socketInterfaceType.IsGenericType
                ? socketInterfaceType.GetGenericTypeDefinition()
                : socketInterfaceType;
            var model = SocketInterfaceModel.FromInterface(socketInterfaceType);
            _typedModels.Add(model);
            return this;
        }

        public IPocketSocketBuilder UseConfiguration(Action<PocketSocketConfig> onConfigure)
        {
            onConfigure(_config);
            return this;
        }

        public IPocketSocketBuilder UseMessageRegistry(IMessageRegistry messageRegistry)
        {
            if (_messageRegistry is not null)
                throw new Exception();
            _messageRegistry = messageRegistry;
            return this;
        }

        public IPocketSocketBuilder UseSerializer(ISerializationProvider serializationProvider)
        {
            _serializationProvider = serializationProvider;
            return this;
        }

        public IPocketSocketBuilder UseLogger(ILogger logger)
        {
            _logger = logger;
            return this;
        }
        
        private void PrepareBuild()
        {
            if (_serializationProvider is null)
                throw new Exception("A serialization provider must be set before building a pocket socket");
            _logger ??= new ConsoleLogger();
            _correlationIdProvider ??= new CorrelationIdProvider();
            _messageRegistry ??= new MessageRegistry();
            foreach (var messageModel in _typedModels.GetMessageModels())
                _messageRegistry.AddMessage(messageModel);
        }

        public IPocketSocketBuilder UseStreamWriterFactory(StreamWriterFactoryDelegate streamWriterFactory)
        {
            throw new NotImplementedException();
        }

        public IPocketSocketClient BuildClient()
        {
            PrepareBuild();
            var interfaceModels = _typedModels.OfType<SocketInterfaceModel>().ToList();
            var eventHandlers = _typedModels.OfType<SocketEventHandlerModel>().ToList();
            return new PocketSocketClient(_config, _messageRegistry, _serializationProvider, _correlationIdProvider, 
                _logger, interfaceModels, eventHandlers);
        }

        public IPocketSocketServer<TConnection> BuildServer<TConnection>()
            where TConnection: class, ISocketConnection
        {
            PrepareBuild();
            _streamWriterFactory ??= CreateDefaultStreamWriterFactory(_config.BulkDelay);
            _streamReaderFactory ??= DefaultStreamReaderFactory;
            var interfaceModels = _typedModels.OfType<SocketInterfaceModel>().ToList();
            return new PocketSocketServer<TConnection>(_config, _logger, _messageRegistry, _serializationProvider,
                _streamWriterFactory, _streamReaderFactory, interfaceModels);
        }

        public static StreamWriterFactoryDelegate CreateDefaultStreamWriterFactory(int delay) =>
            stream => new StreamBulkWriter(stream, delay);

        public static IStreamReader DefaultStreamReaderFactory(Stream stream) => new BasicStreamReader(stream);
    }
}