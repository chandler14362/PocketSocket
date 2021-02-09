using System;
using BinaryRecords;
using PocketSocket.Abstractions;
using PocketSocket.Serializers.BinaryRecords;

namespace PocketSocket
{
    public static class PocketSocketBuilderExtensions
    {
        public static IPocketSocketBuilder UseBinaryRecordsSerialization(this IPocketSocketBuilder builder, Action<BinarySerializerBuilder> onSerializerBuild)
        {
            var serializerBuilder = new BinarySerializerBuilder();
            onSerializerBuild(serializerBuilder);
            var serializer = serializerBuilder.Build();
            return builder.UseSerializer(new BinaryRecordsSerializationProvider(serializer));
        }

        public static IPocketSocketBuilder UseBinaryRecordsSerialization(this IPocketSocketBuilder builder) => builder.UseSerializer<BinaryRecordsSerializationProvider>();
    }
}