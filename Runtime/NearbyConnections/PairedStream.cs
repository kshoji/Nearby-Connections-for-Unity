using System;
using System.Collections.Concurrent;
using System.IO;

namespace jp.kshoji.unity.nearby
{
    /// <summary>
    /// Treats the connected streams(input/output)
    /// Written data to OutputStream will be able to read from InputStream
    /// </summary>
    public class PairedStream
    {
        public Stream InputStream { get; }
        public Stream OutputStream { get; }

        public PairedStream()
        {
            var buffer = new BlockingCollection<byte[]>();
            InputStream = new InputStreamInternal(buffer);
            OutputStream = new OutputStreamInternal(buffer);
        }

        private class OutputStreamInternal : Stream
        {
            private readonly BlockingCollection<byte[]> buffer;

            public OutputStreamInternal(BlockingCollection<byte[]> blockingBytes)
            {
                buffer = blockingBytes;
                CanRead = false;
                CanWrite = true;
                CanSeek = false;
            }

            public override void Close()
            {
                buffer.CompleteAdding();
            }

            public override int Read(byte[] bytes, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] bytes, int offset, int count)
            {
                if (buffer.IsAddingCompleted)
                {
                    return;
                }

                var newData = new byte[count];
                Array.Copy(bytes, offset, newData, 0, count);
                buffer.Add(newData);
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override bool CanRead { get; }
            public override bool CanSeek { get; }
            public override bool CanWrite { get; }

            public override long Length
            {
                get { throw new NotSupportedException(); }
            }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }
        }
        private class InputStreamInternal : Stream
        {
            private BlockingCollection<byte[]> buffer;
            private byte[] currentBuffer;
            private int currentBufferIndex = 0;

            public InputStreamInternal(BlockingCollection<byte[]> blockingBytes)
            {
                CanRead = true;
                CanWrite = false;
                CanSeek = false;
                buffer = blockingBytes;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    buffer.Dispose();
                }
                base.Dispose(disposing);
            }

            public override int Read(byte[] bytes, int offset, int count)
            {
                if (currentBuffer == null)
                {
                    if (buffer.TryTake(out var takenBytes))
                    {
                        currentBuffer = takenBytes;
                    }
                    else
                    {
                        if (buffer.IsAddingCompleted)
                        {
                            return -1;
                        }
                        return 0;
                    }
                }

                var remainingBytes = currentBuffer.Length - currentBufferIndex;
                var readBytes = Math.Min(remainingBytes, count);
                Array.Copy(currentBuffer, currentBufferIndex, bytes, offset, readBytes);
                currentBufferIndex += readBytes;

                if (currentBufferIndex >= currentBuffer.Length)
                {
                    currentBuffer = null;
                    currentBufferIndex = 0;
                }

                return readBytes;
            }

            public override void Write(byte[] bytes, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override bool CanRead { get; }
            public override bool CanSeek { get; }
            public override bool CanWrite { get; }

            public override long Length
            {
                get
                {
                    if (currentBuffer == null)
                    {
                        if (buffer.TryTake(out var takenBytes))
                        {
                            currentBuffer = takenBytes;
                        }
                        else
                        {
                            if (buffer.IsAddingCompleted)
                            {
                                return -1;
                            }
                            return 0;
                        }
                    }
                    return currentBuffer.Length - currentBufferIndex;
                }
            }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }
        }
    }
}
