using System;
using PocketSocket;
using PocketSocket.Abstractions;
using PocketSocket.Implementations;

namespace TestInterface
{
    public static class CommonBuilder
    {
        public static IPocketSocketBuilder Build(IPocketSocketBuilder builder) =>
            builder.UseBinaryRecordsSerialization()
                .AddInterface(typeof(ITestSocketInterface<>))
                .AddEventHandler<ITestSocketEventHandler>()
                .AddEventHandler<ITestSocketEventHandler2>();

        public static IPocketSocketBuilder Build() => Build(new PocketSocketBuilder());
    }
}