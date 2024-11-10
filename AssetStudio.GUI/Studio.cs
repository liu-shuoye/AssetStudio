using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using AssetStudio.GUI.Properties;
using CubismLive2DExtractor;
using static AssetStudio.GUI.Exporter;

namespace AssetStudio.GUI
{
    internal enum ExportFilter
    {
        All,
        Selected,
        Filtered
    }

    internal static class Studio
    {
        public static Game Game;
        public static bool AutoDetectMultipleBundle;

        /// <summary> 是否跳过容器恢复 </summary>
        public static bool SkipContainer = false;

        public static AssetsManager assetsManager = new();
        public static AssemblyLoader assemblyLoader = new();

        /// <summary>  导出资源列表 </summary>
        public static List<AssetItem> exportableAssets = new();

        /// <summary>  当前显示的资源列表 </summary>
        public static List<AssetItem> visibleAssets = new();

        /// <summary>  所有 CubismMoc 脚本文件 </summary>
        public static readonly List<MonoBehaviour> CubismMocMonoBehaviours = new();

        /// <summary> live2d 资源包列表 </summary>
        private static readonly Dictionary<Object, string> Live2dResourceContainers = new();

        /// <summary> Sprite 图集拆分数据 </summary>
        public static readonly Dictionary<Texture2D, SortedDictionary<string, Sprite>> SpriteAtlasSplitData = new();

        /// <summary>  更新状态栏 </summary>
        internal static Action<string> StatusStripUpdate = x => { };

        public static int ExtractFolder(string path, string savePath)
        {
            int extractedCount = 0;
            Progress.Reset();
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var fileOriPath = Path.GetDirectoryName(file);
                var fileSavePath = fileOriPath.Replace(path, savePath);
                extractedCount += ExtractFile(file, fileSavePath);
                Progress.Report(i + 1, files.Length);
            }

            return extractedCount;
        }

        public static int ExtractFile(string[] fileNames, string savePath)
        {
            int extractedCount = 0;
            Progress.Reset();
            for (var i = 0; i < fileNames.Length; i++)
            {
                var fileName = fileNames[i];
                extractedCount += ExtractFile(fileName, savePath);
                Progress.Report(i + 1, fileNames.Length);
            }

            return extractedCount;
        }

        public static int ExtractFile(string fileName, string savePath)
        {
            int extractedCount = 0;
            var reader = new FileReader(fileName);
            reader = reader.PreProcessing(Game, AutoDetectMultipleBundle);
            if (reader.FileType == FileType.BundleFile)
                extractedCount += ExtractBundleFile(reader, savePath);
            else if (reader.FileType == FileType.WebFile)
                extractedCount += ExtractWebDataFile(reader, savePath);
            else if (reader.FileType == FileType.BlkFile)
                extractedCount += ExtractBlkFile(reader, savePath);
            else if (reader.FileType == FileType.BlockFile)
                extractedCount += ExtractBlockFile(reader, savePath);
            else
                reader.Dispose();
            return extractedCount;
        }

        private static int ExtractBundleFile(FileReader reader, string savePath)
        {
            StatusStripUpdate($"正在解压缩 {reader.FileName} ...");
            try
            {
                var bundleFile = new BundleFile(reader, Game);
                reader.Dispose();
                if (bundleFile.fileList.Count > 0)
                {
                    var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                    return ExtractStreamFile(extractPath, bundleFile.fileList);
                }
            }
            catch (InvalidCastException)
            {
                Logger.Error($"游戏类型不匹配，预期为 {nameof(Mr0k)}，但得到 {Game.Name} ({Game.GetType().Name})！！");
            }

            return 0;
        }

        private static int ExtractWebDataFile(FileReader reader, string savePath)
        {
            StatusStripUpdate($"正在解压缩 {reader.FileName} ...");
            var webFile = new WebFile(reader);
            reader.Dispose();
            if (webFile.fileList.Count > 0)
            {
                var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                return ExtractStreamFile(extractPath, webFile.fileList);
            }

            return 0;
        }

        private static int ExtractBlkFile(FileReader reader, string savePath)
        {
            int total = 0;
            StatusStripUpdate($"正在解压缩 {reader.FileName} ...");
            try
            {
                using var stream = BlkUtils.Decrypt(reader, (Blk)Game);
                do
                {
                    stream.Offset = stream.AbsolutePosition;
                    var dummyPath = Path.Combine(reader.FullPath, stream.AbsolutePosition.ToString("X8"));
                    var subReader = new FileReader(dummyPath, stream, true);
                    var subSavePath = Path.Combine(savePath, reader.FileName + "_unpacked");
                    switch (subReader.FileType)
                    {
                        case FileType.BundleFile:
                            total += ExtractBundleFile(subReader, subSavePath);
                            break;
                        case FileType.MhyFile:
                            total += ExtractMhyFile(subReader, subSavePath);
                            break;
                    }
                } while (stream.Remaining > 0);
            }
            catch (InvalidCastException)
            {
                Logger.Error($"游戏类型不匹配，预期为 {nameof(Blk)}，但得到 {Game.Name} ({Game.GetType().Name})！！");
            }

            return total;
        }

        private static int ExtractBlockFile(FileReader reader, string savePath)
        {
            int total = 0;
            StatusStripUpdate($"正在解压缩 {reader.FileName} ...");
            using var stream = new OffsetStream(reader.BaseStream, 0);
            do
            {
                stream.Offset = stream.AbsolutePosition;
                var subSavePath = Path.Combine(savePath, reader.FileName + "_unpacked");
                var dummyPath = Path.Combine(reader.FullPath, stream.AbsolutePosition.ToString("X8"));
                var subReader = new FileReader(dummyPath, stream, true);
                total += ExtractBundleFile(subReader, subSavePath);
            } while (stream.Remaining > 0);

            return total;
        }

        private static int ExtractMhyFile(FileReader reader, string savePath)
        {
            StatusStripUpdate($"正在解压缩 {reader.FileName} ...");
            try
            {
                var mhy0File = new MhyFile(reader, (Mhy)Game);
                reader.Dispose();
                if (mhy0File.fileList.Count > 0)
                {
                    var extractPath = Path.Combine(savePath, reader.FileName + "_unpacked");
                    return ExtractStreamFile(extractPath, mhy0File.fileList);
                }
            }
            catch (InvalidCastException)
            {
                Logger.Error($"游戏类型不匹配，预期为 {nameof(Mhy)}，但得到 {Game.Name} ({Game.GetType().Name})！！");
            }

            return 0;
        }

        private static int ExtractStreamFile(string extractPath, List<StreamFile> fileList)
        {
            int extractedCount = 0;
            foreach (var file in fileList)
            {
                var filePath = Path.Combine(extractPath, file.path);
                var fileDirectory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(fileDirectory))
                {
                    Directory.CreateDirectory(fileDirectory);
                }

                if (!File.Exists(filePath))
                {
                    using (var fileStream = File.Create(filePath))
                    {
                        file.stream.CopyTo(fileStream);
                    }

                    extractedCount += 1;
                }

                file.stream.Dispose();
            }

            return extractedCount;
        }

        public static void UpdateContainers()
        {
            if (exportableAssets.Count > 0)
            {
                Logger.Info("正在更新容器...");
                foreach (var asset in exportableAssets)
                {
                    if (int.TryParse(asset.Container, out var value))
                    {
                        var last = unchecked((uint)value);
                        var name = Path.GetFileNameWithoutExtension(asset.SourceFile.originalPath);
                        if (uint.TryParse(name, out var id))
                        {
                            var path = ResourceIndex.GetContainer(id, last);
                            if (!string.IsNullOrEmpty(path))
                            {
                                asset.Container = path;
                                if (asset.Type == ClassIDType.MiHoYoBinData)
                                {
                                    asset.Text = Path.GetFileNameWithoutExtension(path);
                                }
                            }
                        }
                    }
                }

                Logger.Info("已更新！！");
            }
        }

        /// <summary> 构建资源列表 </summary>
        public static (string, List<TreeNode>) BuildAssetData()
        {
            StatusStripUpdate("正在构建资源列表...");

            int i = 0;
            string productName = null;
            var objectCount = assetsManager.assetsFileList.Sum(x => x.Objects.Count);
            var objectAssetItemDic = new Dictionary<Object, AssetItem>(objectCount);
            var mihoyoBinDataNames = new List<(PPtr<Object>, string)>();
            // 构建容器
            var containers = new List<(PPtr<Object>, string)>();
            Live2dResourceContainers.Clear();
            SpriteAtlasSplitData.Clear();
            Progress.Reset();
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                foreach (var asset in assetsFile.Objects)
                {
                    if (assetsManager.tokenSource.IsCancellationRequested)
                    {
                        Logger.Info("构建资产列表已取消！！");
                        return (string.Empty, Array.Empty<TreeNode>().ToList());
                    }

                    // 创建资产项
                    var assetItem = new AssetItem(asset);
                    objectAssetItemDic.Add(asset, assetItem);
                    assetItem.UniqueID = "#" + i;
                    var exportable = false;
                    switch (asset)
                    {
                        case Texture2D mTexture2D:
                            if (!string.IsNullOrEmpty(mTexture2D.m_StreamData?.path))
                                assetItem.FullSize = asset.byteSize + mTexture2D.m_StreamData.size;
                            exportable = ClassIDType.Texture2D.CanExport();
                            break;
                        case AudioClip mAudioClip:
                            if (!string.IsNullOrEmpty(mAudioClip.m_Source))
                                assetItem.FullSize = asset.byteSize + mAudioClip.m_Size;
                            exportable = ClassIDType.AudioClip.CanExport();
                            break;
                        case VideoClip mVideoClip:
                            if (!string.IsNullOrEmpty(mVideoClip.m_OriginalPath))
                                assetItem.FullSize = asset.byteSize + mVideoClip.m_ExternalResources.m_Size;
                            exportable = ClassIDType.VideoClip.CanExport();
                            break;
                        case PlayerSettings mPlayerSettings:
                            productName = mPlayerSettings.productName;
                            exportable = ClassIDType.PlayerSettings.CanExport();
                            break;

                        case MonoBehaviour monoBehaviour when ClassIDType.MonoBehaviour.CanExport():
                            var assetName = monoBehaviour.m_Name;
                            if (monoBehaviour.m_Script.TryGet(out var script))
                            {
                                assetName = assetName == "" ? script.m_ClassName : assetName;
                                if (script.m_ClassName == "CubismMoc")
                                {
                                    CubismMocMonoBehaviours.Add(monoBehaviour);
                                }
                            }

                            assetItem.Text = assetName;
                            exportable = true;
                            break;
                        case AssetBundle mAssetBundle:
                            if (!SkipContainer)
                            {
                                // 恢复容器
                                foreach (var mContainer in mAssetBundle.Container)
                                {
                                    var preloadIndex = mContainer.Value.PreloadIndex;
                                    var preloadSize = mContainer.Value.PreloadSize;
                                    var preloadEnd = preloadIndex + preloadSize;
                                    for (int k = preloadIndex; k < preloadEnd; k++)
                                    {
                                        containers.Add((mAssetBundle.PreloadTable[k], mContainer.Key));
                                    }
                                }
                            }

                            exportable = ClassIDType.AssetBundle.CanExport();
                            break;
                        case IndexObject mIndexObject:
                            foreach (var index in mIndexObject.AssetMap)
                            {
                                mihoyoBinDataNames.Add((index.Value.Object, index.Key));
                            }

                            exportable = ClassIDType.IndexObject.CanExport();
                            break;
                        case ResourceManager mResourceManager:
                            foreach (var m_Container in mResourceManager.Container)
                            {
                                containers.Add((m_Container.Value, m_Container.Key));
                            }

                            exportable = ClassIDType.ResourceManager.CanExport();
                            break;
                        case Sprite sprite:
                            if (sprite.m_RD.texture.TryGet(out var texture))
                            {
                                if (!SpriteAtlasSplitData.TryGetValue(texture, out var spriteAtlasSplitData))
                                {
                                    spriteAtlasSplitData = new SortedDictionary<string, Sprite>();
                                    SpriteAtlasSplitData.Add(texture, spriteAtlasSplitData);
                                }

                                if (spriteAtlasSplitData.ContainsKey(sprite.Name))
                                {
                                    Logger.Warning($"有冲突的Sprite名称：{sprite.Name}，请检查SpriteAtlas");
                                }

                                spriteAtlasSplitData[sprite.Name] = sprite;
                            }

                            if (sprite.m_RD.alphaTexture.TryGet(out var alphaTexture))
                            {
                                if (!SpriteAtlasSplitData.TryGetValue(alphaTexture, out var spriteAtlasSplitData))
                                {
                                    spriteAtlasSplitData = new SortedDictionary<string, Sprite>();
                                    SpriteAtlasSplitData.Add(alphaTexture, spriteAtlasSplitData);
                                }

                                if (spriteAtlasSplitData.ContainsKey(sprite.Name))
                                {
                                    Logger.Warning($"有冲突的Sprite名称：{sprite.Name}，请检查SpriteAtlas");
                                }

                                spriteAtlasSplitData[sprite.Name] = sprite;
                            }

                            exportable = ClassIDType.Sprite.CanExport();
                            break;
                        case Mesh _ when ClassIDType.Mesh.CanExport():
                        case TextAsset _ when ClassIDType.TextAsset.CanExport():
                        case AnimationClip _ when ClassIDType.AnimationClip.CanExport():
                        case Font _ when ClassIDType.Font.CanExport():
                        case MovieTexture _ when ClassIDType.MovieTexture.CanExport():
                        case Material _ when ClassIDType.Material.CanExport():
                        case MiHoYoBinData _ when ClassIDType.MiHoYoBinData.CanExport():
                        case Shader _ when ClassIDType.Shader.CanExport():
                        case Animator _ when ClassIDType.Animator.CanExport():
                            exportable = true;
                            break;
                    }

                    if (assetItem.Text == "")
                    {
                        assetItem.Text = assetItem.TypeString + assetItem.UniqueID;
                    }

                    if (Properties.Settings.Default.displayAll || exportable)
                    {
                        exportableAssets.Add(assetItem);
                    }

                    Progress.Report(++i, objectCount);
                }
            }

            foreach ((var pptr, var name) in mihoyoBinDataNames)
            {
                if (assetsManager.tokenSource.IsCancellationRequested)
                {
                    Logger.Info("处理资产名称已取消！！");
                    return (string.Empty, Array.Empty<TreeNode>().ToList());
                }

                if (pptr.TryGet<MiHoYoBinData>(out var obj))
                {
                    var assetItem = objectAssetItemDic[obj];
                    if (int.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hash))
                    {
                        assetItem.Text = name;
                        assetItem.Container = hash.ToString();
                    }
                    else assetItem.Text = $"BinFile #{assetItem.PathID}";
                }
            }

            if (!SkipContainer)
            {
                foreach (var (pptr, container) in containers)
                {
                    if (assetsManager.tokenSource.IsCancellationRequested)
                    {
                        Logger.Info("处理容器已取消！！");
                        return (string.Empty, Array.Empty<TreeNode>().ToList());
                    }

                    if (pptr.TryGet(out var obj))
                    {
                        objectAssetItemDic[obj].Container = container;
                        switch (obj)
                        {
                            case AnimationClip:
                            case GameObject:
                            case Texture2D:
                            case MonoBehaviour:
                                Live2dResourceContainers[obj] = container;
                                break;
                        }
                    }
                }

                containers.Clear();
                if (Game.Type.IsGISubGroup())
                {
                    UpdateContainers();
                }
            }

            foreach (var tmp in exportableAssets)
            {
                if (assetsManager.tokenSource.IsCancellationRequested)
                {
                    Logger.Info("处理子项已取消！！");
                    return (string.Empty, Array.Empty<TreeNode>().ToList());
                }

                tmp.SetSubItems();
            }

            visibleAssets = exportableAssets;

            StatusStripUpdate("正在构建树形结构...");

            var treeNodeCollection = new List<TreeNode>();
            var treeNodeDictionary = new Dictionary<GameObject, GameObjectTreeNode>();
            int j = 0;
            Progress.Reset();
            var files = assetsManager.assetsFileList.GroupBy(x => x.originalPath ?? string.Empty).OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var (file, assetsFiles) in files)
            {
                var fileNode = !string.IsNullOrEmpty(file) ? new TreeNode(Path.GetFileName(file)) : null; //RootNode

                foreach (var assetsFile in assetsFiles)
                {
                    var assetsFileNode = new TreeNode(assetsFile.fileName);

                    foreach (var obj in assetsFile.Objects)
                    {
                        if (assetsManager.tokenSource.IsCancellationRequested)
                        {
                            Logger.Info("构建树结构已取消！！");
                            return (string.Empty, Array.Empty<TreeNode>().ToList());
                        }

                        if (obj is GameObject m_GameObject)
                        {
                            if (!treeNodeDictionary.TryGetValue(m_GameObject, out var currentNode))
                            {
                                currentNode = new GameObjectTreeNode(m_GameObject);
                                treeNodeDictionary.Add(m_GameObject, currentNode);
                            }

                            foreach (var pptr in m_GameObject.m_Components)
                            {
                                if (pptr.TryGet(out var m_Component))
                                {
                                    objectAssetItemDic[m_Component].TreeNode = currentNode;
                                    if (m_Component is MeshFilter m_MeshFilter)
                                    {
                                        if (m_MeshFilter.m_Mesh.TryGet(out var m_Mesh))
                                        {
                                            objectAssetItemDic[m_Mesh].TreeNode = currentNode;
                                        }
                                    }
                                    else if (m_Component is SkinnedMeshRenderer m_SkinnedMeshRenderer)
                                    {
                                        if (m_SkinnedMeshRenderer.m_Mesh.TryGet(out var m_Mesh))
                                        {
                                            objectAssetItemDic[m_Mesh].TreeNode = currentNode;
                                        }
                                    }
                                }
                            }

                            var parentNode = assetsFileNode;

                            if (m_GameObject.m_Transform != null)
                            {
                                if (m_GameObject.m_Transform.m_Father.TryGet(out var m_Father))
                                {
                                    if (m_Father.m_GameObject.TryGet(out var parentGameObject))
                                    {
                                        if (!treeNodeDictionary.TryGetValue(parentGameObject, out var parentGameObjectNode))
                                        {
                                            parentGameObjectNode = new GameObjectTreeNode(parentGameObject);
                                            treeNodeDictionary.Add(parentGameObject, parentGameObjectNode);
                                        }

                                        parentNode = parentGameObjectNode;
                                    }
                                }
                            }

                            parentNode.Nodes.Add(currentNode);
                        }
                    }

                    if (assetsFileNode.Nodes.Count > 0)
                    {
                        if (fileNode == null)
                        {
                            treeNodeCollection.Add(assetsFileNode);
                        }
                        else
                        {
                            fileNode.Nodes.Add(assetsFileNode);
                        }
                    }
                }

                if (fileNode?.Nodes.Count > 0)
                {
                    treeNodeCollection.Add(fileNode);
                }

                Progress.Report(++j, files.Count);
            }

            treeNodeDictionary.Clear();

            objectAssetItemDic.Clear();

            return (productName, treeNodeCollection);
        }

        public static Dictionary<string, SortedDictionary<int, TypeTreeItem>> BuildClassStructure()
        {
            var typeMap = new Dictionary<string, SortedDictionary<int, TypeTreeItem>>();
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                if (assetsManager.tokenSource.IsCancellationRequested)
                {
                    Logger.Info("处理类结构已取消！！");
                    return new Dictionary<string, SortedDictionary<int, TypeTreeItem>>();
                }

                if (typeMap.TryGetValue(assetsFile.unityVersion, out var curVer))
                {
                    foreach (var type in assetsFile.m_Types.Where(x => x.m_Type != null))
                    {
                        var key = type.classID;
                        if (type.m_ScriptTypeIndex >= 0)
                        {
                            key = -1 - type.m_ScriptTypeIndex;
                        }

                        curVer[key] = new TypeTreeItem(key, type.m_Type);
                    }
                }
                else
                {
                    var items = new SortedDictionary<int, TypeTreeItem>();
                    foreach (var type in assetsFile.m_Types.Where(x => x.m_Type != null))
                    {
                        var key = type.classID;
                        if (type.m_ScriptTypeIndex >= 0)
                        {
                            key = -1 - type.m_ScriptTypeIndex;
                        }

                        items[key] = new TypeTreeItem(key, type.m_Type);
                    }

                    typeMap.Add(assetsFile.unityVersion, items);
                }
            }

            return typeMap;
        }

        /// <summary>
        /// 导出资源
        /// </summary>
        /// <param name="savePath">保存路径</param>
        /// <param name="toExportAssets">导出的资源列表</param>
        /// <param name="exportType">导出格式类型</param>
        /// <param name="openAfterExport">导出后是否打开文件夹</param>
        /// <returns></returns>
        public static Task ExportAssets(string savePath, List<AssetItem> toExportAssets, ExportType exportType, bool openAfterExport)
        {
            return Task.Run(() =>
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

                var toExportCount = toExportAssets.Count;
                var exportedCount = 0;
                var i = 0;
                Progress.Reset();
                foreach (var asset in toExportAssets)
                {
                    string exportPath;
                    // 导出的文件存放方式
                    switch ((AssetGroupOption)Properties.Settings.Default.assetGroupOption)
                    {
                        case AssetGroupOption.ByType: //type name
                            exportPath = Path.Combine(savePath, asset.TypeString);
                            break;
                        case AssetGroupOption.ByContainer: //container path
                            if (!string.IsNullOrEmpty(asset.Container))
                            {
                                exportPath = Path.HasExtension(asset.Container) ? Path.Combine(savePath, Path.GetDirectoryName(asset.Container) ?? string.Empty) : Path.Combine(savePath, asset.Container);
                            }
                            else
                            {
                                // 如果不存在container，就根据来源导出
                                goto case AssetGroupOption.BySource;
                            }

                            break;
                        case AssetGroupOption.BySource: //source file
                            exportPath = string.IsNullOrEmpty(asset.SourceFile.originalPath) ? Path.Combine(savePath, asset.SourceFile.fileName + "_export") : Path.Combine(savePath, Path.GetFileName(asset.SourceFile.originalPath) + "_export", asset.SourceFile.fileName);

                            break;
                        default:
                            exportPath = savePath;
                            break;
                    }

                    exportPath += Path.DirectorySeparatorChar;
                    StatusStripUpdate($"[{exportedCount}/{toExportCount}] 正在导出 {asset.TypeString}: {asset.Text}");
                    try
                    {
                        switch (exportType)
                        {
                            case ExportType.Raw:
                                if (ExportRawFile(asset, exportPath))
                                {
                                    exportedCount++;
                                }

                                break;
                            case ExportType.Dump:
                                if (ExportDumpFile(asset, exportPath))
                                {
                                    exportedCount++;
                                }

                                break;
                            case ExportType.Convert:
                                if (ExportConvertFile(asset, exportPath))
                                {
                                    exportedCount++;
                                }

                                break;
                            case ExportType.JSON:
                                if (ExportJSONFile(asset, exportPath))
                                {
                                    exportedCount++;
                                }

                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"导出 {asset.Type}:{asset.Text} 错误\r\n{ex.Message}\r\n{ex.StackTrace}");
                    }

                    Progress.Report(++i, toExportCount);
                }

                var statusText = exportedCount == 0 ? "Nothing exported." : $"Finished exporting {exportedCount} assets.";

                if (toExportCount > exportedCount)
                {
                    statusText += $" {toExportCount - exportedCount} assets skipped (not extractable or files already exist)";
                }

                StatusStripUpdate(statusText);

                if (openAfterExport && exportedCount > 0)
                {
                    OpenFolderInExplorer(savePath);
                }
            });
        }

        public static Task ExportAssetsList(string savePath, List<AssetItem> toExportAssets, ExportListType exportListType)
        {
            return Task.Run(() =>
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

                Progress.Reset();

                switch (exportListType)
                {
                    case ExportListType.XML:
                        var filename = Path.Combine(savePath, "assets.xml");
                        var settings = new XmlWriterSettings() { Indent = true };
                        using (XmlWriter writer = XmlWriter.Create(filename, settings))
                        {
                            writer.WriteStartDocument();
                            writer.WriteStartElement("Assets");
                            writer.WriteAttributeString("filename", filename);
                            writer.WriteAttributeString("createdAt", DateTime.UtcNow.ToString("s"));
                            foreach (var asset in toExportAssets)
                            {
                                writer.WriteStartElement("Asset");
                                writer.WriteElementString("Name", asset.Name);
                                writer.WriteElementString("Container", asset.Container);
                                writer.WriteStartElement("Type");
                                writer.WriteAttributeString("id", ((int)asset.Type).ToString());
                                writer.WriteValue(asset.TypeString);
                                writer.WriteEndElement();
                                writer.WriteElementString("PathID", asset.PathID.ToString());
                                writer.WriteElementString("Source", asset.SourceFile.fullName);
                                writer.WriteElementString("Size", asset.FullSize.ToString());
                                writer.WriteEndElement();
                            }

                            writer.WriteEndElement();
                            writer.WriteEndDocument();
                        }

                        break;
                }

                var statusText = $"Finished exporting asset list with {toExportAssets.Count()} items.";

                StatusStripUpdate(statusText);

                if (Properties.Settings.Default.openAfterExport && toExportAssets.Count() > 0)
                {
                    OpenFolderInExplorer(savePath);
                }
            });
        }

        /// <summary> 导出拆分的物体 </summary>
        public static Task ExportSplitObjects(string savePath, TreeNodeCollection nodes)
        {
            return Task.Run(() =>
            {
                var exportNodes = GetNodes(nodes);
                var count = exportNodes.Cast<TreeNode>().Sum(x => x.Nodes.Count);
                int k = 0;
                Progress.Reset();
                foreach (TreeNode node in exportNodes)
                {
                    //遍历一级子节点
                    foreach (GameObjectTreeNode j in node.Nodes)
                    {
                        //收集所有子节点
                        var gameObjects = new List<GameObject>();
                        CollectNode(j, gameObjects);
                        //跳过一些不需要导出的object
                        if (gameObjects.All(x => x.m_SkinnedMeshRenderer == null && x.m_MeshFilter == null))
                        {
                            Progress.Report(++k, count);
                            continue;
                        }

                        //处理非法文件名
                        var filename = FixFileName(j.Text);
                        if (node.Parent != null)
                        {
                            filename = Path.Combine(FixFileName(node.Parent.Text), filename);
                        }

                        //每个文件存放在单独的文件夹
                        var targetPath = $"{savePath}{filename}{Path.DirectorySeparatorChar}";
                        //重名文件处理
                        for (int i = 1;; i++)
                        {
                            if (Directory.Exists(targetPath))
                            {
                                targetPath = $"{savePath}{filename} ({i}){Path.DirectorySeparatorChar}";
                            }
                            else
                            {
                                break;
                            }
                        }

                        Directory.CreateDirectory(targetPath);
                        //导出FBX
                        StatusStripUpdate($"正在导出 {filename}.fbx");
                        try
                        {
                            ExportGameObject(j.gameObject, targetPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"导出游戏对象:{j.Text} 错误\r\n{ex.Message}\r\n{ex.StackTrace}");
                        }

                        Progress.Report(++k, count);
                        StatusStripUpdate($"已完成导出 {filename}.fbx");
                    }
                }

                if (Properties.Settings.Default.openAfterExport)
                {
                    OpenFolderInExplorer(savePath);
                }

                StatusStripUpdate("已完成");

                IEnumerable<TreeNode> GetNodes(TreeNodeCollection nodes)
                {
                    foreach (TreeNode node in nodes)
                    {
                        var subNodes = node.Nodes.OfType<TreeNode>().ToArray();
                        if (subNodes.Length == 0)
                        {
                            yield return node;
                        }
                        else
                        {
                            foreach (TreeNode subNode in subNodes)
                            {
                                yield return subNode;
                            }
                        }
                    }
                }
            });
        }

        private static void CollectNode(GameObjectTreeNode node, List<GameObject> gameObjects)
        {
            gameObjects.Add(node.gameObject);
            foreach (GameObjectTreeNode i in node.Nodes)
            {
                CollectNode(i, gameObjects);
            }
        }

        public static Task ExportAnimatorWithAnimationClip(AssetItem animator, List<AssetItem> animationList, string exportPath)
        {
            return Task.Run(() =>
            {
                Progress.Reset();
                StatusStripUpdate($"正在导出 {animator.Text}");
                try
                {
                    ExportAnimator(animator, exportPath, animationList);
                    if (Properties.Settings.Default.openAfterExport)
                    {
                        OpenFolderInExplorer(exportPath);
                    }

                    Progress.Report(1, 1);
                    StatusStripUpdate($"已完成导出 {animator.Text}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"导出动画器:{animator.Text} 错误\r\n{ex.Message}\r\n{ex.StackTrace}");
                    StatusStripUpdate("导出时出错");
                }
            });
        }

        public static Task ExportObjectsWithAnimationClip(string exportPath, TreeNodeCollection nodes, List<AssetItem> animationList = null)
        {
            return Task.Run(() =>
            {
                var gameObjects = new List<GameObject>();
                GetSelectedParentNode(nodes, gameObjects);
                if (gameObjects.Count > 0)
                {
                    var count = gameObjects.Count;
                    int i = 0;
                    Progress.Reset();
                    foreach (var gameObject in gameObjects)
                    {
                        StatusStripUpdate($"正在导出 {gameObject.m_Name}");
                        try
                        {
                            var subExportPath = Path.Combine(exportPath, gameObject.m_Name) + Path.DirectorySeparatorChar;
                            ExportGameObject(gameObject, subExportPath, animationList);
                            StatusStripUpdate($"已完成导出 {gameObject.m_Name}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"导出游戏对象:{gameObject.m_Name} 错误\r\n{ex.Message}\r\n{ex.StackTrace}");
                            StatusStripUpdate("导出时出错");
                        }

                        Progress.Report(++i, count);
                    }

                    if (Properties.Settings.Default.openAfterExport)
                    {
                        OpenFolderInExplorer(exportPath);
                    }
                }
                else
                {
                    StatusStripUpdate("没有选择要导出的对象。");
                }
            });
        }

        public static Task ExportObjectsMergeWithAnimationClip(string exportPath, List<GameObject> gameObjects, List<AssetItem> animationList = null)
        {
            return Task.Run(() =>
            {
                var name = Path.GetFileName(exportPath);
                Progress.Reset();
                StatusStripUpdate($"正在导出 {name}");
                try
                {
                    ExportGameObjectMerge(gameObjects, exportPath, animationList);
                    Progress.Report(1, 1);
                    StatusStripUpdate($"已完成导出 {name}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"导出模型:{name} 错误\r\n{ex.Message}\r\n{ex.StackTrace}");
                    StatusStripUpdate("导出时出错");
                }

                if (Properties.Settings.Default.openAfterExport)
                {
                    OpenFolderInExplorer(Path.GetDirectoryName(exportPath));
                }
            });
        }

        public static Task ExportNodesWithAnimationClip(string exportPath, List<TreeNode> nodes, List<AssetItem> animationList = null)
        {
            return Task.Run(() =>
            {
                int i = 0;
                Progress.Reset();
                foreach (var node in nodes)
                {
                    var name = node.Text;
                    StatusStripUpdate($"正在导出 {name}");
                    var gameObjects = new List<GameObject>();
                    GetSelectedParentNode(node.Nodes, gameObjects);
                    if (gameObjects.Count > 0)
                    {
                        var subExportPath = exportPath + Path.Combine(node.Text, FixFileName(node.Text) + ".fbx");
                        try
                        {
                            ExportGameObjectMerge(gameObjects, subExportPath, animationList);
                            Progress.Report(++i, nodes.Count);
                            StatusStripUpdate($"已完成导出 {name}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"导出模型:{name} 错误\r\n{ex.Message}\r\n{ex.StackTrace}");
                            StatusStripUpdate("导出时出错");
                        }
                    }
                    else
                    {
                        StatusStripUpdate("选择了空节点进行导出。");
                    }
                }

                if (Properties.Settings.Default.openAfterExport)
                {
                    OpenFolderInExplorer(exportPath);
                }
            });
        }

        public static void GetSelectedParentNode(TreeNodeCollection nodes, List<GameObject> gameObjects)
        {
            foreach (TreeNode i in nodes)
            {
                if (i is GameObjectTreeNode gameObjectTreeNode && i.Checked)
                {
                    gameObjects.Add(gameObjectTreeNode.gameObject);
                }
                else
                {
                    GetSelectedParentNode(i.Nodes, gameObjects);
                }
            }
        }

        public static TypeTree MonoBehaviourToTypeTree(MonoBehaviour m_MonoBehaviour)
        {
            if (!assemblyLoader.Loaded)
            {
                var openFolderDialog = new OpenFolderDialog();
                openFolderDialog.Title = "选择程序集文件夹";
                if (openFolderDialog.ShowDialog() == DialogResult.OK)
                {
                    assemblyLoader.Load(openFolderDialog.Folder);
                }
                else
                {
                    assemblyLoader.Loaded = true;
                }
            }

            return m_MonoBehaviour.ConvertToTypeTree(assemblyLoader);
        }

        public static string DumpAsset(Object obj)
        {
            var str = obj.Dump();
            if (str == null && obj is MonoBehaviour m_MonoBehaviour)
            {
                var type = MonoBehaviourToTypeTree(m_MonoBehaviour);
                str = m_MonoBehaviour.Dump(type);
            }

            if (string.IsNullOrEmpty(str))
            {
                var settings = new JsonSerializerSettings();
                settings.Converters.Add(new StringEnumConverter());
                str = JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented, settings);
            }

            return str;
        }

        public static void OpenFolderInExplorer(string path)
        {
            var info = new ProcessStartInfo(path);
            info.UseShellExecute = true;
            Process.Start(info);
        }

        #region 导出Live2D

        /// <summary>
        /// 导出 Live2D
        /// </summary>
        /// <param name="exportPath">导出路径</param>
        /// <param name="selCubismMocMonoBehaviours">选择的CubismMoc脚本文件</param>
        /// <param name="selClipMotions">选择的动画剪辑文件</param>
        /// <param name="selFadeMotions">选择的淡化动作文件</param>
        /// <param name="selFadeLst"></param>
        public static void ExportLive2D(string exportPath, List<MonoBehaviour> selCubismMocMonoBehaviours = null, List<AnimationClip> selClipMotions = null, List<MonoBehaviour> selFadeMotions = null, MonoBehaviour selFadeLst = null)
        {
            var baseDestPath = Path.Combine(exportPath, "Live2DOutput");
            // 是否将线性运动段计算为贝塞尔曲线段
            var forceBezier = Settings.Default.l2dForceBezier;
            var mocList = selCubismMocMonoBehaviours ?? CubismMocMonoBehaviours;
            var motionMode = Settings.Default.l2dMotionMode;
            if (selClipMotions != null)
                motionMode = Live2DMotionMode.AnimationClipV2;
            else if (selFadeMotions != null || selFadeLst != null)
                motionMode = Live2DMotionMode.MonoBehaviour;

            ThreadPool.QueueUserWorkItem(state =>
            {
                Logger.Info($"正在搜索 Live2D 文件...");

                var mocPathDict = new Dictionary<MonoBehaviour, (string, string)>();
                var mocPathList = new List<string>();
                foreach (var mocMonoBehaviour in CubismMocMonoBehaviours)
                {
                    if (!Live2dResourceContainers.TryGetValue(mocMonoBehaviour, out var fullContainerPath))
                        continue;

                    var pathSepIndex = fullContainerPath.LastIndexOf('/');
                    var basePath = pathSepIndex > 0
                        ? fullContainerPath.Substring(0, pathSepIndex)
                        : fullContainerPath;
                    mocPathDict.Add(mocMonoBehaviour, (fullContainerPath, basePath));
                }

                if (mocPathDict.Count == 0)
                {
                    Logger.Error("Live2D Cubism 导出错误\r\n找不到任何模型相关文件");
                    StatusStripUpdate("Live2D导出已取消");
                    Progress.Reset();
                    return;
                }

                var basePathSet = mocPathDict.Values.Select(x => x.Item2).ToHashSet();
                // 如果所有模型都在一个文件夹中，则使用基础路径
                var useFullContainerPath = mocPathDict.Count != basePathSet.Count;
                foreach (var moc in mocList)
                {
                    var mocPath = useFullContainerPath
                        ? mocPathDict[moc].Item1 //fullContainerPath
                        : mocPathDict[moc].Item2; //basePath
                    mocPathList.Add(mocPath);
                }

                mocPathDict.Clear();

                var lookup = Live2dResourceContainers.AsParallel().ToLookup(
                    x => mocPathList.Find(b => x.Value.Contains(b) && x.Value.Split('/').Any(y => y == b.Substring(b.LastIndexOf('/') + 1))),
                    x => x.Key
                );

                if (mocList[0].serializedType?.m_Type == null && !assemblyLoader.Loaded)
                {
                    Logger.Warning("可能需要指定程序集文件夹以便正确提取");
                    SelectAssemblyFolder();
                }

                var totalModelCount = lookup.LongCount(x => x.Key != null);
                var modelCounter = 0;
                var parallelExportCount = Settings.Default.parallelExportCount <= 0
                    ? Environment.ProcessorCount - 1
                    : Math.Min(Settings.Default.parallelExportCount, Environment.ProcessorCount - 1);
                parallelExportCount = Settings.Default.parallelExport ? parallelExportCount : 1;
                foreach (var assets in lookup)
                {
                    var srcContainer = assets.Key;
                    if (srcContainer == null)
                        continue;
                    var container = srcContainer;

                    Logger.Info($"[{modelCounter + 1}/{totalModelCount}] 正在导出 Live2D：\"{srcContainer}\"...");
                    try
                    {
                        var modelName = useFullContainerPath
                            ? Path.GetFileNameWithoutExtension(container)
                            : container.Substring(container.LastIndexOf('/') + 1);
                        container = Path.HasExtension(container)
                            ? container.Replace(Path.GetExtension(container), "")
                            : container;
                        var destPath = Path.Combine(baseDestPath, container) + Path.DirectorySeparatorChar;

                        var modelExtractor = new Live2DExtractor(assets, selClipMotions, selFadeMotions, selFadeLst);
                        modelExtractor.ExtractCubismModel(destPath, modelName, motionMode, assemblyLoader, forceBezier, parallelExportCount);
                        modelCounter++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Live2D 模型导出错误：\"{srcContainer}\"", ex);
                    }

                    Progress.Report(modelCounter, (int)totalModelCount);
                }

                Logger.Info($"完成导出 [{modelCounter}/{totalModelCount}] 个 Live2D 模型。");
                if (modelCounter < totalModelCount)
                {
                    var total = (int)totalModelCount;
                    Progress.Report(total, total);
                }

                if (Settings.Default.openAfterExport && modelCounter > 0)
                {
                    OpenFolderInExplorer(exportPath);
                }
            });
        }

        /// <summary> 选择程序集文件夹 </summary>
        private static void SelectAssemblyFolder()
        {
            if (!assemblyLoader.Loaded)
            {
                var openFolderDialog = new OpenFolderDialog();
                openFolderDialog.Title = "选择程序集文件夹";
                if (openFolderDialog.ShowDialog() == DialogResult.OK)
                {
                    assemblyLoader.Load(openFolderDialog.Folder);
                }
                else
                {
                    assemblyLoader.Loaded = true;
                }
            }
        }

        #endregion

        #region 导出Sprite图集信息

        /// <summary> 导出Sprite图集拆分信息 </summary>
        public static void ExportSpriteAtlasInfo(string exportPath)
        {
            foreach (var spriteAtlasSplitDataKeyValue in SpriteAtlasSplitData)
            {
                var path = Path.Combine(exportPath, $"{spriteAtlasSplitDataKeyValue.Key.Name}.json");
                ExportSpriteAtlasSplitData(spriteAtlasSplitDataKeyValue.Value, path);
            }
        }

        #endregion
    }
}