namespace PocketSocket.Abstractions.Delegates
{
    public delegate ISocketConnection SocketConnectionFactoryDelegate(
        IStreamWriter streamWriter,
        IStreamReader streamReader);
}