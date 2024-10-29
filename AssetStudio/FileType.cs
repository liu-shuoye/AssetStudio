using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetStudio
{
    /// <summary> 文件类型 </summary>
    public enum FileType
    {
        AssetsFile,
        BundleFile,
        WebFile,
        /// <summary> Resources文件 </summary>
        ResourceFile,
        GZipFile,
        BrotliFile,
        ZipFile,
        BlkFile,
        /// <summary> 米哈游文件 </summary>
        MhyFile,
        BlbFile,
        ENCRFile,
        BlockFile
    }
}
