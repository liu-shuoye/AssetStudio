using System;
using System.Text;
using System.Security.Cryptography;

namespace AssetStudio
{
    public class UnityCNGuiLongChao : UnityCN
    {
        public UnityCNGuiLongChao(EndianBinaryReader reader)
        {
            reader.ReadUInt32();
    
            var infoBytes = reader.ReadBytes(0x8);
            reader.AlignStream();
    
            infoBytes = infoBytes.ToUInt4Array();
            Index = Array.Empty<byte>();
            var subBytes = infoBytes.AsSpan(0, 0x10);
            for (var i = 0; i < subBytes.Length; i++)
            {
                var idx = (i % 4 * 4) + (i / 4);
                Sub[idx] = subBytes[i];
            }
        }

        public override void DecryptBlock(Span<byte> bytes, int size, int index)
        {
            int count = 0;
            var offset = 0;
            while (offset < size)
            {
                if (count++ >= 0x14) break;
                offset += Decrypt(bytes.Slice(offset), index++, size - offset);
            }
        }

        protected override int DecryptByte(Span<byte> bytes, ref int offset, ref int index)
        {
            var b = Sub[((index >> 2) & 3) + 4] + Sub[index & 3] + Sub[((index >> 4) & 3) + 8] + Sub[((byte)index >> 6) + 12];
            bytes[offset] = byte.RotateLeft(bytes[offset], b & 7);
            b = bytes[offset];
            offset++;
            index++;
            return b;
        }
    }
}

