using System;
using System.Threading.Tasks;

namespace PocketSocket.Abstractions.Delegates
{
    public delegate Task OnMessageDelegate(ReadOnlyMemory<byte> data);
}