using System.Text;

namespace AssetStudio
{
    /// <summary> 表示对象的信息。 </summary>
    public class ObjectInfo
    {
        /// <summary> 对象的字节起始位置。 </summary>
        public long byteStart;

        /// <summary> 对象的字节大小。 </summary>
        public uint byteSize;

        /// <summary> 对象的类型ID。 </summary>
        public int typeID;

        /// <summary> 对象的类ID。 </summary>
        public int classID;

        /// <summary> 表示对象是否被销毁。 </summary>
        public ushort isDestroyed;

        /// <summary> 表示对象的剥离状态。 </summary>
        public byte stripped;

        /// <summary> 对象的路径ID。 </summary>
        public long m_PathID;

        /// <summary> 对象的序列化类型。 </summary>
        public SerializedType serializedType;

        /// <summary> 重写ToString方法，返回对象信息的字符串表示。 </summary>
        /// <returns>对象信息的字符串表示。</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"byteStart: 0x{byteStart:X8} | ");
            sb.Append($"byteSize: 0x{byteSize:X8} | ");
            sb.Append($"typeID: {typeID} | ");
            sb.Append($"classID: {classID} | ");
            sb.Append($"isDestroyed: {isDestroyed} | ");
            sb.Append($"stripped: {stripped} | ");
            sb.Append($"PathID: {m_PathID}");
            return sb.ToString();
        }
    }

}
