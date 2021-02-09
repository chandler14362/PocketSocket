using System;

namespace PocketSocket.Abstractions
{
    public interface ISocketConnection : IAsyncDisposable
    {
        void Write(ReadOnlySpan<byte> data);
    }
}