using System.Text;

namespace AssetStudio
{
    /// <summary> 文本资源 </summary>
    public sealed class TextAsset : NamedObject
    {
        /// <summary> 文本内容 </summary>
        private readonly byte[] _content;

        public string Text => Encoding.UTF8.GetString(_content);

        public byte[] Data => _content;

        public TextAsset(ObjectReader reader) : base(reader)
        {
            _content = reader.ReadUInt8Array();
        }
    }
}