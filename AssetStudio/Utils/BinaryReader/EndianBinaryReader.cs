using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AssetStudio
{
    /// <summary> EndianBinaryReader 继承 BinaryReader，支持大端和小端读取 </summary>
    public class EndianBinaryReader : BinaryReader
    {
        /// <summary> 缓存读取的数组 </summary>
        private readonly byte[] buffer;

        /// <summary> 当前大小端类型 </summary>
        public EndianType Endian;

        public EndianBinaryReader(Stream stream, EndianType endian = EndianType.BigEndian, bool leaveOpen = false) : base(stream, Encoding.UTF8, leaveOpen)
        {
            Endian = endian;
            buffer = new byte[8];
        }

        /// <summary> 当前位置 </summary>
        public long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        /// <summary> 文件总长度 </summary>
        public long Length => BaseStream.Length;
        /// <summary> 剩余未读字节数 </summary>
        public long Remaining => Length - Position;

        /// <summary> 读取一个16位整数 </summary>
        public override short ReadInt16()
        {
            if (Endian == EndianType.BigEndian)
            {
                Read(buffer, 0, 2);
                return BinaryPrimitives.ReadInt16BigEndian(buffer);
            }
            return base.ReadInt16();
        }

        /// <summary> 读取一个32位整数 </summary>
        public override int ReadInt32()
        {
            if (Endian == EndianType.BigEndian)
            {
                Read(buffer, 0, 4);
                return BinaryPrimitives.ReadInt32BigEndian(buffer);
            }
            return base.ReadInt32();
        }

        /// <summary> 读取一个64位整数 </summary>
        public override long ReadInt64()
        {
            if (Endian == EndianType.BigEndian)
            {
                Read(buffer, 0, 8);
                return BinaryPrimitives.ReadInt64BigEndian(buffer);
            }
            return base.ReadInt64();
        }

        /// <summary> 读取一个无符号16位整数 </summary>
        public override ushort ReadUInt16()
        {
            if (Endian == EndianType.BigEndian)
            {
                Read(buffer, 0, 2);
                return BinaryPrimitives.ReadUInt16BigEndian(buffer);
            }
            return base.ReadUInt16();
        }

        /// <summary> 读取一个无符号32位整数 </summary>
        public override uint ReadUInt32()
        {
            if (Endian == EndianType.BigEndian)
            {
                Read(buffer, 0, 4);
                return BinaryPrimitives.ReadUInt32BigEndian(buffer);
            }
            return base.ReadUInt32();
        }

        /// <summary> 读取一个无符号64位整数 </summary>
        public override ulong ReadUInt64()
        {
            if (Endian == EndianType.BigEndian)
            {
                Read(buffer, 0, 8);
                return BinaryPrimitives.ReadUInt64BigEndian(buffer);
            }
            return base.ReadUInt64();
        }

        /// <summary> 读取一个单精度浮点型 </summary>
        public override float ReadSingle()
        {
            if (Endian == EndianType.BigEndian)
            {
                Read(buffer, 0, 4);
                Array.Reverse(buffer, 0, 4);
                return BitConverter.ToSingle(buffer, 0);
            }
            return base.ReadSingle();
        }

        /// <summary> 读取一个浮点型 </summary>
        public override double ReadDouble()
        {
            if (Endian == EndianType.BigEndian)
            {
                Read(buffer, 0, 8);
                Array.Reverse(buffer);
                return BitConverter.ToDouble(buffer, 0);
            }
            return base.ReadDouble();
        }
        
        /// <summary> 读取一个字节数组 </summary>
        public override byte[] ReadBytes(int count)
        {
            if (count == 0)
            {
                return Array.Empty<byte>();
            }

            var buffer = ArrayPool<byte>.Shared.Rent(0x1000);
            List<byte> result = new List<byte>();
            do
            {
                var readNum = Math.Min(count, buffer.Length);
                int n = Read(buffer, 0, readNum);
                if (n == 0)
                {
                    break;
                }

                result.AddRange(buffer[..n]);
                count -= n;
            } while (count > 0);

            ArrayPool<byte>.Shared.Return(buffer);
            return result.ToArray();
        }

        /// <summary> 将当前位置对齐到4字节 </summary>
        public void AlignStream()
        {
            AlignStream(4);
        }

        /// <summary> 将当前位置对齐到指定字节 </summary>
        public void AlignStream(int alignment)
        {
            var pos = Position;
            var mod = pos % alignment;
            if (mod != 0)
            {
                Position += alignment - mod;
            }
        }

        /// <summary> 读取一个以对齐方式存储的字符串 </summary>
        public string ReadAlignedString()
        {
            var result = "";
            var length = ReadInt32();
            if (length > 0 && length <= Remaining)
            {
                var stringData = ReadBytes(length);
                result = Encoding.UTF8.GetString(stringData);
            }
            AlignStream();
            return result;
        }
        /// <summary> 读取一个以对齐方式存储的字符串 </summary>
        public string ReadAlignedStringInt16()
        {
            var result = "";
            var length = ReadInt16();
            if (length > 0 && length <= Remaining)
            {
                var stringData = ReadBytes(length);
                result = Encoding.UTF8.GetString(stringData);
            }
            AlignStream();
            return result;
        }

        /// <summary> 读取一个以空字符结尾的字符串，最大长度可指定 </summary>
        public string ReadStringToNull(int maxLength = 32767)
        {
            var bytes = new List<byte>();
            int count = 0;
            while (Remaining > 0 && count < maxLength)
            {
                var b = ReadByte();
                if (b == 0)
                {
                    break;
                }
                bytes.Add(b);
                count++;
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        /// <summary> 读取一个四元数 </summary>
        public Quaternion ReadQuaternion()
        {
            return new Quaternion(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        /// <summary> 读取一个二维向量 </summary>
        public Vector2 ReadVector2()
        {
            return new Vector2(ReadSingle(), ReadSingle());
        }

        /// <summary> 读取一个三维向量 </summary>
        public Vector4 ReadVector4()
        {
            return new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        /// <summary> 读取一个颜色对象（四个分量） </summary>
        public Color ReadColor4()
        {
            return new Color(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        /// <summary> 读取一个4x4矩阵 </summary>
        public Matrix4x4 ReadMatrix()
        {
            return new Matrix4x4(ReadSingleArray(16));
        }

        /// <summary> 读取一个浮点数 </summary>
        public Float ReadFloat()
        {
            return new Float(ReadSingle());
        }

        /// <summary> 读取一个特定格式的整数（米哈游格式）</summary>
        public int ReadMhyInt()
        {
            var buffer = ReadBytes(6);
            return buffer[2] | (buffer[4] << 8) | (buffer[0] << 0x10) | (buffer[5] << 0x18);
        }

        /// <summary>读取一个特定格式的无符号整数（mhy格式）</summary>
        public uint ReadMhyUInt()
        {
            var buffer = ReadBytes(7);
            return (uint)(buffer[1] | (buffer[6] << 8) | (buffer[3] << 0x10) | (buffer[2] << 0x18));
        }

        /// <summary> 读取一个以空字符结尾的mhy字符串，并进行特定的对齐处理 </summary>
        public string ReadMhyString()
        {
            var pos = BaseStream.Position;
            var str = ReadStringToNull();
            BaseStream.Position += 0x105 - (BaseStream.Position - pos);
            return str;
        }

        /// <summary> 内部方法，用于读取一个数组 </summary>
        internal T[] ReadArray<T>(Func<T> del, int length)
        {
            if (length < 0x1000)
            {
                var array = new T[length];
                for (int i = 0; i < length; i++)
                {
                    array[i] = del();
                }
                return array;
            }
            else
            {
                var list = new List<T>();
                for (int i = 0; i < length; i++)
                {
                    list.Add(del());
                }
                return list.ToArray();
            }
        }

        /// <summary> 读取一个布尔数组 </summary>
        public bool[] ReadBooleanArray(int length = -1)
        {
            if (length == -1)
            {
                length = ReadInt32();
            }
            return ReadArray(ReadBoolean, length);
        }

        /// <summary> 读取一个无符号8位整数数组 </summary>
        public byte[] ReadUInt8Array(int length = -1)
        {
            if (length == -1)
            {
                length = ReadInt32();
            }
            return ReadBytes(length);
        }

        /// <summary> 读取一个16位整数数组 </summary>
        public short[] ReadInt16Array(int length = -1)
        {
            if (length == -1)
            {
                length = ReadInt32();
            }
            return ReadArray(ReadInt16, length);
        }

        /// <summary> 读取一个无符号16位整数数组 </summary>
        public ushort[] ReadUInt16Array(int length = -1)
        {
            if (length == -1)
            {
                length = ReadInt32();
            }
            return ReadArray(ReadUInt16, length);
        }

        /// <summary> 读取一个32位整数数组 </summary>
        public int[] ReadInt32Array(int length = -1)
        {
            if (length == -1)
            {
                length = ReadInt32();
            }
            return ReadArray(ReadInt32, length);
        }

        /// <summary> 取一个无符号32位整数的二维数组 </summary>
        public uint[] ReadUInt32Array(int length = -1)
        {
            if (length == -1)
            {
                length = ReadInt32();
            }
            return ReadArray(ReadUInt32, length);
        }

        /// <summary> 读取一个无符号32位整数的二维数组 </summary>
        public uint[][] ReadUInt32ArrayArray(int length = -1)
        {
            if (length == -1)
            {
                length = ReadInt32();
            }
            return ReadArray(() => ReadUInt32Array(), length);
        }

        /// <summary> 读取一个单精度浮点数数组 </summary>
        public float[] ReadSingleArray(int length = -1)
        {
            if (length == -1)
            {
                length = ReadInt32();
            }
            return ReadArray(ReadSingle, length);
        }

        /// <summary> 读取一个字符串数组 </summary>
        public string[] ReadStringArray(int length = -1)
        {
            if (length == -1)
            {
                length = ReadInt32();
            }
            return ReadArray(ReadAlignedString, length);
        }

        /// <summary> 读取一个二维向量数组 </summary>
        public Vector2[] ReadVector2Array(int length = -1)
        {
            if (length == -1)
            {
                length = ReadInt32();
            }
            return ReadArray(ReadVector2, length);
        }

        /// <summary> 读取一个三维向量数组 </summary>
        public Vector4[] ReadVector4Array(int length = -1)
        {
            if (length == -1)
            {
                length = ReadInt32();
            }
            return ReadArray(ReadVector4, length);
        }

        /// <summary> 读取一个4x4矩阵数组 </summary>
        public Matrix4x4[] ReadMatrixArray(int length = -1)
        {
            if (length == -1)
            {
                length = ReadInt32();
            }
            return ReadArray(ReadMatrix, length);
        }
    }
}
