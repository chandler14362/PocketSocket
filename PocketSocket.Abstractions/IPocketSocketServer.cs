using System;
using System.Net;
using System.Threading.Tasks;
using PocketSocket.Abstractions.Delegates;

namespace PocketSocket.Abstractions
{
    public interface IPocketSocketServer : IAsyncDisposable
    {
        void Bind(ISocketInterface socketInterface);

        void Unbind(ISocketInterface socketInterface);

        void Write<TMessage>(ISocketConnection connection, TMessage message);

        Task DisposeConnection(ISocketConnection connection);
    }

    public interface IPocketSocketServer<TConnection> : IPocketSocketServer 
        where TConnection : class, ISocketConnection
    {
        Task Start(IPAddress ipAddress, int port, OnConnectionClosed<TConnection> onConnectionClosed);
        
        Task Start(IPAddress ipAddress, int port, ConnectionFactoryDelegate<TConnection> connectionFactory, OnConnectionClosed<TConnection> onConnectionClosed);
    }
}