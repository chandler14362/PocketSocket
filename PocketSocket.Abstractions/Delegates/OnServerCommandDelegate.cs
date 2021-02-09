using System.Threading.Tasks;

namespace PocketSocket.Abstractions.Delegates
{
    public delegate Task OnServerCommandDelegate<TSocketConnection, TCommand>(
        TSocketConnection socketConnection, TCommand command)
        where TSocketConnection : ISocketConnection;
}