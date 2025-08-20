using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace AssetStudio
{
    public static class TypeTreeHelper
    {
        public static string ReadTypeString(TypeTree m_Type, ObjectReader reader)
        {
            reader.Reset();
            var sb = new StringBuilder();
            var m_Nodes = m_Type.m_Nodes;
            for (int i = 0; i < m_Nodes.Count; i++)
            {
                ReadStringValue(sb, m_Nodes, reader, ref i);
            }

            var readed = reader.Position - reader.byteStart;
            if (readed != reader.byteSize)
            {
                Logger.Info($"读取类型时出错，读取了 {readed} 字节但预期为 {reader.byteSize} 字节");
            }

            return sb.ToString();
        }

        private static void ReadStringValue(StringBuilder sb, List<TypeTreeNode> m_Nodes, EndianBinaryReader reader, ref int i)
        {
            var m_Node = m_Nodes[i];
            var level = m_Node.m_Level;
            var varTypeStr = m_Node.m_Type;
            var varNameStr = m_Node.m_Name;
            object value = null;
            var append = true;
            var align = (m_Node.m_MetaFlag & 0x4000) != 0;
            switch (varTypeStr)
            {
                case "SInt8":
                    value = reader.ReadSByte();
                    break;
                case "UInt8":
                    value = reader.ReadByte();
                    break;
                case "char":
                    value = BitConverter.ToChar(reader.ReadBytes(2), 0);
                    break;
                case "short":
                case "SInt16":
                    value = reader.ReadInt16();
                    break;
                case "UInt16":
                case "unsigned short":
                    value = reader.ReadUInt16();
                    break;
                case "int":
                case "SInt32":
                    value = reader.ReadInt32();
                    break;
                case "UInt32":
                case "unsigned int":
                case "Type*":
                    value = reader.ReadUInt32();
                    break;
                case "long long":
                case "SInt64":
                    value = reader.ReadInt64();
                    if (varNameStr == "m_PathID" && reader is ObjectReader objectReader)
                    {
                        if (objectReader.assetsFile.ObjectsDic.TryGetValue((long)value, out var obj))
                        {
                            value = obj.Name;
                        }
                    }

                    break;
                case "UInt64":
                case "unsigned long long":
                case "FileSize":
                    value = reader.ReadUInt64();
                    break;
                case "float":
                    value = reader.ReadSingle();
                    break;
                case "double":
                    value = reader.ReadDouble();
                    break;
                case "bool":
                    value = reader.ReadBoolean();
                    break;
                case "string":
                    append = false;
                    var str = reader.ReadAlignedString();
                    sb.AppendFormat("{0}{1} {2} = \"{3}\"\r\n", (new string('\t', level)), varTypeStr, varNameStr, str);
                    var toSkip = GetNodes(m_Nodes, i);
                    i += toSkip.Count - 1;
                    break;
                case "map":
                {
                    if ((m_Nodes[i + 1].m_MetaFlag & 0x4000) != 0)
                        align = true;
                    append = false;
                    sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level)), varTypeStr, varNameStr);
                    sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level + 1)), "Array", "Array");
                    var size = reader.ReadInt32();
                    sb.AppendFormat("{0}{1} {2} = {3}\r\n", (new string('\t', level + 1)), "int", "size", size);
                    var map = GetNodes(m_Nodes, i);
                    i += map.Count - 1;
                    var first = GetNodes(map, 4);
                    var next = 4 + first.Count;
                    var second = GetNodes(map, next);
                    for (int j = 0; j < size; j++)
                    {
                        sb.AppendFormat("{0}[{1}]\r\n", (new string('\t', level + 2)), j);
                        sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level + 2)), "pair", "data");
                        int tmp1 = 0;
                        int tmp2 = 0;
                        ReadStringValue(sb, first, reader, ref tmp1);
                        ReadStringValue(sb, second, reader, ref tmp2);
                    }

                    break;
                }
                case "TypelessData":
                {
                    append = false;
                    var size = reader.ReadInt32();
                    reader.ReadBytes(size);
                    i += 2;
                    sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level)), varTypeStr, varNameStr);
                    sb.AppendFormat("{0}{1} {2} = {3}\r\n", (new string('\t', level)), "int", "size", size);
                    break;
                }
                default:
                {
                    if (i < m_Nodes.Count - 1 && m_Nodes[i + 1].m_Type == "Array") //Array
                    {
                        if ((m_Nodes[i + 1].m_MetaFlag & 0x4000) != 0)
                            align = true;
                        append = false;
                        sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level)), varTypeStr, varNameStr);
                        sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level + 1)), "Array", "Array");
                        var size = reader.ReadInt32();
                        sb.AppendFormat("{0}{1} {2} = {3}\r\n", (new string('\t', level + 1)), "int", "size", size);
                        var vector = GetNodes(m_Nodes, i);
                        i += vector.Count - 1;
                        for (int j = 0; j < size; j++)
                        {
                            sb.AppendFormat("{0}[{1}]\r\n", (new string('\t', level + 2)), j);
                            int tmp = 3;
                            ReadStringValue(sb, vector, reader, ref tmp);
                        }

                        break;
                    }
                    else //Class
                    {
                        append = false;
                        sb.AppendFormat("{0}{1} {2}\r\n", (new string('\t', level)), varTypeStr, varNameStr);
                        var @class = GetNodes(m_Nodes, i);
                        i += @class.Count - 1;
                        for (int j = 1; j < @class.Count; j++)
                        {
                            ReadStringValue(sb, @class, reader, ref j);
                        }

                        break;
                    }
                }
            }

            if (append)
                sb.AppendFormat("{0}{1} {2} = {3}\r\n", (new string('\t', level)), varTypeStr, varNameStr, value);
            if (align)
                reader.AlignStream();
        }

        public static OrderedDictionary ReadType(TypeTree m_Types, ObjectReader reader, SerializedFile assetsFile = null)
        {
            reader.Reset();
            var dictionary = new OrderedDictionary();
            var nodes = m_Types.m_Nodes;
            for (var i = 1; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var varNameStr = node.m_Name;
                var readValue = ReadValue(nodes, reader, ref i, assetsFile);
                dictionary[varNameStr] = readValue;
                RecoverInfo(varNameStr, readValue, dictionary, reader);
            }

            var readed = reader.Position - reader.byteStart;
            if (readed != reader.byteSize)
            {
                Logger.Info($"读取类型时出错，读取了 {readed} 字节但预期为 {reader.byteSize} 字节");
            }

            return dictionary;
        }

        private static object ReadValue(List<TypeTreeNode> nodes, EndianBinaryReader reader, ref int i, SerializedFile assetsFile = null)
        {
            var node = nodes[i];
            var varTypeStr = node.m_Type;
            Logger.Verbose($"正在读取类型为 {varTypeStr} 的 {node.m_Name}");
            object value;
            var align = (node.m_MetaFlag & 0x4000) != 0;
            switch (varTypeStr)
            {
                case "SInt8":
                    value = reader.ReadSByte();
                    break;
                case "UInt8":
                    value = reader.ReadByte();
                    break;
                case "char":
                    value = BitConverter.ToChar(reader.ReadBytes(2), 0);
                    break;
                case "short":
                case "SInt16":
                    value = reader.ReadInt16();
                    break;
                case "UInt16":
                case "unsigned short":
                    value = reader.ReadUInt16();
                    break;
                case "int":
                case "SInt32":
                    value = reader.ReadInt32();
                    break;
                case "UInt32":
                case "unsigned int":
                case "Type*":
                    value = reader.ReadUInt32();
                    break;
                case "long long":
                case "SInt64":
                    value = reader.ReadInt64();
                    break;
                case "UInt64":
                case "unsigned long long":
                case "FileSize":
                    value = reader.ReadUInt64();
                    break;
                case "float":
                    value = reader.ReadSingle();
                    break;
                case "double":
                    value = reader.ReadDouble();
                    break;
                case "bool":
                    value = reader.ReadBoolean();
                    break;
                case "string":
                    value = reader.ReadAlignedString();
                    var toSkip = GetNodes(nodes, i);
                    i += toSkip.Count - 1;
                    break;
                case "map":
                {
                    if ((nodes[i + 1].m_MetaFlag & 0x4000) != 0)
                        align = true;
                    var map = GetNodes(nodes, i);
                    i += map.Count - 1;
                    var first = GetNodes(map, 4);
                    var next = 4 + first.Count;
                    var second = GetNodes(map, next);
                    var size = reader.ReadInt32();
                    var dic = new List<KeyValuePair<object, object>>();
                    for (int j = 0; j < size; j++)
                    {
                        int tmp1 = 0;
                        int tmp2 = 0;
                        dic.Add(new KeyValuePair<object, object>(ReadValue(first, reader, ref tmp1), ReadValue(second, reader, ref tmp2)));
                    }

                    value = dic;
                    break;
                }
                case "TypelessData":
                {
                    var size = reader.ReadInt32();
                    value = reader.ReadBytes(size);
                    i += 2;
                    break;
                }
                default:
                {
                    //Array
                    if (i < nodes.Count - 1 && nodes[i + 1].m_Type == "Array")
                    {
                        if ((nodes[i + 1].m_MetaFlag & 0x4000) != 0)
                            align = true;
                        var vector = GetNodes(nodes, i);
                        i += vector.Count - 1;
                        var size = reader.ReadInt32();
                        var list = new List<object>();
                        for (int j = 0; j < size; j++)
                        {
                            int tmp = 3;
                            list.Add(ReadValue(vector, reader, ref tmp, assetsFile));
                        }

                        value = list;
                        break;
                    }

                    //Class
                    var @class = GetNodes(nodes, i);
                    i += @class.Count - 1;
                    var obj = new OrderedDictionary();
                    for (int j = 1; j < @class.Count; j++)
                    {
                        var classmember = @class[j];
                        var name = classmember.m_Name;
                        var objValue = ReadValue(@class, reader, ref j, assetsFile);
                        obj[name] = objValue;
                        RecoverInfo(name, objValue, obj, reader as ObjectReader);
                    }

                    value = obj;
                    break;
                }
            }

            if (align)
                reader.AlignStream();
            return value;
        }

        /// <summary> 恢复信息 </summary>
        private static void RecoverInfo(string name, object objValue, OrderedDictionary dictionary, ObjectReader reader)
        {
            if (reader == null) return;
            var assetsFile = reader.assetsFile;
            if (!assetsFile.assetsManager.EnableAddressesAnalysis) return;

            switch (objValue)
            {
                case long value:
                    switch (name)
                    {
                        // 恢复指针内容
                        case "m_PathID":
                            if (assetsFile.ObjectsDic.TryGetValue(value, out var obj))
                            {
                                dictionary["Name"] = obj.Name;
                                dictionary["Type"] = obj.type.ToString();
                                // 保存读取的位置，ToType会修改reader的位置，所以保存一下，以便恢复
                                var position = reader.Position;
                                switch (obj)
                                {
                                    case MonoScript _:
                                    case Animator _:
                                    case MonoBehaviour _:
                                        var monoBehaviourDict = obj.ToType();
                                        dictionary[obj.type.ToString()] = monoBehaviourDict;
                                        break;
                                }

                                // 恢复位置
                                reader.Position = position;
                            }

                            break;
                    }

                    break;
                case int value:
                    if (name == "m_FileID")
                    {
                        value -= 1;
                        if (value <= 0 || value >= assetsFile.m_Externals.Count) break;
                        var assetsFileMExternal = assetsFile.m_Externals[value];
                        dictionary["Name"] = assetsFileMExternal.fileName;
                    }

                    break;
            }
        }

        private static List<TypeTreeNode> GetNodes(List<TypeTreeNode> m_Nodes, int index)
        {
            var nodes = new List<TypeTreeNode>();
            nodes.Add(m_Nodes[index]);
            var level = m_Nodes[index].m_Level;
            for (int i = index + 1; i < m_Nodes.Count; i++)
            {
                var member = m_Nodes[i];
                var level2 = member.m_Level;
                if (level2 <= level)
                {
                    return nodes;
                }

                nodes.Add(member);
            }

            return nodes;
        }
    }
}