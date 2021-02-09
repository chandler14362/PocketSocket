namespace PocketSocket.Abstractions
{
    public interface ISocketInterface
    {
    }

    public interface ISocketInterface<T> : ISocketInterface where T: class
    {
    }
}