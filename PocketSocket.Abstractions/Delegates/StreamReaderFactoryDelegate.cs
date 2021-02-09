using System.IO;

namespace PocketSocket.Abstractions.Delegates
{
    public delegate IStreamReader StreamReaderFactoryDelegate(Stream stream);
}