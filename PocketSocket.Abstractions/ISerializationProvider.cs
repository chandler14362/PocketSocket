using System;
using PocketSocket.Abstractions.Delegates;

namespace PocketSocket.Abstractions
{
    public interface ISerializationProvider
    {
        void Serialize<TMessage, TState>(TMessage message, TState state, OnSerializedDelegate<TState> onSerialized);

        T Deserialize<T>(ReadOnlyMemory<byte> data);
    }
}