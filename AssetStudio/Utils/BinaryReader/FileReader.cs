using System;
using System.IO;
using System.Linq;
using static AssetStudio.ImportHelper;

namespace AssetStudio
{
    /// <summary> 文件读取器 </summary>
    public class FileReader : EndianBinaryReader
    {
        /// <summary> 文件全路径 </summary>
        public string FullPath;
        /// <summary> 文件名 </summary>
        public string FileName;
        /// <summary> 文件类型 </summary>
        public FileType FileType;

        private static readonly byte[] gzipMagic = { 0x1f, 0x8b };
        private static readonly byte[] brotliMagic = { 0x62, 0x72, 0x6F, 0x74, 0x6C, 0x69 };
        private static readonly byte[] zipMagic = { 0x50, 0x4B, 0x03, 0x04 };
        private static readonly byte[] zipSpannedMagic = { 0x50, 0x4B, 0x07, 0x08 };
        private static readonly byte[] mhy0Magic = { 0x6D, 0x68, 0x79, 0x30 };
        private static readonly byte[] blbMagic = { 0x42, 0x6C, 0x62, 0x02 };
        private static readonly byte[] narakaMagic = { 0x15, 0x1E, 0x1C, 0x0D, 0x0D, 0x23, 0x21 };
        private static readonly byte[] gunfireMagic = { 0x7C, 0x6D, 0x79, 0x72, 0x27, 0x7A, 0x73, 0x78, 0x3F };


        public FileReader(string path) : this(path, File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { }

        public FileReader(string path, Stream stream, bool leaveOpen = false) : base(stream, EndianType.BigEndian, leaveOpen)
        {
            FullPath = Path.GetFullPath(path);
            FileName = Path.GetFileName(path);
            FileType = CheckFileType();
            Logger.Verbose($"文件 {path} 类型是 {FileType}");
        }

        /// <summary> 检查文件类型 </summary>
        private FileType CheckFileType()
        {
            var signature = this.ReadStringToNull(20);
            Position = 0;
            Logger.Verbose($"解析的签名为 {signature}。");
            switch (signature)
            {
                case "UnityWeb":
                case "UnityRaw":
                case "UnityArchive":
                case "UnityFS":
                    return FileType.BundleFile;
                case "UnityWebData1.0":
                    return FileType.WebFile;
                case "TuanjieWebData1.0":
                    return FileType.WebFile;
                case "blk":
                    return FileType.BlkFile;
                case "ENCR":
                    return FileType.ENCRFile;
                default:
                    {
                        Logger.Verbose("签名与任何支持的字符串签名不匹配，正在尝试检查字节签名");
                        byte[] magic = ReadBytes(2);
                        Position = 0;
                        Logger.Verbose($"解析的签名为 {Convert.ToHexString(magic)}。");
                        if (gzipMagic.SequenceEqual(magic))
                        {
                            return FileType.GZipFile;
                        }
                        Logger.Verbose($"解析的签名与预期签名 {Convert.ToHexString(gzipMagic)} 不匹配。");
                        Position = 0x20;
                        magic = ReadBytes(6);
                        Position = 0;
                        Logger.Verbose($"解析的签名为 {Convert.ToHexString(magic)}。");
                        if (brotliMagic.SequenceEqual(magic))
                        {
                            return FileType.BrotliFile;
                        }
                        Logger.Verbose($"解析的签名与预期签名 {Convert.ToHexString(brotliMagic)} 不匹配。");
                        if (IsSerializedFile())
                        {
                            return FileType.AssetsFile;
                        }
                        magic = ReadBytes(4);
                        Position = 0;
                        Logger.Verbose($"解析的签名为 {Convert.ToHexString(magic)}。");
                        if (zipMagic.SequenceEqual(magic) || zipSpannedMagic.SequenceEqual(magic))
                        {
                            return FileType.ZipFile;
                        }
                        Logger.Verbose($"解析的签名与预期签名 {Convert.ToHexString(zipMagic)} 或 {Convert.ToHexString(zipSpannedMagic)} 不匹配。");
                        if (mhy0Magic.SequenceEqual(magic))
                        {
                            return FileType.MhyFile;
                        }
                        Logger.Verbose($"解析的签名与预期签名 {Convert.ToHexString(mhy0Magic)} 不匹配。");
                        if (blbMagic.SequenceEqual(magic))
                        {
                            return FileType.BlbFile;
                        }
                        Logger.Verbose($"解析的签名与预期签名 {Convert.ToHexString(mhy0Magic)} 不匹配。");
                        magic = ReadBytes(7);
                        Position = 0;
                        Logger.Verbose($"解析的签名为 {Convert.ToHexString(magic)}。");
                        if (narakaMagic.SequenceEqual(magic))
                        {
                            return FileType.BundleFile;
                        }
                        Logger.Verbose($"解析的签名与预期签名 {Convert.ToHexString(narakaMagic)} 不匹配。");
                        magic = ReadBytes(9);
                        Position = 0;
                        Logger.Verbose($"解析的签名为 {Convert.ToHexString(magic)}。");
                        if (gunfireMagic.SequenceEqual(magic))
                        {
                            Position = 0x32;
                            return FileType.BundleFile;
                        }
                        Logger.Verbose($"解析的签名与预期签名 {Convert.ToHexString(gunfireMagic)} 不匹配。");
                        Logger.Verbose($"解析的签名不匹配任何支持的签名，假定为资源文件。");
                        return FileType.ResourceFile;
                    }
            }
        }

        private bool IsSerializedFile()
        {
            Logger.Verbose($"正在尝试检查文件是否为序列化文件...");

            var fileSize = BaseStream.Length;
            if (fileSize < 20)
            {
                Logger.Verbose($"文件大小 0x{fileSize:X8} 太小，最小可接受大小为 0x14，中止操作...");
                return false;
            }
            var m_MetadataSize = ReadUInt32();
            long m_FileSize = ReadUInt32();
            var m_Version = ReadUInt32();
            long m_DataOffset = ReadUInt32();
            var m_Endianess = ReadByte();
            var m_Reserved = ReadBytes(3);
            if (m_Version >= 22)
            {
                if (fileSize < 48)
                {
                    Logger.Verbose($"版本 {m_Version} 的文件大小 0x{fileSize:X8} 过小，最小可接受大小为 0x30，操作中止...");
                    Position = 0;
                    return false;
                }
                m_MetadataSize = ReadUInt32();
                m_FileSize = ReadInt64();
                m_DataOffset = ReadInt64();
            }
            Position = 0;
            if (m_FileSize != fileSize)
            {
                Logger.Verbose($"解析的文件大小 0x{m_FileSize:X8} 与流大小 {fileSize} 不匹配，文件可能已损坏，中止操作...");
                return false;
            }
            if (m_DataOffset > fileSize)
            {
                Logger.Verbose($"解析的数据偏移量 0x{m_DataOffset:X8} 超出了流大小 {fileSize}，文件可能已损坏，中止操作...");
                return false;
            }
            Logger.Verbose($"有效的序列化文件！！");
            return true;
        }
    }

    /// <summary> 文件读取器静态扩展方法 </summary>
    public static class FileReaderExtensions
    {

        /// <summary> 预处理文件 将加密的文件解密到内存中 </summary>
        public static FileReader PreProcessing(this FileReader reader, Game game, bool autoDetectMultipleBundle = false)
        {
            Logger.Verbose($"正在对文件 {reader.FileName} 进行预处理");
            if (reader.FileType == FileType.ResourceFile || !game.Type.IsNormal())
            {
                Logger.Verbose("文件已加密！！");
                switch (game.Type)
                {
                    case GameType.GI_Pack:
                        reader = DecryptPack(reader, game);
                        break;
                    case GameType.GI_CB1:
                        reader = DecryptMark(reader);
                        break;
                    case GameType.EnsembleStars:
                        reader = DecryptEnsembleStar(reader);
                        break;
                    case GameType.OPFP:
                    case GameType.FakeHeader:
                    case GameType.ShiningNikki:
                        reader = ParseFakeHeader(reader);
                        break;
                    case GameType.FantasyOfWind:
                        reader = DecryptFantasyOfWind(reader);
                        break;
                    case GameType.HelixWaltz2:
                        reader = ParseHelixWaltz2(reader);
                        break;
                    case GameType.AnchorPanic:
                        reader = DecryptAnchorPanic(reader);
                        break;
                    case GameType.DreamscapeAlbireo:
                        reader = DecryptDreamscapeAlbireo(reader);
                        break;
                    case GameType.ImaginaryFest:
                        reader = DecryptImaginaryFest(reader);
                        break;
                    case GameType.AliceGearAegis:
                        reader = DecryptAliceGearAegis(reader);
                        break;
                    case GameType.ProjectSekai:
                        reader = DecryptProjectSekai(reader);
                        break;
                    case GameType.CodenameJump:
                        reader = DecryptCodenameJump(reader);
                        break;
                    case GameType.GirlsFrontline:
                        reader = DecryptGirlsFrontline(reader);
                        break; 
                    case GameType.Reverse1999:
                        reader = DecryptReverse1999(reader);
                        break;
                    case GameType.JJKPhantomParade:
                        reader = DecryptJJKPhantomParade(reader);
                        break;
                    case GameType.MuvLuvDimensions:
                        reader = DecryptMuvLuvDimensions(reader);
                        break;
                    case GameType.PartyAnimals:
                        reader = DecryptPartyAnimals(reader);
                        break;
                    case GameType.LoveAndDeepspace:
                        reader = DecryptLoveAndDeepspace(reader);
                        break;
                    case GameType.SchoolGirlStrikers:
                        reader = DecryptSchoolGirlStrikers(reader);
                        break;
                    case GameType.CounterSide:
                        reader = DecryptCounterSide(reader);
                        break;
                    case GameType.PerpetualNovelty:
                        reader = DecryptPerpetualNovelty(reader);
                        break;
                }
            }
            if (autoDetectMultipleBundle || reader.FileType == FileType.BundleFile && game.Type.IsBlockFile() || reader.FileType == FileType.ENCRFile || reader.FileType == FileType.BlbFile)
            {
                Logger.Verbose("文件可能包含多个包！！");
                try
                {
                    var signature = reader.ReadStringToNull();
                    reader.ReadInt32();
                    reader.ReadStringToNull();
                    reader.ReadStringToNull();
                    var size = reader.ReadInt64();
                    if (size != reader.BaseStream.Length)
                    {
                        Logger.Verbose($"找到签名 {signature}，预期包大小为 0x{size:X8}，实际为 0x{reader.BaseStream.Length}！！");
                        Logger.Verbose("作为块文件加载！！");
                        reader.FileType = FileType.BlockFile;
                    }
                }
                catch (Exception) { }
                reader.Position = 0;
            }

            Logger.Verbose("不需要预处理");
            return reader;
        }
    } 
}
