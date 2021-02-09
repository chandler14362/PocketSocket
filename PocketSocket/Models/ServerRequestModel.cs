using System;
using System.Threading.Tasks;

namespace PocketSocket.Models
{
    public record ServerRequestModel(
        int RequestId, 
        TaskCompletionSource<ReadOnlyMemory<byte>> CompletionSource);
}