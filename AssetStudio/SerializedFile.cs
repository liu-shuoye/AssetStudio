using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AssetStudio
{
    
    /// <summary> Unity 序列化文件 </summary>
    public class SerializedFile
    {
        /// <summary> AssetStudio的资源管理器 </summary>
        public AssetsManager assetsManager;
        /// <summary> 读取文件的读取器 </summary>
        public FileReader reader;
        /// <summary> 游戏信息 </summary>
        public Game game;
        /// <summary> 文件偏移量 </summary>
        public long offset = 0;
        /// <summary> 完整路径 </summary>
        public string fullName;
        /// <summary> 原始路径 </summary>
        public string originalPath;
        /// <summary> 文件名 </summary>
        public string fileName;
        /// <summary> 版本信息 </summary>
        public int[] version = { 0, 0, 0, 0 };
        /// <summary> 构建类型 </summary>
        public BuildType buildType;
        /// <summary> 所有对象 </summary>
        public List<Object> Objects;
        /// <summary> 所有对象字典 </summary>
        public Dictionary<long, Object> ObjectsDic;

        /// <summary> 文件头 </summary>
        public SerializedFileHeader header;
        /// <summary> 文件字节序 </summary>
        private byte m_FileEndianess;
        /// <summary> Unity 版本 </summary>
        public string unityVersion = "2.5.0f5";
        /// <summary> 项目构建的目标平台 </summary>
        public BuildTarget m_TargetPlatform = BuildTarget.UnknownPlatform;
        /// <summary> 是否启用类型树 </summary>
        private bool m_EnableTypeTree = true;
        /// <summary> 序列号类型列表 </summary>
        public List<SerializedType> m_Types;
        /// <summary> 大ID是否启用 </summary>
        public int bigIDEnabled = 0;
        /// <summary> 所有对象信息 </summary>
        public List<ObjectInfo> m_Objects;
        /// <summary> 脚本文件列表 </summary> 
        private List<LocalSerializedObjectIdentifier> m_ScriptTypes;
        /// <summary> 依赖文件列表 </summary>
        public List<FileIdentifier> m_Externals;
        /// <summary> 引用类型列表 </summary>
        public List<SerializedType> m_RefTypes;
        public string userInformation;

        public SerializedFile(FileReader reader, AssetsManager assetsManager)
        {
            this.assetsManager = assetsManager;
            this.reader = reader;
            game = assetsManager.Game;
            fullName = reader.FullPath;
            fileName = reader.FileName;

            #region 读取文件头

            // ReadHeader
            header = new SerializedFileHeader();
            header.m_MetadataSize = reader.ReadUInt32();
            header.m_FileSize = reader.ReadUInt32();
            header.m_Version = (SerializedFileFormatVersion)reader.ReadUInt32();
            header.m_DataOffset = reader.ReadUInt32();

            if (header.m_Version >= SerializedFileFormatVersion.Unknown_9)
            {
                header.m_Endianess = reader.ReadByte();
                header.m_Reserved = reader.ReadBytes(3);
                m_FileEndianess = header.m_Endianess;
            }
            else
            {
                reader.Position = header.m_FileSize - header.m_MetadataSize;
                m_FileEndianess = reader.ReadByte();
            }
  
            if (header.m_Version >= SerializedFileFormatVersion.LargeFilesSupport)
            {
                header.m_MetadataSize = reader.ReadUInt32();
                header.m_FileSize = reader.ReadInt64();
                header.m_DataOffset = reader.ReadInt64();
                reader.ReadInt64(); // unknown

            }

            Logger.Verbose($"文件 {fileName} 信息: {header}");

            #endregion

            #region 读取Metadata

            // ReadMetadata
            if (m_FileEndianess == 0)
            {
                reader.Endian = EndianType.LittleEndian;
                Logger.Verbose($"字节序 {reader.Endian}");
            }
            if (header.m_Version >= SerializedFileFormatVersion.Unknown_7)
            {
                unityVersion = reader.ReadStringToNull();
                Logger.Verbose($"Unity 版本 {unityVersion}");
                SetVersion(unityVersion);
            }
            if (header.m_Version >= SerializedFileFormatVersion.Unknown_8)
            {
                m_TargetPlatform = (BuildTarget)reader.ReadInt32();
                if (!Enum.IsDefined(typeof(BuildTarget), m_TargetPlatform))
                {
                    Logger.Verbose($"解析的目标格式 {m_TargetPlatform} 与任何支持的格式不匹配，默认使用 {BuildTarget.UnknownPlatform}。");
                    m_TargetPlatform = BuildTarget.UnknownPlatform;
                }
                else if (m_TargetPlatform == BuildTarget.NoTarget && game.Type.IsMhyGroup())
                {
                    Logger.Verbose($"选择的游戏 {game.Name} 是米哈游游戏，强制目标格式为 {BuildTarget.StandaloneWindows64}。");
                    m_TargetPlatform = BuildTarget.StandaloneWindows64;
                }
                Logger.Verbose($"目标格式 {m_TargetPlatform}");
            }
            if (header.m_Version >= SerializedFileFormatVersion.HasTypeTreeHashes)
            {
                m_EnableTypeTree = reader.ReadBoolean();
            }

            #endregion

            #region 读取类型

            // Read Types
            int typeCount = reader.ReadInt32();
            m_Types = new List<SerializedType>();
            Logger.Verbose($"找到 {typeCount} 个序列化类型。");
            for (int i = 0; i < typeCount; i++)
            {
                m_Types.Add(ReadSerializedType(false));
            }

            if (header.m_Version >= SerializedFileFormatVersion.Unknown_7 && header.m_Version < SerializedFileFormatVersion.Unknown_14)
            {
                bigIDEnabled = reader.ReadInt32();
            }

            #endregion

            #region 读取对象

            // Read Objects
            int objectCount = reader.ReadInt32();
            m_Objects = new List<ObjectInfo>();
            Objects = new List<Object>();
            ObjectsDic = new Dictionary<long, Object>();
            Logger.Verbose($"找到 {objectCount} 个对象。");
            for (int i = 0; i < objectCount; i++)
            {
                var objectInfo = new ObjectInfo();
                if (bigIDEnabled != 0)
                {
                    objectInfo.m_PathID = reader.ReadInt64();
                }
                else if (header.m_Version < SerializedFileFormatVersion.Unknown_14)
                {
                    objectInfo.m_PathID = reader.ReadInt32();
                }
                else
                {
                    reader.AlignStream();
                    objectInfo.m_PathID = reader.ReadInt64();
                }

                if (header.m_Version >= SerializedFileFormatVersion.LargeFilesSupport)
                    objectInfo.byteStart = reader.ReadInt64();
                else
                    objectInfo.byteStart = reader.ReadUInt32();

                objectInfo.byteStart += header.m_DataOffset;
                objectInfo.byteSize = reader.ReadUInt32();
                objectInfo.typeID = reader.ReadInt32();
                if (header.m_Version < SerializedFileFormatVersion.RefactoredClassId)
                {
                    objectInfo.classID = reader.ReadUInt16();
                    objectInfo.serializedType = m_Types.Find(x => x.classID == objectInfo.typeID);
                }
                else
                {
                    var type = m_Types[objectInfo.typeID];
                    objectInfo.serializedType = type;
                    objectInfo.classID = type.classID;
                }
                if (header.m_Version < SerializedFileFormatVersion.HasScriptTypeIndex)
                {
                    objectInfo.isDestroyed = reader.ReadUInt16();
                }
                if (header.m_Version >= SerializedFileFormatVersion.HasScriptTypeIndex && header.m_Version < SerializedFileFormatVersion.RefactorTypeData)
                {
                    var m_ScriptTypeIndex = reader.ReadInt16();
                    if (objectInfo.serializedType != null)
                        objectInfo.serializedType.m_ScriptTypeIndex = m_ScriptTypeIndex;
                }
                if (header.m_Version == SerializedFileFormatVersion.SupportsStrippedObject || header.m_Version == SerializedFileFormatVersion.RefactoredClassId)
                {
                    objectInfo.stripped = reader.ReadByte();
                }
                Logger.Verbose($"对象信息: {objectInfo}");
                m_Objects.Add(objectInfo);
            }
            #endregion

            #region 读取脚本

            if (header.m_Version >= SerializedFileFormatVersion.HasScriptTypeIndex)
            {
                int scriptCount = reader.ReadInt32();
                Logger.Verbose($"找到 {scriptCount} 个脚本。");
                m_ScriptTypes = new List<LocalSerializedObjectIdentifier>();
                for (int i = 0; i < scriptCount; i++)
                {
                    var m_ScriptType = new LocalSerializedObjectIdentifier();
                    m_ScriptType.localSerializedFileIndex = reader.ReadInt32();
                    if (header.m_Version < SerializedFileFormatVersion.Unknown_14)
                    {
                        m_ScriptType.localIdentifierInFile = reader.ReadInt32();
                    }
                    else
                    {
                        reader.AlignStream();
                        m_ScriptType.localIdentifierInFile = reader.ReadInt64();
                    }
                    Logger.Verbose($"脚本信息: {m_ScriptType}");
                    m_ScriptTypes.Add(m_ScriptType);
                }
            }

            #endregion

            #region 读取依赖文件

            int externalsCount = reader.ReadInt32();
            m_Externals = new List<FileIdentifier>();
            Logger.Verbose($"找到 {externalsCount} 个外部文件。");
            for (int i = 0; i < externalsCount; i++)
            {
                var m_External = new FileIdentifier();
                if (header.m_Version >= SerializedFileFormatVersion.Unknown_6)
                {
                    var tempEmpty = reader.ReadStringToNull();
                }
                if (header.m_Version >= SerializedFileFormatVersion.Unknown_5)
                {
                    m_External.guid = new Guid(reader.ReadBytes(16));
                    m_External.type = reader.ReadInt32();
                }
                m_External.pathName = reader.ReadStringToNull();
                m_External.fileName = Path.GetFileName(m_External.pathName);
                Logger.Verbose($"外部信息: {m_External}");
                m_Externals.Add(m_External);
            }

            #endregion

            #region 读取引用类型

            if (header.m_Version >= SerializedFileFormatVersion.SupportsRefObject)
            {
                int refTypesCount = reader.ReadInt32();
                m_RefTypes = new List<SerializedType>();
                Logger.Verbose($"找到 {refTypesCount} 个引用类型。");
                for (int i = 0; i < refTypesCount; i++)
                {
                    m_RefTypes.Add(ReadSerializedType(true));
                }
            }

            if (header.m_Version >= SerializedFileFormatVersion.Unknown_5)
            {
                userInformation = reader.ReadStringToNull();
            }

            #endregion



            //reader.AlignStream(16);
        }

        /// <summary> 设置Unity版本  </summary>
        public void SetVersion(string stringVersion)
        {
            if (stringVersion != strippedVersion)
            {
                unityVersion = stringVersion;
                var buildSplit = Regex.Replace(stringVersion, @"\d", "").Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                buildType = new BuildType(buildSplit[0]);
                var versionSplit = Regex.Replace(stringVersion, @"\D", ".").Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                version = versionSplit.Select(int.Parse).ToArray();
            }
        }

        /// <summary> 读取序列化对象类型 </summary>
        private SerializedType ReadSerializedType(bool isRefType)
        {
            Logger.Verbose($"正在尝试解析序列化内容" + (isRefType ? " reference" : " ") + "type");
            var type = new SerializedType();

            type.classID = reader.ReadInt32();

            if (game.Type.IsGIGroup() && BitConverter.ToBoolean(header.m_Reserved))
            {
                Logger.Verbose($"已编码的类ID {type.classID}，解码中...");
                type.classID = DecodeClassID(type.classID);
            }

            if (header.m_Version >= SerializedFileFormatVersion.RefactoredClassId)
            {
                type.m_IsStrippedType = reader.ReadBoolean();
            }

            if (header.m_Version >= SerializedFileFormatVersion.RefactorTypeData)
            {
                type.m_ScriptTypeIndex = reader.ReadInt16();
            }

            if (header.m_Version >= SerializedFileFormatVersion.HasTypeTreeHashes)
            {
                if (isRefType && type.m_ScriptTypeIndex >= 0)
                {
                    type.m_ScriptID = reader.ReadBytes(16);
                }
                else if ((header.m_Version < SerializedFileFormatVersion.RefactoredClassId && type.classID < 0) || (header.m_Version >= SerializedFileFormatVersion.RefactoredClassId && type.classID == 114))
                {
                    type.m_ScriptID = reader.ReadBytes(16);
                }
                type.m_OldTypeHash = reader.ReadBytes(16);
            }

            if (m_EnableTypeTree)
            {
                Logger.Verbose($"文件已启用类型树!!");
                type.m_Type = new TypeTree();
                type.m_Type.m_Nodes = new List<TypeTreeNode>();
                if (header.m_Version >= SerializedFileFormatVersion.Unknown_12 || header.m_Version == SerializedFileFormatVersion.Unknown_10)
                {
                    TypeTreeBlobRead(type.m_Type);
                }
                else
                {
                    ReadTypeTree(type.m_Type);
                }
                if (header.m_Version >= SerializedFileFormatVersion.StoresTypeDependencies)
                {
                    if (isRefType)
                    {
                        type.m_KlassName = reader.ReadStringToNull();
                        type.m_NameSpace = reader.ReadStringToNull();
                        type.m_AsmName = reader.ReadStringToNull();
                    }
                    else
                    {
                        type.m_TypeDependencies = reader.ReadInt32Array();
                    }
                }
            }

            Logger.Verbose($"序列化类型信息: {type}");
            return type;
        }

        private void ReadTypeTree(TypeTree m_Type, int level = 0)
        {
            Logger.Verbose($"正在尝试解析类型树...");
            var typeTreeNode = new TypeTreeNode();
            m_Type.m_Nodes.Add(typeTreeNode);
            typeTreeNode.m_Level = level;
            typeTreeNode.m_Type = reader.ReadStringToNull();
            typeTreeNode.m_Name = reader.ReadStringToNull();
            typeTreeNode.m_ByteSize = reader.ReadInt32();
            if (header.m_Version == SerializedFileFormatVersion.Unknown_2)
            {
                var variableCount = reader.ReadInt32();
            }
            if (header.m_Version != SerializedFileFormatVersion.Unknown_3)
            {
                typeTreeNode.m_Index = reader.ReadInt32();
            }
            typeTreeNode.m_TypeFlags = reader.ReadInt32();
            typeTreeNode.m_Version = reader.ReadInt32();
            if (header.m_Version != SerializedFileFormatVersion.Unknown_3)
            {
                typeTreeNode.m_MetaFlag = reader.ReadInt32();
            }

            int childrenCount = reader.ReadInt32();
            for (int i = 0; i < childrenCount; i++)
            {
                ReadTypeTree(m_Type, level + 1);
            }

            Logger.Verbose($"类型树信息: {m_Type}");
        }

        /// <summary> 读取 blob 类型树 </summary>
        private void TypeTreeBlobRead(TypeTree m_Type)
        {
            Logger.Verbose($"正在尝试解析 blob 类型树...");
            int numberOfNodes = reader.ReadInt32();
            int stringBufferSize = reader.ReadInt32();
            Logger.Verbose($"找到 {numberOfNodes} 个节点和 {stringBufferSize} 个字符串。");
            for (int i = 0; i < numberOfNodes; i++)
            {
                var typeTreeNode = new TypeTreeNode();
                m_Type.m_Nodes.Add(typeTreeNode);
                typeTreeNode.m_Version = reader.ReadUInt16();
                typeTreeNode.m_Level = reader.ReadByte();
                typeTreeNode.m_TypeFlags = reader.ReadByte();
                typeTreeNode.m_TypeStrOffset = reader.ReadUInt32();
                typeTreeNode.m_NameStrOffset = reader.ReadUInt32();
                typeTreeNode.m_ByteSize = reader.ReadInt32();
                typeTreeNode.m_Index = reader.ReadInt32();
                typeTreeNode.m_MetaFlag = reader.ReadInt32();
                if (header.m_Version >= SerializedFileFormatVersion.TypeTreeNodeWithTypeFlags)
                {
                    typeTreeNode.m_RefTypeHash = reader.ReadUInt64();
                }
            }
            m_Type.m_StringBuffer = reader.ReadBytes(stringBufferSize);

            using (var stringBufferReader = new EndianBinaryReader(new MemoryStream(m_Type.m_StringBuffer), EndianType.LittleEndian))
            {
                for (int i = 0; i < numberOfNodes; i++)
                {
                    var m_Node = m_Type.m_Nodes[i];
                    m_Node.m_Type = ReadString(stringBufferReader, m_Node.m_TypeStrOffset);
                    m_Node.m_Name = ReadString(stringBufferReader, m_Node.m_NameStrOffset);
                }
            }

            Logger.Verbose($"类型树信息: {m_Type}");

            // 解析字符串
            string ReadString(EndianBinaryReader stringBufferReader, uint value)
            {
                var isOffset = (value & 0x80000000) == 0;
                if (isOffset)
                {
                    stringBufferReader.BaseStream.Position = value;
                    return stringBufferReader.ReadStringToNull();
                }
                var offset = value & 0x7FFFFFFF;
                if (CommonString.StringBuffer.TryGetValue(offset, out var str))
                {
                    return str;
                }
                return offset.ToString();
            }
        }

        public void AddObject(Object obj)
        {
            Logger.Verbose($"正在缓存文件 {fileName} 中的对象 {obj.m_PathID}...");
            Objects.Add(obj);
            ObjectsDic.Add(obj.m_PathID, obj);
        }

        private static int DecodeClassID(int value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToInt32(bytes, 0);
            return (value ^ 0x23746FBE) - 3;
        }

        /// <summary> 是否为版本移除的 Unity </summary>
        public bool IsVersionStripped => unityVersion == strippedVersion;

        /// <summary> 被移除的版本信息 </summary>
        private const string strippedVersion = "0.0.0";
    }
}
