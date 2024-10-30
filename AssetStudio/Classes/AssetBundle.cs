using System.Collections.Generic;

namespace AssetStudio
{
    /// <summary> 资源包信息 </summary>
    public class AssetInfo
    {
        /// <summary> 预加载索引 </summary>
        public readonly int PreloadIndex;

        /// <summary> 资源包大小 </summary>
        public readonly int PreloadSize;

        /// <summary> 资源包对象 </summary>
        public PPtr<Object> Asset;

        /// <summary> 构造函数 </summary>
        public AssetInfo(ObjectReader reader)
        {
            PreloadIndex = reader.ReadInt32();
            PreloadSize = reader.ReadInt32();
            Asset = new PPtr<Object>(reader);
        }
    }

    /// <summary> ab包信息 </summary>
    public sealed class AssetBundle : NamedObject
    {
        /// <summary> 预加载表 </summary>
        public readonly List<PPtr<Object>> PreloadTable;

        /// <summary> 资源包表 </summary>
        public readonly List<KeyValuePair<string, AssetInfo>> Container;

        public AssetBundle(ObjectReader reader) : base(reader)
        {
            var mPreloadTableSize = reader.ReadInt32();
            PreloadTable = new List<PPtr<Object>>();
            for (int i = 0; i < mPreloadTableSize; i++)
            {
                PreloadTable.Add(new PPtr<Object>(reader));
            }

            var mContainerSize = reader.ReadInt32();
            Container = new List<KeyValuePair<string, AssetInfo>>();
            for (int i = 0; i < mContainerSize; i++)
            {
                Container.Add(new KeyValuePair<string, AssetInfo>(reader.ReadAlignedString(), new AssetInfo(reader)));
            }
        }
    }
}