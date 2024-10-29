using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    /// <summary> 表示序列化类型的类，用于存储类型的信息 </summary>
    public class SerializedType
    {
        /// <summary> 类的唯一标识符 </summary>
        public int classID;

        /// <summary> 表示该类型是否被剥离，剥离的类型不在运行时加载 </summary>
        public bool m_IsStrippedType;

        /// <summary> 脚本类型索引，用于在特定上下文中标识类型 </summary>
        public short m_ScriptTypeIndex = -1;

        /// <summary> 类型树，描述了类型的层次结构和元数据 </summary>
        public TypeTree m_Type;

        /// <summary> 脚本ID，类型的一个哈希标识符 </summary>
        public byte[] m_ScriptID; //Hash128

        /// <summary> 旧类型哈希，用于兼容或回退场景下的类型识别 </summary>
        public byte[] m_OldTypeHash; //Hash128

        /// <summary> 类型依赖，列出了该类型依赖的其他类型的标识符 </summary>
        public int[] m_TypeDependencies;

        /// <summary> 类名，类型的主要标识名称 </summary>
        public string m_KlassName;

        /// <summary> 命名空间，类型的命名空间，用于组织和定位类 </summary>
        public string m_NameSpace;

        /// <summary> 组装名称，包含命名空间和类型名称的完整名称 </summary>
        public string m_AsmName;

        /// <summary> 检查当前类型是否与提供的哈希字符串匹配 </summary>
        /// <param name="hashes">一个或多个哈希字符串，用于比较</param>
        /// <returns>如果任何一个哈希与旧类型哈希匹配，则返回true，否则返回false</returns>
        public bool Match(params string[] hashes) => hashes.Any(x => x == Convert.ToHexString(m_OldTypeHash));

        /// <summary> 重写字符串表示方法，用于生成类型的可读字符串表示 </summary>
        /// <returns>包含类型关键信息的格式化字符串</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"classID: {classID} | ");
            sb.Append($"IsStrippedType: {m_IsStrippedType} | ");
            sb.Append($"ScriptTypeIndex: {m_ScriptTypeIndex} | ");
            sb.Append($"KlassName: {m_KlassName} | ");
            sb.Append($"NameSpace: {m_NameSpace} | ");
            sb.Append($"AsmName: {m_AsmName}");
            return sb.ToString();
        }
    }
}