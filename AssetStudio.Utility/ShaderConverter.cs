using AssetStudio.PInvoke;
using SharpGen.Runtime;
using SpirV;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Vortice.D3DCompiler;

namespace AssetStudio
{
    public static class ShaderConverter
    {
        /// <summary>
        /// 将着色器转换为字符串表示形式。
        /// </summary>
        /// <param name="shader">要转换的着色器对象。</param>
        /// <returns>返回转换后的着色器字符串。</returns>
        /// <exception cref="IOException">当LZ4解压缩过程中发生错误时抛出。</exception>
        public static string Convert(this Shader shader)
        {
            if (shader.m_SubProgramBlob != null) //5.3 - 5.4
            {
                var decompressedBytes = new byte[shader.decompressedSize];
                var numWrite = LZ4.Instance.Decompress(shader.m_SubProgramBlob, decompressedBytes);
                if (numWrite != shader.decompressedSize)
                {
                    throw new IOException($"Lz4 解压缩错误，写入 {numWrite} 字节，但预期为 {shader.decompressedSize} 字节");
                }

                using var blobReader = new EndianBinaryReader(new MemoryStream(decompressedBytes), EndianType.LittleEndian);
                var program = new ShaderProgram(blobReader, shader);
                program.Read(blobReader, 0, shader);
                return header + program.Export(Encoding.UTF8.GetString(shader.m_Script));
            }

            if (shader.compressedBlob != null) //5.5 and up
            {
                return header + ConvertSerializedShader(shader);
            }

            return header + Encoding.UTF8.GetString(shader.m_Script);
        }

        /// <summary>
        /// 将序列化的着色器转换为字符串表示形式。
        /// </summary>
        /// <param name="shader">要转换的着色器对象。</param>
        /// <returns>返回转换后的着色器字符串。</returns>
        /// <exception cref="IOException">当LZ4解压缩过程中发生错误时抛出。</exception>
        private static string ConvertSerializedShader(Shader shader)
        {
            var length = shader.platforms.Length;
            var shaderPrograms = new ShaderProgram[length];
            for (var i = 0; i < length; i++)
            {
                for (var j = 0; j < shader.offsets[i].Length; j++)
                {
                    var offset = shader.offsets[i][j];
                    var compressedLength = shader.compressedLengths[i][j];
                    var decompressedLength = shader.decompressedLengths[i][j];
                    var decompressedBytes = new byte[decompressedLength];
                    if (shader.assetsFile.game.Type.IsGISubGroup())
                    {
                        Buffer.BlockCopy(shader.compressedBlob, (int)offset, decompressedBytes, 0, (int)decompressedLength);
                    }
                    else
                    {
                        var numWrite = LZ4.Instance.Decompress(shader.compressedBlob.AsSpan().Slice((int)offset, (int)compressedLength), decompressedBytes.AsSpan().Slice(0, (int)decompressedLength));
                        if (numWrite != decompressedLength)
                        {
                            throw new IOException($"Lz4 解压缩错误，写入 {numWrite} 字节，但预期为 {decompressedLength} 字节");
                        }
                    }

                    using var blobReader = new EndianBinaryReader(new MemoryStream(decompressedBytes), EndianType.LittleEndian);
                    if (j == 0)
                    {
                        shaderPrograms[i] = new ShaderProgram(blobReader, shader);
                    }

                    shaderPrograms[i].Read(blobReader, j, shader);
                }
            }

            return ConvertSerializedShader(shader.m_ParsedForm, shader.platforms, shaderPrograms);
        }

        /// <summary>
        /// 将序列化的着色器转换为字符串表示形式。
        /// </summary>
        /// <param name="m_ParsedForm">已解析的着色器对象。</param>
        /// <param name="platforms">着色器编译平台数组。</param>
        /// <param name="shaderPrograms">着色器程序数组。</param>
        /// <returns>返回转换后的着色器字符串。</returns>
        private static string ConvertSerializedShader(SerializedShader m_ParsedForm, ShaderCompilerPlatform[] platforms, ShaderProgram[] shaderPrograms)
        {
            var sb = new StringBuilder();
            sb.Append($"Shader \"{m_ParsedForm.m_Name}\" {{\n");

            sb.Append(ConvertSerializedProperties(m_ParsedForm.m_PropInfo));

            foreach (var m_SubShader in m_ParsedForm.m_SubShaders)
            {
                sb.Append(ConvertSerializedSubShader(m_SubShader, platforms, shaderPrograms));
            }

            if (!string.IsNullOrEmpty(m_ParsedForm.m_FallbackName))
            {
                sb.Append($"Fallback \"{m_ParsedForm.m_FallbackName}\"\n");
            }

            if (!string.IsNullOrEmpty(m_ParsedForm.m_CustomEditorName))
            {
                sb.Append($"CustomEditor \"{m_ParsedForm.m_CustomEditorName}\"\n");
            }

            sb.Append("}");
            
            sb.Append("GLSL ES 代码： {\n");
            foreach (var shaderProgram in shaderPrograms)
            {
                foreach (var subProgram in shaderProgram.m_SubPrograms)
                {
                    if (subProgram.IsGlsl)
                    {
                        // if (subProgram.m_Keywords!=null)
                        // {
                        //     foreach (var keyword in subProgram.m_Keywords)
                        //     {
                        //         sb.Append($"  m_Keywords:{keyword}\n");
                        //     }
                        // }
                        // if (subProgram.m_LocalKeywords!=null)
                        // {
                        //     foreach (var keyword in subProgram.m_LocalKeywords)
                        //     {
                        //         sb.Append($"  m_LocalKeywords:{keyword}\n");
                        //     }
                        // }
                        sb.Append($"  {subProgram.ProgramCodeStr}\n");
                        // sb.Append($"  endData:{subProgram.endData[0]},{subProgram.endData[1]}\n");
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 将序列化的子着色器转换为字符串表示形式。
        /// </summary>
        /// <param name="m_SubShader">要转换的序列化子着色器对象。</param>
        /// <param name="platforms">一个数组，包含着色器编译平台。</param>
        /// <param name="shaderPrograms">一个数组，包含着色器程序。</param>
        /// <returns>返回转换后的子着色器字符串。</returns>
        private static string ConvertSerializedSubShader(SerializedSubShader m_SubShader, ShaderCompilerPlatform[] platforms, ShaderProgram[] shaderPrograms)
        {
            var sb = new StringBuilder();
            sb.Append("SubShader {\n");
            if (m_SubShader.m_LOD != 0)
            {
                sb.Append($" LOD {m_SubShader.m_LOD}\n");
            }

            sb.Append(ConvertSerializedTagMap(m_SubShader.m_Tags, 1));

            foreach (var m_Passe in m_SubShader.m_Passes)
            {
                sb.Append(ConvertSerializedPass(m_Passe, platforms, shaderPrograms));
            }

            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// 将序列化的着色器通道转换为字符串表示形式。
        /// </summary>
        /// <param name="m_Passe">要转换的序列化着色器通道对象。</param>
        /// <param name="platforms">支持的着色器编译平台数组。</param>
        /// <param name="shaderPrograms">着色器程序数组。</param>
        /// <returns>返回转换后的着色器通道字符串。</returns>
        private static string ConvertSerializedPass(SerializedPass m_Passe, ShaderCompilerPlatform[] platforms, ShaderProgram[] shaderPrograms)
        {
            var sb = new StringBuilder();
            switch (m_Passe.m_Type)
            {
                case PassType.Normal:
                    sb.Append(" Pass ");
                    break;
                case PassType.Use:
                    sb.Append(" UsePass ");
                    break;
                case PassType.Grab:
                    sb.Append(" GrabPass ");
                    break;
            }

            if (m_Passe.m_Type == PassType.Use)
            {
                sb.Append($"\"{m_Passe.m_UseName}\"\n");
            }
            else
            {
                sb.Append("{\n");

                if (m_Passe.m_Type == PassType.Grab)
                {
                    if (!string.IsNullOrEmpty(m_Passe.m_TextureName))
                    {
                        sb.Append($"  \"{m_Passe.m_TextureName}\"\n");
                    }
                }
                else
                {
                    sb.Append(ConvertSerializedShaderState(m_Passe.m_State));

                    if (m_Passe.progVertex.m_SubPrograms.Count > 0)
                    {
                        sb.Append("Program \"vp\" {\n");
                        sb.Append(ConvertSerializedSubPrograms(m_Passe.progVertex.m_SubPrograms, platforms, shaderPrograms));
                        sb.Append("}\n");
                    }

                    if (m_Passe.progFragment.m_SubPrograms.Count > 0)
                    {
                        sb.Append("Program \"fp\" {\n");
                        sb.Append(ConvertSerializedSubPrograms(m_Passe.progFragment.m_SubPrograms, platforms, shaderPrograms));
                        sb.Append("}\n");
                    }

                    if (m_Passe.progGeometry.m_SubPrograms.Count > 0)
                    {
                        sb.Append("Program \"gp\" {\n");
                        sb.Append(ConvertSerializedSubPrograms(m_Passe.progGeometry.m_SubPrograms, platforms, shaderPrograms));
                        sb.Append("}\n");
                    }

                    if (m_Passe.progHull.m_SubPrograms.Count > 0)
                    {
                        sb.Append("Program \"hp\" {\n");
                        sb.Append(ConvertSerializedSubPrograms(m_Passe.progHull.m_SubPrograms, platforms, shaderPrograms));
                        sb.Append("}\n");
                    }

                    if (m_Passe.progDomain.m_SubPrograms.Count > 0)
                    {
                        sb.Append("Program \"dp\" {\n");
                        sb.Append(ConvertSerializedSubPrograms(m_Passe.progDomain.m_SubPrograms, platforms, shaderPrograms));
                        sb.Append("}\n");
                    }

                    if (m_Passe.progRayTracing?.m_SubPrograms.Count > 0)
                    {
                        sb.Append("Program \"rtp\" {\n");
                        sb.Append(ConvertSerializedSubPrograms(m_Passe.progRayTracing.m_SubPrograms, platforms, shaderPrograms));
                        sb.Append("}\n");
                    }
                }

                sb.Append("}\n");
            }

            return sb.ToString();
        }

        private static string ConvertSerializedSubPrograms(List<SerializedSubProgram> m_SubPrograms, ShaderCompilerPlatform[] platforms, ShaderProgram[] shaderPrograms)
        {
            var sb = new StringBuilder();
            var groups = m_SubPrograms.GroupBy(x => x.m_BlobIndex);
            foreach (var group in groups)
            {
                var programs = group.GroupBy(x => x.m_GpuProgramType);
                foreach (var program in programs)
                {
                    for (int i = 0; i < platforms.Length; i++)
                    {
                        var platform = platforms[i];
                        if (CheckGpuProgramUsable(platform, program.Key))
                        {
                            var subPrograms = program.ToList();
                            var isTier = subPrograms.Count > 1;
                            foreach (var subProgram in subPrograms)
                            {
                                sb.Append($"SubProgram \"{GetPlatformString(platform)} ");
                                if (isTier)
                                {
                                    sb.Append($"hw_tier{subProgram.m_ShaderHardwareTier:00} ");
                                }

                                sb.Append("\" {\n");
                                sb.Append(shaderPrograms[i].m_SubPrograms[subProgram.m_BlobIndex].Export());
                                sb.Append("\n}\n");
                            }

                            break;
                        }
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 将序列化的着色器状态转换为字符串表示形式。
        /// </summary>
        /// <param name="m_State">要转换的序列化着色器状态对象。</param>
        /// <returns>返回转换后的着色器状态字符串。</returns>
        private static string ConvertSerializedShaderState(SerializedShaderState m_State)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(m_State.m_Name))
            {
                sb.Append($"  Name \"{m_State.m_Name}\"\n");
            }

            if (m_State.m_LOD != 0)
            {
                sb.Append($"  LOD {m_State.m_LOD}\n");
            }

            sb.Append(ConvertSerializedTagMap(m_State.m_Tags, 2));

            sb.Append(ConvertSerializedShaderRTBlendState(m_State.rtBlend, m_State.rtSeparateBlend));

            if (m_State.alphaToMask.val > 0f)
            {
                sb.Append("  AlphaToMask On\n");
            }

            if (m_State.zClip?.val != 1f) //ZClip On
            {
                sb.Append("  ZClip Off\n");
            }

            if (m_State.zTest.val != 4f) //ZTest LEqual
            {
                sb.Append("  ZTest ");
                switch (m_State.zTest.val) //enum CompareFunction
                {
                    case 0f: //kFuncDisabled
                        sb.Append("Off");
                        break;
                    case 1f: //kFuncNever
                        sb.Append("Never");
                        break;
                    case 2f: //kFuncLess
                        sb.Append("Less");
                        break;
                    case 3f: //kFuncEqual
                        sb.Append("Equal");
                        break;
                    case 5f: //kFuncGreater
                        sb.Append("Greater");
                        break;
                    case 6f: //kFuncNotEqual
                        sb.Append("NotEqual");
                        break;
                    case 7f: //kFuncGEqual
                        sb.Append("GEqual");
                        break;
                    case 8f: //kFuncAlways
                        sb.Append("Always");
                        break;
                }

                sb.Append("\n");
            }

            if (m_State.zWrite.val != 1f) //ZWrite On
            {
                sb.Append("  ZWrite Off\n");
            }

            if (m_State.culling.val != 2f) //Cull Back
            {
                sb.Append("  Cull ");
                switch (m_State.culling.val) //enum CullMode
                {
                    case 0f: //kCullOff
                        sb.Append("Off");
                        break;
                    case 1f: //kCullFront
                        sb.Append("Front");
                        break;
                }

                sb.Append("\n");
            }

            if (m_State.offsetFactor.val != 0f || m_State.offsetUnits.val != 0f)
            {
                sb.Append($"  Offset {m_State.offsetFactor.val}, {m_State.offsetUnits.val}\n");
            }

            if (m_State.stencilRef.val != 0f ||
                m_State.stencilReadMask.val != 255f ||
                m_State.stencilWriteMask.val != 255f ||
                m_State.stencilOp.pass.val != 0f ||
                m_State.stencilOp.fail.val != 0f ||
                m_State.stencilOp.zFail.val != 0f ||
                m_State.stencilOp.comp.val != 8f ||
                m_State.stencilOpFront.pass.val != 0f ||
                m_State.stencilOpFront.fail.val != 0f ||
                m_State.stencilOpFront.zFail.val != 0f ||
                m_State.stencilOpFront.comp.val != 8f ||
                m_State.stencilOpBack.pass.val != 0f ||
                m_State.stencilOpBack.fail.val != 0f ||
                m_State.stencilOpBack.zFail.val != 0f ||
                m_State.stencilOpBack.comp.val != 8f)
            {
                sb.Append("  Stencil {\n");
                if (m_State.stencilRef.val != 0f)
                {
                    sb.Append($"   Ref {m_State.stencilRef.val}\n");
                }

                if (m_State.stencilReadMask.val != 255f)
                {
                    sb.Append($"   ReadMask {m_State.stencilReadMask.val}\n");
                }

                if (m_State.stencilWriteMask.val != 255f)
                {
                    sb.Append($"   WriteMask {m_State.stencilWriteMask.val}\n");
                }

                if (m_State.stencilOp.pass.val != 0f ||
                    m_State.stencilOp.fail.val != 0f ||
                    m_State.stencilOp.zFail.val != 0f ||
                    m_State.stencilOp.comp.val != 8f)
                {
                    sb.Append(ConvertSerializedStencilOp(m_State.stencilOp, ""));
                }

                if (m_State.stencilOpFront.pass.val != 0f ||
                    m_State.stencilOpFront.fail.val != 0f ||
                    m_State.stencilOpFront.zFail.val != 0f ||
                    m_State.stencilOpFront.comp.val != 8f)
                {
                    sb.Append(ConvertSerializedStencilOp(m_State.stencilOpFront, "Front"));
                }

                if (m_State.stencilOpBack.pass.val != 0f ||
                    m_State.stencilOpBack.fail.val != 0f ||
                    m_State.stencilOpBack.zFail.val != 0f ||
                    m_State.stencilOpBack.comp.val != 8f)
                {
                    sb.Append(ConvertSerializedStencilOp(m_State.stencilOpBack, "Back"));
                }

                sb.Append("  }\n");
            }

            if (m_State.fogMode != FogMode.Unknown ||
                m_State.fogColor.x.val != 0f ||
                m_State.fogColor.y.val != 0f ||
                m_State.fogColor.z.val != 0f ||
                m_State.fogColor.w.val != 0f ||
                m_State.fogDensity.val != 0f ||
                m_State.fogStart.val != 0f ||
                m_State.fogEnd.val != 0f)
            {
                sb.Append("  Fog {\n");
                if (m_State.fogMode != FogMode.Unknown)
                {
                    sb.Append("   Mode ");
                    switch (m_State.fogMode)
                    {
                        case FogMode.Disabled:
                            sb.Append("Off");
                            break;
                        case FogMode.Linear:
                            sb.Append("Linear");
                            break;
                        case FogMode.Exp:
                            sb.Append("Exp");
                            break;
                        case FogMode.Exp2:
                            sb.Append("Exp2");
                            break;
                    }

                    sb.Append("\n");
                }

                if (m_State.fogColor.x.val != 0f ||
                    m_State.fogColor.y.val != 0f ||
                    m_State.fogColor.z.val != 0f ||
                    m_State.fogColor.w.val != 0f)
                {
                    sb.AppendFormat("   Color ({0},{1},{2},{3})\n",
                        m_State.fogColor.x.val.ToString(CultureInfo.InvariantCulture),
                        m_State.fogColor.y.val.ToString(CultureInfo.InvariantCulture),
                        m_State.fogColor.z.val.ToString(CultureInfo.InvariantCulture),
                        m_State.fogColor.w.val.ToString(CultureInfo.InvariantCulture));
                }

                if (m_State.fogDensity.val != 0f)
                {
                    sb.Append($"   Density {m_State.fogDensity.val.ToString(CultureInfo.InvariantCulture)}\n");
                }

                if (m_State.fogStart.val != 0f ||
                    m_State.fogEnd.val != 0f)
                {
                    sb.Append($"   Range {m_State.fogStart.val.ToString(CultureInfo.InvariantCulture)}, {m_State.fogEnd.val.ToString(CultureInfo.InvariantCulture)}\n");
                }

                sb.Append("  }\n");
            }

            if (m_State.lighting)
            {
                sb.Append($"  Lighting {(m_State.lighting ? "On" : "Off")}\n");
            }

            sb.Append($"  GpuProgramID {m_State.gpuProgramID}\n");

            return sb.ToString();
        }

        private static string ConvertSerializedStencilOp(SerializedStencilOp stencilOp, string suffix)
        {
            var sb = new StringBuilder();
            sb.Append($"   Comp{suffix} {ConvertStencilComp(stencilOp.comp)}\n");
            sb.Append($"   Pass{suffix} {ConvertStencilOp(stencilOp.pass)}\n");
            sb.Append($"   Fail{suffix} {ConvertStencilOp(stencilOp.fail)}\n");
            sb.Append($"   ZFail{suffix} {ConvertStencilOp(stencilOp.zFail)}\n");
            return sb.ToString();
        }

        private static string ConvertStencilOp(SerializedShaderFloatValue op)
        {
            switch (op.val)
            {
                case 0f:
                default:
                    return "Keep";
                case 1f:
                    return "Zero";
                case 2f:
                    return "Replace";
                case 3f:
                    return "IncrSat";
                case 4f:
                    return "DecrSat";
                case 5f:
                    return "Invert";
                case 6f:
                    return "IncrWrap";
                case 7f:
                    return "DecrWrap";
            }
        }

        private static string ConvertStencilComp(SerializedShaderFloatValue comp)
        {
            switch (comp.val)
            {
                case 0f:
                    return "Disabled";
                case 1f:
                    return "Never";
                case 2f:
                    return "Less";
                case 3f:
                    return "Equal";
                case 4f:
                    return "LEqual";
                case 5f:
                    return "Greater";
                case 6f:
                    return "NotEqual";
                case 7f:
                    return "GEqual";
                case 8f:
                default:
                    return "Always";
            }
        }

        /// <summary>
        /// 将序列化的着色器渲染目标混合状态转换为字符串表示形式。
        /// </summary>
        /// <param name="rtBlend">包含要转换的渲染目标混合状态的列表。</param>
        /// <param name="rtSeparateBlend">指示是否使用分离混合模式的布尔值。</param>
        /// <returns>返回转换后的渲染目标混合状态字符串。</returns>
        private static string ConvertSerializedShaderRTBlendState(List<SerializedShaderRTBlendState> rtBlend, bool rtSeparateBlend)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < rtBlend.Count; i++)
            {
                var blend = rtBlend[i];
                if (blend.srcBlend.val != 1f ||
                    blend.destBlend.val != 0f ||
                    blend.srcBlendAlpha.val != 1f ||
                    blend.destBlendAlpha.val != 0f)
                {
                    sb.Append("  Blend ");
                    if (i != 0 || rtSeparateBlend)
                    {
                        sb.Append($"{i} ");
                    }

                    sb.Append($"{ConvertBlendFactor(blend.srcBlend)} {ConvertBlendFactor(blend.destBlend)}");
                    if (blend.srcBlendAlpha.val != 1f ||
                        blend.destBlendAlpha.val != 0f)
                    {
                        sb.Append($", {ConvertBlendFactor(blend.srcBlendAlpha)} {ConvertBlendFactor(blend.destBlendAlpha)}");
                    }

                    sb.Append("\n");
                }

                if (blend.blendOp.val != 0f ||
                    blend.blendOpAlpha.val != 0f)
                {
                    sb.Append("  BlendOp ");
                    if (i != 0 || rtSeparateBlend)
                    {
                        sb.Append($"{i} ");
                    }

                    sb.Append(ConvertBlendOp(blend.blendOp));
                    if (blend.blendOpAlpha.val != 0f)
                    {
                        sb.Append($", {ConvertBlendOp(blend.blendOpAlpha)}");
                    }

                    sb.Append("\n");
                }

                var val = (int)blend.colMask.val;
                if (val != 0xf)
                {
                    sb.Append("  ColorMask ");
                    if (val == 0)
                    {
                        sb.Append(0);
                    }
                    else
                    {
                        if ((val & 0x2) != 0)
                        {
                            sb.Append("R");
                        }

                        if ((val & 0x4) != 0)
                        {
                            sb.Append("G");
                        }

                        if ((val & 0x8) != 0)
                        {
                            sb.Append("B");
                        }

                        if ((val & 0x1) != 0)
                        {
                            sb.Append("A");
                        }
                    }

                    sb.Append($" {i}\n");
                }
            }

            return sb.ToString();
        }

        private static string ConvertBlendOp(SerializedShaderFloatValue op)
        {
            switch (op.val)
            {
                case 0f:
                default:
                    return "Add";
                case 1f:
                    return "Sub";
                case 2f:
                    return "RevSub";
                case 3f:
                    return "Min";
                case 4f:
                    return "Max";
                case 5f:
                    return "LogicalClear";
                case 6f:
                    return "LogicalSet";
                case 7f:
                    return "LogicalCopy";
                case 8f:
                    return "LogicalCopyInverted";
                case 9f:
                    return "LogicalNoop";
                case 10f:
                    return "LogicalInvert";
                case 11f:
                    return "LogicalAnd";
                case 12f:
                    return "LogicalNand";
                case 13f:
                    return "LogicalOr";
                case 14f:
                    return "LogicalNor";
                case 15f:
                    return "LogicalXor";
                case 16f:
                    return "LogicalEquiv";
                case 17f:
                    return "LogicalAndReverse";
                case 18f:
                    return "LogicalAndInverted";
                case 19f:
                    return "LogicalOrReverse";
                case 20f:
                    return "LogicalOrInverted";
            }
        }

        private static string ConvertBlendFactor(SerializedShaderFloatValue factor)
        {
            switch (factor.val)
            {
                case 0f:
                    return "Zero";
                case 1f:
                default:
                    return "One";
                case 2f:
                    return "DstColor";
                case 3f:
                    return "SrcColor";
                case 4f:
                    return "OneMinusDstColor";
                case 5f:
                    return "SrcAlpha";
                case 6f:
                    return "OneMinusSrcColor";
                case 7f:
                    return "DstAlpha";
                case 8f:
                    return "OneMinusDstAlpha";
                case 9f:
                    return "SrcAlphaSaturate";
                case 10f:
                    return "OneMinusSrcAlpha";
            }
        }

        /// <summary>
        /// 将序列化的标签映射转换为字符串表示形式。
        /// </summary>
        /// <param name="m_Tags">要转换的序列化标签映射对象。</param>
        /// <param name="intent">用于格式化输出的缩进级别。</param>
        /// <returns>返回转换后的标签映射字符串。</returns>
        private static string ConvertSerializedTagMap(SerializedTagMap m_Tags, int intent)
        {
            var sb = new StringBuilder();
            if (m_Tags.tags.Count > 0)
            {
                sb.Append(new string(' ', intent));
                sb.Append("Tags { ");
                foreach (var pair in m_Tags.tags)
                {
                    sb.Append($"\"{pair.Key}\" = \"{pair.Value}\" ");
                }

                sb.Append("}\n");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 将序列化的属性转换为字符串表示形式。
        /// </summary>
        /// <param name="m_PropInfo">包含要转换的序列化属性信息的对象。</param>
        /// <returns>返回包含所有属性的字符串，格式化为Properties块。</returns>
        private static string ConvertSerializedProperties(SerializedProperties m_PropInfo)
        {
            var sb = new StringBuilder();
            sb.Append("Properties {\n");
            foreach (var m_Prop in m_PropInfo.m_Props)
            {
                sb.Append(ConvertSerializedProperty(m_Prop));
            }

            sb.Append("}\n");
            return sb.ToString();
        }

        private static string ConvertSerializedProperty(SerializedProperty m_Prop)
        {
            var sb = new StringBuilder();
            foreach (var m_Attribute in m_Prop.m_Attributes)
            {
                sb.Append($"[{m_Attribute}] ");
            }

            foreach (var flag in Enum.GetValues<SerializedPropertyFlag>().Where(x => m_Prop.m_Flags.HasFlag(x)))
            {
                sb.Append($"[{flag}] ");
            }

            sb.Append($"{m_Prop.m_Name} (\"{m_Prop.m_Description}\", ");
            switch (m_Prop.m_Type)
            {
                case SerializedPropertyType.Color:
                    sb.Append("Color");
                    break;
                case SerializedPropertyType.Vector:
                    sb.Append("Vector");
                    break;
                case SerializedPropertyType.Float:
                    sb.Append("Float");
                    break;
                case SerializedPropertyType.Range:
                    sb.Append($"Range({m_Prop.m_DefValue[1]}, {m_Prop.m_DefValue[2]})");
                    break;
                case SerializedPropertyType.Texture:
                    switch (m_Prop.m_DefTexture.m_TexDim)
                    {
                        case TextureDimension.Any:
                            sb.Append("any");
                            break;
                        case TextureDimension.Tex2D:
                            sb.Append("2D");
                            break;
                        case TextureDimension.Tex3D:
                            sb.Append("3D");
                            break;
                        case TextureDimension.Cube:
                            sb.Append("Cube");
                            break;
                        case TextureDimension.Tex2DArray:
                            sb.Append("2DArray");
                            break;
                        case TextureDimension.CubeArray:
                            sb.Append("CubeArray");
                            break;
                    }

                    break;
            }

            sb.Append(") = ");
            switch (m_Prop.m_Type)
            {
                case SerializedPropertyType.Color:
                case SerializedPropertyType.Vector:
                    sb.Append($"({m_Prop.m_DefValue[0]},{m_Prop.m_DefValue[1]},{m_Prop.m_DefValue[2]},{m_Prop.m_DefValue[3]})");
                    break;
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Range:
                    sb.Append(m_Prop.m_DefValue[0]);
                    break;
                case SerializedPropertyType.Texture:
                    sb.Append($"\"{m_Prop.m_DefTexture.m_DefaultName}\" {{ }}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            sb.Append("\n");
            return sb.ToString();
        }

        private static bool CheckGpuProgramUsable(ShaderCompilerPlatform platform, ShaderGpuProgramType programType)
        {
            switch (platform)
            {
                case ShaderCompilerPlatform.GL:
                    return programType == ShaderGpuProgramType.GLLegacy;
                case ShaderCompilerPlatform.D3D9:
                    return programType == ShaderGpuProgramType.DX9VertexSM20
                           || programType == ShaderGpuProgramType.DX9VertexSM30
                           || programType == ShaderGpuProgramType.DX9PixelSM20
                           || programType == ShaderGpuProgramType.DX9PixelSM30;
                case ShaderCompilerPlatform.Xbox360:
                case ShaderCompilerPlatform.PS3:
                case ShaderCompilerPlatform.PSP2:
                case ShaderCompilerPlatform.PS4:
                case ShaderCompilerPlatform.XboxOne:
                case ShaderCompilerPlatform.N3DS:
                case ShaderCompilerPlatform.WiiU:
                case ShaderCompilerPlatform.Switch:
                case ShaderCompilerPlatform.XboxOneD3D12:
                case ShaderCompilerPlatform.GameCoreXboxOne:
                case ShaderCompilerPlatform.GameCoreScarlett:
                case ShaderCompilerPlatform.PS5:
                    return programType == ShaderGpuProgramType.ConsoleVS
                           || programType == ShaderGpuProgramType.ConsoleFS
                           || programType == ShaderGpuProgramType.ConsoleHS
                           || programType == ShaderGpuProgramType.ConsoleDS
                           || programType == ShaderGpuProgramType.ConsoleGS;
                case ShaderCompilerPlatform.PS5NGGC:
                    return programType == ShaderGpuProgramType.PS5NGGC;
                case ShaderCompilerPlatform.D3D11:
                    return programType == ShaderGpuProgramType.DX11VertexSM40
                           || programType == ShaderGpuProgramType.DX11VertexSM50
                           || programType == ShaderGpuProgramType.DX11PixelSM40
                           || programType == ShaderGpuProgramType.DX11PixelSM50
                           || programType == ShaderGpuProgramType.DX11GeometrySM40
                           || programType == ShaderGpuProgramType.DX11GeometrySM50
                           || programType == ShaderGpuProgramType.DX11HullSM50
                           || programType == ShaderGpuProgramType.DX11DomainSM50;
                case ShaderCompilerPlatform.GLES20:
                    return programType == ShaderGpuProgramType.GLES;
                case ShaderCompilerPlatform.NaCl: //Obsolete
                    throw new NotSupportedException();
                case ShaderCompilerPlatform.Flash: //Obsolete
                    throw new NotSupportedException();
                case ShaderCompilerPlatform.D3D11_9x:
                    return programType == ShaderGpuProgramType.DX10Level9Vertex
                           || programType == ShaderGpuProgramType.DX10Level9Pixel;
                case ShaderCompilerPlatform.GLES3Plus:
                    return programType == ShaderGpuProgramType.GLES31AEP
                           || programType == ShaderGpuProgramType.GLES31
                           || programType == ShaderGpuProgramType.GLES3;
                case ShaderCompilerPlatform.PSM: //Unknown
                    throw new NotSupportedException();
                case ShaderCompilerPlatform.Metal:
                    return programType == ShaderGpuProgramType.MetalVS
                           || programType == ShaderGpuProgramType.MetalFS;
                case ShaderCompilerPlatform.OpenGLCore:
                    return programType == ShaderGpuProgramType.GLCore32
                           || programType == ShaderGpuProgramType.GLCore41
                           || programType == ShaderGpuProgramType.GLCore43;
                case ShaderCompilerPlatform.Vulkan:
                    return programType == ShaderGpuProgramType.SPIRV;
                default:
                    throw new NotSupportedException();
            }
        }

        public static string GetPlatformString(ShaderCompilerPlatform platform)
        {
            switch (platform)
            {
                case ShaderCompilerPlatform.GL:
                    return "openGL";
                case ShaderCompilerPlatform.D3D9:
                    return "d3d9";
                case ShaderCompilerPlatform.Xbox360:
                    return "xbox360";
                case ShaderCompilerPlatform.PS3:
                    return "ps3";
                case ShaderCompilerPlatform.D3D11:
                    return "d3d11";
                case ShaderCompilerPlatform.GLES20:
                    return "gles";
                case ShaderCompilerPlatform.NaCl:
                    return "glesdesktop";
                case ShaderCompilerPlatform.Flash:
                    return "flash";
                case ShaderCompilerPlatform.D3D11_9x:
                    return "d3d11_9x";
                case ShaderCompilerPlatform.GLES3Plus:
                    return "gles3";
                case ShaderCompilerPlatform.PSP2:
                    return "psp2";
                case ShaderCompilerPlatform.PS4:
                    return "ps4";
                case ShaderCompilerPlatform.XboxOne:
                    return "xboxone";
                case ShaderCompilerPlatform.PSM:
                    return "psm";
                case ShaderCompilerPlatform.Metal:
                    return "metal";
                case ShaderCompilerPlatform.OpenGLCore:
                    return "glcore";
                case ShaderCompilerPlatform.N3DS:
                    return "n3ds";
                case ShaderCompilerPlatform.WiiU:
                    return "wiiu";
                case ShaderCompilerPlatform.Vulkan:
                    return "vulkan";
                case ShaderCompilerPlatform.Switch:
                    return "switch";
                case ShaderCompilerPlatform.XboxOneD3D12:
                    return "xboxone_d3d12";
                case ShaderCompilerPlatform.GameCoreXboxOne:
                    return "xboxone";
                case ShaderCompilerPlatform.GameCoreScarlett:
                    return "xbox_scarlett";
                case ShaderCompilerPlatform.PS5:
                    return "ps5";
                case ShaderCompilerPlatform.PS5NGGC:
                    return "ps5_nggc";
                default:
                    return "unknown";
            }
        }

        private static string header = "//////////////////////////////////////////\n" +
                                       "//\n" +
                                       "// 注意：这不是一个有效的着色器文件               \n" +
                                       "//\n" +
                                       "///////////////////////////////////////////\n";
    }

    /// <summary>
    /// ShaderSubProgramEntry 类用于表示着色器子程序的条目信息，包括子程序在二进制数据中的偏移量、长度以及所在段落编号。
    /// 该类主要用于从给定的EndianBinaryReader中读取着色器子程序的相关元数据。它支持根据Unity版本的不同来决定是否读取段落编号。
    /// </summary>
    /// <remarks>
    /// 此类是ShaderProgram类的一部分，用于存储和处理着色器子程序的具体位置信息，以便后续可以从二进制资源中准确地提取出这些子程序。
    /// 它与ShaderConverter类协同工作，共同完成着色器数据的解析任务。
    /// </remarks>
    public class ShaderSubProgramEntry
    {
        /// <summary>
        /// 表示着色器子程序在二进制数据中的起始位置偏移量。此偏移量用于定位子程序的具体位置，以便从二进制资源中读取和解析该子程序。
        /// </summary>
        public int Offset;

        /// <summary>
        /// 表示着色器子程序在二进制数据中的长度。此长度用于确定子程序占用的字节数，以便从二进制资源中准确读取和解析该子程序。
        /// </summary>
        public int Length;

        /// <summary>
        /// 表示着色器子程序所属的段落编号。此编号用于标识子程序在二进制数据中的特定段落，以便从正确的段落中读取和解析该子程序。
        /// </summary>
        public int Segment;

        public ShaderSubProgramEntry(EndianBinaryReader reader, int[] version)
        {
            Offset = reader.ReadInt32();
            Length = reader.ReadInt32();
            if (version[0] > 2019 || (version[0] == 2019 && version[1] >= 3)) //2019.3 and up
            {
                Segment = reader.ReadInt32();
            }
        }
    }

    /// <summary>
    /// ShaderProgram 类用于表示着色器程序，并提供读取和导出着色器子程序的方法。
    /// 该类主要用于处理从二进制数据中解压并解析出来的着色器信息，支持通过指定的段落读取具体的着色器子程序内容，
    /// 并且能够将这些子程序按照一定格式导出为字符串形式。
    /// </summary>
    /// <remarks>
    /// 此类通常与ShaderConverter类一起工作，用于在AssetStudio项目中处理Unity资源文件中的着色器数据。
    /// 它依赖于EndianBinaryReader来读取不同字节序的数据。
    /// </remarks>
    public class ShaderProgram
    {
        /// <summary>
        /// 表示着色器程序中的子程序条目数组。每个条目包含关于子程序的偏移量、长度和段信息，用于从二进制数据中定位和读取具体的着色器子程序。
        /// </summary>
        public ShaderSubProgramEntry[] entries;

        /// <summary>
        /// 表示着色器程序中的子程序数组。每个元素包含关于子程序的类型、关键字、本地关键字以及程序代码等信息，用于存储和处理具体的着色器子程序数据。
        /// </summary>
        public ShaderSubProgram[] m_SubPrograms;

        private bool hasUpdatedGpuProgram = false;

        public ShaderProgram(EndianBinaryReader reader, Shader shader)
        {
            var subProgramsCapacity = reader.ReadInt32();
            entries = new ShaderSubProgramEntry[subProgramsCapacity];
            for (int i = 0; i < subProgramsCapacity; i++)
            {
                entries[i] = new ShaderSubProgramEntry(reader, shader.version);
            }

            m_SubPrograms = new ShaderSubProgram[subProgramsCapacity];
            if (shader.assetsFile.game.Type.IsGI())
            {
                hasUpdatedGpuProgram = SerializedSubProgram.HasInstancedStructuredBuffers(shader.serializedType) || SerializedSubProgram.HasGlobalLocalKeywordIndices(shader.serializedType);
            }
        }

        public void Read(EndianBinaryReader reader, int segment, Shader shader)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.Segment == segment)
                {
                    reader.BaseStream.Position = entry.Offset;
                    m_SubPrograms[i] = new ShaderSubProgram(reader, hasUpdatedGpuProgram, shader);
                }
            }
        }

        public string Export(string shader)
        {
            var evaluator = new MatchEvaluator(match =>
            {
                var index = int.Parse(match.Groups[1].Value);
                return m_SubPrograms[index].Export();
            });
            shader = Regex.Replace(shader, "GpuProgramIndex (.+)", evaluator);
            return shader;
        }
    }

    /// <summary>
    /// ShaderSubProgram 类用于表示着色器子程序的具体内容，包括程序类型、关键字列表以及程序代码。
    /// 该类通过从给定的 EndianBinaryReader 中读取数据来初始化，并支持根据 Unity 版本的不同进行适当的解析处理。
    /// </summary>
    /// <remarks>
    /// 此类是 ShaderProgram 类的一部分，主要用于存储和处理着色器子程序的数据。它与 ShaderConverter 类协同工作，共同完成着色器数据的解析任务。
    /// 在导出时，ShaderSubProgram 会将程序代码和相关的关键字信息转换为字符串格式。
    /// </remarks>
    public class ShaderSubProgram
    {
        /// <summary>
        /// 表示着色器子程序的版本号。此版本号用于确定解析着色器数据时使用的具体格式和规则。
        /// </summary>
        private int m_Version;

        /// <summary>
        /// 表示着色器子程序的GPU程序类型。该枚举值定义了着色器支持的不同图形API和版本，用于区分着色器代码的目标平台与硬件要求。
        /// </summary>
        public ShaderGpuProgramType m_ProgramType;

        /// <summary>
        /// 表示着色器子程序中使用的关键字数组。这些关键字用于定义着色器的不同变体，允许根据不同的条件启用或禁用特定的着色器功能。
        /// </summary>
        public string[] m_Keywords;

        /// <summary>
        /// 表示着色器子程序中使用的本地关键字数组。这些关键字用于定义着色器内部的特定行为或特性，通常在着色器代码中通过预处理器指令来控制不同的编译路径。
        /// </summary>
        public string[] m_LocalKeywords;

        /// <summary>
        /// 表示着色器子程序的二进制代码。该字节数组包含了编译后的着色器程序，用于在图形处理单元（GPU）上执行。
        /// </summary>
        public byte[] m_ProgramCode;

        public bool IsGlsl;
        
        public string ProgramCodeStr;
        public int[] endData;

        public ShaderSubProgram(EndianBinaryReader reader, bool hasUpdatedGpuProgram, Shader shader)
        {
            //LoadGpuProgramFromData
            //201509030 - Unity 5.3
            //201510240 - Unity 5.4
            //201608170 - Unity 5.5
            //201609010 - Unity 5.6, 2017.1 & 2017.2
            //201708220 - Unity 2017.3, Unity 2017.4 & Unity 2018.1
            //201802150 - Unity 2018.2 & Unity 2018.3
            //201806140 - Unity 2019.1~2021.1
            //202012090 - Unity 2021.2
            m_Version = reader.ReadInt32();
            if (hasUpdatedGpuProgram && m_Version > 201806140)
            {
                m_Version = 201806140;
            }
            m_ProgramType = (ShaderGpuProgramType)reader.ReadInt32();
            reader.BaseStream.Position += 12;
            if (m_Version >= 201608170)
            {
                reader.BaseStream.Position += 4;
            }

            if (shader.assetsFile.game.Type == GameType.Orisries)
            {
                IsGlsl = true;
                if (m_ProgramType == ShaderGpuProgramType.GLES31AEP)
                {
                    var globals =reader.ReadAlignedString();
                    var int32=reader.ReadInt32();
                    var count = reader.ReadInt32();
                    m_Keywords = new string[count+1];
                    m_Keywords[0] = $"{globals}:{int32}";
                    for (var i = 0; i < count; i++)
                    {
                        var str = reader.ReadAlignedString();
                        var data = reader.ReadInt32Array(6);
                        m_Keywords[i+1] = $"{str}:{data[0]},{data[1]},{data[2]},{data[3]},{data[4]},{data[5]}";
                    }
                }
                else
                {
                    var count = reader.ReadInt32();
                    m_LocalKeywords = new string[count];
                    for (var i = 0; i < count; i++)
                    {
                        m_LocalKeywords[i] = reader.ReadAlignedString();
                    }
                    
                    m_ProgramCode = reader.ReadUInt8Array();
                    reader.AlignStream();
                    ProgramCodeStr = Encoding.UTF8.GetString(m_ProgramCode);
                }

                endData = reader.ReadInt32Array(2);
                return;
            }
            
            var m_KeywordsSize = reader.ReadInt32();
            m_Keywords = new string[m_KeywordsSize];
            for (int i = 0; i < m_KeywordsSize; i++)
            {
                m_Keywords[i] = reader.ReadAlignedString();
            }

            if (m_Version >= 201806140 && m_Version < 202012090)
            {
                var m_LocalKeywordsSize = reader.ReadInt32();
                m_LocalKeywords = new string[m_LocalKeywordsSize];
                for (int i = 0; i < m_LocalKeywordsSize; i++)
                {
                    m_LocalKeywords[i] = reader.ReadAlignedString();
                }
            }

            m_ProgramCode = reader.ReadUInt8Array();
            reader.AlignStream();

            //TODO
        }

        public string Export()
        {
            var sb = new StringBuilder();
            if (m_Keywords.Length > 0)
            {
                sb.Append("Keywords { ");
                foreach (string keyword in m_Keywords)
                {
                    sb.Append($"\"{keyword}\" ");
                }

                sb.Append("}\n");
            }

            if (m_LocalKeywords != null && m_LocalKeywords.Length > 0)
            {
                sb.Append("Local Keywords { ");
                foreach (string keyword in m_LocalKeywords)
                {
                    sb.Append($"\"{keyword}\" ");
                }

                sb.Append("}\n");
            }

            sb.Append("\"");
            if (m_ProgramCode.Length > 0)
            {
                switch (m_ProgramType)
                {
                    case ShaderGpuProgramType.GLLegacy:
                    case ShaderGpuProgramType.GLES31AEP:
                    case ShaderGpuProgramType.GLES31:
                    case ShaderGpuProgramType.GLES3:
                    case ShaderGpuProgramType.GLES:
                    case ShaderGpuProgramType.GLCore32:
                    case ShaderGpuProgramType.GLCore41:
                    case ShaderGpuProgramType.GLCore43:
                        sb.Append($"// hash: {ComputeHash64(m_ProgramCode):x8}\n");
                        sb.Append(Encoding.UTF8.GetString(m_ProgramCode));
                        break;
                    case ShaderGpuProgramType.DX9VertexSM20:
                    case ShaderGpuProgramType.DX9VertexSM30:
                    case ShaderGpuProgramType.DX9PixelSM20:
                    case ShaderGpuProgramType.DX9PixelSM30:
                    {
                        try
                        {
                            var programCodeSpan = m_ProgramCode.AsSpan();
                            var g = Compiler.Disassemble(programCodeSpan.GetPinnableReference(), programCodeSpan.Length, DisasmFlags.None, "");

                            sb.Append($"// hash: {ComputeHash64(programCodeSpan):x8}\n");
                            sb.Append(g.AsString());
                        }
                        catch (Exception e)
                        {
                            sb.Append($"// disassembly error {e.Message}\n");
                        }

                        break;
                    }
                    case ShaderGpuProgramType.DX10Level9Vertex:
                    case ShaderGpuProgramType.DX10Level9Pixel:
                    case ShaderGpuProgramType.DX11VertexSM40:
                    case ShaderGpuProgramType.DX11VertexSM50:
                    case ShaderGpuProgramType.DX11PixelSM40:
                    case ShaderGpuProgramType.DX11PixelSM50:
                    case ShaderGpuProgramType.DX11GeometrySM40:
                    case ShaderGpuProgramType.DX11GeometrySM50:
                    case ShaderGpuProgramType.DX11HullSM50:
                    case ShaderGpuProgramType.DX11DomainSM50:
                    {
                        int type = m_ProgramCode[0];
                        int start = 1;
                        if (type > 0)
                        {
                            if (type == 1)
                            {
                                start = 6;
                            }
                            else if (type == 2)
                            {
                                start = 38;
                            }
                        }

                        var buffSpan = m_ProgramCode.AsSpan(start);

                        sb.Append($"// hash: {ComputeHash64(buffSpan):x8}\n");
                        try
                        {
                            HLSLDecompiler.DecompileShader(buffSpan.ToArray(), buffSpan.Length, out var hlslText);
                            sb.Append(hlslText);
                        }
                        catch (Exception e)
                        {
                            Logger.Verbose($"反编译错误 {e.Message}");
                            Logger.Verbose($"正在尝试反汇编...");

                            try
                            {
                                var g = Compiler.Disassemble(buffSpan.GetPinnableReference(), buffSpan.Length, DisasmFlags.None, "");
                                sb.Append(g.AsString());
                            }
                            catch (Exception ex)
                            {
                                sb.Append($"// decompile/disassembly error {ex.Message}\n");
                            }
                        }

                        break;
                    }
                    case ShaderGpuProgramType.MetalVS:
                    case ShaderGpuProgramType.MetalFS:
                        sb.Append($"// hash: {ComputeHash64(m_ProgramCode):x8}\n");
                        using (var reader = new EndianBinaryReader(new MemoryStream(m_ProgramCode), EndianType.LittleEndian))
                        {
                            var fourCC = reader.ReadUInt32();
                            if (fourCC == 0xf00dcafe)
                            {
                                int offset = reader.ReadInt32();
                                reader.BaseStream.Position = offset;
                            }

                            var entryName = reader.ReadStringToNull();
                            var buff = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                            sb.Append(Encoding.UTF8.GetString(buff));
                        }

                        break;
                    case ShaderGpuProgramType.SPIRV:
                        try
                        {
                            sb.Append($"// hash: {ComputeHash64(m_ProgramCode):x8}\n");
                            sb.Append(SpirVShaderConverter.Convert(m_ProgramCode));
                        }
                        catch (Exception e)
                        {
                            sb.Append($"// disassembly error {e.Message}\n");
                        }

                        break;
                    case ShaderGpuProgramType.ConsoleVS:
                    case ShaderGpuProgramType.ConsoleFS:
                    case ShaderGpuProgramType.ConsoleHS:
                    case ShaderGpuProgramType.ConsoleDS:
                    case ShaderGpuProgramType.ConsoleGS:
                        sb.Append($"//hash: {ComputeHash64(m_ProgramCode):x8}\n");
                        sb.Append(Encoding.UTF8.GetString(m_ProgramCode));
                        break;
                    default:
                        sb.Append($"//hash: {ComputeHash64(m_ProgramCode):x8}\n");
                        sb.Append($"//shader disassembly not supported on {m_ProgramType}");
                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        public ulong ComputeHash64(Span<byte> data)
        {
            ulong hval = 0;
            foreach (var b in data)
            {
                hval *= 0x100000001B3;
                hval ^= b;
            }

            return hval;
        }
    }

    public static class HLSLDecompiler
    {
        private const string DLL_NAME = "HLSLDecompiler";

        static HLSLDecompiler()
        {
            DllLoader.PreloadDll(DLL_NAME);
        }

        public static void DecompileShader(byte[] shaderByteCode, int shaderByteCodeSize, out string hlslText)
        {
            var code = Decompile(shaderByteCode, shaderByteCodeSize, out var shaderText, out var shaderTextSize);
            if (code != 0)
            {
                throw new Exception($"无法反编译着色器，错误代码：{code}");
            }

            hlslText = Marshal.PtrToStringAnsi(shaderText, shaderTextSize);
            Marshal.FreeHGlobal(shaderText);
        }

        #region importfunctions

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Decompile(byte[] shaderByteCode, int shaderByteCodeSize, out IntPtr shaderText, out int shaderTextSize);

        #endregion
    }
}