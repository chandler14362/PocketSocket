using System;

namespace PocketSocket.Abstractions
{
    public interface ILogger
    {
        void Verbose(string message);

        void Debug(string message);

        void Information(string message);
        
        void Warning(string message);

        void Error(string message);

        void Error(Exception e, string message);
        
        void Fatal(string message);

        void Fatal(Exception e, string message);
    }
}