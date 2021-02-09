using System.Threading.Tasks;
using PocketSocket.Abstractions;

namespace TestInterface
{
    public record TestEvent();
    
    public interface ITestSocketEventHandler : ISocketEventHandler
    {
        Task OnTestEvent(TestEvent @event);
    }
    
    public record TestEvent2();
    
    public interface ITestSocketEventHandler2 : ISocketEventHandler
    {
        Task OnTestEvent(TestEvent2 @event);
    }
}