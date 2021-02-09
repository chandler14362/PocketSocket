using System;
using PocketSocket.Abstractions.Delegates;

namespace PocketSocket.Abstractions
{
    public interface IPocketSocketBuilder
    {
        IPocketSocketBuilder AddEventHandler<T>() where T : ISocketEventHandler;
        
        IPocketSocketBuilder AddInterface(Type socketInterfaceType);

        IPocketSocketBuilder UseConfiguration(Action<PocketSocketConfig> onConfigure);

        IPocketSocketBuilder UseMessageRegistry(IMessageRegistry messageRegistry);

        IPocketSocketBuilder UseMessageRegistry<T>() where T : IMessageRegistry, new() =>
            UseMessageRegistry(new T());

        IPocketSocketBuilder UseSerializer(ISerializationProvider serializationProvider);

        IPocketSocketBuilder UseSerializer<T>() where T : ISerializationProvider, new() => UseSerializer(new T());

        IPocketSocketBuilder UseLogger(ILogger logger);

        IPocketSocketBuilder UseLogger<T>() where T : ILogger, new() => UseLogger(new T());
        
        IPocketSocketBuilder UseStreamWriterFactory(StreamWriterFactoryDelegate streamWriterFactory);
        
        IPocketSocketClient BuildClient();
        
        IPocketSocketServer<TConnection> BuildServer<TConnection>() where TConnection : class, ISocketConnection;

        IPocketSocketServer<ISocketConnection> BuildServer() => BuildServer<ISocketConnection>();
    }
}