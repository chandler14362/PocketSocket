using System.Threading.Tasks;

namespace PocketSocket.Abstractions.Delegates
{
    public delegate Task<TResponse> OnServerRequestDelegate<TSocketConnection, TRequest, TResponse>(
        TSocketConnection socketConnection, TRequest request)
        where TSocketConnection : ISocketConnection;
}