using System.Windows.Forms;

namespace AssetStudio.GUI
{
    /// <summary> UI资产列表项 </summary>
    public class AssetItem : ListViewItem
    {
        /// <summary> 资产对象 </summary>
        public readonly Object Asset;
        /// <summary> 资产序列化文件 </summary>
        public readonly SerializedFile SourceFile;
        /// <summary> 资产所在容器 </summary>
        public string Container = string.Empty;
        /// <summary> 资产类型字符串 </summary>
        public readonly string TypeString;
        /// <summary> 资产路径ID </summary>
        public readonly long PathID;
        /// <summary> 资产大小 </summary>
        public long FullSize;
        /// <summary> 资产类型 </summary>
        public readonly ClassIDType Type;
        /// <summary> 资产信息 </summary>
        public string InfoText;
        /// <summary> 资产唯一ID </summary>
        public string UniqueID;
        /// <summary> 资产树节点 </summary>
        public GameObjectTreeNode TreeNode;

        public AssetItem(Object asset)
        {
            Asset = asset;
            Text = asset.Name;
            SourceFile = asset.assetsFile;
            Type = asset.type;
            TypeString = Type.ToString();
            PathID = asset.m_PathID;
            FullSize = asset.byteSize;
        }

        /// <summary> 设置子项 </summary>
        public void SetSubItems()
        {
            SubItems.AddRange(new[]
            {
                Container, //Container
                TypeString, //Type
                PathID.ToString(), //PathID
                FullSize.ToString(), //Size
            });
        }
    }
}