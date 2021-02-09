using System;
using BinaryRecords;
using PocketSocket.Abstractions;
using PocketSocket.Abstractions.Delegates;

namespace PocketSocket.Serializers.BinaryRecords
{
    public class BinaryRecordsSerializationProvider : ISerializationProvider
    {
        private BinarySerializer _serializer;

        public BinaryRecordsSerializationProvider(BinarySerializer serializer) =>_serializer = serializer;

        public BinaryRecordsSerializationProvider() => _serializer = BinarySerializerBuilder.BuildDefault();

        public T Deserialize<T>(ReadOnlyMemory<byte> data) => _serializer.Deserialize<T>(data.Span);

        public void Serialize<TMessage, TState>(TMessage message, TState state, OnSerializedDelegate<TState> onSerialized) =>
            _serializer.Serialize(message, (state, onSerialized), (data, state) => state.onSerialized(data, state.state));
    }
}