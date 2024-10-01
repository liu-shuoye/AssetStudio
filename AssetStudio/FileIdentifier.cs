using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    /// <summary> 表示文件的标识信息。 </summary>
    public class FileIdentifier
    {
        /// <summary> 唯一标识文件的GUID。 </summary>
        public Guid guid;

        /// <summary> 文件类型，使用枚举值表示不同的资产类型。 </summary>
        public int type; //enum { kNonAssetType = 0, kDeprecatedCachedAssetType = 1, kSerializedAssetType = 2, kMetaAssetType = 3 };

        /// <summary> 文件的路径名称。 </summary>
        public string pathName;

        // 自定义属性：文件名。
        public string fileName;

        /// <summary> 重写ToString方法，以字符串形式展示文件标识的信息。 </summary>
        /// <returns>返回包含文件GUID、类型、路径和文件名的字符串。</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"Guid: {guid} | ");
            sb.Append($"type: {type} | ");
            sb.Append($"pathName: {pathName} | ");
            sb.Append($"fileName: {fileName}");
            return sb.ToString();
        }
    }

}
