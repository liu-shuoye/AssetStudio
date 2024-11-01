﻿namespace AssetStudio
{
    /// <summary> 脚本文件 </summary>
    public sealed class MonoBehaviour : Behaviour
    {
        public PPtr<MonoScript> m_Script;
        public string m_Name;

        public override string Name => string.IsNullOrEmpty(m_Name) ? m_Script.Name : m_Name;

        public MonoBehaviour(ObjectReader reader) : base(reader)
        {
            m_Script = new PPtr<MonoScript>(reader);
            m_Name = reader.ReadAlignedString();
        }
    }
}