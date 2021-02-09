using System.Net;

namespace PocketSocket.Abstractions
{
    public class PocketSocketConfig
    {
        // Bulk delay in milliseconds, 0 means no bulking
        public int BulkDelay { get; set; } = 0;
    }
}