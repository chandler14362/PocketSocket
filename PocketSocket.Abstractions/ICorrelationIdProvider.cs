namespace PocketSocket.Abstractions
{
    public interface ICorrelationIdProvider
    {
        int GetNextCorrelationId();
    }
}