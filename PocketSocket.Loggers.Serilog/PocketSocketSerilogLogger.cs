using System;
using Serilog;
using ILogger = PocketSocket.Abstractions.ILogger;
using SerilogLogger = Serilog.ILogger;

namespace PocketSocket.Loggers.Serilog
{
    public class PocketSocketSerilogLogger : ILogger
    {
        private sealed class PocketSocket
        {
        }

        private readonly SerilogLogger _logger;

        public PocketSocketSerilogLogger(SerilogLogger logger) => _logger = logger.ForContext<PocketSocket>();

        public PocketSocketSerilogLogger() => _logger = Log.ForContext<PocketSocket>();
        
        public void Verbose(string message) => _logger.Verbose(message);
        
        public void Debug(string message) => _logger.Debug(message);

        public void Information(string message) => _logger.Information(message);

        public void Warning(string message) => _logger.Warning(message);

        public void Error(string message) => _logger.Error(message);

        public void Error(Exception e, string message) => _logger.Error(e, message);

        public void Fatal(string message) => _logger.Fatal(message);

        public void Fatal(Exception e, string message) => _logger.Fatal(e, message);
    }
}