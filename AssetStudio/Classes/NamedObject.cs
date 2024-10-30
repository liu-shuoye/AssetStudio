using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    /// <summary> 名称对象 </summary>
    public class NamedObject : EditorExtension
    {
        /// <summary> 名称 </summary>
        protected string m_Name;

        public override string Name => m_Name;

        protected NamedObject(ObjectReader reader) : base(reader)
        {
            m_Name = reader.ReadAlignedString();
        }
    }
}