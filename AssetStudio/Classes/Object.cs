using Newtonsoft.Json;
using System.Collections.Specialized;

namespace AssetStudio
{
    public class Object
    {
        [JsonIgnore]
        public SerializedFile assetsFile;
        [JsonIgnore]
        public ObjectReader reader;
        [JsonIgnore]
        public long m_PathID;
        [JsonIgnore]
        public int[] version;
        [JsonIgnore]
        protected BuildType buildType;
        [JsonIgnore]
        public BuildTarget platform;
        [JsonIgnore]
        public ClassIDType type;
        [JsonIgnore]
        public SerializedType serializedType;
        [JsonIgnore]
        public uint byteSize;

        public virtual string Name => string.Empty;

        public Object(ObjectReader reader)
        {
            this.reader = reader;
            reader.Reset();
            assetsFile = reader.assetsFile;
            type = reader.type;
            m_PathID = reader.m_PathID;
            version = reader.version;
            buildType = reader.buildType;
            platform = reader.platform;
            serializedType = reader.serializedType;
            byteSize = reader.byteSize;

            Logger.Verbose($"正在尝试读取文件 {assetsFile.fileName} 中类型为 {type} 的对象 {m_PathID}，从偏移量 0x{reader.byteStart:X8} 开始，大小为 0x{byteSize:X8}！！");

            if (platform == BuildTarget.NoTarget)
            {
                var m_ObjectHideFlags = reader.ReadUInt32();
            }
        }

        public string Dump()
        {
            if (serializedType?.m_Type != null)
            {
                return TypeTreeHelper.ReadTypeString(serializedType.m_Type, reader);
            }
            return null;
        }

        public string Dump(TypeTree m_Type)
        {
            if (m_Type != null)
            {
                return TypeTreeHelper.ReadTypeString(m_Type, reader);
            }
            return null;
        }

        public OrderedDictionary ToType()
        {
            if (serializedType?.m_Type != null)
            {
                var dictionary = TypeTreeHelper.ReadType(serializedType.m_Type, reader, assetsFile);
                return dictionary;
            }
            return null;
        }

        public OrderedDictionary ToType(TypeTree m_Type)
        {
            if (m_Type != null)
            {
                return TypeTreeHelper.ReadType(m_Type, reader);
            }
            return null;
        }

        public byte[] GetRawData()
        {
            Logger.Verbose($"正在转储文件 {assetsFile.fileName} 中对象 {m_PathID} 的原始字节...");
            reader.Reset();
            return reader.ReadBytes((int)byteSize);
        }
    }
}
