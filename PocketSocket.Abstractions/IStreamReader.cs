using System;
using PocketSocket.Abstractions.Delegates;

namespace PocketSocket.Abstractions
{
    public interface IStreamReader : IAsyncDisposable
    {
        public void Start(OnMessageDelegate onMessage, OnReaderComplete onReaderComplete);
    }
}