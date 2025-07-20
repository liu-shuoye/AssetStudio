﻿using AssetStudio;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    /// <summary> 游戏对象 </summary>
    public sealed class GameObject : EditorExtension
    {
        /// <summary> 层级 </summary>
        private int layer;
        /// <summary> 组件列表 </summary>
        public List<PPtr<Component>> m_Components;
        /// <summary> 名称 </summary>
        public string m_Name;

        public Transform m_Transform;
        /// <summary> 网格体渲染 </summary>
        public MeshRenderer m_MeshRenderer;
        /// <summary> 网格体过滤器 </summary>
        public MeshFilter m_MeshFilter;
        /// <summary> 蒙皮网格体渲染 </summary>
        public SkinnedMeshRenderer m_SkinnedMeshRenderer;
        /// <summary> 动画器 </summary>
        public Animator m_Animator;
        /// <summary> 动画 </summary>
        public Animation m_Animation;

        public override string Name => m_Name;

        public GameObject(ObjectReader reader) : base(reader)
        {
            int m_Component_size = reader.ReadInt32();
            m_Components = new List<PPtr<Component>>();
            for (int i = 0; i < m_Component_size; i++)
            {
                if ((version[0] == 5 && version[1] < 5) || version[0] < 5) //5.5 down
                {
                    int first = reader.ReadInt32();
                }

                m_Components.Add(new PPtr<Component>(reader));
            }

            layer = reader.ReadInt32();
            if (reader.IsTuanJie && reader.version[3] >= 13)
            {
                bool m_HasEditorInfo = reader.ReadBoolean();
                reader.AlignStream();
            }

            m_Name = reader.ReadAlignedString();
        }

        public bool HasModel() => HasMesh(m_Transform, new List<bool>());

        private static bool HasMesh(Transform m_Transform, List<bool> meshes)
        {
            try
            {
                m_Transform.m_GameObject.TryGet(out var m_GameObject);

                if (m_GameObject.m_MeshRenderer != null)
                {
                    var mesh = GetMesh(m_GameObject.m_MeshRenderer);
                    meshes.Add(mesh != null);
                }

                if (m_GameObject.m_SkinnedMeshRenderer != null)
                {
                    var mesh = GetMesh(m_GameObject.m_SkinnedMeshRenderer);
                    meshes.Add(mesh != null);
                }

                foreach (var pptr in m_Transform.m_Children)
                {
                    if (pptr.TryGet(out var child))
                        meshes.Add(HasMesh(child, meshes));
                }

                return meshes.Any(x => x == true);
            }
            catch (Exception e)
            {
                Logger.Warning($"无法验证 {m_Transform?.Name} 是否有网格，跳过...");
                return false;
            }
        }

        private static Mesh GetMesh(Renderer meshR)
        {
            if (meshR is SkinnedMeshRenderer sMesh)
            {
                if (sMesh.m_Mesh.TryGet(out var m_Mesh))
                {
                    return m_Mesh;
                }
            }
            else
            {
                meshR.m_GameObject.TryGet(out var m_GameObject);
                if (m_GameObject.m_MeshFilter != null)
                {
                    if (m_GameObject.m_MeshFilter.m_Mesh.TryGet(out var m_Mesh))
                    {
                        return m_Mesh;
                    }
                }
            }

            return null;
        }
    }
}