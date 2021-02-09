using PocketSocket.Abstractions;
using PocketSocket.Loggers.Serilog;
using SerilogLogger = Serilog.ILogger;

namespace PocketSocket
{
    public static class PocketSocketBuilderExtensions
    {
        public static IPocketSocketBuilder UseSerilog(this IPocketSocketBuilder builder) =>
            builder.UseLogger(new PocketSocketSerilogLogger());

        public static IPocketSocketBuilder UseSerilog(this IPocketSocketBuilder builder, SerilogLogger logger) =>
            builder.UseLogger(new PocketSocketSerilogLogger(logger));
    }
}