using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetStudio.Utils;

namespace AssetStudio
{
    /// <summary>
    /// 表示一个关键帧，包含时间点和对应的值，以及插值信息。
    /// </summary>
    /// <typeparam name="T">值的类型，必须实现IYAMLExportable接口。</typeparam>
    public class Keyframe<T> : IYAMLExportable where T : IYAMLExportable
    {
        /// <summary>
        /// 关键帧的时间点。
        /// </summary>
        public float time;

        /// <summary>
        /// 关键帧的值。
        /// </summary>
        public T value;

        /// <summary>
        /// 关键帧的入切线斜率。
        /// </summary>
        public T inSlope;

        /// <summary>
        /// 关键帧的出切线斜率。
        /// </summary>
        public T outSlope;

        /// <summary>
        /// 权重模式，决定如何混合关键帧。
        /// </summary>
        public int weightedMode;

        /// <summary>
        /// 关键帧的入权重值。
        /// </summary>
        public T inWeight;

        /// <summary>
        /// 关键帧的出权重值。
        /// </summary>
        public T outWeight;

        /// <summary>
        /// 初始化Keyframe对象。
        /// </summary>
        /// <param name="time">关键帧的时间点。</param>
        /// <param name="value">关键帧的值。</param>
        /// <param name="inSlope">关键帧的入切线斜率。</param>
        /// <param name="outSlope">关键帧的出切线斜率。</param>
        /// <param name="weight">关键帧的权重值。</param>
        public Keyframe(float time, T value, T inSlope, T outSlope, T weight)
        {
            this.time = time;
            this.value = value;
            this.inSlope = inSlope;
            this.outSlope = outSlope;
            weightedMode = 0;
            inWeight = weight;
            outWeight = weight;
        }

        /// <summary>
        /// 从ObjectReader中读取数据，初始化Keyframe对象。
        /// </summary>
        /// <param name="reader">用于读取数据的ObjectReader对象。</param>
        /// <param name="readerFunc">用于读取T类型值的委托函数。</param>
        public Keyframe(ObjectReader reader, Func<T> readerFunc)
        {
            time = reader.ReadSingle();
            value = readerFunc();
            inSlope = readerFunc();
            outSlope = readerFunc();
            if (reader.version[0] >= 2018) //2018 and up
            {
                weightedMode = reader.ReadInt32();
                inWeight = readerFunc();
                outWeight = readerFunc();
            }
        }

        /// <summary>
        /// 将Keyframe对象导出为YAML节点。
        /// </summary>
        /// <param name="version">版本数组，用于确定导出的版本。</param>
        /// <returns>包含Keyframe数据的YAML节点。</returns>
        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.AddSerializedVersion(ToSerializedVersion(version));
            node.Add(nameof(time), time);
            node.Add(nameof(value), value.ExportYAML(version));
            node.Add(nameof(inSlope), inSlope.ExportYAML(version));
            node.Add(nameof(outSlope), outSlope.ExportYAML(version));
            if (version[0] >= 2018) //2018 and up
            {
                node.Add(nameof(weightedMode), weightedMode);
                node.Add(nameof(inWeight), inWeight.ExportYAML(version));
                node.Add(nameof(outWeight), outWeight.ExportYAML(version));
            }

            return node;
        }

        /// <summary>
        /// 根据版本数组确定序列化版本。
        /// </summary>
        /// <param name="version">版本数组。</param>
        /// <returns>序列化版本号。</returns>
        private int ToSerializedVersion(int[] version)
        {
            if (version[0] >= 2018) //2018 and up
            {
                return 3;
            }
            else if (version[0] > 5 || (version[0] == 5 && version[1] >= 5))
            {
                return 2;
            }

            return 1;
        }
    }


    /// <summary>
    /// 泛型类AnimationCurve用于表示动画曲线，其中T为曲线关键帧的数据类型
    /// </summary>
    public class AnimationCurve<T> : IYAMLExportable where T : IYAMLExportable
    {
        // 曲线的关键帧列表
        public List<Keyframe<T>> m_Curve;

        // 描述曲线在开始之前的延伸方式
        public int m_PreInfinity;

        // 描述曲线在结束之后的延伸方式
        public int m_PostInfinity;

        // 旋转顺序，主要用于动画曲线的插值计算
        public int m_RotationOrder;

        /// <summary>
        /// 默认构造函数，初始化AnimationCurve对象
        /// </summary>
        public AnimationCurve()
        {
            m_PreInfinity = 2;
            m_PostInfinity = 2;
            m_RotationOrder = 4;
            m_Curve = new List<Keyframe<T>>();
        }

        /// <summary>
        /// 构造函数，从ObjectReader中读取数据初始化AnimationCurve对象
        /// </summary>
        /// <param name="reader">用于读取动画曲线数据的ObjectReader对象</param>
        /// <param name="readerFunc">用于读取关键帧数据的函数</param>
        public AnimationCurve(ObjectReader reader, Func<T> readerFunc)
        {
            var version = reader.version;
            int numCurves = reader.ReadInt32();
            m_Curve = new List<Keyframe<T>>();
            for (int i = 0; i < numCurves; i++)
            {
                m_Curve.Add(new Keyframe<T>(reader, readerFunc));
            }

            m_PreInfinity = reader.ReadInt32();
            m_PostInfinity = reader.ReadInt32();
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 3)) //5.3 and up
            {
                m_RotationOrder = reader.ReadInt32();
            }
        }

        /// <summary>
        /// 将动画曲线的数据导出为YAML格式
        /// </summary>
        /// <param name="version">Unity的版本号，用于确定导出格式</param>
        /// <returns>表示动画曲线数据的YAML节点</returns>
        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.AddSerializedVersion(ToSerializedVersion(version));
            node.Add(nameof(m_Curve), m_Curve.ExportYAML(version));
            node.Add(nameof(m_PreInfinity), m_PreInfinity);
            node.Add(nameof(m_PostInfinity), m_PostInfinity);
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 3)) //5.3 and up
            {
                node.Add(nameof(m_RotationOrder), m_RotationOrder);
            }

            return node;
        }

        /// <summary>
        /// 根据Unity版本号确定序列化版本
        /// </summary>
        /// <param name="version">Unity的版本号</param>
        /// <returns>序列化版本号</returns>
        private int ToSerializedVersion(int[] version)
        {
            if (version[0] > 2 || (version[0] == 2 && version[1] >= 1))
            {
                return 2;
            }

            return 1;
        }
    }


    /// <summary>
    /// 表示一个四元数曲线，用于动画 interpolation。
    /// 实现了 YAML 导出接口，以便于序列化和存储。
    /// </summary>
    public class QuaternionCurve : IYAMLExportable
    {
        /// <summary>
        /// 存储四元数关键帧的动画曲线。
        /// </summary>
        public AnimationCurve<Quaternion> curve;

        /// <summary>
        /// 曲线在文件系统中的路径，用于标识和存储。
        /// </summary>
        public string path;

        /// <summary>
        /// 初始化 QuaternionCurve 实例，指定路径。
        /// </summary>
        /// <param name="path">曲线的标识路径。</param>
        public QuaternionCurve(string path)
        {
            curve = new AnimationCurve<Quaternion>();
            this.path = path;
        }

        /// <summary>
        /// 从 ObjectReader 初始化 QuaternionCurve 实例，用于反序列化。
        /// </summary>
        /// <param name="reader">用于读取曲线数据的 ObjectReader 实例。</param>
        public QuaternionCurve(ObjectReader reader)
        {
            curve = new AnimationCurve<Quaternion>(reader, reader.ReadQuaternion);
            path = reader.ReadAlignedString();
        }

        /// <summary>
        /// 将曲线数据导出为 YAML 格式。
        /// </summary>
        /// <param name="version">当前数据的版本，用于处理不同版本的导出需求。</param>
        /// <returns>表示曲线数据的 YAML 节点。</returns>
        public YAMLNode ExportYAML(int[] version)
        {
            YAMLMappingNode node = new YAMLMappingNode();
            node.Add(nameof(curve), curve.ExportYAML(version));
            node.Add(nameof(path), path);
            return node;
        }

        /// <summary>
        /// 重写 Equals 方法，用于比较两个 QuaternionCurve 实例是否相等。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>如果对象相等则返回 true，否则返回 false。</returns>
        public override bool Equals(object obj)
        {
            if (obj is QuaternionCurve quaternionCurve)
            {
                return path == quaternionCurve.path;
            }

            return false;
        }

        /// <summary>
        /// 重写 GetHashCode 方法，为曲线实例生成哈希码。
        /// </summary>
        /// <returns>曲线实例的哈希码。</returns>
        public override int GetHashCode()
        {
            int hash = 199;
            unchecked
            {
                hash = 617 + hash * path.GetHashCode();
            }

            return hash;
        }
    }


    /// <summary>
    /// 表示一个打包的浮点数向量，用于有效存储和操作大量浮点数数据。
    /// </summary>
    public class PackedFloatVector : IYAMLExportable
    {
        /// <summary>
        /// 打包的浮点数的数量。
        /// </summary>
        public uint m_NumItems;

        /// <summary>
        /// 打包数据的范围，用于缩放解包。
        /// </summary>
        public float m_Range;

        /// <summary>
        /// 打包数据的起始值，用于解包时定位。
        /// </summary>
        public float m_Start;

        /// <summary>
        /// 存储打包浮点数数据的字节数组。
        /// </summary>
        public byte[] m_Data;

        /// <summary>
        /// 每个浮点数使用的位数，用于解包时的位操作。
        /// </summary>
        public byte m_BitSize;

        /// <summary>
        /// 初始化PackedFloatVector对象，从给定的ObjectReader中读取数据。
        /// </summary>
        /// <param name="reader">用于读取打包数据的ObjectReader实例。</param>
        public PackedFloatVector(ObjectReader reader)
        {
            m_NumItems = reader.ReadUInt32();
            m_Range = reader.ReadSingle();
            m_Start = reader.ReadSingle();

            int numData = reader.ReadInt32();
            m_Data = reader.ReadBytes(numData);
            reader.AlignStream();

            m_BitSize = reader.ReadByte();
            reader.AlignStream();
        }

        /// <summary>
        /// 将打包的浮点数向量数据导出为YAML格式。
        /// </summary>
        /// <param name="version">导出时指定的版本信息。</param>
        /// <returns>表示打包的浮点数向量数据的YAMLNode。</returns>
        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(m_NumItems), m_NumItems);
            node.Add(nameof(m_Range), m_Range);
            node.Add(nameof(m_Start), m_Start);
            node.Add(nameof(m_Data), m_Data.ExportYAML());
            node.Add(nameof(m_BitSize), m_BitSize);
            return node;
        }

        /// <summary>
        /// 解包浮点数数据。
        /// </summary>
        /// <param name="itemCountInChunk">每个块中的浮点数项数。</param>
        /// <param name="chunkStride">块之间的步长。</param>
        /// <param name="start">开始解包的索引位置。</param>
        /// <param name="numChunks">要解包的块数，默认为-1，表示解包所有数据。</param>
        /// <returns>解包后的浮点数数组。</returns>
        public float[] UnpackFloats(int itemCountInChunk, int chunkStride, int start = 0, int numChunks = -1)
        {
            int bitPos = m_BitSize * start;
            int indexPos = bitPos / 8;
            bitPos %= 8;

            float scale = 1.0f / m_Range;
            if (numChunks == -1)
                numChunks = (int)m_NumItems / itemCountInChunk;
            var end = chunkStride * numChunks / 4;
            var data = new List<float>();
            for (var index = 0; index != end; index += chunkStride / 4)
            {
                for (int i = 0; i < itemCountInChunk; ++i)
                {
                    uint x = 0;

                    int bits = 0;
                    while (bits < m_BitSize)
                    {
                        x |= (uint)((m_Data[indexPos] >> bitPos) << bits);
                        int num = Math.Min(m_BitSize - bits, 8 - bitPos);
                        bitPos += num;
                        bits += num;
                        if (bitPos == 8)
                        {
                            indexPos++;
                            bitPos = 0;
                        }
                    }

                    x &= (uint)(1 << m_BitSize) - 1u;
                    data.Add(x / (scale * ((1 << m_BitSize) - 1)) + m_Start);
                }
            }

            return data.ToArray();
        }
    }


    /// <summary>
    /// 表示一个打包的整数向量，用于高效存储和处理大量整数数据。
    /// </summary>
    public class PackedIntVector : IYAMLExportable
    {
        /// <summary>
        /// 打包的整数项的数量。
        /// </summary>
        public uint m_NumItems;

        /// <summary>
        /// 存储整数数据的字节数组。
        /// </summary>
        public byte[] m_Data;

        /// <summary>
        /// 每个整数的位大小。
        /// </summary>
        public byte m_BitSize;

        /// <summary>
        /// 从二进制流中读取数据，初始化PackedIntVector对象。
        /// </summary>
        /// <param name="reader">用于读取二进制数据的ObjectReader对象。</param>
        public PackedIntVector(ObjectReader reader)
        {
            m_NumItems = reader.ReadUInt32();

            int numData = reader.ReadInt32();
            m_Data = reader.ReadBytes(numData);
            reader.AlignStream();

            m_BitSize = reader.ReadByte();
            reader.AlignStream();
        }

        /// <summary>
        /// 将PackedIntVector对象导出为YAML格式。
        /// </summary>
        /// <param name="version">表示导出时的版本信息。</param>
        /// <returns>返回表示PackedIntVector对象的YAML节点。</returns>
        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(m_NumItems), m_NumItems);
            node.Add(nameof(m_Data), m_Data.ExportYAML());
            node.Add(nameof(m_BitSize), m_BitSize);
            return node;
        }

        /// <summary>
        /// 解包出整数数组。
        /// </summary>
        /// <returns>返回解包后的整数数组。</returns>
        public int[] UnpackInts()
        {
            var data = new int[m_NumItems];
            int indexPos = 0;
            int bitPos = 0;
            for (int i = 0; i < m_NumItems; i++)
            {
                int bits = 0;
                data[i] = 0;
                while (bits < m_BitSize)
                {
                    data[i] |= (m_Data[indexPos] >> bitPos) << bits;
                    int num = Math.Min(m_BitSize - bits, 8 - bitPos);
                    bitPos += num;
                    bits += num;
                    if (bitPos == 8)
                    {
                        indexPos++;
                        bitPos = 0;
                    }
                }

                data[i] &= (1 << m_BitSize) - 1;
            }

            return data;
        }
    }


    /// <summary>
    /// 表示一个打包的四元数向量，用于有效存储和传输四元数数据。
    /// 实现了IYAMLExportable接口，支持导出为YAML格式。
    /// </summary>
    public class PackedQuatVector : IYAMLExportable
    {
        /// <summary>
        /// 打包的四元数项的数量。
        /// </summary>
        public uint m_NumItems;

        /// <summary>
        /// 打包的四元数数据数组。
        /// </summary>
        public byte[] m_Data;

        /// <summary>
        /// 使用ObjectReader初始化PackedQuatVector实例。
        /// </summary>
        /// <param name="reader">用于读取打包数据的ObjectReader对象。</param>
        public PackedQuatVector(ObjectReader reader)
        {
            m_NumItems = reader.ReadUInt32();

            int numData = reader.ReadInt32();
            m_Data = reader.ReadBytes(numData);

            reader.AlignStream();
        }

        /// <summary>
        /// 将打包的四元数向量导出为YAML节点。
        /// </summary>
        /// <param name="version">表示导出版本的整数数组。</param>
        /// <returns>表示打包的四元数向量的YAMLNode对象。</returns>
        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(m_NumItems), m_NumItems);
            node.Add(nameof(m_Data), m_Data.ExportYAML());
            return node;
        }

        /// <summary>
        /// 解包出存储在m_Data中的四元数数组。
        /// </summary>
        /// <returns>包含解包四元数的数组。</returns>
        public Quaternion[] UnpackQuats()
        {
            var data = new Quaternion[m_NumItems];
            int indexPos = 0;
            int bitPos = 0;

            for (int i = 0; i < m_NumItems; i++)
            {
                uint flags = 0;

                int bits = 0;
                while (bits < 3)
                {
                    flags |= (uint)((m_Data[indexPos] >> bitPos) << bits);
                    int num = Math.Min(3 - bits, 8 - bitPos);
                    bitPos += num;
                    bits += num;
                    if (bitPos == 8)
                    {
                        indexPos++;
                        bitPos = 0;
                    }
                }

                flags &= 7;


                var q = new Quaternion();
                float sum = 0;
                for (int j = 0; j < 4; j++)
                {
                    if ((flags & 3) != j)
                    {
                        int bitSize = ((flags & 3) + 1) % 4 == j ? 9 : 10;
                        uint x = 0;

                        bits = 0;
                        while (bits < bitSize)
                        {
                            x |= (uint)((m_Data[indexPos] >> bitPos) << bits);
                            int num = Math.Min(bitSize - bits, 8 - bitPos);
                            bitPos += num;
                            bits += num;
                            if (bitPos == 8)
                            {
                                indexPos++;
                                bitPos = 0;
                            }
                        }

                        x &= (uint)((1 << bitSize) - 1);
                        q[j] = x / (0.5f * ((1 << bitSize) - 1)) - 1;
                        sum += q[j] * q[j];
                    }
                }

                int lastComponent = (int)(flags & 3);
                q[lastComponent] = (float)Math.Sqrt(1 - sum);
                if ((flags & 4) != 0u)
                    q[lastComponent] = -q[lastComponent];
                data[i] = q;
            }

            return data;
        }
    }

    /// <summary>
    /// 表示一个压缩的动画曲线，实现了YAML导出接口。
    /// </summary>
    public class CompressedAnimationCurve : IYAMLExportable
    {
        /// <summary>
        /// 动画曲线的路径。
        /// </summary>
        public string m_Path;

        /// <summary>
        /// 动画关键帧的时间。
        /// </summary>
        public PackedIntVector m_Times;

        /// <summary>
        /// 动画关键帧的值，以四元数形式表示。
        /// </summary>
        public PackedQuatVector m_Values;

        /// <summary>
        /// 动画关键帧的斜率，用于插值。
        /// </summary>
        public PackedFloatVector m_Slopes;

        /// <summary>
        /// 动画曲线的前置无限大类型。
        /// </summary>
        public int m_PreInfinity;

        /// <summary>
        /// 动画曲线的后置无限大类型。
        /// </summary>
        public int m_PostInfinity;

        /// <summary>
        /// 初始化CompressedAnimationCurve对象。
        /// </summary>
        /// <param name="reader">用于读取动画曲线数据的ObjectReader对象。</param>
        public CompressedAnimationCurve(ObjectReader reader)
        {
            m_Path = reader.ReadAlignedString();
            m_Times = new PackedIntVector(reader);
            m_Values = new PackedQuatVector(reader);
            m_Slopes = new PackedFloatVector(reader);
            m_PreInfinity = reader.ReadInt32();
            m_PostInfinity = reader.ReadInt32();
        }

        /// <summary>
        /// 将动画曲线数据导出为YAML格式。
        /// </summary>
        /// <param name="version">当前数据的版本号，用于处理不同版本的数据格式。</param>
        /// <returns>表示动画曲线的YAML节点。</returns>
        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(m_Path), m_Path);
            node.Add(nameof(m_Times), m_Times.ExportYAML(version));
            node.Add(nameof(m_Values), m_Values.ExportYAML(version));
            node.Add(nameof(m_Slopes), m_Slopes.ExportYAML(version));
            node.Add(nameof(m_PreInfinity), m_PreInfinity);
            node.Add(nameof(m_PostInfinity), m_PostInfinity);
            return node;
        }
    }


    public class Vector3Curve : IYAMLExportable
    {
        public AnimationCurve<Vector3> curve;
        public string path;

        public Vector3Curve(string path)
        {
            curve = new AnimationCurve<Vector3>();
            this.path = path;
        }

        public Vector3Curve(ObjectReader reader)
        {
            curve = new AnimationCurve<Vector3>(reader, reader.ReadVector3);
            path = reader.ReadAlignedString();
        }

        public YAMLNode ExportYAML(int[] version)
        {
            YAMLMappingNode node = new YAMLMappingNode();
            node.Add(nameof(curve), curve.ExportYAML(version));
            node.Add(nameof(path), path);
            return node;
        }

        public override bool Equals(object obj)
        {
            if (obj is Vector3Curve vector3Curve)
            {
                return path == vector3Curve.path;
            }

            return false;
        }

        public override int GetHashCode()
        {
            int hash = 577;
            unchecked
            {
                hash = 419 + hash * path.GetHashCode();
            }

            return hash;
        }
    }

    /// <summary>
    /// 表示一个浮点数值的动画曲线，实现了YAML导出接口。
    /// </summary>
    public class FloatCurve : IYAMLExportable
    {
        /// <summary>
        /// 动画曲线。
        /// </summary>
        public AnimationCurve<Float> curve;

        /// <summary>
        /// 动画曲线对应的属性名称。
        /// </summary>
        public string attribute;

        /// <summary>
        /// 动画曲线在场景中的路径。
        /// </summary>
        public string path;

        /// <summary>
        /// 动画曲线所属的类ID。
        /// </summary>
        public ClassIDType classID;

        /// <summary>
        /// 关联的脚本指针。
        /// </summary>
        public PPtr<MonoScript> script;

        /// <summary>
        /// 额外的标记信息。
        /// </summary>
        public int flags;

        /// <summary>
        /// 初始化FloatCurve实例。
        /// </summary>
        /// <param name="path">动画曲线在场景中的路径。</param>
        /// <param name="attribute">动画曲线对应的属性名称。</param>
        /// <param name="classID">动画曲线所属的类ID。</param>
        /// <param name="script">关联的脚本指针。</param>
        public FloatCurve(string path, string attribute, ClassIDType classID, PPtr<MonoScript> script)
        {
            curve = new AnimationCurve<Float>();
            this.attribute = attribute;
            this.path = path;
            this.classID = classID;
            this.script = script;
            flags = 0;
        }

        /// <summary>
        /// 从ObjectReader中读取并初始化FloatCurve实例。
        /// </summary>
        /// <param name="reader">用于读取动画曲线信息的ObjectReader对象。</param>
        public FloatCurve(ObjectReader reader)
        {
            var version = reader.version;

            curve = new AnimationCurve<Float>(reader, reader.ReadFloat);
            attribute = reader.ReadAlignedString();
            path = reader.ReadAlignedString();
            classID = (ClassIDType)reader.ReadInt32();
            script = new PPtr<MonoScript>(reader);
            if (version[0] == 2022 && version[1] >= 2) //2022.2及更高版本
            {
                flags = reader.ReadInt32();
            }
        }

        /// <summary>
        /// 将动画曲线信息导出为YAML格式。
        /// </summary>
        /// <param name="version">Unity引擎的版本。</param>
        /// <returns>包含动画曲线信息的YAML节点。</returns>
        public YAMLNode ExportYAML(int[] version)
        {
            YAMLMappingNode node = new YAMLMappingNode();
            node.Add(nameof(curve), curve.ExportYAML(version));
            node.Add(nameof(attribute), attribute);
            node.Add(nameof(path), path);
            node.Add(nameof(classID), (int)classID);
            if (version[0] >= 2)
            {
                node.Add(nameof(script), script.ExportYAML(version));
            }

            node.Add(nameof(flags), flags);
            return node;
        }

        /// <summary>
        /// 重写Equals方法，用于比较两个FloatCurve对象是否相等。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>如果对象相等则返回true，否则返回false。</returns>
        public override bool Equals(object obj)
        {
            if (obj is FloatCurve floatCurve)
            {
                return attribute == floatCurve.attribute && path == floatCurve.path && classID == floatCurve.classID;
            }

            return false;
        }

        /// <summary>
        /// 重写GetHashCode方法，生成对象的哈希码。
        /// </summary>
        /// <returns>对象的哈希码。</returns>
        public override int GetHashCode()
        {
            int hash = 17;
            unchecked
            {
                hash = hash * 23 + path.GetHashCode();
            }

            return hash;
        }
    }


    public class PPtrKeyframe : IYAMLExportable
    {
        public float time;
        public PPtr<Object> value;

        public PPtrKeyframe(float time, PPtr<Object> value)
        {
            this.time = time;
            this.value = value;
        }

        public PPtrKeyframe(ObjectReader reader)
        {
            time = reader.ReadSingle();
            value = new PPtr<Object>(reader);
        }

        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(time), time);
            node.Add(nameof(value), value.ExportYAML(version));
            return node;
        }
    }

    public class PPtrCurve : IYAMLExportable
    {
        public List<PPtrKeyframe> curve;
        public string attribute;
        public string path;
        public int classID;
        public PPtr<MonoScript> script;
        public int flags;

        public PPtrCurve(string path, string attribute, ClassIDType classID, PPtr<MonoScript> script)
        {
            curve = new List<PPtrKeyframe>();
            this.attribute = attribute;
            this.path = path;
            this.classID = (int)classID;
            this.script = script;
            flags = 0;
        }

        public PPtrCurve(ObjectReader reader)
        {
            var version = reader.version;

            int numCurves = reader.ReadInt32();
            curve = new List<PPtrKeyframe>();
            for (int i = 0; i < numCurves; i++)
            {
                curve.Add(new PPtrKeyframe(reader));
            }

            attribute = reader.ReadAlignedString();
            path = reader.ReadAlignedString();
            classID = reader.ReadInt32();
            script = new PPtr<MonoScript>(reader);
            if (version[0] == 2022 && version[1] >= 2) //2022.2 and up
            {
                flags = reader.ReadInt32();
            }
        }

        public YAMLNode ExportYAML(int[] version)
        {
            YAMLMappingNode node = new YAMLMappingNode();
            node.Add(nameof(curve), curve.ExportYAML(version));
            node.Add(nameof(attribute), attribute);
            node.Add(nameof(path), path);
            node.Add(nameof(classID), (classID).ToString());
            node.Add(nameof(script), script.ExportYAML(version));
            node.Add(nameof(flags), flags);
            return node;
        }

        public override bool Equals(object obj)
        {
            if (obj is PPtrCurve pptrCurve)
            {
                return this == pptrCurve;
            }

            return false;
        }

        public override int GetHashCode()
        {
            int hash = 113;
            unchecked
            {
                hash = hash + 457 * attribute.GetHashCode();
                hash = hash * 433 + path.GetHashCode();
                hash = hash * 223 + classID.GetHashCode();
                hash = hash * 911 + script.GetHashCode();
                hash = hash * 342 + flags.GetHashCode();
            }

            return hash;
        }
    }

    /// <summary>
    /// 用于表示三维空间中的轴对齐边界框（AABB）
    /// </summary>
    public class AABB : IYAMLExportable
    {
        /// 中心坐标
        public Vector3 m_Center;

        /// 尺寸范围
        public Vector3 m_Extent;

        /// 构造函数，通过ObjectReader对象读取中心和范围的值
        public AABB(ObjectReader reader)
        {
            m_Center = reader.ReadVector3(); // 读取中心向量
            m_Extent = reader.ReadVector3(); // 读取范围向量
        }

        /// 将对象导出为YAML格式
        public YAMLNode ExportYAML(int[] version)
        {
            // 创建YAML映射节点
            var node = new YAMLMappingNode();

            // 将中心和范围的YAML表示添加到节点中
            node.Add(nameof(m_Center), m_Center.ExportYAML(version));
            node.Add(nameof(m_Extent), m_Extent.ExportYAML(version));

            // 返回生成的YAML节点
            return node;
        }
    }


    public class HandPose
    {
        public XForm m_GrabX;
        public float[] m_DoFArray;
        public float m_Override;
        public float m_CloseOpen;
        public float m_InOut;
        public float m_Grab;

        public HandPose()
        {
        }

        public HandPose(ObjectReader reader)
        {
            m_GrabX = reader.ReadXForm();
            m_DoFArray = reader.ReadSingleArray();
            m_Override = reader.ReadSingle();
            m_CloseOpen = reader.ReadSingle();
            m_InOut = reader.ReadSingle();
            m_Grab = reader.ReadSingle();
        }

        public static HandPose ParseGI(ObjectReader reader)
        {
            var handPose = new HandPose();

            handPose.m_GrabX = reader.ReadXForm4();
            handPose.m_DoFArray = reader.ReadSingleArray(20);
            handPose.m_Override = reader.ReadSingle();
            handPose.m_CloseOpen = reader.ReadSingle();
            handPose.m_InOut = reader.ReadSingle();
            handPose.m_Grab = reader.ReadSingle();

            return handPose;
        }
    }

    public class HumanGoal
    {
        public XForm m_X;
        public float m_WeightT;
        public float m_WeightR;
        public Vector3 m_HintT;
        public float m_HintWeightT;

        public HumanGoal()
        {
        }

        public HumanGoal(ObjectReader reader)
        {
            var version = reader.version;
            m_X = reader.ReadXForm();
            m_WeightT = reader.ReadSingle();
            m_WeightR = reader.ReadSingle();
            if (version[0] >= 5) //5.0 and up
            {
                m_HintT = version[0] > 5 || (version[0] == 5 && version[1] >= 4) ? reader.ReadVector3() : (Vector3)reader.ReadVector4(); //5.4 and up
                m_HintWeightT = reader.ReadSingle();
            }
        }

        public static HumanGoal ParseGI(ObjectReader reader)
        {
            var humanGoal = new HumanGoal();

            humanGoal.m_X = reader.ReadXForm4();
            humanGoal.m_WeightT = reader.ReadSingle();
            humanGoal.m_WeightR = reader.ReadSingle();

            humanGoal.m_HintT = (Vector3)reader.ReadVector4();
            humanGoal.m_HintWeightT = reader.ReadSingle();

            var m_HintR = (Vector3)reader.ReadVector4();
            var m_HintWeightR = reader.ReadSingle();

            return humanGoal;
        }
    }

    /// <summary> 人形姿势 </summary>
    public class HumanPose
    {
        /// 根变换
        public XForm m_RootX;

        /// 视线目标位置
        public Vector3 m_LookAtPosition;

        /// 视线目标权重
        public Vector4 m_LookAtWeight;

        /// 人体目标数组
        public List<HumanGoal> m_GoalArray;

        /// 左手姿势
        public HandPose m_LeftHandPose;

        /// 右手姿势
        public HandPose m_RightHandPose;

        /// 自由度数组
        public float[] m_DoFArray;

        /// 变换自由度数组
        public Vector3[] m_TDoFArray;

        /// 空的构造函数
        public HumanPose()
        {
        }

        /// 构造函数，通过ObjectReader对象读取姿势数据
        public HumanPose(ObjectReader reader)
        {
            // 版本信息
            var version = reader.version;

            // 读取根变换
            m_RootX = reader.ReadXForm();

            // 根据版本判断是否读取Vector3或Vector4作为视线目标位置
            m_LookAtPosition = version[0] > 5 || (version[0] == 5 && version[1] >= 4) ? reader.ReadVector3() : (Vector3)reader.ReadVector4(); // 版本5.4及以上
            m_LookAtWeight = reader.ReadVector4(); // 读取视线目标权重

            // 读取目标数量
            int numGoals = reader.ReadInt32();
            m_GoalArray = new List<HumanGoal>();

            // 读取每个人体目标
            for (int i = 0; i < numGoals; i++)
            {
                m_GoalArray.Add(new HumanGoal(reader));
            }

            // 读取左手和右手的姿势
            m_LeftHandPose = new HandPose(reader);
            m_RightHandPose = new HandPose(reader);

            // 读取自由度数组
            m_DoFArray = reader.ReadSingleArray();

            // 根据版本读取变换自由度数组
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 2)) // 版本5.2及以上
            {
                m_TDoFArray = reader.ReadVector3Array();
            }
        }

        /// 解析GI格式的HumanPose
        public static HumanPose ParseGI(ObjectReader reader)
        {
            // 版本信息
            var version = reader.version;

            // 创建一个新的HumanPose实例
            var humanPose = new HumanPose();

            // 读取根变换，视线位置，视线权重等
            humanPose.m_RootX = reader.ReadXForm4();
            humanPose.m_LookAtPosition = (Vector3)reader.ReadVector4();
            humanPose.m_LookAtWeight = reader.ReadVector4();

            // 读取4个目标并添加到Goal数组中
            humanPose.m_GoalArray = new List<HumanGoal>();
            for (int i = 0; i < 4; i++)
            {
                humanPose.m_GoalArray.Add(HumanGoal.ParseGI(reader));
            }

            // 读取左手和右手的姿势
            humanPose.m_LeftHandPose = HandPose.ParseGI(reader);
            humanPose.m_RightHandPose = HandPose.ParseGI(reader);

            // 读取自由度数组
            humanPose.m_DoFArray = reader.ReadSingleArray(0x37);

            // 读取变换自由度数组并将Vector4转换为Vector3
            humanPose.m_TDoFArray = reader.ReadVector4Array(0x15).Select(x => (Vector3)x).ToArray();

            // 跳过4个字节
            reader.Position += 4;

            return humanPose;
        }
    }


    public abstract class ACLClip
    {
        public virtual bool IsSet => false;
        public virtual uint CurveCount => 0;
        public abstract void Read(ObjectReader reader);
    }

    public class EmptyACLClip : ACLClip
    {
        public override void Read(ObjectReader reader)
        {
        }
    }

    public class MHYACLClip : ACLClip
    {
        public uint m_CurveCount;
        public uint m_ConstCurveCount;

        public byte[] m_ClipData;

        public override bool IsSet => !m_ClipData.IsNullOrEmpty();
        public override uint CurveCount => m_CurveCount;

        public MHYACLClip()
        {
            m_CurveCount = 0;
            m_ConstCurveCount = 0;
            m_ClipData = Array.Empty<byte>();
        }

        public override void Read(ObjectReader reader)
        {
            var byteCount = reader.ReadInt32();

            if (reader.Game.Type.IsSRGroup())
            {
                byteCount *= 4;
            }

            m_ClipData = reader.ReadBytes(byteCount);
            reader.AlignStream();

            m_CurveCount = reader.ReadUInt32();

            if (reader.Game.Type.IsSRGroup())
            {
                m_ConstCurveCount = reader.ReadUInt32();
            }
        }
    }

    public class AclTransformTrackIDToBindingCurveID
    {
        public uint rotationIDToBindingCurveID;
        public uint positionIDToBindingCurveID;
        public uint scaleIDToBindingCurveID;

        public AclTransformTrackIDToBindingCurveID(ObjectReader reader)
        {
            rotationIDToBindingCurveID = reader.ReadUInt32();
            positionIDToBindingCurveID = reader.ReadUInt32();
            scaleIDToBindingCurveID = reader.ReadUInt32();
        }
    }

    public class LnDACLClip : ACLClip
    {
        public uint m_CurveCount;
        public byte[] m_ClipData;

        public override bool IsSet => !m_ClipData.IsNullOrEmpty();
        public override uint CurveCount => m_CurveCount;

        public override void Read(ObjectReader reader)
        {
            m_CurveCount = reader.ReadUInt32();
            var compressedTransformTracksSize = reader.ReadUInt32();
            var compressedScalarTracksSize = reader.ReadUInt32();
            var aclTransformCount = reader.ReadUInt32();
            var aclScalarCount = reader.ReadUInt32();

            var compressedTransformTracksCount = reader.ReadInt32() * 0x10;
            var compressedTransformTracks = reader.ReadBytes(compressedTransformTracksCount);
            var compressedScalarTracksCount = reader.ReadInt32() * 0x10;
            var compressedScalarTracks = reader.ReadBytes(compressedScalarTracksCount);

            int numaclTransformTrackIDToBindingCurveID = reader.ReadInt32();
            var aclTransformTrackIDToBindingCurveID = new List<AclTransformTrackIDToBindingCurveID>();
            for (int i = 0; i < numaclTransformTrackIDToBindingCurveID; i++)
            {
                aclTransformTrackIDToBindingCurveID.Add(new AclTransformTrackIDToBindingCurveID(reader));
            }

            var aclScalarTrackIDToBindingCurveID = reader.ReadUInt32Array();
        }
    }

    public class GIACLClip : ACLClip
    {
        public uint m_CurveCount;
        public uint m_ConstCurveCount;

        public byte[] m_ClipData;
        public byte[] m_DatabaseData;

        public override bool IsSet => !m_ClipData.IsNullOrEmpty() && !m_DatabaseData.IsNullOrEmpty();
        public override uint CurveCount => m_CurveCount;

        public GIACLClip()
        {
            m_CurveCount = 0;
            m_ConstCurveCount = 0;
            m_ClipData = Array.Empty<byte>();
            m_DatabaseData = Array.Empty<byte>();
        }

        public override void Read(ObjectReader reader)
        {
            var aclTracksCount = (int)reader.ReadUInt64();
            var aclTracksOffset = reader.Position + reader.ReadInt64();
            var aclTracksCurveCount = reader.ReadUInt32();
            if (aclTracksOffset > reader.Length)
            {
                throw new IOException("偏移量超出范围");
            }

            var pos = reader.Position;
            reader.Position = aclTracksOffset;

            var tracksBytes = reader.ReadBytes(aclTracksCount);
            reader.AlignStream();

            using var tracksMS = new MemoryStream();
            tracksMS.Write(tracksBytes);
            tracksMS.AlignStream();
            m_CurveCount = aclTracksCurveCount;
            m_ClipData = tracksMS.ToArray();

            reader.Position = pos;

            var aclDatabaseCount = reader.ReadInt32();
            var aclDatabaseOffset = reader.Position + reader.ReadInt64();
            var aclDatabaseCurveCount = (uint)reader.ReadUInt64();
            if (aclDatabaseOffset > reader.Length)
            {
                throw new IOException("偏移量超出范围");
            }

            pos = reader.Position;
            reader.Position = aclDatabaseOffset;

            var databaseBytes = reader.ReadBytes(aclDatabaseCount);
            reader.AlignStream();

            using var databaseMS = new MemoryStream();
            databaseMS.Write(databaseBytes);
            databaseMS.AlignStream();

            m_ConstCurveCount = aclDatabaseCurveCount;
            m_DatabaseData = databaseMS.ToArray();

            reader.Position = pos;
        }
    }

    public class StreamedClip
    {
        public uint[] data;
        public uint curveCount;

        public StreamedClip()
        {
        }

        public StreamedClip(ObjectReader reader)
        {
            data = reader.ReadUInt32Array();
            curveCount = reader.ReadUInt32();
        }

        public static StreamedClip ParseGI(ObjectReader reader)
        {
            var streamedClipCount = (int)reader.ReadUInt64();
            var streamedClipOffset = reader.Position + reader.ReadInt64();
            var streamedClipCurveCount = (uint)reader.ReadUInt64();
            if (streamedClipOffset > reader.Length)
            {
                throw new IOException("偏移量超出范围");
            }

            var pos = reader.Position;
            reader.Position = streamedClipOffset;

            var streamedClip = new StreamedClip()
            {
                data = reader.ReadUInt32Array(streamedClipCount),
                curveCount = streamedClipCurveCount
            };

            reader.Position = pos;

            return streamedClip;
        }

        public class StreamedCurveKey
        {
            public int index;
            public float[] coeff;

            public float value;
            public float outSlope;
            public float inSlope;

            public StreamedCurveKey(EndianBinaryReader reader)
            {
                index = reader.ReadInt32();
                coeff = reader.ReadSingleArray(4);

                outSlope = coeff[2];
                value = coeff[3];
            }

            public float CalculateNextInSlope(float dx, StreamedCurveKey rhs)
            {
                //Stepped
                if (coeff[0] == 0f && coeff[1] == 0f && coeff[2] == 0f)
                {
                    return float.PositiveInfinity;
                }

                dx = Math.Max(dx, 0.0001f);
                var dy = rhs.value - value;
                var length = 1.0f / (dx * dx);
                var d1 = outSlope * dx;
                var d2 = dy + dy + dy - d1 - d1 - coeff[1] / length;
                return d2 / dx;
            }
        }

        public class StreamedFrame
        {
            public float time;
            public List<StreamedCurveKey> keyList;

            public StreamedFrame(EndianBinaryReader reader)
            {
                time = reader.ReadSingle();

                int numKeys = reader.ReadInt32();
                keyList = new List<StreamedCurveKey>();
                for (int i = 0; i < numKeys; i++)
                {
                    keyList.Add(new StreamedCurveKey(reader));
                }
            }
        }

        public List<StreamedFrame> ReadData()
        {
            var frameList = new List<StreamedFrame>();
            var buffer = new byte[data.Length * 4];
            Buffer.BlockCopy(data, 0, buffer, 0, buffer.Length);
            using (var reader = new EndianBinaryReader(new MemoryStream(buffer), EndianType.LittleEndian))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    frameList.Add(new StreamedFrame(reader));
                }
            }

            for (int frameIndex = 2; frameIndex < frameList.Count - 1; frameIndex++)
            {
                var frame = frameList[frameIndex];
                foreach (var curveKey in frame.keyList)
                {
                    for (int i = frameIndex - 1; i >= 0; i--)
                    {
                        var preFrame = frameList[i];
                        var preCurveKey = preFrame.keyList.FirstOrDefault(x => x.index == curveKey.index);
                        if (preCurveKey != null)
                        {
                            curveKey.inSlope = preCurveKey.CalculateNextInSlope(frame.time - preFrame.time, curveKey);
                            break;
                        }
                    }
                }
            }

            return frameList;
        }
    }

    public class DenseClip
    {
        public int m_FrameCount;
        public uint m_CurveCount;
        public float m_SampleRate;
        public float m_BeginTime;
        public float[] m_SampleArray;

        public DenseClip()
        {
        }

        public DenseClip(ObjectReader reader)
        {
            m_FrameCount = reader.ReadInt32();
            m_CurveCount = reader.ReadUInt32();
            m_SampleRate = reader.ReadSingle();
            m_BeginTime = reader.ReadSingle();
            m_SampleArray = reader.ReadSingleArray();
        }

        public static DenseClip ParseGI(ObjectReader reader)
        {
            var denseClip = new DenseClip();

            denseClip.m_FrameCount = reader.ReadInt32();
            denseClip.m_CurveCount = reader.ReadUInt32();
            denseClip.m_SampleRate = reader.ReadSingle();
            denseClip.m_BeginTime = reader.ReadSingle();

            var denseClipCount = (int)reader.ReadUInt64();
            var denseClipOffset = reader.Position + reader.ReadInt64();
            if (denseClipOffset > reader.Length)
            {
                throw new IOException("偏移量超出范围");
            }

            var pos = reader.Position;
            reader.Position = denseClipOffset;

            denseClip.m_SampleArray = reader.ReadSingleArray(denseClipCount);

            reader.Position = pos;

            return denseClip;
        }
    }

    public class ACLDenseClip : DenseClip
    {
        public int m_ACLType;
        public byte[] m_ACLArray;
        public float m_PositionFactor;
        public float m_EulerFactor;
        public float m_ScaleFactor;
        public float m_FloatFactor;
        public uint m_nPositionCurves;
        public uint m_nRotationCurves;
        public uint m_nEulerCurves;
        public uint m_nScaleCurves;
        public uint m_nGenericCurves;

        public ACLDenseClip(ObjectReader reader) : base(reader)
        {
            m_ACLType = reader.ReadInt32();
            if (reader.Game.Type.IsArknightsEndfield())
            {
                m_ACLArray = reader.ReadUInt8Array();
                reader.AlignStream();
                m_PositionFactor = reader.ReadSingle();
                m_EulerFactor = reader.ReadSingle();
                m_ScaleFactor = reader.ReadSingle();
                m_FloatFactor = reader.ReadSingle();
                m_nPositionCurves = reader.ReadUInt32();
                m_nRotationCurves = reader.ReadUInt32();
                m_nEulerCurves = reader.ReadUInt32();
                m_nScaleCurves = reader.ReadUInt32();
            }
            else if (reader.Game.Type.IsExAstris())
            {
                m_nPositionCurves = reader.ReadUInt32();
                m_nRotationCurves = reader.ReadUInt32();
                m_nEulerCurves = reader.ReadUInt32();
                m_nScaleCurves = reader.ReadUInt32();
                m_nGenericCurves = reader.ReadUInt32();
                m_PositionFactor = reader.ReadSingle();
                m_EulerFactor = reader.ReadSingle();
                m_ScaleFactor = reader.ReadSingle();
                m_FloatFactor = reader.ReadSingle();
                m_ACLArray = reader.ReadUInt8Array();
                reader.AlignStream();
            }

            Process();
        }

        private void Process()
        {
            if (m_ACLType == 0 || !m_SampleArray.IsNullOrEmpty())
            {
                return;
            }

            var sampleArray = new List<float>();

            var size = m_ACLType >> 2;
            var factor = (float)((1 << m_ACLType) - 1);
            var aclSpan = m_ACLArray.ToUInt4Array().AsSpan();
            var buffer = (stackalloc byte[8]);

            for (int i = 0; i < m_FrameCount; i++)
            {
                var index = i * (int)(m_CurveCount * size);
                for (int j = 0; j < m_nPositionCurves; j++)
                {
                    sampleArray.Add(ReadCurve(aclSpan, m_PositionFactor, ref index));
                }

                for (int j = 0; j < m_nRotationCurves; j++)
                {
                    sampleArray.Add(ReadCurve(aclSpan, 1.0f, ref index));
                }

                for (int j = 0; j < m_nEulerCurves; j++)
                {
                    sampleArray.Add(ReadCurve(aclSpan, m_EulerFactor, ref index));
                }

                for (int j = 0; j < m_nScaleCurves; j++)
                {
                    sampleArray.Add(ReadCurve(aclSpan, m_ScaleFactor, ref index));
                }

                var m_nFloatCurves = m_CurveCount - (m_nPositionCurves + m_nRotationCurves + m_nEulerCurves + m_nScaleCurves + m_nGenericCurves);
                for (int j = 0; j < m_nFloatCurves; j++)
                {
                    sampleArray.Add(ReadCurve(aclSpan, m_FloatFactor, ref index));
                }
            }

            m_SampleArray = sampleArray.ToArray();
        }

        private float ReadCurve(Span<byte> aclSpan, float curveFactor, ref int curveIndex)
        {
            var buffer = (stackalloc byte[8]);

            var curveSize = m_ACLType >> 2;
            var factor = (float)((1 << m_ACLType) - 1);

            aclSpan.Slice(curveIndex, curveSize).CopyTo(buffer);
            var temp = buffer.ToArray().ToUInt8Array(0, curveSize);
            buffer.Clear();
            temp.CopyTo(buffer);

            float curve;
            var value = BitConverter.ToUInt64(buffer);
            if (value != 0)
            {
                curve = ((value / factor) - 0.5f) * 2;
            }
            else
            {
                curve = -1.0f;
            }

            curve *= curveFactor;
            curveIndex += curveSize;

            return curve;
        }
    }

    public class ConstantClip
    {
        public float[] data;

        public ConstantClip()
        {
        }

        public ConstantClip(ObjectReader reader)
        {
            data = reader.ReadSingleArray();
        }

        public static ConstantClip ParseGI(ObjectReader reader)
        {
            var constantClipCount = (int)reader.ReadUInt64();
            var constantClipOffset = reader.Position + reader.ReadInt64();
            if (constantClipOffset > reader.Length)
            {
                throw new IOException("偏移量超出范围");
            }

            var pos = reader.Position;
            reader.Position = constantClipOffset;

            var constantClip = new ConstantClip();
            constantClip.data = reader.ReadSingleArray(constantClipCount);

            reader.Position = pos;

            return constantClip;
        }
    }

    public class ValueConstant
    {
        public uint m_ID;
        public uint m_TypeID;
        public uint m_Type;
        public uint m_Index;

        public ValueConstant(ObjectReader reader)
        {
            var version = reader.version;
            m_ID = reader.ReadUInt32();
            if (version[0] < 5 || (version[0] == 5 && version[1] < 5)) //5.5 down
            {
                m_TypeID = reader.ReadUInt32();
            }

            m_Type = reader.ReadUInt32();
            m_Index = reader.ReadUInt32();
        }
    }

    public class ValueArrayConstant
    {
        public List<ValueConstant> m_ValueArray;

        public ValueArrayConstant(ObjectReader reader)
        {
            int numVals = reader.ReadInt32();
            m_ValueArray = new List<ValueConstant>();
            for (int i = 0; i < numVals; i++)
            {
                m_ValueArray.Add(new ValueConstant(reader));
            }
        }
    }

    /// <summary>
    /// 表示一个动画剪辑，包含多种类型的动画数据。
    /// </summary>
    public class Clip
    {
        /// <summary>
        /// 存储ACL动画剪辑。
        /// </summary>
        public ACLClip m_ACLClip = new EmptyACLClip();

        /// <summary>
        /// 存储流式动画剪辑。
        /// </summary>
        public StreamedClip m_StreamedClip;

        /// <summary>
        /// 存储密集动画剪辑。
        /// </summary>
        public DenseClip m_DenseClip;

        /// <summary>
        /// 存储常量动画剪辑。
        /// </summary>
        public ConstantClip m_ConstantClip;

        /// <summary>
        /// 存储绑定信息。
        /// </summary>
        public ValueArrayConstant m_Binding;

        /// <summary>
        /// 默认构造函数。
        /// </summary>
        public Clip()
        {
        }

        /// <summary>
        /// 使用ObjectReader解析动画剪辑数据。
        /// </summary>
        /// <param name="reader">用于读取动画剪辑数据的ObjectReader对象。</param>
        public Clip(ObjectReader reader)
        {
            var version = reader.version;
            m_StreamedClip = new StreamedClip(reader);

            // 根据游戏类型选择性地初始化m_DenseClip
            if (reader.Game.Type.IsArknightsEndfield() || reader.Game.Type.IsExAstris())
            {
                m_DenseClip = new ACLDenseClip(reader);
            }
            else
            {
                m_DenseClip = new DenseClip(reader);
            }

            // 如果是SRGroup类型，读取MHYACLClip
            if (reader.Game.Type.IsSRGroup())
            {
                m_ACLClip = new MHYACLClip();
                m_ACLClip.Read(reader);
            }

            // 如果版本高于4.3，读取ConstantClip
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                m_ConstantClip = new ConstantClip(reader);
            }

            // 如果是GIGroup、BH3Group或ZZZCB1类型，读取MHYACLClip
            if (reader.Game.Type.IsGIGroup() || reader.Game.Type.IsBH3Group() || reader.Game.Type.IsZZZCB1())
            {
                m_ACLClip = new MHYACLClip();
                m_ACLClip.Read(reader);
            }

            // 如果是LoveAndDeepspace类型，读取LnDACLClip
            if (reader.Game.Type.IsLoveAndDeepspace())
            {
                m_ACLClip = new LnDACLClip();
                m_ACLClip.Read(reader);
            }

            // 如果版本低于2018.3，读取ValueArrayConstant
            if (version[0] < 2018 || (version[0] == 2018 && version[1] < 3)) //2018.3 down
            {
                m_Binding = new ValueArrayConstant(reader);
            }
        }

        /// <summary>
        /// 解析并返回GI类型的动画剪辑。
        /// </summary>
        /// <param name="reader">用于读取动画剪辑数据的ObjectReader对象。</param>
        /// <returns>解析后的Clip对象。</returns>
        public static Clip ParseGI(ObjectReader reader)
        {
            var clipOffset = reader.Position + reader.ReadInt64();
            if (clipOffset > reader.Length)
            {
                throw new IOException("偏移量超出范围");
            }

            var pos = reader.Position;
            reader.Position = clipOffset;

            var clip = new Clip();
            clip.m_StreamedClip = StreamedClip.ParseGI(reader);
            clip.m_DenseClip = DenseClip.ParseGI(reader);
            clip.m_ConstantClip = ConstantClip.ParseGI(reader);
            clip.m_ACLClip = new GIACLClip();
            clip.m_ACLClip.Read(reader);

            reader.Position = pos;

            return clip;
        }

        /// <summary>
        /// 将ValueArrayConstant转换为AnimationClipBindingConstant。
        /// </summary>
        /// <returns>转换后的AnimationClipBindingConstant对象。</returns>
        public AnimationClipBindingConstant ConvertValueArrayToGenericBinding()
        {
            var bindings = new AnimationClipBindingConstant();
            var genericBindings = new List<GenericBinding>();
            var values = m_Binding;

            for (int i = 0; i < values.m_ValueArray.Count;)
            {
                var curveID = values.m_ValueArray[i].m_ID;
                var curveTypeID = values.m_ValueArray[i].m_TypeID;
                var binding = new GenericBinding();
                genericBindings.Add(binding);

                // 根据curveTypeID处理不同的动画属性
                if (curveTypeID == 4174552735) //CRC(PositionX))
                {
                    binding.path = curveID;
                    binding.attribute = 1; //kBindTransformPosition
                    binding.typeID = ClassIDType.Transform;
                    i += 3;
                }
                else if (curveTypeID == 2211994246) //CRC(QuaternionX))
                {
                    binding.path = curveID;
                    binding.attribute = 2; //kBindTransformRotation
                    binding.typeID = ClassIDType.Transform;
                    i += 4;
                }
                else if (curveTypeID == 1512518241) //CRC(ScaleX))
                {
                    binding.path = curveID;
                    binding.attribute = 3; //kBindTransformScale
                    binding.typeID = ClassIDType.Transform;
                    i += 3;
                }
                else
                {
                    binding.typeID = ClassIDType.Animator;
                    binding.path = 0;
                    binding.attribute = curveID;
                    i++;
                }
            }

            bindings.genericBindings = genericBindings;
            return bindings;
        }
    }

    /// <summary>
    /// 表示值的变化范围。
    /// </summary>
    public class ValueDelta
    {
        /// <summary>
        /// 起始值。
        /// </summary>
        public float m_Start;

        /// <summary>
        /// 结束值。
        /// </summary>
        public float m_Stop;

        /// <summary>
        /// 初始化ValueDelta类的新实例。
        /// </summary>
        /// <param name="reader">用于读取起始和结束值的ObjectReader对象。</param>
        public ValueDelta(ObjectReader reader)
        {
            m_Start = reader.ReadSingle();
            m_Stop = reader.ReadSingle();
        }
    }


    /// <summary>
    /// 表示动画片段中的肌肉常量设置，包含各种姿态、变换、速度等信息以及循环和镜像设置。
    /// </summary>
    public class ClipMuscleConstant : IYAMLExportable
    {
        /// <summary>
        /// 表示关键帧之间的姿势变化。
        /// </summary>
        public HumanPose m_DeltaPose;

        /// <summary>
        /// 表示动画开始时的变换。
        /// </summary>
        public XForm m_StartX;

        /// <summary>
        /// 动画结束时的变换。
        /// </summary>
        public XForm m_StopX;

        /// <summary>
        /// 左脚起始位置的变换信息。
        /// </summary>
        public XForm m_LeftFootStartX;

        /// <summary>
        /// 右脚的起始位置变换。
        /// </summary>
        public XForm m_RightFootStartX;

        /// <summary>
        /// 表示动画开始时的运动变换。
        /// </summary>
        public XForm m_MotionStartX;

        /// <summary>
        /// 表示动画片段中运动结束时的变换信息，包括位置、旋转和缩放。
        /// </summary>
        public XForm m_MotionStopX;

        /// <summary>
        /// 表示动画片段中的平均速度。
        /// </summary>
        public Vector3 m_AverageSpeed;

        /// <summary>
        /// 与肌肉常量关联的动画片段。
        /// </summary>
        public Clip m_Clip;

        /// <summary>
        /// 动画片段的开始时间。
        /// </summary>
        public float m_StartTime;

        /// <summary>
        /// 动画停止的时间点。
        /// </summary>
        public float m_StopTime;

        /// <summary>
        /// Y轴方向的偏移角度。
        /// </summary>
        public float m_OrientationOffsetY;

        /// <summary>
        /// 动画片段的层级。
        /// </summary>
        public float m_Level;

        /// <summary>
        /// 动画循环的偏移量。
        /// </summary>
        public float m_CycleOffset;

        /// <summary>
        /// 动画片段的平均角速度。
        /// </summary>
        public float m_AverageAngularSpeed;

        /// <summary>
        /// 用于存储索引的整数数组，这些索引可能与动画关键帧或其他相关数据有关。
        /// </summary>
        public int[] m_IndexArray;

        /// <summary>
        /// 保存了动画中关键帧值的变化数组。每个元素代表一个特定时间点上值的变化情况。
        /// </summary>
        public List<ValueDelta> m_ValueArrayDelta;

        /// <summary>
        /// 表示参考姿态值数组，用于存储动画片段中关键的姿态数据。
        /// </summary>
        public float[] m_ValueArrayReferencePose;

        /// <summary>
        /// 表示是否启用镜像效果。
        /// </summary>
        public bool m_Mirror;

        /// <summary>
        /// 表示动画是否循环播放。
        /// </summary>
        public bool m_LoopTime;

        /// <summary>
        /// 表示动画片段是否启用循环混合。
        /// </summary>
        public bool m_LoopBlend;

        public bool m_LoopBlendOrientation;

        public bool m_LoopBlendPositionY;

        public bool m_LoopBlendPositionXZ;

        public bool m_StartAtOrigin;

        public bool m_KeepOriginalOrientation;

        public bool m_KeepOriginalPositionY;

        public bool m_KeepOriginalPositionXZ;

        public bool m_HeightFromFeet;

        public static bool HasShortIndexArray(SerializedType type) =>
            type.Match("E708B1872AE48FD688AC012DF4A7A178") ||
            type.Match("055AA41C7639327940F8900103A10356") ||
            type.Match("82E1E738FBDE87C5A8DAE868F0578A4D");

        public ClipMuscleConstant()
        {
        }

        public ClipMuscleConstant(ObjectReader reader)
        {
            var version = reader.version;

            // 判断游戏类型是否是特定类型
            if (reader.Game.Type.IsLoveAndDeepspace())
            {
                m_StartX = reader.ReadXForm();
                if (version[0] > 5 || (version[0] == 5 && version[1] >= 5)) // 版本5.5及以上
                {
                    m_StopX = reader.ReadXForm();
                }
            }
            else
            {
                // 读取姿势变化量
                m_DeltaPose = new HumanPose(reader);
                m_StartX = reader.ReadXForm();

                if (version[0] > 5 || (version[0] == 5 && version[1] >= 5)) // 版本5.5及以上
                {
                    m_StopX = reader.ReadXForm();
                }

                // 读取左右脚的起始位置
                m_LeftFootStartX = reader.ReadXForm();
                m_RightFootStartX = reader.ReadXForm();

                if (version[0] < 5) // 版本5.0及以下
                {
                    m_MotionStartX = reader.ReadXForm();
                    m_MotionStopX = reader.ReadXForm();
                }
            }

            // 根据版本读取平均速度
            m_AverageSpeed = version[0] > 5 || (version[0] == 5 && version[1] >= 4) ? reader.ReadVector3() : (Vector3)reader.ReadVector4(); // 版本5.4及以上

            m_Clip = new Clip(reader); // 读取动画片段
            m_StartTime = reader.ReadSingle(); // 读取起始时间
            m_StopTime = reader.ReadSingle(); // 读取结束时间
            m_OrientationOffsetY = reader.ReadSingle(); // 读取Y轴方向的偏移角度
            m_Level = reader.ReadSingle(); // 读取层级
            m_CycleOffset = reader.ReadSingle(); // 读取循环偏移量
            m_AverageAngularSpeed = reader.ReadSingle(); // 读取平均角速度

            // 判断是否需要使用短索引数组
            if (reader.Game.Type.IsSR() && HasShortIndexArray(reader.serializedType))
            {
                m_IndexArray = reader.ReadInt16Array().Select(x => (int)x).ToArray();
            }
            else
            {
                m_IndexArray = reader.ReadInt32Array();
            }

            if (version[0] < 4 || (version[0] == 4 && version[1] < 3)) // 版本4.3及以下
            {
                var m_AdditionalCurveIndexArray = reader.ReadInt32Array();
            }

            // 读取值变化数组的数量
            int numDeltas = reader.ReadInt32();
            m_ValueArrayDelta = new List<ValueDelta>();
            for (int i = 0; i < numDeltas; i++)
            {
                m_ValueArrayDelta.Add(new ValueDelta(reader));
            }

            if (version[0] > 5 || (version[0] == 5 && version[1] >= 3)) // 版本5.3及以上
            {
                m_ValueArrayReferencePose = reader.ReadSingleArray();
            }

            // 读取布尔标志
            m_Mirror = reader.ReadBoolean();
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) // 版本4.3及以上
            {
                m_LoopTime = reader.ReadBoolean();
            }

            m_LoopBlend = reader.ReadBoolean();
            m_LoopBlendOrientation = reader.ReadBoolean();
            m_LoopBlendPositionY = reader.ReadBoolean();
            m_LoopBlendPositionXZ = reader.ReadBoolean();

            if (version[0] > 5 || (version[0] == 5 && version[1] >= 5)) // 版本5.5及以上
            {
                m_StartAtOrigin = reader.ReadBoolean();
            }

            m_KeepOriginalOrientation = reader.ReadBoolean();
            m_KeepOriginalPositionY = reader.ReadBoolean();
            m_KeepOriginalPositionXZ = reader.ReadBoolean();
            m_HeightFromFeet = reader.ReadBoolean();

            reader.AlignStream(); // 对齐流
        }

        public static ClipMuscleConstant ParseGI(ObjectReader reader)
        {
            var version = reader.version;
            var clipMuscleConstant = new ClipMuscleConstant();

            clipMuscleConstant.m_DeltaPose = HumanPose.ParseGI(reader);
            clipMuscleConstant.m_StartX = reader.ReadXForm4();
            clipMuscleConstant.m_StopX = reader.ReadXForm4();
            clipMuscleConstant.m_LeftFootStartX = reader.ReadXForm4();
            clipMuscleConstant.m_RightFootStartX = reader.ReadXForm4();

            clipMuscleConstant.m_AverageSpeed = (Vector3)reader.ReadVector4();

            clipMuscleConstant.m_Clip = Clip.ParseGI(reader);

            clipMuscleConstant.m_StartTime = reader.ReadSingle();
            clipMuscleConstant.m_StopTime = reader.ReadSingle();
            clipMuscleConstant.m_OrientationOffsetY = reader.ReadSingle();
            clipMuscleConstant.m_Level = reader.ReadSingle();
            clipMuscleConstant.m_CycleOffset = reader.ReadSingle();
            clipMuscleConstant.m_AverageAngularSpeed = reader.ReadSingle();

            clipMuscleConstant.m_IndexArray = reader.ReadInt16Array(0xC8).Select(x => (int)x).ToArray();

            var valueArrayDeltaCount = (int)reader.ReadUInt64();
            var valueArrayDeltaOffset = reader.Position + reader.ReadInt64();

            if (valueArrayDeltaOffset > reader.Length)
            {
                throw new IOException("偏移量超出范围");
            }

            var valueArrayReferencePoseCount = (int)reader.ReadUInt64();
            var valueArrayReferencePoseOffset = reader.Position + reader.ReadInt64();

            if (valueArrayReferencePoseOffset > reader.Length)
            {
                throw new IOException("偏移量超出范围");
            }

            clipMuscleConstant.m_Mirror = reader.ReadBoolean();
            clipMuscleConstant.m_LoopTime = reader.ReadBoolean();
            clipMuscleConstant.m_LoopBlend = reader.ReadBoolean();
            clipMuscleConstant.m_LoopBlendOrientation = reader.ReadBoolean();
            clipMuscleConstant.m_LoopBlendPositionY = reader.ReadBoolean();
            clipMuscleConstant.m_LoopBlendPositionXZ = reader.ReadBoolean();
            clipMuscleConstant.m_StartAtOrigin = reader.ReadBoolean();
            clipMuscleConstant.m_KeepOriginalOrientation = reader.ReadBoolean();
            clipMuscleConstant.m_KeepOriginalPositionY = reader.ReadBoolean();
            clipMuscleConstant.m_KeepOriginalPositionXZ = reader.ReadBoolean();
            clipMuscleConstant.m_HeightFromFeet = reader.ReadBoolean();
            reader.AlignStream();

            if (valueArrayDeltaCount > 0)
            {
                reader.Position = valueArrayDeltaOffset;
                clipMuscleConstant.m_ValueArrayDelta = new List<ValueDelta>();
                for (int i = 0; i < valueArrayDeltaCount; i++)
                {
                    clipMuscleConstant.m_ValueArrayDelta.Add(new ValueDelta(reader));
                }
            }

            if (valueArrayReferencePoseCount > 0)
            {
                reader.Position = valueArrayReferencePoseOffset;
                clipMuscleConstant.m_ValueArrayReferencePose = reader.ReadSingleArray(valueArrayReferencePoseCount);
            }

            return clipMuscleConstant;
        }

        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.AddSerializedVersion(ToSerializedVersion(version));
            node.Add(nameof(m_StartTime), m_StartTime);
            node.Add(nameof(m_StopTime), m_StopTime);
            node.Add(nameof(m_OrientationOffsetY), m_OrientationOffsetY);
            node.Add(nameof(m_Level), m_Level);
            node.Add(nameof(m_CycleOffset), m_CycleOffset);
            node.Add(nameof(m_LoopTime), m_LoopTime);
            node.Add(nameof(m_LoopBlend), m_LoopBlend);
            node.Add(nameof(m_LoopBlendOrientation), m_LoopBlendOrientation);
            node.Add(nameof(m_LoopBlendPositionY), m_LoopBlendPositionY);
            node.Add(nameof(m_LoopBlendPositionXZ), m_LoopBlendPositionXZ);
            node.Add(nameof(m_KeepOriginalOrientation), m_KeepOriginalOrientation);
            node.Add(nameof(m_KeepOriginalPositionY), m_KeepOriginalPositionY);
            node.Add(nameof(m_KeepOriginalPositionXZ), m_KeepOriginalPositionXZ);
            node.Add(nameof(m_HeightFromFeet), m_HeightFromFeet);
            node.Add(nameof(m_Mirror), m_Mirror);
            return node;
        }

        private int ToSerializedVersion(int[] version)
        {
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 6))
            {
                return 3;
            }
            else if (version[0] > 4 || (version[0] == 4 && version[1] >= 3))
            {
                return 2;
            }

            return 1;
        }
    }


    /// <summary>
    /// 表示泛型绑定，用于在动画剪辑中存储特定路径和属性的绑定信息。此类实现了IYAMLExportable接口，可以将对象导出为YAML格式。
    /// </summary>
    public class GenericBinding : IYAMLExportable
    {
        /// <summary>
        /// 表示当前文件的版本信息，用于兼容不同版本的数据解析。
        /// </summary>
        public int[] version;

        /// <summary>
        /// 表示动画属性绑定的路径标识符。这个值用于确定动画曲线在动画剪辑中的具体对象和属性。
        /// </summary>
        public uint path;

        /// <summary>
        /// 表示绑定的具体属性，用于区分不同的动画数据类型，如位置、旋转或缩放等。
        /// </summary>
        public uint attribute;

        /// <summary>
        /// 绑定到脚本的引用，用于在动画剪辑中指向特定的MonoScript对象。这允许动画数据与特定的脚本逻辑关联。
        /// </summary>
        public PPtr<Object> script;

        /// <summary>
        /// 表示绑定对象的类ID类型。
        /// </summary>
        public ClassIDType typeID;

        /// <summary>
        /// 表示自定义类型的标识符，用于区分不同的绑定类型。
        /// </summary>
        public byte customType;

        /// <summary>
        /// 表示该曲线是否为PPtrCurve类型。如果值为0x01，则表示该曲线是PPtrCurve。
        /// </summary>
        public byte isPPtrCurve;

        /// <summary>
        /// 表示当前曲线是否为整数类型。此标志用于区分动画数据中的整数和非整数曲线。
        /// </summary>
        public byte isIntCurve;

        /// <summary>
        /// 表示是否序列化引用曲线。此标志用于确定在导出或处理动画数据时，是否包含对特定对象的引用曲线。
        /// </summary>
        public byte isSerializeReferenceCurve;


        public GenericBinding()
        {
        }

        /// <summary>
        /// 表示泛型绑定，用于在动画剪辑中存储特定路径和属性的绑定信息。
        /// </summary>
        public GenericBinding(ObjectReader reader)
        {
            version = reader.version;
            path = reader.ReadUInt32();
            attribute = reader.ReadUInt32();
            script = new PPtr<Object>(reader);
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 6)) //5.6 and up
            {
                typeID = (ClassIDType)reader.ReadInt32();
            }
            else
            {
                typeID = (ClassIDType)reader.ReadUInt16();
            }

            customType = reader.ReadByte();
            isPPtrCurve = reader.ReadByte();
            if (version[0] > 2022 || (version[0] == 2022 && version[1] >= 1)) //2022.1 and up
            {
                isIntCurve = reader.ReadByte();
            }

            reader.AlignStream();
        }

        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(path), TryResolvePathHash());
            node.Add(nameof(attribute), TryResolveAttributeHash());
            node.Add(nameof(script), script.ExportYAML(version));
            node.Add("classID", ((int)typeID).ToString());
            node.Add(nameof(customType), customType);
            node.Add(nameof(isPPtrCurve), isPPtrCurve);
            return node;
        }


        /// <summary>
        /// 尝试解析属性哈希值，并返回对应的字符串表示。
        /// </summary>
        /// <returns>属性哈希值对应的字符串。</returns>
        public string TryResolveAttributeHash()
        {
            return JsonUtils.GetFieldByHash(attribute);
        }

        /// <summary>
        /// 尝试根据哈希值解析并返回路径字符串。
        /// </summary>
        /// <returns>如果找到，则返回与哈希值关联的路径字符串；如果未找到或键为0，则返回空字符串。</returns>
        public string TryResolvePathHash()
        {
            return JsonUtils.GetPathByHash(path);
        }
    }

    /// <summary>
    /// 表示动画剪辑绑定常量，用于存储和管理动画数据的绑定信息，包括通用绑定和指针曲线映射。
    /// </summary>
    public class AnimationClipBindingConstant : IYAMLExportable
    {
        /// <summary>
        /// 用于存储通用绑定的列表，这些绑定定义了动画数据如何映射到目标对象的属性。
        /// </summary>
        public List<GenericBinding> genericBindings;

        /// <summary>
        /// 存储指向Unity资源对象的指针列表，这些指针用于动画剪辑中的特定曲线映射。
        /// </summary>
        public List<PPtr<Object>> pptrCurveMapping;

        public AnimationClipBindingConstant()
        {
        }

        /// <summary>
        /// 表示动画剪辑绑定常量，用于存储动画数据的绑定信息。
        /// </summary>
        public AnimationClipBindingConstant(ObjectReader reader)
        {
            int numBindings = reader.ReadInt32();
            genericBindings = new List<GenericBinding>();
            for (int i = 0; i < numBindings; i++)
            {
                genericBindings.Add(new GenericBinding(reader));
            }

            int numMappings = reader.ReadInt32();
            pptrCurveMapping = new List<PPtr<Object>>();
            for (int i = 0; i < numMappings; i++)
            {
                pptrCurveMapping.Add(new PPtr<Object>(reader));
            }
        }

        /// <summary>
        /// 将AnimationClipBindingConstant对象导出为YAML格式。
        /// </summary>
        /// <param name="version">版本数组，用于指定导出的YAML格式版本。</param>
        /// <returns>返回一个表示该对象的YAML节点。</returns>
        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(genericBindings), genericBindings.ExportYAML(version));
            node.Add(nameof(pptrCurveMapping), pptrCurveMapping.ExportYAML(version));
            return node;
        }

        /// <summary>
        /// 查找并返回指定索引处的GenericBinding对象。
        /// </summary>
        /// <param name="index">要查找的绑定的索引。</param>
        /// <returns>如果找到，返回对应的GenericBinding对象；否则返回null。</returns>
        public GenericBinding FindBinding(int index)
        {
            int curves = 0;
            foreach (var b in genericBindings)
            {
                if (b.typeID == ClassIDType.Transform)
                {
                    switch (b.attribute)
                    {
                        case 1: //kBindTransformPosition
                        case 3: //kBindTransformScale
                        case 4: //kBindTransformEuler
                            curves += 3;
                            break;
                        case 2: //kBindTransformRotation
                            curves += 4;
                            break;
                        default:
                            curves += 1;
                            break;
                    }
                }
                else
                {
                    curves += 1;
                }

                if (curves > index)
                {
                    return b;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 代表动画事件，包含触发时间、函数名、数据等信息。
    /// </summary>
    public class AnimationEvent : IYAMLExportable
    {
        /// <summary>
        /// 动画事件触发的时间点。
        /// </summary>
        public float time;

        /// <summary>
        /// 动画事件调用的函数名。
        /// </summary>
        public string functionName;

        /// <summary>
        /// 与动画事件关联的数据字符串。
        /// </summary>
        public string data;

        /// <summary>
        /// 事件关联的对象引用参数。
        /// </summary>
        public PPtr<Object> objectReferenceParameter;

        /// <summary>
        /// 浮点参数，用于存储与动画事件相关的浮点数值。
        /// </summary>
        public float floatParameter;

        /// <summary>
        /// 整型参数，用于存储与动画事件相关的整数值。
        /// </summary>
        public int intParameter;

        /// <summary>
        /// 动画事件的消息选项，用于指定消息传递时的额外配置。
        /// </summary>
        public int messageOptions;

        /// <summary>
        /// 代表动画事件，包含触发时间、函数名、数据等信息。
        /// </summary>
        public AnimationEvent(ObjectReader reader)
        {
            var version = reader.version;

            time = reader.ReadSingle();
            functionName = reader.ReadAlignedString();
            data = reader.ReadAlignedString();
            objectReferenceParameter = new PPtr<Object>(reader);
            floatParameter = reader.ReadSingle();
            if (version[0] >= 3) //3 and up
            {
                intParameter = reader.ReadInt32();
            }

            messageOptions = reader.ReadInt32();
        }

        /// <summary>
        /// 将AnimationEvent对象导出为YAML格式。
        /// </summary>
        /// <param name="version">Unity版本信息，用于确定导出格式。</param>
        /// <returns>包含AnimationEvent数据的YAMLNode实例。</returns>
        public YAMLNode ExportYAML(int[] version)
        {
            var node = new YAMLMappingNode();
            node.Add(nameof(time), time);
            node.Add(nameof(functionName), functionName);
            node.Add(nameof(data), data);
            node.Add(nameof(objectReferenceParameter), objectReferenceParameter.ExportYAML(version));
            node.Add(nameof(floatParameter), floatParameter);
            node.Add(nameof(intParameter), intParameter);
            node.Add(nameof(messageOptions), messageOptions);
            return node;
        }
    }

    ///  <summary> 动画类型 </summary>
    public enum AnimationType
    {
        /// <summary>
        /// 表示动画是传统类型。这种类型的动画通常用于非人形角色或物体，且不包含骨骼绑定信息。
        /// </summary>
        Legacy = 1,

        /// <summary>
        /// 表示动画是通用类型。这种类型的动画适用于多种角色或物体，具有较高的灵活性和广泛的适用性。
        /// </summary>
        Generic = 2,

        /// <summary>
        /// 表示动画是为人形角色设计的类型。这种类型的动画通常包含了骨骼绑定信息，适用于具有复杂动作和姿态的人形角色。
        /// </summary>
        Humanoid = 3
    };

    /// <summary>
    /// 表示一个动画片段，包含动画数据和相关属性。
    /// </summary>
    public sealed class AnimationClip : NamedObject
    {
        /// <summary>
        /// 动画片段的类型，指示动画是传统类型、通用类型还是人形角色类型。
        /// </summary>
        public AnimationType m_AnimationType;

        /// <summary>
        /// 表示该动画剪辑是否为旧版（Legacy）格式。
        /// </summary>
        public bool m_Legacy;

        /// <summary>
        /// 表示动画剪辑是否被压缩。
        /// </summary>
        public bool m_Compressed;

        /// <summary>
        /// 指示是否使用高质量曲线来提高动画的平滑度和精度。
        /// </summary>
        public bool m_UseHighQualityCurve;

        /// <summary>
        /// 代表旋转曲线的列表，用于存储动画中的旋转数据。
        /// </summary>
        public List<QuaternionCurve> m_RotationCurves;

        /// <summary>
        /// 存储压缩后的旋转动画曲线列表。
        /// </summary>
        public List<CompressedAnimationCurve> m_CompressedRotationCurves;

        /// <summary>
        /// 存储动画剪辑中的欧拉曲线列表，用于表示旋转数据。
        /// </summary>
        public List<Vector3Curve> m_EulerCurves;

        /// <summary>
        /// 位置曲线列表，用于存储和控制动画中对象的位置变化。
        /// </summary>
        public List<Vector3Curve> m_PositionCurves;

        /// <summary>
        /// 表示动画片段中的缩放曲线集合，用于存储和操作对象在动画过程中的缩放变化。
        /// </summary>
        public List<Vector3Curve> m_ScaleCurves;

        /// <summary>
        /// 存储浮点曲线的列表，这些曲线用于定义动画中随时间变化的浮点属性。
        /// </summary>
        public List<FloatCurve> m_FloatCurves;

        /// <summary>
        /// 存储指向其他对象的曲线列表，这些曲线用于动画中的引用。
        /// </summary>
        public List<PPtrCurve> m_PPtrCurves;

        /// <summary>
        /// 采样率，表示动画每秒的帧数。
        /// </summary>
        public float m_SampleRate;

        /// <summary>
        /// 定义动画剪辑在播放到末尾或开头时的行为。
        /// </summary>
        public int m_WrapMode;

        /// <summary>
        /// 动画剪辑的边界框，用于定义动画中对象的最大范围。
        /// </summary>
        public AABB m_Bounds;

        /// <summary>
        /// 表示肌肉动画片段的大小，用于确定肌肉动画数据占用的空间。
        /// </summary>
        public uint m_MuscleClipSize;

        /// <summary>
        /// 与肌肉相关的动画剪辑数据，用于存储和管理角色的肌肉动画信息。
        /// </summary>
        public ClipMuscleConstant m_MuscleClip;

        /// <summary>
        /// 与动画剪辑关联的绑定常量，用于存储关于如何将动画数据映射到目标对象的信息。
        /// </summary>
        public AnimationClipBindingConstant m_ClipBindingConstant;

        /// <summary>
        /// 动画事件列表，包含在动画播放过程中触发的事件。
        /// </summary>
        public List<AnimationEvent> m_Events;

        /// <summary>
        /// 动画数据的流信息，包含偏移量、大小和路径等信息。
        /// </summary>
        public StreamingInfo m_StreamData;

        /// <summary>
        /// 表示动画剪辑是否包含流式信息。
        /// </summary>
        private bool hasStreamingInfo = false;

        /// <summary>
        /// 表示动画剪辑的类，继承自NamedObject。
        /// </summary>
        /// <param name="reader">用于读取动画剪辑数据的对象读取器。</param>
        public AnimationClip(ObjectReader reader) : base(reader)
        {
            if (reader.Game.Type.IsShiningNikki())
            {
                reader.RelativePosition += 27;
            }

            if (version[0] >= 5) //5.0 and up
            {
                m_Legacy = reader.ReadBoolean();
            }
            else if (version[0] >= 4) //4.0 and up
            {
                m_AnimationType = (AnimationType)reader.ReadInt32();
                if (m_AnimationType == AnimationType.Legacy)
                    m_Legacy = true;
            }
            else
            {
                m_Legacy = true;
            }

            if (reader.Game.Type.IsLoveAndDeepspace())
            {
                reader.AlignStream();
                var m_aclTransformCache = reader.ReadUInt8Array();
                var m_aclScalarCache = reader.ReadUInt8Array();
                int numaclTransformTrackId2CurveId = reader.ReadInt32();
                var m_aclTransformTrackId2CurveId = new List<AclTransformTrackIDToBindingCurveID>();
                for (int i = 0; i < numaclTransformTrackId2CurveId; i++)
                {
                    m_aclTransformTrackId2CurveId.Add(new AclTransformTrackIDToBindingCurveID(reader));
                }

                var m_aclScalarTrackId2CurveId = reader.ReadUInt32Array();
            }

            m_Compressed = reader.ReadBoolean();
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                m_UseHighQualityCurve = reader.ReadBoolean();
            }

            reader.AlignStream();
            int numRCurves = reader.ReadInt32();
            m_RotationCurves = new List<QuaternionCurve>();
            for (int i = 0; i < numRCurves; i++)
            {
                m_RotationCurves.Add(new QuaternionCurve(reader));
            }

            int numCRCurves = reader.ReadInt32();
            m_CompressedRotationCurves = new List<CompressedAnimationCurve>();
            for (int i = 0; i < numCRCurves; i++)
            {
                m_CompressedRotationCurves.Add(new CompressedAnimationCurve(reader));
            }

            if (reader.Game.Type.IsExAstris())
            {
                var m_aclType = reader.ReadInt32();
            }

            if (reader.IsTuanJie)
            {
                m_EulerCurves = new List<Vector3Curve>();
                m_PositionCurves = new List<Vector3Curve>();
                m_ScaleCurves = new List<Vector3Curve>();
            }
            else
            {
                if (version[0] > 5 || (version[0] == 5 && version[1] >= 3)) //5.3 and up
                {
                    int numEulerCurves = reader.ReadInt32();
                    m_EulerCurves = new List<Vector3Curve>();
                    for (int i = 0; i < numEulerCurves; i++)
                    {
                        m_EulerCurves.Add(new Vector3Curve(reader));
                    }
                }

                int numPCurves = reader.ReadInt32();
                m_PositionCurves = new List<Vector3Curve>();
                for (int i = 0; i < numPCurves; i++)
                {
                    m_PositionCurves.Add(new Vector3Curve(reader));
                }

                int numSCurves = reader.ReadInt32();
                m_ScaleCurves = new List<Vector3Curve>();
                for (int i = 0; i < numSCurves; i++)
                {
                    m_ScaleCurves.Add(new Vector3Curve(reader));
                }
            }

            int numFCurves = reader.ReadInt32();
            m_FloatCurves = new List<FloatCurve>();
            for (int i = 0; i < numFCurves; i++)
            {
                m_FloatCurves.Add(new FloatCurve(reader));
            }

            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                int numPtrCurves = reader.ReadInt32();
                m_PPtrCurves = new List<PPtrCurve>();
                for (int i = 0; i < numPtrCurves; i++)
                {
                    m_PPtrCurves.Add(new PPtrCurve(reader));
                }
            }

            m_SampleRate = reader.ReadSingle();
            m_WrapMode = reader.ReadInt32();
            if (reader.Game.Type.IsArknightsEndfield())
            {
                var m_aclType = reader.ReadInt32();
            }

            if (version[0] > 3 || (version[0] == 3 && version[1] >= 4)) //3.4 and up
            {
                m_Bounds = new AABB(reader);
            }

            if (version[0] >= 4) //4.0 and up
            {
                if (reader.Game.Type.IsGI())
                {
                    var muscleClipSize = reader.ReadInt32();
                    if (muscleClipSize < 0)
                    {
                        hasStreamingInfo = true;
                        m_MuscleClipSize = reader.ReadUInt32();
                        var pos = reader.Position;
                        m_MuscleClip = ClipMuscleConstant.ParseGI(reader);
                        reader.Position = pos + m_MuscleClipSize;
                    }
                    else if (muscleClipSize > 0)
                    {
                        m_MuscleClipSize = (uint)muscleClipSize;
                        m_MuscleClip = new ClipMuscleConstant(reader);
                    }
                }
                else
                {
                    if (reader.IsTuanJie)
                    {
                        m_MuscleClipSize = reader.ReadUInt32();
                        if (m_MuscleClipSize > 0)
                        {
                            reader.ReadUInt32(); // not needed
                            m_MuscleClip = new ClipMuscleConstant(reader);
                            m_StreamData = new StreamingInfo(reader);
                        }
                    }
                    else
                    {
                        m_MuscleClipSize = reader.ReadUInt32();
                        m_MuscleClip = new ClipMuscleConstant(reader);
                    }
                }
            }

            if (reader.Game.Type.IsSRGroup())
            {
                var m_AclClipData = reader.ReadUInt8Array();
                var aclBindingsCount = reader.ReadInt32();
                var m_AclBindings = new List<GenericBinding>();
                for (int i = 0; i < aclBindingsCount; i++)
                {
                    m_AclBindings.Add(new GenericBinding(reader));
                }

                var m_AclRange = new KeyValuePair<float, float>(reader.ReadSingle(), reader.ReadSingle());
            }

            if (version[0] > 4 || (version[0] == 4 && version[1] >= 3)) //4.3 and up
            {
                m_ClipBindingConstant = new AnimationClipBindingConstant(reader);
            }

            if (version[0] > 2018 || (version[0] == 2018 && version[1] >= 3)) //2018.3 and up
            {
                var m_HasGenericRootTransform = reader.ReadBoolean();
                var m_HasMotionFloatCurves = reader.ReadBoolean();
                reader.AlignStream();
            }

            int numEvents = reader.ReadInt32();
            m_Events = new List<AnimationEvent>();
            for (int i = 0; i < numEvents; i++)
            {
                m_Events.Add(new AnimationEvent(reader));
            }

            if (version[0] >= 2017) //2017 and up
            {
                reader.AlignStream();
            }

            if (hasStreamingInfo)
            {
                m_StreamData = new StreamingInfo(reader);
                if (!string.IsNullOrEmpty(m_StreamData?.path))
                {
                    var aclClip = m_MuscleClip.m_Clip.m_ACLClip as GIACLClip;

                    var resourceReader = new ResourceReader(m_StreamData.path, assetsFile, m_StreamData.offset, m_StreamData.size);
                    using var ms = new MemoryStream();
                    ms.Write(aclClip.m_DatabaseData);

                    ms.Write(resourceReader.GetData());
                    ms.AlignStream();

                    aclClip.m_DatabaseData = ms.ToArray();
                }
            }
        }
    }
}