using System;
using System.Threading.Tasks;
using PocketSocket.Abstractions.Delegates;

namespace PocketSocket.Abstractions
{
    public interface IPocketSocketClient : IAsyncDisposable
    {
        Task Start(string hostname, int port, OnConnectionClosed<IPocketSocketClient> onConnectionClosed);

        Task<TResponse> Publish<TRequest, TResponse>(TRequest request);

        void Publish<TCommand>(TCommand command);
        
        void Bind(ISocketEventHandler eventHandler);

        void Unbind(ISocketEventHandler eventHandler);

        TSocketInterface Open<TSocketInterface>() where TSocketInterface : ISocketInterface<IPocketSocketClient>;
    }
}