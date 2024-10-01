using System.IO;

namespace AssetStudio
{
    /// <summary>
    /// 表示一个文件流的类，用于封装文件的路径、名称及其对应的流对象。
    /// </summary>
    public class StreamFile
    {
        /// <summary>
        /// 文件的完整路径。
        /// </summary>
        public string path;

        /// <summary>
        /// 文件的名称。
        /// </summary>
        public string fileName;

        /// <summary>
        /// 文件的流对象，用于文件数据的读写。
        /// </summary>
        public Stream stream;
    }
}