using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AssetStudio
{
    public class Hash128
    {
        public byte[] bytes;

        public Hash128(EndianBinaryReader reader)
        {
            bytes = reader.ReadBytes(16);
        }
    }

    public class StructParameter
    {
        public List<MatrixParameter> m_MatrixParams;
        public List<VectorParameter> m_VectorParams;

        public StructParameter(EndianBinaryReader reader)
        {
            var m_NameIndex = reader.ReadInt32();
            var m_Index = reader.ReadInt32();
            var m_ArraySize = reader.ReadInt32();
            var m_StructSize = reader.ReadInt32();

            int numVectorParams = reader.ReadInt32();
            m_VectorParams = new List<VectorParameter>();
            for (int i = 0; i < numVectorParams; i++)
            {
                m_VectorParams.Add(new VectorParameter(reader));
            }

            int numMatrixParams = reader.ReadInt32();
            m_MatrixParams = new List<MatrixParameter>();
            for (int i = 0; i < numMatrixParams; i++)
            {
                m_MatrixParams.Add(new MatrixParameter(reader));
            }
        }
    }

    public class SamplerParameter
    {
        public uint sampler;
        public int bindPoint;

        public SamplerParameter(EndianBinaryReader reader)
        {
            sampler = reader.ReadUInt32();
            bindPoint = reader.ReadInt32();
        }
    }
    public enum TextureDimension
    {
        Unknown = -1,
        None = 0,
        Any = 1,
        Tex2D = 2,
        Tex3D = 3,
        Cube = 4,
        Tex2DArray = 5,
        CubeArray = 6
    };

    public class SerializedTextureProperty
    {
        public string m_DefaultName;
        public TextureDimension m_TexDim;

        public SerializedTextureProperty(EndianBinaryReader reader)
        {
            m_DefaultName = reader.ReadAlignedString();
            m_TexDim = (TextureDimension)reader.ReadInt32();
        }
    }

    public enum SerializedPropertyType
    {
        Color = 0,
        Vector = 1,
        Float = 2,
        Range = 3,
        Texture = 4,
        Int = 5
    };

    [Flags]
    public enum SerializedPropertyFlag
    {
        HideInInspector = 1 << 0,
        PerRendererData = 1 << 1,
        NoScaleOffset = 1 << 2,
        Normal = 1 << 3,
        HDR = 1 << 4,
        Gamma = 1 << 5,
        NonModifiableTextureData = 1 << 6,
        MainTexture = 1 << 7,
        MainColor = 1 << 8,
    }

    /// <summary>
    /// 该类用于表示序列化的单个属性。
    /// 它存储了Shader中某个属性的名称、描述、类型、默认值等信息，并通过从输入流读取数据来初始化这些信息。
    /// </summary>
    public class SerializedProperty
    {
        /// <summary>
        /// 该字符串表示序列化属性的名称。它是识别Shader中特定属性的关键标识符。
        /// 属性名称用于在Shader代码和编辑器中引用此属性，确保了属性的唯一性和可访问性。
        /// </summary>
        public string m_Name;

        /// <summary>
        /// 该字符串提供了序列化属性的描述信息。它用于在Shader编辑器或相关工具中显示关于属性的详细说明，
        /// 帮助用户理解属性的功能、预期值及其对Shader效果的影响。
        /// </summary>
        public string m_Description;

        /// <summary>
        /// 该字符串数组存储了序列化属性的附加属性。这些属性可以包含关于如何在Shader中处理特定属性的额外信息或修饰符。
        /// 通过m_Attributes，可以定义如颜色空间、默认值类型等特性，增强了属性的功能性和灵活性。
        /// </summary>
        public string[] m_Attributes;

        /// <summary>
        /// 该枚举值表示序列化属性的数据类型。它定义了Shader中特定属性可以存储的数据种类。
        /// 属性类型对于确定如何在Shader代码和编辑器中处理数据至关重要，确保了数据的正确解析与使用。
        /// </summary>
        public SerializedPropertyType m_Type;

        /// <summary>
        /// 该枚举值集合表示序列化属性的标志。它用于指定属性的各种行为和特性。
        /// 通过设置不同的标志，可以控制属性在编辑器中的显示方式、数据处理方式等。
        /// </summary>
        public SerializedPropertyFlag m_Flags;

        /// <summary>
        /// 该浮点数组表示序列化属性的默认值。它用于在Shader中为特定属性设置初始值。
        /// 根据属性类型的不同，m_DefValue可能包含颜色、向量或范围等信息的一个或多个分量。
        /// 对于某些类型的属性，如Range，数组中的元素还定义了范围的最小值和最大值。
        /// </summary>
        public float[] m_DefValue;

        /// <summary>
        /// 该属性表示序列化属性的默认纹理。它用于在Shader中存储和引用默认的纹理资源。
        /// 默认纹理通常作为属性的初始值，当没有为该属性指定特定纹理时使用。
        /// </summary>
        public SerializedTextureProperty m_DefTexture;

        public SerializedProperty(EndianBinaryReader reader)
        {
            m_Name = reader.ReadAlignedString();
            m_Description = reader.ReadAlignedString();
            m_Attributes = reader.ReadStringArray();
            m_Type = (SerializedPropertyType)reader.ReadInt32();
            m_Flags = (SerializedPropertyFlag)reader.ReadUInt32();
            m_DefValue = reader.ReadSingleArray(4);
            m_DefTexture = new SerializedTextureProperty(reader);
        }
    }

    /// <summary>
    /// 该类用于表示序列化的属性集合。
    /// 它主要用于存储和管理Shader中的多个属性，通过从输入流读取数据来初始化这些属性。
    /// </summary>
    public class SerializedProperties
    {
        /// <summary>
        /// 该列表存储了Shader的所有序列化属性。每个元素都是一个SerializedProperty对象，代表Shader中的一个特定属性。
        /// 这些属性包含了名称、描述、类型、默认值等信息，对于解析和使用Shader至关重要。
        /// </summary>
        public List<SerializedProperty> m_Props;

        public SerializedProperties(EndianBinaryReader reader)
        {
            int numProps = reader.ReadInt32();
            m_Props = new List<SerializedProperty>();
            for (int i = 0; i < numProps; i++)
            {
                m_Props.Add(new SerializedProperty(reader));
            }
        }
    }

    public class SerializedShaderFloatValue
    {
        public float val;
        public string name;

        public SerializedShaderFloatValue(EndianBinaryReader reader)
        {
            val = reader.ReadSingle();
            name = reader.ReadAlignedString();
        }
    }

    public class SerializedShaderRTBlendState
    {
        public SerializedShaderFloatValue srcBlend;
        public SerializedShaderFloatValue destBlend;
        public SerializedShaderFloatValue srcBlendAlpha;
        public SerializedShaderFloatValue destBlendAlpha;
        public SerializedShaderFloatValue blendOp;
        public SerializedShaderFloatValue blendOpAlpha;
        public SerializedShaderFloatValue colMask;

        public SerializedShaderRTBlendState(EndianBinaryReader reader)
        {
            srcBlend = new SerializedShaderFloatValue(reader);
            destBlend = new SerializedShaderFloatValue(reader);
            srcBlendAlpha = new SerializedShaderFloatValue(reader);
            destBlendAlpha = new SerializedShaderFloatValue(reader);
            blendOp = new SerializedShaderFloatValue(reader);
            blendOpAlpha = new SerializedShaderFloatValue(reader);
            colMask = new SerializedShaderFloatValue(reader);
        }
    }

    public class SerializedStencilOp
    {
        public SerializedShaderFloatValue pass;
        public SerializedShaderFloatValue fail;
        public SerializedShaderFloatValue zFail;
        public SerializedShaderFloatValue comp;

        public SerializedStencilOp(EndianBinaryReader reader)
        {
            pass = new SerializedShaderFloatValue(reader);
            fail = new SerializedShaderFloatValue(reader);
            zFail = new SerializedShaderFloatValue(reader);
            comp = new SerializedShaderFloatValue(reader);
        }
    }

    public class SerializedShaderVectorValue
    {
        public SerializedShaderFloatValue x;
        public SerializedShaderFloatValue y;
        public SerializedShaderFloatValue z;
        public SerializedShaderFloatValue w;
        public string name;

        public SerializedShaderVectorValue(EndianBinaryReader reader)
        {
            x = new SerializedShaderFloatValue(reader);
            y = new SerializedShaderFloatValue(reader);
            z = new SerializedShaderFloatValue(reader);
            w = new SerializedShaderFloatValue(reader);
            name = reader.ReadAlignedString();
        }
    }

    public enum FogMode
    {
        Unknown = -1,
        Disabled = 0,
        Linear = 1,
        Exp = 2,
        Exp2 = 3
    };

    public class SerializedShaderState
    {
        public string m_Name;
        public List<SerializedShaderRTBlendState> rtBlend;
        public bool rtSeparateBlend;
        public SerializedShaderFloatValue zClip;
        public SerializedShaderFloatValue zTest;
        public SerializedShaderFloatValue zWrite;
        public SerializedShaderFloatValue culling;
        public SerializedShaderFloatValue conservative;
        public SerializedShaderFloatValue offsetFactor;
        public SerializedShaderFloatValue offsetUnits;
        public SerializedShaderFloatValue alphaToMask;
        public SerializedStencilOp stencilOp;
        public SerializedStencilOp stencilOpFront;
        public SerializedStencilOp stencilOpBack;
        public SerializedShaderFloatValue stencilReadMask;
        public SerializedShaderFloatValue stencilWriteMask;
        public SerializedShaderFloatValue stencilRef;
        public SerializedShaderFloatValue fogStart;
        public SerializedShaderFloatValue fogEnd;
        public SerializedShaderFloatValue fogDensity;
        public SerializedShaderVectorValue fogColor;
        public FogMode fogMode;
        public int gpuProgramID;
        public SerializedTagMap m_Tags;
        public int m_LOD;
        public bool lighting;

        public SerializedShaderState(ObjectReader reader)
        {
            var version = reader.version;

            m_Name = reader.ReadAlignedString();
            rtBlend = new List<SerializedShaderRTBlendState>();
            for (int i = 0; i < 8; i++)
            {
                rtBlend.Add(new SerializedShaderRTBlendState(reader));
            }
            rtSeparateBlend = reader.ReadBoolean();
            reader.AlignStream();
            if (version[0] > 2017 || (version[0] == 2017 && version[1] >= 2)) //2017.2 and up
            {
                zClip = new SerializedShaderFloatValue(reader);
            }
            zTest = new SerializedShaderFloatValue(reader);
            zWrite = new SerializedShaderFloatValue(reader);
            culling = new SerializedShaderFloatValue(reader);
            if (version[0] >= 2020) //2020.1 and up
            {
                conservative = new SerializedShaderFloatValue(reader);
            }
            offsetFactor = new SerializedShaderFloatValue(reader);
            offsetUnits = new SerializedShaderFloatValue(reader);
            alphaToMask = new SerializedShaderFloatValue(reader);
            stencilOp = new SerializedStencilOp(reader);
            stencilOpFront = new SerializedStencilOp(reader);
            stencilOpBack = new SerializedStencilOp(reader);
            stencilReadMask = new SerializedShaderFloatValue(reader);
            stencilWriteMask = new SerializedShaderFloatValue(reader);
            stencilRef = new SerializedShaderFloatValue(reader);
            fogStart = new SerializedShaderFloatValue(reader);
            fogEnd = new SerializedShaderFloatValue(reader);
            fogDensity = new SerializedShaderFloatValue(reader);
            fogColor = new SerializedShaderVectorValue(reader);
            fogMode = (FogMode)reader.ReadInt32();
            gpuProgramID = reader.ReadInt32();
            m_Tags = new SerializedTagMap(reader);
            m_LOD = reader.ReadInt32();
            if (reader.Game.Type.IsLoveAndDeepspace())
            {
                int numOverrideKeywordAndStage = reader.ReadInt32();
                var m_OverrideKeywordAndStage = new List<KeyValuePair<string, uint>>();
                for (int i = 0; i < numOverrideKeywordAndStage; i++)
                {
                    m_OverrideKeywordAndStage.Add(new KeyValuePair<string, uint>(reader.ReadAlignedString(), reader.ReadUInt32()));
                }
            }
            lighting = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public class ShaderBindChannel
    {
        public sbyte source;
        public sbyte target;

        public ShaderBindChannel(EndianBinaryReader reader)
        {
            source = reader.ReadSByte();
            target = reader.ReadSByte();
        }
    }

    public class ParserBindChannels
    {
        public List<ShaderBindChannel> m_Channels;
        public uint m_SourceMap;

        public ParserBindChannels(EndianBinaryReader reader)
        {
            int numChannels = reader.ReadInt32();
            m_Channels = new List<ShaderBindChannel>();
            for (int i = 0; i < numChannels; i++)
            {
                m_Channels.Add(new ShaderBindChannel(reader));
            }
            reader.AlignStream();

            m_SourceMap = reader.ReadUInt32();
        }
    }

    public class VectorParameter
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_ArraySize;
        public int m_IndexInCB;
        public sbyte m_Type;
        public sbyte m_Dim;

        public VectorParameter(EndianBinaryReader reader)
        {
            m_NameIndex = reader.ReadInt32();
            m_Index = reader.ReadInt32();
            m_ArraySize = reader.ReadInt32();
            var r = reader as ObjectReader;
            if (null != r && r.IsTuanJie)
                m_IndexInCB = reader.ReadInt32();
            m_Type = reader.ReadSByte();
            m_Dim = reader.ReadSByte();
            reader.AlignStream();
        }
    }

    public class MatrixParameter
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_ArraySize;
        public int m_IndexInCB;
        public sbyte m_Type;
        public sbyte m_RowCount;

        public MatrixParameter(EndianBinaryReader reader)
        {
            m_NameIndex = reader.ReadInt32();
            m_Index = reader.ReadInt32();
            m_ArraySize = reader.ReadInt32();
            var r = reader as ObjectReader;
            if (null != r && r.IsTuanJie)
                m_IndexInCB = reader.ReadInt32(); 
            m_Type = reader.ReadSByte();
            m_RowCount = reader.ReadSByte();
            reader.AlignStream();
        }
    }

    public class TextureParameter
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_SamplerIndex;
        public sbyte m_Dim;

        public TextureParameter(ObjectReader reader)
        {
            var version = reader.version;

            m_NameIndex = reader.ReadInt32();
            m_Index = reader.ReadInt32();
            m_SamplerIndex = reader.ReadInt32();
            if (version[0] > 2017 || (version[0] == 2017 && version[1] >= 3)) //2017.3 and up
            {
                var m_MultiSampled = reader.ReadBoolean();
            }
            m_Dim = reader.ReadSByte();
            reader.AlignStream();
        }
    }

    public class BufferBinding
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_ArraySize;

        public BufferBinding(ObjectReader reader)
        {
            var version = reader.version;

            m_NameIndex = reader.ReadInt32();
            m_Index = reader.ReadInt32();
            if (version[0] >= 2020) //2020.1 and up
            {
                m_ArraySize = reader.ReadInt32();
            }
        }
    }

    public class ConstantBuffer
    {
        public int m_NameIndex;
        public List<MatrixParameter> m_MatrixParams;
        public List<VectorParameter> m_VectorParams;
        public List<StructParameter> m_StructParams;
        public int m_Size;
        public bool m_IsPartialCB;
        public int m_totalParameterCount;

        public ConstantBuffer(ObjectReader reader)
        {
            var version = reader.version;

            m_NameIndex = reader.ReadInt32();

            int numMatrixParams = reader.ReadInt32();
            m_MatrixParams = new List<MatrixParameter>();
            for (int i = 0; i < numMatrixParams; i++)
            {
                m_MatrixParams.Add(new MatrixParameter(reader));
            }

            int numVectorParams = reader.ReadInt32();
            m_VectorParams = new List<VectorParameter>();
            for (int i = 0; i < numVectorParams; i++)
            {
                m_VectorParams.Add(new VectorParameter(reader));
            }
            if (version[0] > 2017 || (version[0] == 2017 && version[1] >= 3)) //2017.3 and up
            {
                int numStructParams = reader.ReadInt32();
                m_StructParams = new List<StructParameter>();
                for (int i = 0; i < numStructParams; i++)
                {
                    m_StructParams.Add(new StructParameter(reader));
                }
            }
            m_Size = reader.ReadInt32();

            if ((version[0] == 2020 && version[1] > 3) ||
               (version[0] == 2020 && version[1] == 3 && version[2] >= 2) || //2020.3.2f1 and up
               (version[0] > 2021) ||
               (version[0] == 2021 && version[1] > 1) ||
               (version[0] == 2021 && version[1] == 1 && version[2] >= 4)) //2021.1.4f1 and up
            {
                if (reader.IsTuanJie)
                    m_totalParameterCount = reader.ReadInt32();
                m_IsPartialCB = reader.ReadBoolean();
                reader.AlignStream();
            }
        }
    }

    public class UAVParameter
    {
        public int m_NameIndex;
        public int m_Index;
        public int m_OriginalIndex;

        public UAVParameter(EndianBinaryReader reader)
        {
            m_NameIndex = reader.ReadInt32();
            m_Index = reader.ReadInt32();
            m_OriginalIndex = reader.ReadInt32();
        }
    }

    public enum ShaderGpuProgramType
    {
        Unknown = 0,
        GLLegacy = 1,
        GLES31AEP = 2,
        GLES31 = 3,
        GLES3 = 4,
        GLES = 5,
        GLCore32 = 6,
        GLCore41 = 7,
        GLCore43 = 8,
        DX9VertexSM20 = 9,
        DX9VertexSM30 = 10,
        DX9PixelSM20 = 11,
        DX9PixelSM30 = 12,
        DX10Level9Vertex = 13,
        DX10Level9Pixel = 14,
        DX11VertexSM40 = 15,
        DX11VertexSM50 = 16,
        DX11PixelSM40 = 17,
        DX11PixelSM50 = 18,
        DX11GeometrySM40 = 19,
        DX11GeometrySM50 = 20,
        DX11HullSM50 = 21,
        DX11DomainSM50 = 22,
        MetalVS = 23,
        MetalFS = 24,
        SPIRV = 25,
        ConsoleVS = 26,
        ConsoleFS = 27,
        ConsoleHS = 28,
        ConsoleDS = 29,
        ConsoleGS = 30,
        RayTracing = 31,
        PS5NGGC = 32
    };

    public class SerializedProgramParameters
    {
        public List<VectorParameter> m_VectorParams;
        public List<MatrixParameter> m_MatrixParams;
        public List<TextureParameter> m_TextureParams;
        public List<BufferBinding> m_BufferParams;
        public List<ConstantBuffer> m_ConstantBuffers;
        public List<BufferBinding> m_ConstantBufferBindings;
        public List<UAVParameter> m_UAVParams;
        public List<SamplerParameter> m_Samplers;

        public SerializedProgramParameters(ObjectReader reader)
        {
            int numVectorParams = reader.ReadInt32();
            m_VectorParams = new List<VectorParameter>();
            for (int i = 0; i < numVectorParams; i++)
            {
                m_VectorParams.Add(new VectorParameter(reader));
            }

            int numMatrixParams = reader.ReadInt32();
            m_MatrixParams = new List<MatrixParameter>();
            for (int i = 0; i < numMatrixParams; i++)
            {
                m_MatrixParams.Add(new MatrixParameter(reader));
            }

            int numTextureParams = reader.ReadInt32();
            m_TextureParams = new List<TextureParameter>();
            for (int i = 0; i < numTextureParams; i++)
            {
                m_TextureParams.Add(new TextureParameter(reader));
            }

            int numBufferParams = reader.ReadInt32();
            m_BufferParams = new List<BufferBinding>();
            for (int i = 0; i < numBufferParams; i++)
            {
                m_BufferParams.Add(new BufferBinding(reader));
            }

            int numConstantBuffers = reader.ReadInt32();
            m_ConstantBuffers = new List<ConstantBuffer>();
            for (int i = 0; i < numConstantBuffers; i++)
            {
                m_ConstantBuffers.Add(new ConstantBuffer(reader));
            }

            int numConstantBufferBindings = reader.ReadInt32();
            m_ConstantBufferBindings = new List<BufferBinding>();
            for (int i = 0; i < numConstantBufferBindings; i++)
            {
                m_ConstantBufferBindings.Add(new BufferBinding(reader));
            }

            int numUAVParams = reader.ReadInt32();
            m_UAVParams = new List<UAVParameter>();
            for (int i = 0; i < numUAVParams; i++)
            {
                m_UAVParams.Add(new UAVParameter(reader));
            }

            int numSamplers = reader.ReadInt32();
            m_Samplers = new List<SamplerParameter>();
            for (int i = 0; i < numSamplers; i++)
            {
                m_Samplers.Add(new SamplerParameter(reader));
            }
        }
    }

    public class SerializedSubProgram
    {
        public uint m_BlobIndex;
        public ParserBindChannels m_Channels;
        public ushort[] m_KeywordIndices;
        public sbyte m_ShaderHardwareTier;
        public ShaderGpuProgramType m_GpuProgramType;
        public SerializedProgramParameters m_Parameters;
        public List<VectorParameter> m_VectorParams;
        public List<MatrixParameter> m_MatrixParams;
        public List<TextureParameter> m_TextureParams;
        public List<BufferBinding> m_BufferParams;
        public List<ConstantBuffer> m_ConstantBuffers;
        public List<BufferBinding> m_ConstantBufferBindings;
        public List<UAVParameter> m_UAVParams;
        public List<SamplerParameter> m_Samplers;

        public static bool HasGlobalLocalKeywordIndices(SerializedType type) => type.Match("E99740711222CD922E9A6F92FF1EB07A", "450A058C218DAF000647948F2F59DA6D", "B239746E4EC6E4D6D7BA27C84178610A", "3FD560648A91A99210D5DDF2BE320536");
        public static bool HasInstancedStructuredBuffers(SerializedType type) => type.Match("E99740711222CD922E9A6F92FF1EB07A", "B239746E4EC6E4D6D7BA27C84178610A", "3FD560648A91A99210D5DDF2BE320536");
        public static bool HasIsAdditionalBlob(SerializedType type) => type.Match("B239746E4EC6E4D6D7BA27C84178610A");

        public SerializedSubProgram(ObjectReader reader)
        {
            var version = reader.version;
            
            if (reader.Game.Type.IsLoveAndDeepspace())
            {
                var m_CodeHash = new Hash128(reader);
            }

            m_BlobIndex = reader.ReadUInt32();
            if (HasIsAdditionalBlob(reader.serializedType))
            {
                var m_IsAdditionalBlob = reader.ReadBoolean();
                reader.AlignStream();
            }
            m_Channels = new ParserBindChannels(reader);

            if ((version[0] >= 2019 && version[0] < 2021) || (version[0] == 2021 && version[1] < 2) || HasGlobalLocalKeywordIndices(reader.serializedType)) //2019 ~2021.1
            {
                var m_GlobalKeywordIndices = reader.ReadUInt16Array();
                reader.AlignStream();
                var m_LocalKeywordIndices = reader.ReadUInt16Array();
                reader.AlignStream();
            }
            else
            {
                m_KeywordIndices = reader.ReadUInt16Array();
                if (version[0] >= 2017) //2017 and up
                {
                    reader.AlignStream();
                }
            }

            m_ShaderHardwareTier = reader.ReadSByte();
            m_GpuProgramType = (ShaderGpuProgramType)reader.ReadSByte();
            reader.AlignStream();

            if (reader.Game.Name == "GI" && (m_GpuProgramType == ShaderGpuProgramType.Unknown || !Enum.IsDefined(typeof(ShaderGpuProgramType), m_GpuProgramType)))
            {
                reader.Position -= 4;
                var m_LocalKeywordIndices = reader.ReadUInt16Array();
                reader.AlignStream();

                m_ShaderHardwareTier = reader.ReadSByte();
                m_GpuProgramType = (ShaderGpuProgramType)reader.ReadSByte();
                reader.AlignStream();
            }

            if ((version[0] == 2020 && version[1] > 3) ||
               (version[0] == 2020 && version[1] == 3 && version[2] >= 2) || //2020.3.2f1 and up
               (version[0] > 2021) ||
               (version[0] == 2021 && version[1] > 1) ||
               (version[0] == 2021 && version[1] == 1 && version[2] >= 1)) //2021.1.1f1 and up
            {
                m_Parameters = new SerializedProgramParameters(reader);
            }
            else
            {
                int numVectorParams = reader.ReadInt32();
                m_VectorParams = new List<VectorParameter>();
                for (int i = 0; i < numVectorParams; i++)
                {
                    m_VectorParams.Add(new VectorParameter(reader));
                }

                int numMatrixParams = reader.ReadInt32();
                m_MatrixParams = new List<MatrixParameter>();
                for (int i = 0; i < numMatrixParams; i++)
                {
                    m_MatrixParams.Add(new MatrixParameter(reader));
                }

                int numTextureParams = reader.ReadInt32();
                m_TextureParams = new List<TextureParameter>();
                for (int i = 0; i < numTextureParams; i++)
                {
                    m_TextureParams.Add(new TextureParameter(reader));
                }

                int numBufferParams = reader.ReadInt32();
                m_BufferParams = new List<BufferBinding>();
                for (int i = 0; i < numBufferParams; i++)
                {
                    m_BufferParams.Add(new BufferBinding(reader));
                }

                int numConstantBuffers = reader.ReadInt32();
                m_ConstantBuffers = new List<ConstantBuffer>();
                for (int i = 0; i < numConstantBuffers; i++)
                {
                    m_ConstantBuffers.Add(new ConstantBuffer(reader));
                }

                int numConstantBufferBindings = reader.ReadInt32();
                m_ConstantBufferBindings = new List<BufferBinding>();
                for (int i = 0; i < numConstantBufferBindings; i++)
                {
                    m_ConstantBufferBindings.Add(new BufferBinding(reader));
                }

                int numUAVParams = reader.ReadInt32();
                m_UAVParams = new List<UAVParameter>();
                for (int i = 0; i < numUAVParams; i++)
                {
                    m_UAVParams.Add(new UAVParameter(reader));
                }

                if (version[0] >= 2017) //2017 and up
                {
                    int numSamplers = reader.ReadInt32();
                    m_Samplers = new List<SamplerParameter>();
                    for (int i = 0; i < numSamplers; i++)
                    {
                        m_Samplers.Add(new SamplerParameter(reader));
                    }
                }
            }

            if (version[0] > 2017 || (version[0] == 2017 && version[1] >= 2)) //2017.2 and up
            {
                if (version[0] >= 2021) //2021.1 and up
                {
                    var m_ShaderRequirements = reader.ReadInt64();
                }
                else
                {
                    var m_ShaderRequirements = reader.ReadInt32();
                }
            }

            if (HasInstancedStructuredBuffers(reader.serializedType))
            {
                int numInstancedStructuredBuffers = reader.ReadInt32();
                var m_InstancedStructuredBuffers = new List<ConstantBuffer>();
                for (int i = 0; i < numInstancedStructuredBuffers; i++)
                {
                    m_InstancedStructuredBuffers.Add(new ConstantBuffer(reader));
                }
            }
        }
    }

    public class SerializedPlayerSubProgram
    {
        public uint m_BlobIndex;
        public ushort[] m_KeywordIndices;
        public long m_ShaderRequirements;
        public ShaderGpuProgramType m_GpuProgramType;

        public SerializedPlayerSubProgram(ObjectReader reader)
        {
            m_BlobIndex = reader.ReadUInt32();

            m_KeywordIndices = reader.ReadUInt16Array();
            reader.AlignStream();

            m_ShaderRequirements = reader.ReadInt64();
            m_GpuProgramType = (ShaderGpuProgramType)reader.ReadSByte();
            reader.AlignStream();
        }
    }

    public class SerializedProgram
    {
        /// <summary>
        /// 该列表包含序列化的子程序集合，每个子程序代表了Shader的一个可执行部分。
        /// 子程序可以是顶点着色器、片段着色器、几何着色器等，具体取决于Shader的类型和配置。
        /// 每个子程序都包含了执行所需的所有参数和资源绑定信息，确保了在不同硬件平台上正确渲染。
        /// </summary>
        public List<SerializedSubProgram> m_SubPrograms;

        /// <summary>
        /// 该列表包含了针对不同播放器（平台）的序列化子程序集合，每个子程序集对应一个特定的硬件或软件配置。
        /// 每个子程序集内部又包含了多个子程序实例，这些实例提供了在特定条件下执行Shader所需的详细信息。
        /// 通过这种方式，m_PlayerSubPrograms支持了跨平台兼容性，确保Shader可以在多种不同的环境中正确运行。
        /// </summary>
        public List<List<SerializedPlayerSubProgram>> m_PlayerSubPrograms;
        public uint[][] m_ParameterBlobIndices;
        public SerializedProgramParameters m_CommonParameters;
        public ushort[] m_SerializedKeywordStateMask;

        public SerializedProgram(ObjectReader reader)
        {
            var version = reader.version;

            int numSubPrograms = reader.ReadInt32();
            m_SubPrograms = new List<SerializedSubProgram>();
            for (int i = 0; i < numSubPrograms; i++)
            {
                m_SubPrograms.Add(new SerializedSubProgram(reader));
            }

            if ((version[0] == 2021 && version[1] > 3) ||
               version[0] == 2021 && version[1] == 3 && version[2] >= 10 || //2021.3.10f1 and up
               (version[0] == 2022 && version[1] > 1) ||
               version[0] == 2022 && version[1] == 1 && version[2] >= 13) //2022.1.13f1 and up
            {
                int numPlayerSubPrograms = reader.ReadInt32();
                m_PlayerSubPrograms = new List<List<SerializedPlayerSubProgram>>();
                for (int i = 0; i < numPlayerSubPrograms; i++)
                {
                    m_PlayerSubPrograms.Add(new List<SerializedPlayerSubProgram>());
                    int numPlatformPrograms = reader.ReadInt32();
                    for (int j = 0; j < numPlatformPrograms; j++)
                    {
                        m_PlayerSubPrograms[i].Add(new SerializedPlayerSubProgram(reader));
                    }
                }

                m_ParameterBlobIndices = reader.ReadUInt32ArrayArray();
            }

            if ((version[0] == 2020 && version[1] > 3) ||
               (version[0] == 2020 && version[1] == 3 && version[2] >= 2) || //2020.3.2f1 and up
               (version[0] > 2021) ||
               (version[0] == 2021 && version[1] > 1) ||
               (version[0] == 2021 && version[1] == 1 && version[2] >= 1)) //2021.1.1f1 and up
            {
                m_CommonParameters = new SerializedProgramParameters(reader);
            }

            if (version[0] > 2022 || (version[0] == 2022 && version[1] >= 1)) //2022.1 and up
            {
                m_SerializedKeywordStateMask = reader.ReadUInt16Array();
                reader.AlignStream();
            }
        }
    }

    public enum PassType
    {
        Normal = 0,
        Use = 1,
        Grab = 2
    };

    /// <summary>
    /// 该类用于表示序列化的Shader Pass。
    /// 它存储了Pass的多种信息，如编辑器数据哈希、平台信息、关键字掩码、名称索引、类型、状态、程序掩码以及各种类型的程序（顶点、片段、几何、外壳、域、光线追踪）等，并通过从输入流读取数据来初始化这些信息。
    /// </summary>
    public class SerializedPass
    {
        /// <summary>
        /// 该列表包含序列化Pass的编辑器数据哈希值。每个哈希值都是一个128位的哈希，用于标识特定的编辑器数据。
        /// 这些哈希值在Shader的序列化过程中生成，并且用于在编辑器中管理和引用与Shader Pass相关的编辑器数据。
        /// </summary>
        public List<Hash128> m_EditorDataHash;

        /// <summary>
        /// 该字节数组表示序列化Shader Pass支持的平台信息。它包含了与特定Pass兼容的不同硬件或软件平台的数据。
        /// 平台信息对于确保Shader在不同设备上正确运行至关重要，通过此数组可以识别和配置Pass所适用的平台范围。
        /// </summary>
        public byte[] m_Platforms;

        /// <summary>
        /// 该数组表示序列化Shader Pass的局部关键字掩码。每个元素都是一个16位无符号整数，用于定义Pass中启用的局部关键字。
        /// 局部关键字是Shader中特定于某个Pass的关键字，通过这些掩码可以控制哪些局部关键字在当前Pass中被激活或禁用，从而影响着色器的行为和输出。
        /// </summary>
        public ushort[] m_LocalKeywordMask;

        /// <summary>
        /// 该字节数组表示序列化Shader Pass的全局关键字掩码。每个元素对应于一个全局关键字的状态，用于确定在编译Shader时是否启用特定的关键字。
        /// 全局关键字对于控制Shader的行为和特性至关重要，通过此数组可以灵活地管理和配置全局关键字的启用状态。
        /// </summary>
        public ushort[] m_GlobalKeywordMask;

        /// <summary>
        /// 该列表存储了名称与索引的键值对，用于在Shader Pass中快速查找和引用特定名称及其对应的索引。
        /// 每个键值对中的键是一个字符串，代表某个属性或资源的名称；值则是一个整数，表示该名称在Shader数据结构中的位置索引。
        /// 这种映射关系有助于提高访问效率，特别是在处理大量Shader属性时。
        /// </summary>
        public List<KeyValuePair<string, int>> m_NameIndices;

        /// <summary>
        /// 该枚举值定义了序列化通过的类型，用于标识Shader中不同类型的Pass。它对于确定如何处理特定的Pass以及在转换或编译过程中应用哪些规则至关重要。
        /// Pass类型包括普通Pass、UsePass和GrabPass，每种类型决定了渲染过程中的具体行为和功能。
        /// </summary>
        public PassType m_Type;

        /// <summary>
        /// 该对象表示序列化后的Shader状态。它包含了渲染过程中使用的各种配置和设置信息。
        /// m_State用于存储Shader的状态数据，这些数据对于定义Shader的行为至关重要，包括但不限于着色器程序、材质属性以及渲染状态等。
        /// </summary>
        public SerializedShaderState m_State;

        /// <summary>
        /// 该无符号整数用于表示程序掩码，它指定了哪些着色器阶段（如顶点、片段、几何等）在当前Pass中被激活。
        /// m_ProgramMask 的每一位对应一个特定的着色器阶段，通过设置相应的位可以控制该阶段是否启用。
        /// </summary>
        public uint m_ProgramMask;

        /// <summary>
        /// 该对象表示顶点着色器程序。它包含了顶点着色器相关的子程序信息，用于在渲染管线中处理顶点数据。
        /// 顶点着色器程序是图形渲染过程中不可或缺的一部分，负责将输入的顶点数据转换为屏幕上的坐标位置，同时可以进行如光照计算等操作。
        /// </summary>
        public SerializedProgram progVertex;

        /// <summary>
        /// 该对象表示片段着色器程序。它包含了片段着色器相关的子程序信息，这些信息用于描述如何在渲染管线中处理像素级别的操作。
        /// progFragment 对象是Shader中不可或缺的部分，负责定义材质的最终颜色、纹理混合等视觉效果。
        /// </summary>
        public SerializedProgram progFragment;
        public SerializedProgram progGeometry;
        public SerializedProgram progHull;
        public SerializedProgram progDomain;
        public SerializedProgram progRayTracing;
        public bool m_HasInstancingVariant;
        public string m_UseName;
        public string m_Name;
        public string m_TextureName;
        public SerializedTagMap m_Tags;
        public ushort[] m_SerializedKeywordStateMask;

        public SerializedPass(ObjectReader reader)
        {
            var version = reader.version;

            if (version[0] > 2020 || (version[0] == 2020 && version[1] >= 2)) //2020.2 and up
            {
                int numEditorDataHash = reader.ReadInt32();
                m_EditorDataHash = new List<Hash128>();
                for (int i = 0; i < numEditorDataHash; i++)
                {
                    m_EditorDataHash.Add(new Hash128(reader));
                }
                reader.AlignStream();
                m_Platforms = reader.ReadUInt8Array();
                reader.AlignStream();
                if (version[0] < 2021 || (version[0] == 2021 && version[1] < 2)) //2021.1 and down
                {
                    m_LocalKeywordMask = reader.ReadUInt16Array();
                    reader.AlignStream();
                    m_GlobalKeywordMask = reader.ReadUInt16Array();
                    reader.AlignStream();
                }
            }

            int numIndices = reader.ReadInt32();
            m_NameIndices = new List<KeyValuePair<string, int>>();
            for (int i = 0; i < numIndices; i++)
            {
                m_NameIndices.Add(new KeyValuePair<string, int>(reader.ReadAlignedString(), reader.ReadInt32()));
            }

            m_Type = (PassType)reader.ReadInt32();
            m_State = new SerializedShaderState(reader);
            m_ProgramMask = reader.ReadUInt32();
            progVertex = new SerializedProgram(reader);
            progFragment = new SerializedProgram(reader);
            progGeometry = new SerializedProgram(reader);
            progHull = new SerializedProgram(reader);
            progDomain = new SerializedProgram(reader);
            if (version[0] > 2019 || (version[0] == 2019 && version[1] >= 3)) //2019.3 and up
            {
                progRayTracing = new SerializedProgram(reader);
            }
            m_HasInstancingVariant = reader.ReadBoolean();
            if (version[0] >= 2018) //2018 and up
            {
                var m_HasProceduralInstancingVariant = reader.ReadBoolean();
            }
            reader.AlignStream();
            m_UseName = reader.ReadAlignedString();
            m_Name = reader.ReadAlignedString();
            m_TextureName = reader.ReadAlignedString();
            m_Tags = new SerializedTagMap(reader);
            if (version[0] == 2021 && version[1] >= 2) //2021.2 ~2021.x
            {
                m_SerializedKeywordStateMask = reader.ReadUInt16Array();
                reader.AlignStream();
            }
        }
    }

    /// <summary>
    /// 该类用于表示序列化的标签映射。
    /// 它存储了一组键值对，每个键值对代表一个标签及其对应的值。
    /// 通过从输入流读取数据来初始化这些标签信息。
    /// </summary>
    public class SerializedTagMap
    {
        public List<KeyValuePair<string, string>> tags;

        public SerializedTagMap(EndianBinaryReader reader)
        {
            int numTags = reader.ReadInt32();
            tags = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < numTags; i++)
            {
                tags.Add(new KeyValuePair<string, string>(reader.ReadAlignedString(), reader.ReadAlignedString()));
            }
        }
    }

    /// <summary>
    /// 该类用于表示序列化的子着色器。
    /// 它存储了子着色器的多个通道（Pass）、标签映射（Tags）以及LOD级别等信息，并通过从输入流读取数据来初始化这些信息。
    /// </summary>
    public class SerializedSubShader
    {
        /// <summary>
        /// 该列表包含了序列化子着色器中的所有通道（Pass）。每个元素都是一个SerializedPass对象，代表了Shader中的一个单独的渲染通道。
        /// 这些通道定义了如何以及何时在渲染管线中执行特定的绘制操作，包括顶点、片段等不同类型的着色程序及其相关状态设置。
        /// </summary>
        public List<SerializedPass> m_Passes;

        /// <summary>
        /// 该字段表示序列化子着色器的标签映射。它是一个SerializedTagMap类型的对象，用于存储Shader中的各种标签信息。
        /// 这些标签定义了Shader的行为和属性，例如RenderType、Queue等，它们在Unity中控制着Shader的渲染顺序及其它特性。
        /// </summary>
        public SerializedTagMap m_Tags;
        public int m_LOD;

        public SerializedSubShader(ObjectReader reader)
        {
            int numPasses = reader.ReadInt32();
            m_Passes = new List<SerializedPass>();
            for (int i = 0; i < numPasses; i++)
            {
                m_Passes.Add(new SerializedPass(reader));
            }

            m_Tags = new SerializedTagMap(reader);
            m_LOD = reader.ReadInt32();
        }
    }

    public class SerializedShaderDependency
    {
        public string from;
        public string to;

        public SerializedShaderDependency(EndianBinaryReader reader)
        {
            from = reader.ReadAlignedString();
            to = reader.ReadAlignedString();
        }
    }

    public class SerializedCustomEditorForRenderPipeline
    {
        public string customEditorName;
        public string renderPipelineType;

        public SerializedCustomEditorForRenderPipeline(EndianBinaryReader reader)
        {
            customEditorName = reader.ReadAlignedString();
            renderPipelineType = reader.ReadAlignedString();
        }
    }

    /// <summary>
    /// 该类用于表示序列化的Shader对象。
    /// 它包含了Shader的属性信息、子Shader列表、关键字名称数组、关键字标志字节数组、Shader名称、
    /// 自定义编辑器名称、回退名称、依赖项列表、渲染管线的自定义编辑器列表以及一个布尔值来指示是否禁用无子Shader的消息。
    /// </summary>
    public class SerializedShader
    {
        /// <summary>
        /// 该变量存储了Shader的属性信息。它是一个SerializedProperties类型的对象，包含了关于Shader的各种属性数据。
        /// 这些属性数据对于理解和解析Shader的具体配置至关重要，例如其参数、材质属性等。
        /// </summary>
        public SerializedProperties m_PropInfo;

        /// <summary>
        /// 该变量存储了Shader的子Shader列表。它是一个包含SerializedSubShader对象的List，每个元素代表一个子Shader。
        /// 子Shader是Shader的一部分，用于在不同硬件或渲染条件下提供替代的渲染路径。
        /// 每个子Shader可以定义自己的Passes、Tags以及LOD等属性，从而允许Shader根据当前的渲染环境选择最合适的子Shader进行渲染。
        /// </summary>
        public List<SerializedSubShader> m_SubShaders;
        public string[] m_KeywordNames;
        public byte[] m_KeywordFlags;
        public string m_Name;
        public string m_CustomEditorName;
        public string m_FallbackName;
        public List<SerializedShaderDependency> m_Dependencies;
        public List<SerializedCustomEditorForRenderPipeline> m_CustomEditorForRenderPipelines;
        public bool m_DisableNoSubshadersMessage;

        public SerializedShader(ObjectReader reader)
        {
            var version = reader.version;

            m_PropInfo = new SerializedProperties(reader);

            int numSubShaders = reader.ReadInt32();
            m_SubShaders = new List<SerializedSubShader>();
            for (int i = 0; i < numSubShaders; i++)
            {
                m_SubShaders.Add(new SerializedSubShader(reader));
            }

            if (version[0] > 2021 || (version[0] == 2021 && version[1] >= 2)) //2021.2 and up
            {
                m_KeywordNames = reader.ReadStringArray();
                m_KeywordFlags = reader.ReadUInt8Array();
                reader.AlignStream();
            }

            m_Name = reader.ReadAlignedString();
            m_CustomEditorName = reader.ReadAlignedString();
            m_FallbackName = reader.ReadAlignedString();

            int numDependencies = reader.ReadInt32();
            m_Dependencies = new List<SerializedShaderDependency>();
            for (int i = 0; i < numDependencies; i++)
            {
                m_Dependencies.Add(new SerializedShaderDependency(reader));
            }

            if (version[0] >= 2021) //2021.1 and up
            {
                int m_CustomEditorForRenderPipelinesSize = reader.ReadInt32();
                m_CustomEditorForRenderPipelines = new List<SerializedCustomEditorForRenderPipeline>();
                for (int i = 0; i < m_CustomEditorForRenderPipelinesSize; i++)
                {
                    m_CustomEditorForRenderPipelines.Add(new SerializedCustomEditorForRenderPipeline(reader));
                }
            }

            m_DisableNoSubshadersMessage = reader.ReadBoolean();
            reader.AlignStream();
        }
    }

    public enum ShaderCompilerPlatform
    {
        None = -1,
        GL = 0,
        D3D9 = 1,
        Xbox360 = 2,
        PS3 = 3,
        D3D11 = 4,
        GLES20 = 5,
        NaCl = 6,
        Flash = 7,
        D3D11_9x = 8,
        GLES3Plus = 9,
        PSP2 = 10,
        PS4 = 11,
        XboxOne = 12,
        PSM = 13,
        Metal = 14,
        OpenGLCore = 15,
        N3DS = 16,
        WiiU = 17,
        Vulkan = 18,
        Switch = 19,
        XboxOneD3D12 = 20,
        GameCoreXboxOne = 21,
        GameCoreScarlett = 22,
        PS5 = 23,
        PS5NGGC = 24
    };

    public class Shader : NamedObject
    {
        public byte[] m_Script;
        //5.3 - 5.4
        public uint decompressedSize;
        public byte[] m_SubProgramBlob;
        //5.5 and up
        public SerializedShader m_ParsedForm;
        public ShaderCompilerPlatform[] platforms;
        public uint[][] offsets;
        public uint[][] compressedLengths;
        public uint[][] decompressedLengths;
        public byte[] compressedBlob;
        public uint[] stageCounts;

        public override string Name => m_ParsedForm?.m_Name ?? m_Name;

        public Shader(ObjectReader reader) : base(reader)
        {
            if (version[0] == 5 && version[1] >= 5 || version[0] > 5) //5.5 and up
            {
                m_ParsedForm = new SerializedShader(reader);
                platforms = reader.ReadUInt32Array().Select(x => (ShaderCompilerPlatform)x).ToArray();
                if (version[0] > 2019 || (version[0] == 2019 && version[1] >= 3)) //2019.3 and up
                {
                    offsets = reader.ReadUInt32ArrayArray();
                    compressedLengths = reader.ReadUInt32ArrayArray();
                    decompressedLengths = reader.ReadUInt32ArrayArray();
                }
                else
                {
                    offsets = reader.ReadUInt32Array().Select(x => new[] { x }).ToArray();
                    compressedLengths = reader.ReadUInt32Array().Select(x => new[] { x }).ToArray();
                    decompressedLengths = reader.ReadUInt32Array().Select(x => new[] { x }).ToArray();
                }
                compressedBlob = reader.ReadUInt8Array();
                reader.AlignStream();
                if (reader.Game.Type.IsGISubGroup())
                {
                    if (BinaryPrimitives.ReadInt32LittleEndian(compressedBlob) == -1)
                    {
                        compressedBlob = reader.ReadUInt8Array(); //blobDataBlocks
                        reader.AlignStream();
                    }
                }

                if (reader.Game.Type.IsLoveAndDeepspace())
                {
                    var codeOffsets = reader.ReadUInt32ArrayArray();
                    var codeCompressedLengths = reader.ReadUInt32ArrayArray();
                    var codeDecompressedLengths = reader.ReadUInt32ArrayArray();
                    var codeCompressedBlob = reader.ReadUInt8Array();
                    reader.AlignStream();
                }

                if ((version[0] == 2021 && version[1] > 3) ||
                    version[0] == 2021 && version[1] == 3 && version[2] >= 12 || //2021.3.12f1 and up
                    (version[0] == 2022 && version[1] > 1) ||
                    version[0] == 2022 && version[1] == 1 && version[2] >= 21) //2022.1.21f1 and up
                {
                    stageCounts = reader.ReadUInt32Array();
                }

                var m_DependenciesCount = reader.ReadInt32();
                for (int i = 0; i < m_DependenciesCount; i++)
                {
                    new PPtr<Shader>(reader);
                }

                if (version[0] >= 2018)
                {
                    var m_NonModifiableTexturesCount = reader.ReadInt32();
                    for (int i = 0; i < m_NonModifiableTexturesCount; i++)
                    {
                        var first = reader.ReadAlignedString();
                        new PPtr<Texture>(reader);
                    }
                }

                var m_ShaderIsBaked = reader.ReadBoolean();
                reader.AlignStream();
            }
            else
            {
                m_Script = reader.ReadUInt8Array();
                reader.AlignStream();
                var m_PathName = reader.ReadAlignedString();
                if (version[0] == 5 && version[1] >= 3) //5.3 - 5.4
                {
                    decompressedSize = reader.ReadUInt32();
                    m_SubProgramBlob = reader.ReadUInt8Array();
                }
            }
        }
    }
}
