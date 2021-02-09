using System;

namespace PocketSocket.Abstractions.Delegates
{
    public delegate void OnSerializedDelegate<in TState>(ReadOnlySpan<byte> data, TState state);
}
