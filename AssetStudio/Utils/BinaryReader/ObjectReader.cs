using System;
using System.IO;

namespace AssetStudio
{
    /// <summary> 封装了 BinaryReader，并提供了读取各种类型的方法。 </summary>
    public class ObjectReader : EndianBinaryReader
    {
        /// 关联的资源文件对象
        public SerializedFile assetsFile;

        /// 游戏类型枚举
        public Game Game;

        /// 路径ID，唯一标识一个对象
        public long m_PathID;

        /// 字节开始位置
        public long byteStart;

        /// 字节大小
        public uint byteSize;

        /// 类ID类型
        public ClassIDType type;

        /// 序列化类型
        public SerializedType serializedType;

        /// 构建目标平台
        public BuildTarget platform;

        /// 序列化文件格式版本
        public SerializedFileFormatVersion m_Version;

        /// 版本属性，代理到关联的资源文件
        public int[] version => assetsFile.version;

        /// 构建类型属性，代理到关联的资源文件
        public BuildType buildType => assetsFile.buildType;

        /// Unity版本号，默认值为"2.5.0f5"
        public string unityVersion = "2.5.0f5";

        /// 表示是否为Unity内部版本的布尔值
        public bool IsTuanJie = false;

        /// <summary>
        /// 初始化 ObjectReader 实例。
        /// </summary>
        /// <param name="reader">基础的 EndianBinaryReader 对象。</param>
        /// <param name="assetsFile">关联的 SerializedFile 对象。</param>
        /// <param name="objectInfo">对象的信息，包含路径ID、字节开始位置、字节大小和类ID。</param>
        /// <param name="game">游戏类型枚举。</param>
        public ObjectReader(EndianBinaryReader reader, SerializedFile assetsFile, ObjectInfo objectInfo, Game game) : base(reader.BaseStream, reader.Endian)
        {
            this.assetsFile = assetsFile;
            Game = game;
            m_PathID = objectInfo.m_PathID;
            byteStart = objectInfo.byteStart;
            byteSize = objectInfo.byteSize;
            if (Enum.IsDefined(typeof(ClassIDType), objectInfo.classID))
            {
                type = (ClassIDType)objectInfo.classID;
            }
            else
            {
                type = ClassIDType.UnknownType;
            }

            serializedType = objectInfo.serializedType;
            platform = assetsFile.m_TargetPlatform;
            m_Version = assetsFile.header.m_Version;
            unityVersion = assetsFile.unityVersion;

            IsTuanJie = unityVersion.Contains("t");

            Logger.Verbose($"为文件 {assetsFile.fileName} 中具有 {m_PathID} 的 {type} 对象初始化读取器！！");
        }

        /// <summary>
        /// 读取字节到指定数组中。
        /// </summary>
        /// <param name="buffer">目标缓冲区。</param>
        /// <param name="index">开始写入的索引。</param>
        /// <param name="count">要读取的字节数。</param>
        /// <returns>实际读取的字节数。</returns>
        /// <exception cref="EndOfStreamException">尝试读取超过流末端的数据时抛出。</exception>
        public override int Read(byte[] buffer, int index, int count)
        {
            var pos = Position - byteStart;
            if (pos + count > byteSize)
            {
                throw new EndOfStreamException("无法读取超过流末端的数据。");
            }

            return base.Read(buffer, index, count);
        }

        /// <summary>
        /// 重置读取器的位置到对象的开始偏移量。
        /// </summary>
        public void Reset()
        {
            Logger.Verbose($"重置读取器位置到对象偏移量 0x{byteStart:X8}...");
            Position = byteStart;
        }

        /// <summary>
        /// 读取一个 Vector3 对象。
        /// </summary>
        /// <returns>读取到的 Vector3 对象。</returns>
        /// <remarks>根据Unity版本调整读取策略，在5.4版本后，Vector3由3个分量组成；之前则由4个分量组成。</remarks>
        public Vector3 ReadVector3()
        {
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 4))
            {
                return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
            }
            else
            {
                return new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
            }
        }

        /// <summary>
        /// 读取一个 XForm 对象，包括位置、旋转和缩放信息。
        /// </summary>
        /// <returns>读取到的 XForm 对象。</returns>
        public XForm ReadXForm()
        {
            var t = ReadVector3();
            var q = ReadQuaternion();
            var s = ReadVector3();

            return new XForm(t, q, s);
        }

        /// <summary>
        /// 读取一个 XForm4 对象，包括位置、旋转和缩放信息。
        /// </summary>
        /// <returns>读取到的 XForm4 对象。</returns>
        public XForm ReadXForm4()
        {
            var t = ReadVector4();
            var q = ReadQuaternion();
            var s = ReadVector4();

            return new XForm(t, q, s);
        }

        /// <summary>
        /// 读取一个 Vector3 数组。
        /// </summary>
        /// <param name="length">数组长度，如果为0，则从流中读取长度。</param>
        /// <returns>读取到的 Vector3 数组。</returns>
        public Vector3[] ReadVector3Array(int length = 0)
        {
            if (length == 0)
            {
                length = ReadInt32();
            }

            return ReadArray(ReadVector3, length);
        }

        /// <summary>
        /// 读取一个 XForm 数组。
        /// </summary>
        /// <returns>读取到的 XForm 数组。</returns>
        public XForm[] ReadXFormArray()
        {
            return ReadArray(ReadXForm, ReadInt32());
        }
        
        /// <summary> 相对位置 </summary>
        public long RelativePosition
        {
            get => Position - byteStart;
            set => Position = value + byteStart;
        }

        /// 保存到文件
        public void Save(string name)
        {
            string path = $@"E:\Project\Unpack\{Game.Name}\UnpackData\{type.ToString()}\{name}";
            var position = Position;
            Reset();
            Logger.Info($"开始保存位置：{RelativePosition}，Position：{Position}");
            // 创建文件夹
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var file = new FileStream(path, FileMode.Create))
            {
                var buffer = new byte[Remaining];
                Read(buffer);
                file.Write(buffer, 0, buffer.Length);
            }
            Logger.Info($"保存完毕！{RelativePosition}，Position：{Position}");
            Position = position;
        }
    }
}