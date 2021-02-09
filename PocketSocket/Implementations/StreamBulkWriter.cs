using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Pipelines;
using PocketSocket.Abstractions;

namespace PocketSocket.Implementations
{
    public class StreamBulkWriter : IStreamWriter
    {
        private readonly CancellationTokenSource _cts = new ();
        private readonly Pipe _bulkingPipe = new ();
        private readonly PipeReader _pipeReader;
        private readonly PipeWriter _pipeWriter;
        private readonly Stream _stream;
        private readonly int _delay;
        private Task _processTask;

        private SpinLock _spinLock = new();
        
        public StreamBulkWriter(Stream stream, int delay)
        {
            _pipeReader = _bulkingPipe.Reader;
            _pipeWriter = _bulkingPipe.Writer;
            _stream = stream;
            _delay = delay;
        }

        public void Write(ReadOnlySpan<byte> data)
        {
            var lockTaken = false;
            _spinLock.Enter(ref lockTaken);
            _pipeWriter.Write(data);
            if (lockTaken) _spinLock.Exit();
        }

        public void Start()
        {
            _processTask = Task.Run(async () => { await ProcessBulkedData(_cts.Token); }, _cts.Token);
        }
        
        private async Task ProcessBulkedData(CancellationToken cancellationToken)
        {
            async Task<bool> Process(CancellationToken cancellationToken)
            {
                FlushResult flushResult;
                try
                {
                    flushResult = await _pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                
                if (flushResult.IsCanceled) 
                    return false;
                
                if (_pipeReader.TryRead(out var readResult))
                {
                    if (!TryWriteByteSequence(readResult.Buffer))
                        return false;
                    
                    _pipeReader.AdvanceTo(readResult.Buffer.End);
                    try
                    {
                        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        return false;
                    }
                    catch (OperationCanceledException)
                    {
                        return false;
                    }
                }
                return true;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                var processed = await Process(cancellationToken).ConfigureAwait(false);
                if (!processed) 
                    break;
                try
                {
                    await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
            await _bulkingPipe.Reader.CompleteAsync().ConfigureAwait(false);
        }

        private bool TryWriteByteSequence(ReadOnlySequence<byte> sequence)
        {
            var sequenceLength = (int)sequence.Length;
            var neededLength = sequenceLength + 2;
            var sequenceData = neededLength <= 8192 
                ? stackalloc byte[neededLength] 
                : new byte[neededLength];
            BinaryPrimitives.WriteUInt16LittleEndian(sequenceData, (ushort)sequenceLength);
            sequence.CopyTo(sequenceData[2..]);
            
            try
            {
                _stream.Write(sequenceData);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            await _processTask.ConfigureAwait(false);
            _cts.Dispose();
        }
    }
}