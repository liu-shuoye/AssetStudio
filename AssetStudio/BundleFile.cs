using ZstdSharp;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Buffers;

namespace AssetStudio
{
    /// <summary>
    /// 指示存档文件中包含的数据类型以及如何处理这些数据的标志。
    /// 使用Flags属性表示该枚举可以进行位操作。
    /// </summary>
    [Flags]
    public enum ArchiveFlags
    {
        /// <summary>
        /// 压缩类型掩码，用于表示不同的压缩类型。
        /// </summary>
        CompressionTypeMask = 0x3f,
        
        /// <summary>
        /// 表示文件块和目录信息是结合在一起的。
        /// </summary>
        BlocksAndDirectoryInfoCombined = 0x40,
        
        /// <summary>
        /// 表示文件块信息位于文件末尾。
        /// </summary>
        BlocksInfoAtTheEnd = 0x80,
        
        /// <summary>
        /// 保留旧Web插件兼容性选项。
        /// </summary>
        OldWebPluginCompatibility = 0x100,
        
        /// <summary>
        /// 表示块信息需要在开始处填充。
        /// </summary>
        BlockInfoNeedPaddingAtStart = 0x200,
        
        /// <summary>
        /// 表示使用Unity CN加密。
        /// </summary>
        UnityCNEncryption = 0x400
    }

    /// <summary> 定义存储块标志的枚举，用于表示存储块的属性和状态 </summary>
    [Flags]
    public enum StorageBlockFlags
    {
        /// <summary>  定义压缩类型掩码，用于表示压缩类型的标志位 </summary>
        CompressionTypeMask = 0x3f,
        
        /// <summary>  表示存储块是否为流式传输的状态标志 </summary>
        Streamed = 0x40,
    }
    /// <summary>
    /// 压缩类型的枚举，列出了各种可用的压缩算法。
    /// </summary>
    public enum CompressionType
    {
        /// <summary>
        /// 无压缩。
        /// </summary>
        None,
    
        /// <summary>
        /// Lzma压缩算法。
        /// </summary>
        Lzma,
    
        /// <summary>
        /// Lz4压缩算法，提供较快的压缩和解压缩速度。
        /// </summary>
        Lz4,
    
        /// <summary>
        /// Lz4高压缩版本（HC），提供比Lz4更高的压缩率，但速度较慢。
        /// </summary>
        Lz4HC,
    
        /// <summary>
        /// Lzham压缩算法，支持多线程压缩和高压缩率。
        /// </summary>
        Lzham,
    
        /// <summary>
        /// Lz4Mr0k，Lz4的多线程版本，具有更快的压缩速度。
        /// </summary>
        Lz4Mr0k,
    
        /// <summary>
        /// Lz4Inv，一种使用Lz4算法的特殊压缩模式，值为5。
        /// </summary>
        Lz4Inv = 5,
    
        /// <summary>
        /// Zstd压缩算法，提供良好的压缩率和快速的解压缩速度，值也为5。
        /// </summary>
        Zstd = 5,
    
        /// <summary>
        /// Lz4Lit4，Lz4的一种文学模式，值为4。
        /// </summary>
        Lz4Lit4 = 4,
    
        /// <summary>
        /// Lz4Lit5，Lz4的另一种文学模式，值为5。
        /// </summary>
        Lz4Lit5 = 5,
    }
    /// <summary> AB包文件 </summary>
    public class BundleFile
    {
        /// <summary> AB包文件头 </summary>
        public class Header
        {
            /// <summary> 签名：UnityFS </summary>
            public string signature;
            /// <summary> 版本号 </summary>
            public uint version;
            /// <summary> Unity版本号： 5.x.x</summary>
            public string unityVersion;
            /// <summary> Unity版本提交号： 2019.4.25f1</summary>
            public string unityRevision;
            /// <summary> 文件大小 </summary>
            public long size;
            /// <summary> 压缩的块信息大小 </summary>
            public uint compressedBlocksInfoSize;
            /// <summary> 解压的块信息大小 </summary>
            public uint uncompressedBlocksInfoSize;
            /// <summary> 存档文件中包含的数据类型 </summary>
            public ArchiveFlags flags;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append($"signature: {signature} | ");
                sb.Append($"version: {version} | ");
                sb.Append($"unityVersion: {unityVersion} | ");
                sb.Append($"unityRevision: {unityRevision} | ");
                sb.Append($"size: 0x{size:X8} | ");
                sb.Append($"compressedBlocksInfoSize: 0x{compressedBlocksInfoSize:X8} | ");
                sb.Append($"uncompressedBlocksInfoSize: 0x{uncompressedBlocksInfoSize:X8} | ");
                sb.Append($"flags: 0x{(int)flags:X8}");
                return sb.ToString();
            }
        }

        /// <summary> 存储块信息 </summary>
        public class StorageBlock
        {
            /// <summary> 压缩大小 </summary>
            public uint compressedSize;
            /// <summary> 解压大小 </summary>
            public uint uncompressedSize;
            /// <summary> 压缩类型 </summary>
            public StorageBlockFlags flags;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append($"compressedSize: 0x{compressedSize:X8} | ");
                sb.Append($"uncompressedSize: 0x{uncompressedSize:X8} | ");
                sb.Append($"flags: 0x{(int)flags:X8}");
                return sb.ToString();
            }
        }

        /// <summary> 资源节点 </summary>
        public class Node
        {
            /// <summary> 文件偏移量 </summary>
            public long offset;
            /// <summary> 文件大小 </summary>
            public long size;
            /// <summary> 节点标志 </summary>
            public uint flags;
            /// <summary> 资源路径 </summary>
            public string path;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append($"offset: 0x{offset:X8} | ");
                sb.Append($"size: 0x{size:X8} | ");
                sb.Append($"flags: {flags} | ");
                sb.Append($"path: {path}");
                return sb.ToString();
            }
        }

        /// <summary> 游戏信息 </summary>
        private Game Game;
        private UnityCN UnityCN;

        /// <summary> 文件头 </summary>
        public Header m_Header;
        /// <summary> 资源目录信息 </summary>
        private List<Node> m_DirectoryInfo;
        /// <summary> 资源块信息 </summary>
        private List<StorageBlock> m_BlocksInfo;

        /// <summary> 文件列表 </summary>
        public List<StreamFile> fileList;
        
        /// <summary> 默认为true，表示有无解压数据哈希值 </summary>
        private bool HasUncompressedDataHash = true;
        /// <summary> 默认为true，表示是否需要填充数据到块的开始位置 </summary>
        private bool HasBlockInfoNeedPaddingAtStart = true;

        /// <summary> 读取AB包文件 </summary>
        public BundleFile(FileReader reader, Game game)
        {
            Game = game;
            m_Header = ReadBundleHeader(reader);
            switch (m_Header.signature)
            {
                case "UnityArchive":
                    break; //TODO
                case "UnityWeb":
                case "UnityRaw":
                    if (m_Header.version == 6)
                    {
                        goto case "UnityFS";
                    }
                    ReadHeaderAndBlocksInfo(reader);
                    using (var blocksStream = CreateBlocksStream(reader.FullPath))
                    {
                        ReadBlocksAndDirectory(reader, blocksStream);
                        ReadFiles(blocksStream, reader.FullPath);
                    }
                    break;
                case "UnityFS":
                case "ENCR":
                    ReadHeader(reader);
                    if (game.Type.IsUnityCN())
                    {
                        ReadUnityCN(reader);
                    }
                    ReadBlocksInfoAndDirectory(reader);
                    using (var blocksStream = CreateBlocksStream(reader.FullPath))
                    {
                        ReadBlocks(reader, blocksStream);
                        ReadFiles(blocksStream, reader.FullPath);
                    }
                    break;
            }
        }

        /// <summary> 读取文件头 </summary>
        private Header ReadBundleHeader(FileReader reader)
        {
            Header header = new Header();
            header.signature = reader.ReadStringToNull(20);
            Logger.Verbose($"解析的签名 {header.signature}。");
            switch (header.signature)
            {
                case "UnityFS":
                    if (Game.Type.IsBH3Group() || Game.Type.IsBH3PrePre())
                    {
                        if (Game.Type.IsBH3Group())
                        {
                            var key = reader.ReadUInt32();
                            if (key <= 11)
                            {
                                reader.Position -= 4;
                                goto default;
                            }
                            Logger.Verbose($"使用密钥 {key} 加密的包头");
                            XORShift128.InitSeed(key);
                        }
                        else if (Game.Type.IsBH3PrePre())
                        {
                            Logger.Verbose($"使用密钥 {reader.Length} 加密的包头");
                            XORShift128.InitSeed((uint)reader.Length);
                        }

                        header.version = 6;
                        header.unityVersion = "5.x.x";
                        header.unityRevision = "2017.4.18f1";
                    }
                    else
                    {
                        header.version = reader.ReadUInt32();
                        header.unityVersion = reader.ReadStringToNull();
                        header.unityRevision = reader.ReadStringToNull();
                    }
                    break;
                case "ENCR":
                    header.version = 6; // is 7 but does not have uncompressedDataHash
                    header.unityVersion = "5.x.x";
                    header.unityRevision = "2019.4.32f1";
                    HasUncompressedDataHash = false;
                    break;
                default:
                    if (Game.Type.IsNaraka())
                    {
                        header.signature = "UnityFS";
                        goto case "UnityFS";
                    }
                    header.version = reader.ReadUInt32();
                    header.unityVersion = reader.ReadStringToNull();
                    header.unityRevision = reader.ReadStringToNull();
                    break;

            }

            return header;
        }

        private void ReadHeaderAndBlocksInfo(FileReader reader)
        {
            if (m_Header.version >= 4)
            {
                var hash = reader.ReadBytes(16);
                var crc = reader.ReadUInt32();
            }
            var minimumStreamedBytes = reader.ReadUInt32();
            m_Header.size = reader.ReadUInt32();
            var numberOfLevelsToDownloadBeforeStreaming = reader.ReadUInt32();
            var levelCount = reader.ReadInt32();
            m_BlocksInfo = new List<StorageBlock>();
            for (int i = 0; i < levelCount; i++)
            {
                var storageBlock = new StorageBlock()
                {
                    compressedSize = reader.ReadUInt32(),
                    uncompressedSize = reader.ReadUInt32(),
                };
                if (i == levelCount - 1)
                {
                    m_BlocksInfo.Add(storageBlock);
                }
            }
            if (m_Header.version >= 2)
            {
                var completeFileSize = reader.ReadUInt32();
            }
            if (m_Header.version >= 3)
            {
                var fileInfoHeaderSize = reader.ReadUInt32();
            }
            reader.Position = m_Header.size;
        }

        /// <summary> 创建解压块的流 </summary>
        private Stream CreateBlocksStream(string path)
        {
            Stream blocksStream;
            var uncompressedSizeSum = m_BlocksInfo.Sum(x => x.uncompressedSize);
            Logger.Verbose($"解压块的总大小: {uncompressedSizeSum}");
            if (uncompressedSizeSum >= int.MaxValue)
            {
                /*var memoryMappedFile = MemoryMappedFile.CreateNew(null, uncompressedSizeSum);
                assetsDataStream = memoryMappedFile.CreateViewStream();*/
                blocksStream = new FileStream(path + ".temp", FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            }
            else
            {
                blocksStream = new MemoryStream((int)uncompressedSizeSum);
            }
            return blocksStream;
        }

        private void ReadBlocksAndDirectory(FileReader reader, Stream blocksStream)
        {
            Logger.Verbose($"正在将块和目录写入块流...");

            var isCompressed = m_Header.signature == "UnityWeb";
            foreach (var blockInfo in m_BlocksInfo)
            {
                var uncompressedBytes = reader.ReadBytes((int)blockInfo.compressedSize);
                if (isCompressed)
                {
                    using var memoryStream = new MemoryStream(uncompressedBytes);
                    using var decompressStream = SevenZipHelper.StreamDecompress(memoryStream);
                    uncompressedBytes = decompressStream.ToArray();
                }
                blocksStream.Write(uncompressedBytes, 0, uncompressedBytes.Length);
            }
            blocksStream.Position = 0;
            var blocksReader = new EndianBinaryReader(blocksStream);
            var nodesCount = blocksReader.ReadInt32();
            m_DirectoryInfo = new List<Node>();
            Logger.Verbose($"目录计数: {nodesCount}");
            for (int i = 0; i < nodesCount; i++)
            {
                m_DirectoryInfo.Add(new Node
                {
                    path = blocksReader.ReadStringToNull(),
                    offset = blocksReader.ReadUInt32(),
                    size = blocksReader.ReadUInt32()
                });
            }
        }

        /// <summary> 读取文件 </summary>
        public void ReadFiles(Stream blocksStream, string path)
        {
            Logger.Verbose($"正在从块流中写入文件...");

            fileList = new List<StreamFile>();
            for (int i = 0; i < m_DirectoryInfo.Count; i++)
            {
                var node = m_DirectoryInfo[i];
                var file = new StreamFile();
                fileList.Add(file);
                file.path = node.path;
                file.fileName = Path.GetFileName(node.path);
                if (node.size >= int.MaxValue)
                {
                    /*var memoryMappedFile = MemoryMappedFile.CreateNew(null, entryinfo_size);
                    file.stream = memoryMappedFile.CreateViewStream();*/
                    var extractPath = path + "_unpacked" + Path.DirectorySeparatorChar;
                    Directory.CreateDirectory(extractPath);
                    file.stream = new FileStream(extractPath + file.fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                }
                else
                {
                    file.stream = new MemoryStream((int)node.size);
                }
                blocksStream.Position = node.offset;
                blocksStream.CopyTo(file.stream, node.size);
                file.stream.Position = 0;
            }
        }

        /// <summary> 读取文件头 </summary>
        private void ReadHeader(FileReader reader)
        {
            if (XORShift128.Init)
            {
                if (Game.Type.IsBH3PrePre())
                {
                    m_Header.uncompressedBlocksInfoSize = reader.ReadUInt32() ^ XORShift128.NextDecryptUInt();
                    m_Header.compressedBlocksInfoSize = reader.ReadUInt32() ^ XORShift128.NextDecryptUInt();
                    m_Header.flags = (ArchiveFlags)(reader.ReadUInt32() ^ XORShift128.NextDecryptInt());
                    m_Header.size = reader.ReadInt64() ^ XORShift128.NextDecryptLong();
                    reader.ReadUInt32(); // version
                }
                else
                {
                    m_Header.flags = (ArchiveFlags)(reader.ReadUInt32() ^ XORShift128.NextDecryptInt());
                    m_Header.size = reader.ReadInt64() ^ XORShift128.NextDecryptLong();
                    m_Header.uncompressedBlocksInfoSize = reader.ReadUInt32() ^ XORShift128.NextDecryptUInt();
                    m_Header.compressedBlocksInfoSize = reader.ReadUInt32() ^ XORShift128.NextDecryptUInt();
                }

                XORShift128.Init = false;
                Logger.Verbose($"包头已解密");
               
                var encUnityVersion = reader.ReadStringToNull();
                var encUnityRevision = reader.ReadStringToNull();
                return;
            }

            m_Header.size = reader.ReadInt64();
            m_Header.compressedBlocksInfoSize = reader.ReadUInt32();
            m_Header.uncompressedBlocksInfoSize = reader.ReadUInt32();
            m_Header.flags = (ArchiveFlags)reader.ReadUInt32();
            if (m_Header.signature != "UnityFS" && !Game.Type.IsSRGroup())
            {
                reader.ReadByte();
            }

            if (Game.Type.IsNaraka())
            {
                m_Header.compressedBlocksInfoSize -= 0xCA;
                m_Header.uncompressedBlocksInfoSize -= 0xCA;
            }

            Logger.Verbose($"包头信息: {m_Header}");
        }

        /// <summary> 解密UnityCN文件 </summary>
        private void ReadUnityCN(FileReader reader)
        {
            Logger.Verbose($"正在尝试使用 UnityCN 加密解密文件 {reader.FileName}");
            ArchiveFlags mask;

            var version = ParseVersion();
            //Flag changed it in these versions
            if (version[0] < 2020 || //2020 and earlier
                (version[0] == 2020 && version[1] == 3 && version[2] <= 34) || //2020.3.34 and earlier
                (version[0] == 2021 && version[1] == 3 && version[2] <= 2) || //2021.3.2 and earlier
                (version[0] == 2022 && version[1] == 3 && version[2] <= 1)) //2022.3.1 and earlier
            {
                mask = ArchiveFlags.BlockInfoNeedPaddingAtStart;
                HasBlockInfoNeedPaddingAtStart = false;
            }
            else
            {
                mask = ArchiveFlags.UnityCNEncryption;
                HasBlockInfoNeedPaddingAtStart = true;
            }

            Logger.Verbose($"掩码设置为 {mask}");

            if ((m_Header.flags & mask) != 0)
            {
                Logger.Verbose($"存在加密标志，文件已加密，尝试解密中");
                if (Game.Type.IsGuiLongChao())
                {
                    UnityCN = new UnityCNGuiLongChao(reader);
                }
                else
                {
                    UnityCN = new UnityCN(reader);
                }
            }
        }

        /// <summary> 读取包中的文件信息 块信息和目录信息</summary>
        private void ReadBlocksInfoAndDirectory(FileReader reader)
        {
            byte[] blocksInfoBytes;
            var version = ParseVersion();
            if (m_Header.version >= 7 && !Game.Type.IsSRGroup())
            {
                reader.AlignStream(16);
            }
            else if (version[0] == 2019 && version[1] == 4)
            {
                var p = reader.Position;
                var len = 16 - p % 16;
                var bytes = reader.ReadBytes((int)len);
                if (bytes.Any(x => x != 0))
                {
                    reader.Position = p;
                }
                else
                {
                    reader.AlignStream(16);
                }
            }
            if ((m_Header.flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0) //kArchiveBlocksInfoAtTheEnd
            {
                var position = reader.Position;
                reader.Position = m_Header.size - m_Header.compressedBlocksInfoSize;
                blocksInfoBytes = reader.ReadBytes((int)m_Header.compressedBlocksInfoSize);
                reader.Position = position;
            }
            else //0x40 BlocksAndDirectoryInfoCombined
            {
                blocksInfoBytes = reader.ReadBytes((int)m_Header.compressedBlocksInfoSize);
            }
            //读取解压后的文件信息
            MemoryStream blocksInfoUncompresseddStream;
            var blocksInfoBytesSpan = blocksInfoBytes.AsSpan(0, (int)m_Header.compressedBlocksInfoSize);
            var uncompressedSize = m_Header.uncompressedBlocksInfoSize;
            var compressionType = (CompressionType)(m_Header.flags & ArchiveFlags.CompressionTypeMask);
            Logger.Verbose($"BlockInfo 压缩类型: {compressionType}");
            switch (compressionType) //kArchiveCompressionTypeMask
            {
                case CompressionType.None: //None
                    {
                        blocksInfoUncompresseddStream = new MemoryStream(blocksInfoBytes);
                        break;
                    }
                case CompressionType.Lzma: //LZMA
                    {
                        blocksInfoUncompresseddStream = new MemoryStream((int)(uncompressedSize));
                        using (var blocksInfoCompressedStream = new MemoryStream(blocksInfoBytes))
                        {
                            SevenZipHelper.StreamDecompress(blocksInfoCompressedStream, blocksInfoUncompresseddStream, m_Header.compressedBlocksInfoSize, m_Header.uncompressedBlocksInfoSize);
                        }
                        blocksInfoUncompresseddStream.Position = 0;
                        break;
                    }
                case CompressionType.Lz4: //LZ4
                case CompressionType.Lz4HC: //LZ4HC
                    {
                        var uncompressedBytes = ArrayPool<byte>.Shared.Rent((int)uncompressedSize);
                        try
                        {
                            var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, (int)uncompressedSize);
                            var numWrite = LZ4.Instance.Decompress(blocksInfoBytesSpan, uncompressedBytesSpan);
                            if (numWrite != uncompressedSize)
                            {
                                throw new IOException($"Lz4 解压缩错误，写入 {numWrite} 字节，但预期为 {uncompressedSize} 字节");
                            }
                            blocksInfoUncompresseddStream = new MemoryStream(uncompressedBytesSpan.ToArray());
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(uncompressedBytes, true);
                        }
                        break;
                    }
                case CompressionType.Lz4Mr0k: //Lz4Mr0k
                    if (Mr0kUtils.IsMr0k(blocksInfoBytesSpan))
                    {
                        Logger.Verbose($"头部被 mr0k 加密，正在解密...");
                        blocksInfoBytesSpan = Mr0kUtils.Decrypt(blocksInfoBytesSpan, (Mr0k)Game).ToArray();
                    }
                    goto case CompressionType.Lz4HC;
                default:
                    throw new IOException($"不支持的压缩类型 {compressionType}");
            }
            using (var blocksInfoReader = new EndianBinaryReader(blocksInfoUncompresseddStream))
            {
                if (HasUncompressedDataHash)
                {
                    var uncompressedDataHash = blocksInfoReader.ReadBytes(16);
                }
                var blocksInfoCount = blocksInfoReader.ReadInt32();
                m_BlocksInfo = new List<StorageBlock>();
                Logger.Verbose($"块数量: {blocksInfoCount}");
                for (int i = 0; i < blocksInfoCount; i++)
                {
                    m_BlocksInfo.Add(new StorageBlock
                    {
                        uncompressedSize = blocksInfoReader.ReadUInt32(),
                        compressedSize = blocksInfoReader.ReadUInt32(),
                        flags = (StorageBlockFlags)blocksInfoReader.ReadUInt16()
                    });

                    Logger.Verbose($"块 {i} 信息: {m_BlocksInfo[i]}");
                }

                var nodesCount = blocksInfoReader.ReadInt32();
                m_DirectoryInfo = new List<Node>();
                Logger.Verbose($"目录计数: {nodesCount}");
                for (int i = 0; i < nodesCount; i++)
                {
                    m_DirectoryInfo.Add(new Node
                    {
                        offset = blocksInfoReader.ReadInt64(),
                        size = blocksInfoReader.ReadInt64(),
                        flags = blocksInfoReader.ReadUInt32(),
                        path = blocksInfoReader.ReadStringToNull(),
                    });

                    Logger.Verbose($"目录 {i} 信息: {m_DirectoryInfo[i]}");
                }
            }
            if (HasBlockInfoNeedPaddingAtStart && (m_Header.flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            {
                reader.AlignStream(16);
            }
        }

        /// <summary> 读取块内容到 blocksStream </summary>
        private void ReadBlocks(FileReader reader, Stream blocksStream)
        {
            Logger.Verbose($"正在将块写入块流...");

            for (int i = 0; i < m_BlocksInfo.Count; i++)
            {
                Logger.Verbose($"正在读取块 {i}...");
                var blockInfo = m_BlocksInfo[i];
                var compressionType = (CompressionType)(blockInfo.flags & StorageBlockFlags.CompressionTypeMask);
                Logger.Verbose($"块压缩类型 {compressionType}");
                switch (compressionType) //kStorageBlockCompressionTypeMask
                {
                    case CompressionType.None: //None
                        {
                            reader.BaseStream.CopyTo(blocksStream, blockInfo.compressedSize);
                            break;
                        }
                    case CompressionType.Lzma: //LZMA
                        {
                            var compressedStream = reader.BaseStream;
                            if (Game.Type.IsNetEase() && i == 0)
                            {
                                var compressedBytesSpan = reader.ReadBytes((int)blockInfo.compressedSize).AsSpan();
                                NetEaseUtils.DecryptWithoutHeader(compressedBytesSpan);
                                var ms = new MemoryStream(compressedBytesSpan.ToArray());
                                compressedStream = ms;
                            }
                            SevenZipHelper.StreamDecompress(compressedStream, blocksStream, blockInfo.compressedSize, blockInfo.uncompressedSize);
                            break;
                        }
                    case CompressionType.Lz4: //LZ4
                    case CompressionType.Lz4HC: //LZ4HC
                    case CompressionType.Lz4Mr0k when Game.Type.IsMhyGroup(): //Lz4Mr0k
                        {
                            var compressedSize = (int)blockInfo.compressedSize;
                            var uncompressedSize = (int)blockInfo.uncompressedSize;

                            var compressedBytes = ArrayPool<byte>.Shared.Rent(compressedSize);
                            var uncompressedBytes = ArrayPool<byte>.Shared.Rent(uncompressedSize);

                            try
                            {
                                var compressedBytesSpan = compressedBytes.AsSpan(0, compressedSize);
                                var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, uncompressedSize);

                                reader.Read(compressedBytesSpan);
                                if (compressionType == CompressionType.Lz4Mr0k && Mr0kUtils.IsMr0k(compressedBytes))
                                {
                                    Logger.Verbose($"块使用 mr0k 加密，正在解密...");
                                    compressedBytesSpan = Mr0kUtils.Decrypt(compressedBytesSpan, (Mr0k)Game);
                                }
                                if (Game.Type.IsUnityCN() && ((int)blockInfo.flags & 0x100) != 0)
                                {
                                    Logger.Verbose($"使用 UnityCN 解密块中...");
                                    UnityCN.DecryptBlock(compressedBytes, compressedSize, i);
                                }
                                if (Game.Type.IsNetEase() && i == 0)
                                {
                                    NetEaseUtils.DecryptWithHeader(compressedBytesSpan);
                                }
                                if (Game.Type.IsArknightsEndfield() && i == 0)
                                {
                                    FairGuardUtils.Decrypt(compressedBytesSpan);
                                }
                                if (Game.Type.IsOPFP())
                                {
                                    OPFPUtils.Decrypt(compressedBytesSpan, reader.FullPath);
                                }
                                var numWrite = LZ4.Instance.Decompress(compressedBytesSpan, uncompressedBytesSpan);
                                if (numWrite != uncompressedSize)
                                {
                                    throw new IOException($"Lz4 解压缩错误，写入 {numWrite} 字节，但预期为 {uncompressedSize} 字节");
                                }
                                blocksStream.Write(uncompressedBytesSpan);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(compressedBytes, true);
                                ArrayPool<byte>.Shared.Return(uncompressedBytes, true);
                            }
                            break;
                        }
                    case CompressionType.Lz4Inv when Game.Type.IsArknightsEndfield():
                        {
                            var compressedSize = (int)blockInfo.compressedSize;
                            var uncompressedSize = (int)blockInfo.uncompressedSize;

                            var compressedBytes = ArrayPool<byte>.Shared.Rent(compressedSize);
                            var uncompressedBytes = ArrayPool<byte>.Shared.Rent(uncompressedSize);

                            var compressedBytesSpan = compressedBytes.AsSpan(0, compressedSize);
                            var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, uncompressedSize);

                            try
                            {
                                reader.Read(compressedBytesSpan);
                                if (i == 0)
                                {
                                    FairGuardUtils.Decrypt(compressedBytesSpan);
                                }

                                var numWrite = LZ4Inv.Instance.Decompress(compressedBytesSpan, uncompressedBytesSpan);
                                if (numWrite != uncompressedSize)
                                {
                                    throw new IOException($"Lz4 解压缩错误，写入 {numWrite} 字节，但预期为 {uncompressedSize} 字节");
                                }
                                blocksStream.Write(uncompressedBytesSpan);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(compressedBytes, true);
                                ArrayPool<byte>.Shared.Return(uncompressedBytes, true);
                            }
                            break;
                        }
                    case CompressionType.Lz4Lit4 or CompressionType.Lz4Lit5 when Game.Type.IsExAstris():
                        {
                            var compressedSize = (int)blockInfo.compressedSize;
                            var uncompressedSize = (int)blockInfo.uncompressedSize;

                            var compressedBytes = ArrayPool<byte>.Shared.Rent(compressedSize);
                            var uncompressedBytes = ArrayPool<byte>.Shared.Rent(uncompressedSize);

                            var compressedBytesSpan = compressedBytes.AsSpan(0, compressedSize);
                            var uncompressedBytesSpan = uncompressedBytes.AsSpan(0, uncompressedSize);

                            try
                            {
                                reader.Read(compressedBytesSpan);
                                var numWrite = LZ4Lit.Instance.Decompress(compressedBytesSpan, uncompressedBytesSpan);
                                if (numWrite != uncompressedSize)
                                {
                                    throw new IOException($"Lz4 解压缩错误，写入 {numWrite} 字节，但预期为 {uncompressedSize} 字节");
                                }
                                blocksStream.Write(uncompressedBytesSpan);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(compressedBytes, true);
                                ArrayPool<byte>.Shared.Return(uncompressedBytes, true);
                            }
                            break;
                        }
                    case CompressionType.Zstd when !Game.Type.IsMhyGroup(): //Zstd
                        {
                            var compressedSize = (int)blockInfo.compressedSize;
                            var uncompressedSize = (int)blockInfo.uncompressedSize;

                            var compressedBytes = ArrayPool<byte>.Shared.Rent(compressedSize);
                            var uncompressedBytes = ArrayPool<byte>.Shared.Rent(uncompressedSize);

                            try
                            {
                                reader.Read(compressedBytes, 0, compressedSize);
                                using var decompressor = new Decompressor();
                                var numWrite = decompressor.Unwrap(compressedBytes, 0, compressedSize, uncompressedBytes, 0, uncompressedSize);
                                if (numWrite != uncompressedSize)
                                {
                                    throw new IOException($"Zstd 解压缩错误，写入 {numWrite} 字节，但预期为 {uncompressedSize} 字节");
                                }
                                blocksStream.Write(uncompressedBytes.ToArray(), 0, uncompressedSize);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Zstd decompression error:\n{ex}");
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(compressedBytes, true);
                                ArrayPool<byte>.Shared.Return(uncompressedBytes, true);
                            }
                            break;
                        }
                    default:
                        throw new IOException($"不支持的压缩类型 {compressionType}");
                }
            }
            blocksStream.Position = 0;
        }

        /// <summary>  解析版本号，返回一个数组，数组中的元素为版本号，如 2018.4.0f1 解析为 [2018, 4, 0]</summary>
        public int[] ParseVersion()
        {
            var versionSplit = Regex.Replace(m_Header.unityRevision, @"\D", ".").Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
            return versionSplit.Select(int.Parse).ToArray();
        }
    }
}
