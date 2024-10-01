using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    /// <summary> 表示序列化文件的头部信息。  </summary>
    public class SerializedFileHeader
    {
        /// <summary> 元数据大小，以字节为单位。 </summary>
        public uint m_MetadataSize;

        /// <summary> 文件大小，以字节为单位。 </summary>
        public long m_FileSize;

        /// <summary> 序列化文件的格式版本。 </summary>
        public SerializedFileFormatVersion m_Version;

        /// <summary> 数据偏移量，表示数据开始的位置。 </summary>
        public long m_DataOffset;

        /// <summary> 字节序，0表示小端，1表示大端。 </summary>
        public byte m_Endianess;

        /// <summary>  保留字段，用于将来扩展。  </summary>
        public byte[] m_Reserved;

        /// <summary>
        /// 重写ToString方法，提供SerializedFileHeader对象的字符串表示。
        /// </summary>
        /// <returns>包含SerializedFileHeader所有字段信息的字符串。</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"MetadataSize: 0x{m_MetadataSize:X8} | ");
            sb.Append($"FileSize: 0x{m_FileSize:X8} | ");
            sb.Append($"Version: {m_Version} | ");
            sb.Append($"DataOffset: 0x{m_DataOffset:X8} | ");
            sb.Append($"Endianness: {(EndianType)m_Endianess}");
            return sb.ToString();
        }
    }
}