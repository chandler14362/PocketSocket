using System.Threading.Tasks;

namespace PocketSocket.Abstractions.Delegates
{
    public delegate Task OnClientEventDelegate<TEvent>(TEvent @event);
}