﻿using System;
using System.Collections.Generic;
using System.IO;

namespace AssetStudio
{
    public class SecondarySpriteTexture
    {
        public PPtr<Texture2D> texture;
        public string name;

        public SecondarySpriteTexture(ObjectReader reader)
        {
            texture = new PPtr<Texture2D>(reader);
            name = reader.ReadStringToNull();
        }
    }

    public enum SpritePackingRotation
    {
        None = 0,
        FlipHorizontal = 1,
        FlipVertical = 2,
        Rotate180 = 3,
        Rotate90 = 4
    };

    public enum SpritePackingMode
    {
        Tight = 0,
        Rectangle
    };

    public enum SpriteMeshType
    {
        FullRect,
        Tight
    };

    public class SpriteSettings
    {
        public uint settingsRaw;

        public uint packed;
        public SpritePackingMode packingMode;
        public SpritePackingRotation packingRotation;
        public SpriteMeshType meshType;

        public SpriteSettings(BinaryReader reader)
        {
            settingsRaw = reader.ReadUInt32();

            packed = settingsRaw & 1; //1
            packingMode = (SpritePackingMode)((settingsRaw >> 1) & 1); //1
            packingRotation = (SpritePackingRotation)((settingsRaw >> 2) & 0xf); //4
            meshType = (SpriteMeshType)((settingsRaw >> 6) & 1); //1
            //reserved
        }
    }

    public class SpriteVertex
    {
        public Vector3 pos;
        public Vector2 uv;

        public SpriteVertex(ObjectReader reader)
        {
            var version = reader.version;

            pos = reader.ReadVector3();
            if (version[0] < 4 || (version[0] == 4 && version[1] <= 3)) //4.3 and down
            {
                uv = reader.ReadVector2();
            }
        }
    }

    /// <summary> 精灵渲染数据 </summary>
    public class SpriteRenderData
    {
        /// <summary> 精灵纹理 </summary>
        public PPtr<Texture2D> texture;
        /// <summary> 透明纹理 </summary>
        public PPtr<Texture2D> alphaTexture;
        /// <summary> 辅助纹理 </summary>
        public List<SecondarySpriteTexture> secondaryTextures;
        /// <summary> 子网格 </summary>
        public List<SubMesh> m_SubMeshes;
        /// <summary> 索引 </summary>
        public byte[] m_IndexBuffer;
        /// <summary> 顶点数据 </summary>
        public VertexData m_VertexData;
        /// <summary> 顶点 </summary>
        public List<SpriteVertex> vertices;
        /// <summary> 索引 </summary>
        public ushort[] indices;
        /// <summary> 绑定矩阵 </summary>
        public Matrix4x4[] m_Bindpose;
        public List<BoneWeights4> m_SourceSkin;
        /// <summary> 纹理区域 </summary>
        public Rectf textureRect;
        /// <summary> 纹理区域偏移 </summary>
        public Vector2 textureRectOffset;
        /// <summary> 贴图区域偏移 </summary>
        public Vector2 atlasRectOffset;
        /// <summary> 精灵设置 </summary>
        public SpriteSettings settingsRaw;
        /// <summary> UV变换 </summary>
        public Vector4 uvTransform;
        /// <summary> 缩放 </summary>
        public float downscaleMultiplier;

        public SpriteRenderData(ObjectReader reader)
        {
            var version = reader.version;

            texture = new PPtr<Texture2D>(reader);
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 2)) //5.2 and up
            {
                alphaTexture = new PPtr<Texture2D>(reader);
            }

            if (version[0] >= 2019) //2019 and up
            {
                var secondaryTexturesSize = reader.ReadInt32();
                secondaryTextures = new List<SecondarySpriteTexture>();
                for (int i = 0; i < secondaryTexturesSize; i++)
                {
                    secondaryTextures.Add(new SecondarySpriteTexture(reader));
                }
            }

            if (version[0] > 5 || (version[0] == 5 && version[1] >= 6)) //5.6 and up
            {
                var m_SubMeshesSize = reader.ReadInt32();
                m_SubMeshes = new List<SubMesh>();
                for (int i = 0; i < m_SubMeshesSize; i++)
                {
                    m_SubMeshes.Add(new SubMesh(reader));
                }

                m_IndexBuffer = reader.ReadUInt8Array();
                reader.AlignStream();

                m_VertexData = new VertexData(reader);
            }
            else
            {
                var verticesSize = reader.ReadInt32();
                vertices = new List<SpriteVertex>();
                for (int i = 0; i < verticesSize; i++)
                {
                    vertices.Add(new SpriteVertex(reader));
                }

                indices = reader.ReadUInt16Array();
                reader.AlignStream();
            }

            if (version[0] >= 2018) //2018 and up
            {
                m_Bindpose = reader.ReadMatrixArray();

                if (version[0] == 2018 && version[1] < 2) //2018.2 down
                {
                    var m_SourceSkinSize = reader.ReadInt32();
                    for (int i = 0; i < m_SourceSkinSize; i++)
                    {
                        m_SourceSkin[i] = new BoneWeights4(reader);
                    }
                }
            }

            textureRect = new Rectf(reader);
            textureRectOffset = reader.ReadVector2();
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 6)) //5.6 and up
            {
                atlasRectOffset = reader.ReadVector2();
            }

            settingsRaw = new SpriteSettings(reader);
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 5)) //4.5 and up
            {
                uvTransform = reader.ReadVector4();
            }

            if (version[0] >= 2017) //2017 and up
            {
                downscaleMultiplier = reader.ReadSingle();
            }
        }
    }

    /// <summary> 矩形 </summary>
    public class Rectf
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public Rectf(BinaryReader reader)
        {
            x = reader.ReadSingle();
            y = reader.ReadSingle();
            width = reader.ReadSingle();
            height = reader.ReadSingle();
        }
    }

    /// <summary> 精灵图 </summary>
    public sealed class Sprite : NamedObject
    {
        public Rectf m_Rect;
        public Vector2 m_Offset;
        public Vector4 m_Border;
        public float m_PixelsToUnits;
        public Vector2 m_Pivot = new Vector2(0.5f, 0.5f);
        public uint m_Extrude;
        public bool m_IsPolygon;
        public KeyValuePair<Guid, long> m_RenderDataKey;
        public string[] m_AtlasTags;
        public PPtr<SpriteAtlas> m_SpriteAtlas;
        public SpriteRenderData m_RD;
        public List<Vector2[]> m_PhysicsShape;

        public Sprite(ObjectReader reader) : base(reader)
        {
            m_Rect = new Rectf(reader);
            m_Offset = reader.ReadVector2();
            if (version[0] > 4 || (version[0] == 4 && version[1] >= 5)) //4.5 and up
            {
                m_Border = reader.ReadVector4();
            }

            m_PixelsToUnits = reader.ReadSingle();
            if (version[0] > 5
                || (version[0] == 5 && version[1] > 4)
                || (version[0] == 5 && version[1] == 4 && version[2] >= 2)
                || (version[0] == 5 && version[1] == 4 && version[2] == 1 && buildType.IsPatch && version[3] >= 3)) //5.4.1p3 and up
            {
                m_Pivot = reader.ReadVector2();
            }

            m_Extrude = reader.ReadUInt32();
            if (version[0] > 5 || (version[0] == 5 && version[1] >= 3)) //5.3 and up
            {
                m_IsPolygon = reader.ReadBoolean();
                reader.AlignStream();
            }

            if (version[0] >= 2017) //2017 and up
            {
                var first = new Guid(reader.ReadBytes(16));
                var second = reader.ReadInt64();
                m_RenderDataKey = new KeyValuePair<Guid, long>(first, second);

                m_AtlasTags = reader.ReadStringArray();

                m_SpriteAtlas = new PPtr<SpriteAtlas>(reader);
            }

            m_RD = new SpriteRenderData(reader);

            if (version[0] >= 2017) //2017 and up
            {
                var m_PhysicsShapeSize = reader.ReadInt32();
                m_PhysicsShape = new List<Vector2[]>();
                for (int i = 0; i < m_PhysicsShapeSize; i++)
                {
                    m_PhysicsShape.Add(reader.ReadVector2Array());
                }
            }

            //vector m_Bones 2018 and up
        }
    }
}
