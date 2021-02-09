using System.Threading;
using PocketSocket.Abstractions;

namespace PocketSocket.Implementations
{
    public class CorrelationIdProvider : ICorrelationIdProvider
    {
        private int _current = 0;

        public int GetNextCorrelationId() => Interlocked.Increment(ref _current);
    }
}