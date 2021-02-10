using System;
using System.Threading.Tasks;
using PocketSocket.Abstractions;

namespace PocketSocket.Abstractions
{
    public class BaseSocketConnection : ISocketConnection
    {
        private readonly IStreamReader _streamReader;
        private readonly IStreamWriter _streamWriter;
        
        public BaseSocketConnection(IStreamReader streamReader, IStreamWriter streamWriter)
        {
            _streamReader = streamReader;
            _streamWriter = streamWriter;
        }

        public void Write(ReadOnlySpan<byte> data) => _streamWriter.Write(data);

        public async ValueTask DisposeAsync()
        {
            await _streamReader.DisposeAsync();
            await _streamWriter.DisposeAsync();
        }
    }
}