using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PocketSocket.Abstractions;
using PocketSocket.Abstractions.Delegates;
using System.IO.Pipelines;
using PocketSocket.Abstractions.Enums;

namespace PocketSocket.Implementations
{
    public class BasicStreamReader : IStreamReader
    {
        private readonly CancellationTokenSource _cts;
        private readonly Stream _stream;
        private readonly Pipe _pipe;

        private ReaderCompletionStatus _completionStatus = ReaderCompletionStatus.Graceful;
        
        private OnMessageDelegate _onMessage;
        private OnReaderComplete _onReaderComplete;
        private Task _fillTask;
        private Task _processTask;
        
        public BasicStreamReader(Stream stream)
        {
            _pipe = new();
            _cts = new();
            _stream = stream;
        }

        public void Start(OnMessageDelegate onMessage, OnReaderComplete onReaderComplete)
        {
            _onMessage = onMessage;
            _onReaderComplete = onReaderComplete;
            _fillTask = Task.Run(async () => await FillPipe(_cts.Token));
            _processTask = Task.Run(async () => await ProcessIncomingMessages(_cts.Token));
        }

        private async Task FillPipe(CancellationToken cancellationToken)
        {
            const int minimumReceiveSize = 512;
            var pipeWriter = _pipe.Writer;
            while (true)
            {
                var memory = pipeWriter.GetMemory(minimumReceiveSize);
                var read = 0;
                try
                {
                    read = await _stream.ReadAsync(memory, cancellationToken).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    _completionStatus = ReaderCompletionStatus.Abrupt;
                    break;
                }
                if (read == 0)
                    break;
                pipeWriter.Advance(read);
                var flushResult = await pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (flushResult.IsCanceled)
                    break;
            }
            await pipeWriter.CompleteAsync().ConfigureAwait(false);
        }

        private async Task ProcessIncomingMessages(CancellationToken cancellationToken)
        {
            var reader = _pipe.Reader;
            while (true)
            {
                ReadResult result;
                try
                {
                    result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                var buffer = result.Buffer;
                while (TryProcessMessage(ref buffer))
                {}
                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCanceled || result.IsCompleted) break;
            }
            await reader.CompleteAsync().ConfigureAwait(false);
            _onReaderComplete(_completionStatus);
        }

        private bool TryProcessMessage(ref ReadOnlySequence<byte> message)
        {
            const int lengthSize = sizeof(ushort);
            var length = (int)message.Length;
            if (length < lengthSize)
                return false;
            var messageLength = ReadUInt16LittleEndian(message);
            if (messageLength > length - lengthSize)
                return false;
            var pooledBuffer = ArrayPool<byte>.Shared.Rent(messageLength);
            var messageBuffer = pooledBuffer.AsMemory()[..messageLength];
            message.Slice(lengthSize, messageLength).CopyTo(messageBuffer.Span);
            _onMessage(messageBuffer).ContinueWith(_ => ArrayPool<byte>.Shared.Return(pooledBuffer));
            message = message.Slice(messageLength + lengthSize);
            return true;
        }

        private static ushort ReadUInt16LittleEndian(in ReadOnlySequence<byte> sequence)
        {
            if (sequence.FirstSpan.Length > sizeof(ushort))
                return BinaryPrimitives.ReadUInt16LittleEndian(sequence.FirstSpan);
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            sequence.Slice(0, sizeof(ushort)).CopyTo(buffer);
            return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        }
        
        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            await _fillTask.ConfigureAwait(false);
            await _processTask.ConfigureAwait(false);
            _cts.Dispose();
        }
    }
}