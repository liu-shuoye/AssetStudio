using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    /// <summary> 本地序列化对象标识符类，用于唯一标识一个在序列化文件中的对象 </summary>
    public class LocalSerializedObjectIdentifier
    {
        /// <summary> 序列化文件的本地索引，用于定位特定的序列化文件 </summary>
        public int localSerializedFileIndex;

        /// <summary> 在文件中的本地标识符，用于唯一标识文件内的特定对象 </summary>
        public long localIdentifierInFile;

        /// <summary> 重写ToString方法，以方便调试和日志记录时查看对象信息 </summary>
        /// <returns>包含序列化文件索引和文件内标识符的字符串</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"localSerializedFileIndex: {localSerializedFileIndex} | ");
            sb.Append($"localIdentifierInFile: {localIdentifierInFile}");
            return sb.ToString();
        }
    }

}
