using System.IO;

namespace PocketSocket.Abstractions.Delegates
{
    public delegate IStreamWriter StreamWriterFactoryDelegate(Stream stream);
}