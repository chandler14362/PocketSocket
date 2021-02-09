namespace PocketSocket.Abstractions.Delegates
{
    public delegate TConnection ConnectionFactoryDelegate<TConnection>(IStreamReader streamReader, IStreamWriter streamWriter)
        where TConnection: ISocketConnection;
}