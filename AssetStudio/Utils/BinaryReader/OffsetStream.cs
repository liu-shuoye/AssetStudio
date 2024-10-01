using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AssetStudio
{
    /// <summary> 封装流，以支持从指定位置开始读取流。 </summary>
    public class OffsetStream : Stream
    {
        /// <summary> 缓存大小。 </summary>
        private const int BufferSize = 0x10000;

        /// <summary> 源流。 </summary>
        private readonly Stream _baseStream;
        /// <summary> 当前偏移量。 </summary>
        private long _offset;

        /// <summary> 源流是否可读。 </summary>
        public override bool CanRead => _baseStream.CanRead;
        /// <summary> 源流是否可定位。 </summary>
        public override bool CanSeek => _baseStream.CanSeek;
        /// <summary> 源流是否可写入。 </summary>
        public override bool CanWrite => false;

        /// <summary> 当前偏移量。 </summary>
        public long Offset
        {
            get => _offset;
            set
            {
                if (value < 0 || value > _baseStream.Length)
                {
                    throw new IOException($"{nameof(Offset)} 超出流范围");
                }
                _offset = value;
                Seek(0, SeekOrigin.Begin);
            }
        }
        /// <summary> 当前绝对位置。 </summary>
        public long AbsolutePosition => _baseStream.Position;
        /// <summary> 剩余可读取字节数。 </summary>
        public long Remaining => Length - Position;

        /// <summary> 源流长度。 </summary>
        public override long Length => _baseStream.Length - _offset;
        /// <summary> 当前偏位置。 </summary>
        public override long Position
        {
            get => _baseStream.Position - _offset;
            set => Seek(value, SeekOrigin.Begin);
        }

        public OffsetStream(Stream stream, long offset)
        {
            _baseStream = stream;

            Offset = offset;
        }

        /// <summary> 移动到流中的指定位置。 </summary>
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (offset > _baseStream.Length)
            {
                throw new IOException("无法在流界限之外查找");
            }

            var target = origin switch
            {
                SeekOrigin.Begin => offset + _offset,
                SeekOrigin.Current => offset + Position,
                SeekOrigin.End => offset + _baseStream.Length,
                _ => throw new NotSupportedException()
            };

            _baseStream.Seek(target, SeekOrigin.Begin);
            return Position;
        }
        /// <summary> 从流中读取字节。 </summary>
        public override int Read(byte[] buffer, int offset, int count) => _baseStream.Read(buffer, offset, count);
        /// <summary> 将字节写入流。 </summary>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        /// <summary> 设置流的长度。 </summary>
        public override void SetLength(long value) => throw new NotImplementedException();
        /// <summary> 将流刷新到基础流。 </summary>
        public override void Flush() => throw new NotImplementedException();
        /// <summary> 获取流中的所有偏移量。 </summary>
        public IEnumerable<long> GetOffsets(string path)
        {
            if (AssetsHelper.TryGet(path, out var offsets))
            {
                foreach (var offset in offsets)
                {
                    Offset = offset;
                    yield return offset;
                }
            }
            else
            {
                while (Remaining > 0)
                {
                    Offset = AbsolutePosition;
                    yield return AbsolutePosition;
                    if (Offset == AbsolutePosition)
                    {
                        break;
                    }
                }
            }
        }
    }
}
