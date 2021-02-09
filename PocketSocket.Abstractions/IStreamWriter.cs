using System;

namespace PocketSocket.Abstractions
{
    public interface IStreamWriter : IAsyncDisposable
    {
        public void Start();
        
        void Write(ReadOnlySpan<byte> data);
    }
}
