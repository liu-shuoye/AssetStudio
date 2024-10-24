using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using NLua;
using static AssetStudio.ImportHelper;

namespace AssetStudio
{
    /// <summary> AssetStudio的资源管理器 </summary>
    public class AssetsManager
    {
        private Game _game;
        public Game Game
        {
            get
            {
                return _game;
            }
            set
            {
                _game = value;
                switch (value.Type)
                {
                    case GameType.ProjectSekai:
                        SpecifyUnityVersion = "2022.3.32f1";
                        break;
                    case GameType.Orisries:
                        SpecifyUnityVersion = "2022.3.32f1";
                        break;
                    case GameType.GirlsFrontline:
                        SpecifyUnityVersion = "2019.4.40f1";
                        break;
                    default:
                        break;
                }
            }
        }
        private bool _enableLuaScript = false;

        /// <summary> 是否启用Lua脚本 </summary>
        public bool EnableLuaScript
        {
            get => _enableLuaScript; 
            set 
            {
                _enableLuaScript = value;
                if (value)
                {
                    InitLuaEnv();
                }
            }
        }
        private Lua luaEnvironment = new Lua();
        /// <summary> Lua脚本 </summary>
        public string LuaScript = "";
        public bool Silent = false;
        /// <summary> 是否跳过解析 </summary>
        public bool SkipProcess = false;
        /// 是否解析依赖关系
        public bool ResolveDependencies = false;        
        public string SpecifyUnityVersion;
        /// <summary> 取消令牌 </summary>
        public CancellationTokenSource tokenSource = new CancellationTokenSource();
        /// <summary> 所有的资源文件 </summary>
        public List<SerializedFile> assetsFileList = new List<SerializedFile>();

        internal Dictionary<string, int> assetsFileIndexCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        /// <summary> resource 资源文件读取器 </summary>
        internal Dictionary<string, BinaryReader> resourceFileReaders = new Dictionary<string, BinaryReader>(StringComparer.OrdinalIgnoreCase);

        /// <summary> 导入的文件列表 </summary>
        internal List<string> importFiles = new List<string>();
        /// <summary> 导入文件的哈希列表 </summary>
        internal HashSet<string> importFilesHash = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary> 不存在的文件 </summary>
        internal HashSet<string> noexistFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary> 资源文件列表哈希表 </summary>
        internal HashSet<string> assetsFileListHash = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary> 是否自动检测多包 </summary>
        public bool autoDetectMultipleBundle = false;
        
        public void SetSpecifyUnityVersion(string version)
        {
            SpecifyUnityVersion = version;
        }
        
        public void SetGame(string game)
        {
            Game = GameManager.GetGame(game);
        }

        public void SetUnityCNKey(string Name, string Key)
        {
            UnityCN.SetKey(new UnityCN.Entry(Name, Key));
        }
        
        private void InitLuaEnv()
        {
            var luaMethods = new LuaMethods();
            var methods = typeof(LuaMethods).GetMethods();
            foreach (var method in methods)
            {
                luaEnvironment.RegisterFunction(method.Name, luaMethods, method);
            }
            luaEnvironment.RegisterFunction("SetUnityVersion", this, GetType().GetMethod("SetSpecifyUnityVersion"));
            luaEnvironment.RegisterFunction("SetGame", this, GetType().GetMethod("SetGame"));
            luaEnvironment.RegisterFunction("SetUnityCNKey", this, GetType().GetMethod("SetUnityCNKey"));
        }

        /// <summary> 加载文件 </summary>
        public void LoadFiles(params string[] files)
        {
            if (Silent)
            {
                Logger.Silent = true;
                Progress.Silent = true;
            }

            var path = Path.GetDirectoryName(Path.GetFullPath(files[0]));
            MergeSplitAssets(path);
            var toReadFile = ProcessingSplitFiles(files.ToList());
            if (ResolveDependencies)
                toReadFile = AssetsHelper.ProcessDependencies(toReadFile);
            Load(toReadFile);

            if (Silent)
            {
                Logger.Silent = false;
                Progress.Silent = false;
            }
        }

        public void LoadFolder(string path)
        {
            if (Silent)
            {
                Logger.Silent = true;
                Progress.Silent = true;
            }

            MergeSplitAssets(path, true);
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).ToList();
            var toReadFile = ProcessingSplitFiles(files);
            Load(toReadFile);

            if (Silent)
            {
                Logger.Silent = false;
                Progress.Silent = false;
            }
        }

        /// <summary> 加载文件 </summary>
        private void Load(string[] files)
        {
            foreach (var file in files)
            {
                Logger.Verbose($"缓存 {file} 路径和名称以过滤重复项");
                importFiles.Add(file);
                importFilesHash.Add(Path.GetFileName(file));
            }

            Progress.Reset();
            //use a for loop because list size can change
            for (var i = 0; i < importFiles.Count; i++)
            {
                LoadFile(importFiles[i]);
                Progress.Report(i + 1, importFiles.Count);
                if (tokenSource.IsCancellationRequested)
                {
                    Logger.Info("文件加载已中止！！");
                    break;
                }
            }

            importFiles.Clear();
            importFilesHash.Clear();
            noexistFiles.Clear();
            assetsFileListHash.Clear();
            AssetsHelper.ClearOffsets();

            if (!SkipProcess)
            {
                ReadAssets();
                ProcessAssets();
            }
        }

        /// <summary> 加载文件 </summary>
        private void LoadFile(string fullName)
        {
            FileReader reader = null;
            if (!EnableLuaScript || LuaScript == "")
            {
                reader = new FileReader(fullName);
            }
            else
            {
                Logger.Info("使用Lua脚本处理文件...");
                luaEnvironment["filepath"] = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(fullName));
                luaEnvironment["filename"] = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(Path.GetFileName(fullName)));
                luaEnvironment["filestream"] = new FileStream(fullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                try
                {
                    var result = luaEnvironment.DoString(LuaScript);
                    Stream fs = (Stream)result[0];
                    reader = new FileReader(fullName, fs);
                }
                catch (Exception e)
                {
                    Logger.Error($"使用 lua 读取文件 {fullName} 时出错", e);
                }
            }
            reader = reader.PreProcessing(Game, autoDetectMultipleBundle);
            LoadFile(reader);
        }

        /// <summary> 从内存中加载文件 </summary>
        private void LoadFile(FileReader reader)
        {
            switch (reader.FileType)
            {
                case FileType.AssetsFile:
                    LoadAssetsFile(reader);
                    break;
                case FileType.BundleFile:
                    LoadBundleFile(reader);
                    break;
                case FileType.WebFile:
                    LoadWebFile(reader);
                    break;
                case FileType.GZipFile:
                    LoadFile(DecompressGZip(reader));
                    break;
                case FileType.BrotliFile:
                    LoadFile(DecompressBrotli(reader));
                    break;
                case FileType.ZipFile:
                    LoadZipFile(reader);
                    break;
                case FileType.BlockFile:
                    LoadBlockFile(reader);
                    break;
                case FileType.BlkFile:
                    LoadBlkFile(reader);
                    break;
                case FileType.MhyFile:
                    LoadMhyFile(reader);
                    break;
            }
        }

        /// <summary> 加载Assets文件 </summary>
        private void LoadAssetsFile(FileReader reader)
        {
            if (!assetsFileListHash.Contains(reader.FileName))
            {
                Logger.Info($"正在加载 {reader.FullPath}");
                try
                {
                    var assetsFile = new SerializedFile(reader, this);
                    CheckStrippedVersion(assetsFile);
                    assetsFileList.Add(assetsFile);
                    assetsFileListHash.Add(assetsFile.fileName);

                    foreach (var sharedFile in assetsFile.m_Externals)
                    {
                        Logger.Verbose($"{assetsFile.fileName}需要外部文件 {sharedFile.fileName}，尝试查找...");
                        var sharedFileName = sharedFile.fileName;

                        if (!importFilesHash.Contains(sharedFileName))
                        {
                            var sharedFilePath = Path.Combine(Path.GetDirectoryName(reader.FullPath), sharedFileName);
                            if (!noexistFiles.Contains(sharedFilePath))
                            {
                                if (!File.Exists(sharedFilePath))
                                {
                                    var findFiles = Directory.GetFiles(Path.GetDirectoryName(reader.FullPath), sharedFileName, SearchOption.AllDirectories);
                                    if (findFiles.Length > 0)
                                    {
                                        Logger.Verbose($"找到 {findFiles.Length} 个匹配文件，选择第一个文件 {findFiles[0]}！！");
                                        sharedFilePath = findFiles[0];
                                    }
                                }
                                if (File.Exists(sharedFilePath))
                                {
                                    importFiles.Add(sharedFilePath);
                                    importFilesHash.Add(sharedFileName);
                                }
                                else
                                {
                                    Logger.Verbose("未找到任何内容，正在缓存到不存在的文件中以避免重复搜索！！");
                                    noexistFiles.Add(sharedFilePath);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"读取资产文件 {reader.FullPath} 时出错", e);
                    reader.Dispose();
                }
            }
            else
            {
                Logger.Info($"跳过 {reader.FullPath}");
                reader.Dispose();
            }
        }

        ///  <summary> 加载内存中的Assets文件 </summary>
        private void LoadAssetsFromMemory(FileReader reader, string originalPath, string unityVersion = null, long originalOffset = 0)
        {
            Logger.Verbose($"从 {originalPath} 处的偏移量 0x{originalOffset:X8} 加载版本为 {unityVersion} 的资产文件 {reader.FileName}");
            if (!assetsFileListHash.Contains(reader.FileName))
            {
                try
                {
                    var assetsFile = new SerializedFile(reader, this);
                    assetsFile.originalPath = originalPath;
                    assetsFile.offset = originalOffset;
                    if (!string.IsNullOrEmpty(unityVersion) && assetsFile.header.m_Version < SerializedFileFormatVersion.Unknown_7)
                    {
                        assetsFile.SetVersion(unityVersion);
                    }
                    CheckStrippedVersion(assetsFile);
                    assetsFileList.Add(assetsFile);
                    assetsFileListHash.Add(assetsFile.fileName);
                }
                catch (Exception e)
                {
                    Logger.Error($"读取资产文件 {reader.FullPath} 自 {Path.GetFileName(originalPath)} 时出错", e);
                    resourceFileReaders.TryAdd(reader.FileName, reader);
                }
            }
            else
                Logger.Info($"跳过 {originalPath} ({reader.FileName})");
        }

        /// <summary> 加载AssetBundle(ab包)资源文件 </summary>
        private void LoadBundleFile(FileReader reader, string originalPath = null, long originalOffset = 0, bool log = true)
        {
            if (log)
            {
                Logger.Info("正在加载 " + reader.FullPath);
            }
            try
            {
                var bundleFile = new BundleFile(reader, Game);
                foreach (var file in bundleFile.fileList)
                {
                    var dummyPath = Path.Combine(Path.GetDirectoryName(reader.FullPath), file.fileName);
                    var subReader = new FileReader(dummyPath, file.stream);
                    if (subReader.FileType == FileType.AssetsFile)
                    {
                        LoadAssetsFromMemory(subReader, originalPath ?? reader.FullPath, bundleFile.m_Header.unityRevision, originalOffset);
                    }
                    else
                    {
                        Logger.Verbose("缓存资源流");
                        resourceFileReaders.TryAdd(file.fileName, subReader); //TODO
                    }
                }
            }
            catch (InvalidCastException)
            {
                Logger.Error($"游戏类型不匹配，预期为 {nameof(Mr0k)}，但得到 {Game.Name} ({Game.GetType().Name})！！");
            }
            catch (Exception e)
            {
                var str = $"Error while reading bundle file {reader.FullPath}";
                if (originalPath != null)
                {
                    str += $" from {Path.GetFileName(originalPath)}";
                }
                Logger.Error(str, e);
            }
            finally
            {
                reader.Dispose();
            }
        }

        private void LoadWebFile(FileReader reader)
        {
            Logger.Info("正在加载 " + reader.FullPath);
            try
            {
                var webFile = new WebFile(reader);
                foreach (var file in webFile.fileList)
                {
                    var dummyPath = Path.Combine(Path.GetDirectoryName(reader.FullPath), file.fileName);
                    var subReader = new FileReader(dummyPath, file.stream);
                    switch (subReader.FileType)
                    {
                        case FileType.AssetsFile:
                            LoadAssetsFromMemory(subReader, reader.FullPath);
                            break;
                        case FileType.BundleFile:
                            LoadBundleFile(subReader, reader.FullPath);
                            break;
                        case FileType.WebFile:
                            LoadWebFile(subReader);
                            break;
                        case FileType.ResourceFile:
                            Logger.Verbose("缓存资源流");
                            resourceFileReaders.TryAdd(file.fileName, subReader); //TODO
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"读取 web 文件 {reader.FullPath} 时出错", e);
            }
            finally
            {
                reader.Dispose();
            }
        }

        private void LoadZipFile(FileReader reader)
        {
            Logger.Info("正在加载 " + reader.FileName);
            try
            {
                using (ZipArchive archive = new ZipArchive(reader.BaseStream, ZipArchiveMode.Read))
                {
                    List<string> splitFiles = new List<string>();
                    Logger.Verbose("在解析资产之前注册所有文件，以便找到外部引用并找到拆分文件");
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.Name.Contains(".split"))
                        {
                            string baseName = Path.GetFileNameWithoutExtension(entry.Name);
                            string basePath = Path.Combine(Path.GetDirectoryName(entry.FullName), baseName);
                            if (!splitFiles.Contains(basePath))
                            {
                                splitFiles.Add(basePath);
                                importFilesHash.Add(baseName);
                            }
                        }
                        else
                        {
                            importFilesHash.Add(entry.Name);
                        }
                    }

                    Logger.Verbose("合并拆分文件并加载结果");
                    foreach (string basePath in splitFiles)
                    {
                        try
                        {
                            Stream splitStream = new MemoryStream();
                            int i = 0;
                            while (true)
                            {
                                string path = $"{basePath}.split{i++}";
                                ZipArchiveEntry entry = archive.GetEntry(path);
                                if (entry == null)
                                    break;
                                using (Stream entryStream = entry.Open())
                                {
                                    entryStream.CopyTo(splitStream);
                                }
                            }
                            splitStream.Seek(0, SeekOrigin.Begin);
                            FileReader entryReader = new FileReader(basePath, splitStream);
                            entryReader = entryReader.PreProcessing(Game);
                            LoadFile(entryReader);
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"读取 zip 分卷文件 {basePath} 时出错", e);
                        }
                    }

                    Logger.Verbose("加载所有条目");
                    Logger.Verbose($"找到 {archive.Entries.Count} 个条目。"); 
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        try
                        {
                            string dummyPath = Path.Combine(Path.GetDirectoryName(reader.FullPath), reader.FileName, entry.FullName);
                            Logger.Verbose("创建一个新流来存储解压缩的流，并保留数据以供稍后提取");
                            Stream streamReader = new MemoryStream();
                            using (Stream entryStream = entry.Open())
                            {
                                entryStream.CopyTo(streamReader);
                            }
                            streamReader.Position = 0;

                            FileReader entryReader = new FileReader(dummyPath, streamReader);
                            entryReader = entryReader.PreProcessing(Game);
                            LoadFile(entryReader);
                            if (entryReader.FileType == FileType.ResourceFile)
                            {
                                entryReader.Position = 0;
                                Logger.Verbose("缓存资源文件");
                                resourceFileReaders.TryAdd(entry.Name, entryReader);
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"读取 zip 条目 {entry.FullName} 时出错", e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"读取 zip 文件 {reader.FileName} 时出错", e);
            }
            finally
            {
                reader.Dispose();
            }
        }
        private void LoadBlockFile(FileReader reader)
        {
            Logger.Info("正在加载 " + reader.FullPath);
            try
            {
                using var stream = new OffsetStream(reader.BaseStream, 0);
                foreach (var offset in stream.GetOffsets(reader.FullPath))
                {
                    var name = offset.ToString("X8");
                    Logger.Info($"正在加载块 {name}");

                    var dummyPath = Path.Combine(Path.GetDirectoryName(reader.FullPath), name);
                    var subReader = new FileReader(dummyPath, stream, true);
                    switch (subReader.FileType)
                    {
                        case FileType.ENCRFile:
                        case FileType.BundleFile:
                            LoadBundleFile(subReader, reader.FullPath, offset, false);
                            break;
                        case FileType.BlbFile:
                            LoadBlbFile(subReader, reader.FullPath, offset, false);
                            break;
                        case FileType.MhyFile:
                            LoadMhyFile(subReader, reader.FullPath, offset, false);
                            break;
                    }
                }    
            }
            catch (Exception e)
            {
                Logger.Error($"读取块文件 {reader.FileName} 时出错", e);
            }
            finally
            {
                reader.Dispose();
            }
        }
        private void LoadBlkFile(FileReader reader)
        {
            Logger.Info("正在加载 " + reader.FullPath);
            try
            {
                using var stream = BlkUtils.Decrypt(reader, (Blk)Game);
                foreach (var offset in stream.GetOffsets(reader.FullPath))
                {
                    var name = offset.ToString("X8");
                    Logger.Info($"正在加载块 {name}");

                    var dummyPath = Path.Combine(Path.GetDirectoryName(reader.FullPath), name);
                    var subReader = new FileReader(dummyPath, stream, true);
                    switch (subReader.FileType)
                    {
                        case FileType.BundleFile:
                            LoadBundleFile(subReader, reader.FullPath, offset, false);
                            break;
                        case FileType.MhyFile:
                            LoadMhyFile(subReader, reader.FullPath, offset, false);
                            break;
                    }
                }
            }
            catch (InvalidCastException)
            {
                Logger.Error($"游戏类型不匹配，预期为 {nameof(Blk)}，但得到 {Game.Name} ({Game.GetType().Name})！！");
            }
            catch (Exception e)
            {
                Logger.Error($"读取 blk 文件 {reader.FileName} 时出错", e);
            }
            finally
            {
                reader.Dispose();
            }
        }
        private void LoadMhyFile(FileReader reader, string originalPath = null, long originalOffset = 0, bool log = true)
        {
            if (log)
            {
                Logger.Info("正在加载 " + reader.FullPath);
            }
            try
            {
                var mhyFile = new MhyFile(reader, (Mhy)Game);
                Logger.Verbose($"米哈游文件总大小: {mhyFile.m_Header.size:X8}");
                foreach (var file in mhyFile.fileList)
                {
                    var dummyPath = Path.Combine(Path.GetDirectoryName(reader.FullPath), file.fileName);
                    var cabReader = new FileReader(dummyPath, file.stream);
                    if (cabReader.FileType == FileType.AssetsFile)
                    {
                        LoadAssetsFromMemory(cabReader, originalPath ?? reader.FullPath, mhyFile.m_Header.unityRevision, originalOffset);
                    }
                    else
                    {
                        Logger.Verbose("缓存资源流");
                        resourceFileReaders.TryAdd(file.fileName, cabReader); //TODO
                    }
                }
            }
            catch (InvalidCastException)
            {
                Logger.Error($"游戏类型不匹配，预期为 {nameof(Mhy)}，但得到 {Game.Name} ({Game.GetType().Name})！！");
            }
            catch (Exception e)
            {
                var str = $"Error while reading mhy file {reader.FullPath}";
                if (originalPath != null)
                {
                    str += $" from {Path.GetFileName(originalPath)}";
                }
                Logger.Error(str, e);
            }
            finally
            {
                reader.Dispose();
            }
        }
        
        private void LoadBlbFile(FileReader reader, string originalPath = null, long originalOffset = 0, bool log = true)
        {
            if (log)
            {
                Logger.Info("正在加载 " + reader.FullPath);
            }
            try
            {
                var blbFile = new BlbFile(reader, reader.FullPath);
                foreach (var file in blbFile.fileList)
                {
                    var dummyPath = Path.Combine(Path.GetDirectoryName(reader.FullPath), file.fileName);
                    var cabReader = new FileReader(dummyPath, file.stream);
                    if (cabReader.FileType == FileType.AssetsFile)
                    {
                        LoadAssetsFromMemory(cabReader, originalPath ?? reader.FullPath, blbFile.m_Header.unityRevision, originalOffset);
                    }
                    else
                    {
                        Logger.Verbose("缓存资源流");
                        resourceFileReaders.TryAdd(file.fileName, cabReader); //TODO
                    }
                }
            }
            catch (Exception e)
            {
                var str = $"Error while reading Blb file {reader.FullPath}";
                if (originalPath != null)
                {
                    str += $" from {Path.GetFileName(originalPath)}";
                }
                Logger.Error(str, e);
            }
            finally
            {
                reader.Dispose();
            }
        }

        /// <summary>  检查是否已设置 Unity 版本  </summary>
        public void CheckStrippedVersion(SerializedFile assetsFile)
        {
            if (assetsFile.IsVersionStripped && string.IsNullOrEmpty(SpecifyUnityVersion))
            {
                throw new Exception("Unity 版本已被剥离，请在选项中设置版本");
            }
            if (!string.IsNullOrEmpty(SpecifyUnityVersion))
            {
                assetsFile.SetVersion(SpecifyUnityVersion);
            }
        }

        public void Clear()
        {
            Logger.Verbose("正在清理...");

            foreach (var assetsFile in assetsFileList)
            {
                assetsFile.Objects.Clear();
                assetsFile.reader.Close();
            }
            assetsFileList.Clear();

            foreach (var resourceFileReader in resourceFileReaders)
            {
                resourceFileReader.Value.Close();
            }
            resourceFileReaders.Clear();

            assetsFileIndexCache.Clear();

            tokenSource.Dispose();
            tokenSource = new CancellationTokenSource();

            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>  读取资源文件  </summary>
        private void ReadAssets()
        {
            Logger.Info("读取资产...");

            var progressCount = assetsFileList.Sum(x => x.m_Objects.Count);
            int i = 0;
            Progress.Reset();
            foreach (var assetsFile in assetsFileList)
            {
                foreach (var objectInfo in assetsFile.m_Objects)
                {
                    if (tokenSource.IsCancellationRequested)
                    {
                        Logger.Info("读取资产已取消！！");
                        return;
                    }
                    var objectReader = new ObjectReader(assetsFile.reader, assetsFile, objectInfo, Game);
                    try
                    {
                        Object obj = objectReader.type switch
                        {
                            ClassIDType.Animation when ClassIDType.Animation.CanParse() => new Animation(objectReader),
                            ClassIDType.AnimationClip when ClassIDType.AnimationClip.CanParse() => new AnimationClip(objectReader),
                            ClassIDType.Animator when ClassIDType.Animator.CanParse() => new Animator(objectReader),
                            ClassIDType.AnimatorController when ClassIDType.AnimatorController.CanParse() => new AnimatorController(objectReader),
                            ClassIDType.AnimatorOverrideController when ClassIDType.AnimatorOverrideController.CanParse() => new AnimatorOverrideController(objectReader),
                            ClassIDType.AssetBundle when ClassIDType.AssetBundle.CanParse() => new AssetBundle(objectReader),
                            ClassIDType.AudioClip when ClassIDType.AudioClip.CanParse() => new AudioClip(objectReader),
                            ClassIDType.Avatar when ClassIDType.Avatar.CanParse() => new Avatar(objectReader),
                            ClassIDType.Font when ClassIDType.Font.CanParse() => new Font(objectReader),
                            ClassIDType.GameObject when ClassIDType.GameObject.CanParse() => new GameObject(objectReader),
                            ClassIDType.IndexObject when ClassIDType.IndexObject.CanParse() => new IndexObject(objectReader),
                            ClassIDType.Material when ClassIDType.Material.CanParse() => new Material(objectReader),
                            ClassIDType.Mesh when ClassIDType.Mesh.CanParse() => new Mesh(objectReader),
                            ClassIDType.MeshFilter when ClassIDType.MeshFilter.CanParse() => new MeshFilter(objectReader),
                            ClassIDType.MeshRenderer when ClassIDType.MeshRenderer.CanParse() => new MeshRenderer(objectReader),
                            ClassIDType.MiHoYoBinData when ClassIDType.MiHoYoBinData.CanParse() => new MiHoYoBinData(objectReader),
                            ClassIDType.MonoBehaviour when ClassIDType.MonoBehaviour.CanParse() => new MonoBehaviour(objectReader),
                            ClassIDType.MonoScript when ClassIDType.MonoScript.CanParse() => new MonoScript(objectReader),
                            ClassIDType.MovieTexture when ClassIDType.MovieTexture.CanParse() => new MovieTexture(objectReader),
                            ClassIDType.PlayerSettings when ClassIDType.PlayerSettings.CanParse() => new PlayerSettings(objectReader),
                            ClassIDType.RectTransform when ClassIDType.RectTransform.CanParse() => new RectTransform(objectReader),
                            ClassIDType.Shader when ClassIDType.Shader.CanParse() => new Shader(objectReader),
                            ClassIDType.SkinnedMeshRenderer when ClassIDType.SkinnedMeshRenderer.CanParse() => new SkinnedMeshRenderer(objectReader),
                            ClassIDType.Sprite when ClassIDType.Sprite.CanParse() => new Sprite(objectReader),
                            ClassIDType.SpriteAtlas when ClassIDType.SpriteAtlas.CanParse() => new SpriteAtlas(objectReader),
                            ClassIDType.TextAsset when ClassIDType.TextAsset.CanParse() => new TextAsset(objectReader),
                            ClassIDType.Texture2D when ClassIDType.Texture2D.CanParse() => new Texture2D(objectReader),
                            ClassIDType.Transform when ClassIDType.Transform.CanParse() => new Transform(objectReader),
                            ClassIDType.VideoClip when ClassIDType.VideoClip.CanParse() => new VideoClip(objectReader),
                            ClassIDType.ResourceManager when ClassIDType.ResourceManager.CanParse() => new ResourceManager(objectReader),
                            _ => new Object(objectReader),
                        };
                        assetsFile.AddObject(obj);
                    }
                    catch (Exception e)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("无法加载对象")
                            .AppendLine($"Assets {assetsFile.fileName}")
                            .AppendLine($"Path {assetsFile.originalPath}")
                            .AppendLine($"Type {objectReader.type}")
                            .AppendLine($"PathID {objectInfo.m_PathID}")
                            .Append(e);
                        Logger.Error(sb.ToString());
                    }

                    Progress.Report(++i, progressCount);
                }
            }
        }

        private void ProcessAssets()
        {
            Logger.Info("处理资产...");

            foreach (var assetsFile in assetsFileList)
            {
                foreach (var obj in assetsFile.Objects)
                {
                    if (tokenSource.IsCancellationRequested)
                    {
                        Logger.Info("处理资产已取消！！");
                        return;
                    }
                    if (obj is GameObject m_GameObject)
                    {
                        Logger.Verbose($"文件 {m_GameObject.assetsFile.fileName} 中具有 {m_GameObject.m_PathID} 的游戏对象包含 {m_GameObject.m_Components.Count} 个组件，尝试获取它们...");
                        foreach (var pptr in m_GameObject.m_Components)
                        {
                            if (pptr.TryGet(out var m_Component))
                            {
                                switch (m_Component)
                                {
                                    case Transform m_Transform:
                                        Logger.Verbose($"从文件 {m_Transform.assetsFile.fileName} 获取的变换组件 {m_Transform.m_PathID}，正在分配到游戏对象组件...");
                                        m_GameObject.m_Transform = m_Transform;
                                            break;
                                    case MeshRenderer m_MeshRenderer:
                                        Logger.Verbose($"从文件 {m_MeshRenderer.assetsFile.fileName} 获取的网格渲染器组件 {m_MeshRenderer.m_PathID}，正在分配到游戏对象组件...");
                                        m_GameObject.m_MeshRenderer = m_MeshRenderer;
                                            break;
                                    case MeshFilter m_MeshFilter:
                                        Logger.Verbose($"从文件 {m_MeshFilter.assetsFile.fileName} 获取的网格过滤器组件 {m_MeshFilter.m_PathID}，正在分配到游戏对象组件...");
                                        m_GameObject.m_MeshFilter = m_MeshFilter;
                                            break;
                                    case SkinnedMeshRenderer m_SkinnedMeshRenderer:
                                        Logger.Verbose($"从文件 {m_SkinnedMeshRenderer.assetsFile.fileName} 获取的蒙皮网格渲染器组件 {m_SkinnedMeshRenderer.m_PathID}，正在分配到游戏对象组件...");
                                        m_GameObject.m_SkinnedMeshRenderer = m_SkinnedMeshRenderer;
                                            break;
                                    case Animator m_Animator:
                                        Logger.Verbose($"从文件 {m_Animator.assetsFile.fileName} 获取的动画器组件 {m_Animator.m_PathID}，正在分配到游戏对象组件...");
                                        m_GameObject.m_Animator = m_Animator;
                                            break;
                                    case Animation m_Animation:
                                        Logger.Verbose($"从文件 {m_Animation.assetsFile.fileName} 获取的动画组件 {m_Animation.m_PathID}，正在分配到游戏对象组件...");
                                        m_GameObject.m_Animation = m_Animation;
                                            break;
                                }
                            }
                        }
                    }
                    else if (obj is SpriteAtlas m_SpriteAtlas)
                    {
                        if (m_SpriteAtlas.m_RenderDataMap.Count > 0)
                        {
                            Logger.Verbose($"文件 {m_SpriteAtlas.assetsFile.fileName} 中具有 {m_SpriteAtlas.m_PathID} 的 SpriteAtlas 包含 {m_SpriteAtlas.m_PackedSprites.Count} 个已打包的精灵，尝试获取它们...");
                            foreach (var m_PackedSprite in m_SpriteAtlas.m_PackedSprites)
                            {
                                if (m_PackedSprite.TryGet(out var m_Sprite))
                                {
                                    if (m_Sprite.m_SpriteAtlas.IsNull)
                                    {
                                        Logger.Verbose($"从文件 {m_Sprite.assetsFile.fileName} 获取的精灵 {m_Sprite.m_PathID}，正在分配到父精灵图集...");
                                        m_Sprite.m_SpriteAtlas.Set(m_SpriteAtlas);
                                    }
                                    else
                                    {
                                        m_Sprite.m_SpriteAtlas.TryGet(out var m_SpriteAtlaOld);
                                        if (m_SpriteAtlaOld.m_IsVariant)
                                        {
                                            Logger.Verbose($"文件 {m_Sprite.assetsFile.fileName} 中的精灵 {m_Sprite.m_PathID} 拥有原始精灵图集的变体，正在处理变体并分配到父精灵图集...");
                                            m_Sprite.m_SpriteAtlas.Set(m_SpriteAtlas);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
