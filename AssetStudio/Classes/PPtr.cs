using System;
using System.IO;
using System.Collections.Generic;
using AssetStudio.Utils;

namespace AssetStudio
{
    /// <summary>
    /// 表示一个指向Unity资源对象的指针。
    /// </summary>
    /// <typeparam name="T">资源对象的类型。</typeparam>
    public sealed class PPtr<T> : IYAMLExportable where T : Object
    {
        /// <summary>
        /// 资源对象在资产文件中的ID。
        /// </summary>
        public int m_FileID;

        /// <summary>
        /// 资源对象在资产文件路径中的ID。
        /// </summary>
        public long m_PathID;

        /// <summary>
        /// 当前指针关联的序列化文件。
        /// </summary>
        private SerializedFile assetsFile;

        /// <summary>
        /// 指针状态索引，默认为-2表示准备状态，-1表示丢失。
        /// </summary>
        private int index = -2;

        /// <summary>
        /// 获取指针指向的资源对象的名称。
        /// </summary>
        /// <returns>资源对象的名称。</returns>
        public string Name => TryGet(out var obj) ? obj.Name : string.Empty;

        /// <summary>
        /// 初始化PPtr对象。
        /// </summary>
        /// <param name="m_FileID">文件ID。</param>
        /// <param name="m_PathID">路径ID。</param>
        /// <param name="assetsFile">关联的序列化文件。</param>
        public PPtr(int m_FileID, long m_PathID, SerializedFile assetsFile)
        {
            this.m_FileID = m_FileID;
            this.m_PathID = m_PathID;
            this.assetsFile = assetsFile;
        }

        /// <summary>
        /// 从ObjectReader中读取并初始化PPtr对象。
        /// </summary>
        /// <param name="reader">用于读取数据的ObjectReader对象。</param>
        public PPtr(ObjectReader reader)
        {
            m_FileID = reader.ReadInt32();
            m_PathID = reader.m_Version < SerializedFileFormatVersion.Unknown_14 ? reader.ReadInt32() : reader.ReadInt64();
            assetsFile = reader.assetsFile;
        }

        /// <summary>
        /// 将PPtr对象导出为YAML节点。
        /// </summary>
        /// <param name="version">版本信息。</param>
        /// <returns>导出的YAML节点。</returns>
        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Style = MappingStyle.Flow;
            if (assetsFile != null)
            {
                // node.Add("Name", Name);
                var token = JsonUtils.ReadJson(Name);
                if (token != null)
                {
                    node.Add("fileID", token["fileID"]!.ToString());
                    node.Add("guid", token["guid"]!.ToString());
                    node.Add("type", token["type"]!.ToString());
                    return node;
                }
            }

            node.Add("fileID", m_FileID);

            return node;
        }

        /// <summary>
        /// 尝试获取关联的资产文件。
        /// </summary>
        /// <param name="result">获取的资产文件。</param>
        /// <returns>是否成功获取资产文件。</returns>
        private bool TryGetAssetsFile(out SerializedFile result)
        {
            result = null;
            if (m_FileID == 0)
            {
                result = assetsFile;
                return true;
            }

            if (m_FileID > 0 && m_FileID - 1 < assetsFile.m_Externals.Count)
            {
                var assetsManager = assetsFile.assetsManager;
                var assetsFileList = assetsManager.assetsFileList;
                var assetsFileIndexCache = assetsManager.assetsFileIndexCache;

                if (index == -2)
                {
                    var m_External = assetsFile.m_Externals[m_FileID - 1];
                    var name = m_External.fileName;
                    if (!assetsFileIndexCache.TryGetValue(name, out index))
                    {
                        index = assetsFileList.FindIndex(x => x.fileName.Equals(name, StringComparison.OrdinalIgnoreCase));
                        assetsFileIndexCache.Add(name, index);
                    }
                }

                if (index >= 0)
                {
                    result = assetsFileList[index];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试获取指针指向的资源对象。
        /// </summary>
        /// <param name="result">获取的资源对象。</param>
        /// <returns>是否成功获取资源对象。</returns>
        public bool TryGet(out T result)
        {
            if (TryGetAssetsFile(out var sourceFile))
            {
                if (sourceFile.ObjectsDic.TryGetValue(m_PathID, out var obj))
                {
                    if (obj is T variable)
                    {
                        result = variable;
                        return true;
                    }
                }
            }

            result = null;
            return false;
        }

        /// <summary>
        /// 尝试获取指针指向的资源对象，并指定类型T2。
        /// </summary>
        /// <param name="result">获取的资源对象。</param>
        /// <typeparam name="T2">指定的资源对象类型。</typeparam>
        /// <returns>是否成功获取资源对象。</returns>
        public bool TryGet<T2>(out T2 result) where T2 : Object
        {
            if (TryGetAssetsFile(out var sourceFile))
            {
                if (sourceFile.ObjectsDic.TryGetValue(m_PathID, out var obj))
                {
                    if (obj is T2 variable)
                    {
                        result = variable;
                        return true;
                    }
                }
            }

            result = null;
            return false;
        }

        /// <summary>
        /// 设置指针指向的资源对象。
        /// </summary>
        /// <param name="m_Object">要设置的资源对象。</param>
        public void Set(T m_Object)
        {
            var name = m_Object.assetsFile.fileName;
            if (string.Equals(assetsFile.fileName, name, StringComparison.OrdinalIgnoreCase))
            {
                m_FileID = 0;
            }
            else
            {
                m_FileID = assetsFile.m_Externals.FindIndex(x => string.Equals(x.fileName, name, StringComparison.OrdinalIgnoreCase));
                if (m_FileID == -1)
                {
                    assetsFile.m_Externals.Add(new FileIdentifier
                    {
                        fileName = m_Object.assetsFile.fileName
                    });
                    m_FileID = assetsFile.m_Externals.Count;
                }
                else
                {
                    m_FileID += 1;
                }
            }

            var assetsManager = assetsFile.assetsManager;
            var assetsFileList = assetsManager.assetsFileList;
            var assetsFileIndexCache = assetsManager.assetsFileIndexCache;

            if (!assetsFileIndexCache.TryGetValue(name, out index))
            {
                index = assetsFileList.FindIndex(x => x.fileName.Equals(name, StringComparison.OrdinalIgnoreCase));
                assetsFileIndexCache.Add(name, index);
            }

            m_PathID = m_Object.m_PathID;
        }

        /// <summary>
        /// 将当前PPtr对象转换为指定类型的PPtr对象。
        /// </summary>
        /// <typeparam name="T2">要转换的类型。</typeparam>
        /// <returns>转换后的PPtr对象。</returns>
        public PPtr<T2> Cast<T2>() where T2 : Object
        {
            return new PPtr<T2>(m_FileID, m_PathID, assetsFile);
        }

        /// <summary>
        /// 检查指针是否为null。
        /// </summary>
        /// <returns>如果指针为null，则返回true；否则返回false。</returns>
        public bool IsNull => m_PathID == 0 || m_FileID < 0;
    }
}