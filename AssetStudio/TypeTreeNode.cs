using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    /// <summary>
    /// 表示类型树节点的类，用于描述一个类型的各项属性和特征
    /// </summary>
    public class TypeTreeNode
    {
        /// <summary> 类型字符串表示 </summary>
        public string m_Type;

        /// <summary>  节点名称 </summary>
        public string m_Name;

        /// <summary>  节点的字节大小 </summary>
        public int m_ByteSize;

        /// <summary> 节点索引 </summary>
        public int m_Index;

        /// <summary>  类型标志位，包含类型属性如是否为数组等 </summary>
        public int m_TypeFlags; //m_IsArray

        /// <summary>  版本号，用于跟踪类型变更 </summary>
        public int m_Version;

        /// <summary>  元标志位，表示对齐等属性 </summary>
        public int m_MetaFlag;

        /// <summary>  类型层级，表示类型的深度或层级关系 </summary>
        public int m_Level;

        /// <summary>  类型字符串偏移量，用于定位类型字符串在字符串表中的位置 </summary>
        public uint m_TypeStrOffset;

        /// <summary>  名称字符串偏移量，用于定位名称字符串在字符串表中的位置 </summary>
        public uint m_NameStrOffset;

        /// <summary>  引用类型哈希值，用于快速比较和查找类型 </summary>
        public ulong m_RefTypeHash;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public TypeTreeNode()
        {
        }

        /// <summary>
        /// 构造函数，用于初始化类型树节点
        /// </summary>
        /// <param name="type">节点的类型</param>
        /// <param name="name">节点的名称</param>
        /// <param name="level">节点的层级</param>
        /// <param name="align">节点是否对齐</param>
        public TypeTreeNode(string type, string name, int level, bool align)
        {
            m_Type = type;
            m_Name = name;
            m_Level = level;
            m_MetaFlag = align ? 0x4000 : 0;
        }

        /// <summary>
        /// 重写ToString方法，提供类型树节点的字符串表示
        /// </summary>
        /// <returns>类型树节点的详细信息字符串</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"Type: {m_Type} | ");
            sb.Append($"Name: {m_Name} | ");
            sb.Append($"ByteSize: 0x{m_ByteSize:X8} | ");
            sb.Append($"Index: {m_Index} | ");
            sb.Append($"TypeFlags: {m_TypeFlags} | ");
            sb.Append($"Version: {m_Version} | ");
            sb.Append($"TypeStrOffset: 0x{m_TypeStrOffset:X8} | ");
            sb.Append($"NameStrOffset: 0x{m_NameStrOffset:X8}");
            return sb.ToString();
        }
    }
}