using PocketSocket.Abstractions.Enums;

namespace PocketSocket.Abstractions.Delegates
{
    public delegate void OnConnectionClosed<T>(T connection, ConnectionCloseStatus closeStatus);
}