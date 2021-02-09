using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading.Tasks;
using PocketSocket.Abstractions;

namespace PocketSocket.Implementations
{
    public class BasicStreamWriter : IStreamWriter
    {
        private readonly Stream _stream;
        
        public BasicStreamWriter(Stream stream)
        {
            _stream = stream;
        }

        public void Start()
        {
        }

        public void Write(ReadOnlySpan<byte> data) => _stream.Write(data);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}