using System.Threading.Tasks;
using PocketSocket.Abstractions;

namespace TestInterface
{
    public record TestRequest(int X);

    public record TestResponse(int X);

    public record TestCommand(string Value);
    
    public interface ITestSocketInterface<T> : ISocketInterface<T> where T: class
    {
        Task<TestResponse> DoTestRequest(T connection, TestRequest request);
        Task DoTestCommand(T connection, TestCommand command);
    }
}