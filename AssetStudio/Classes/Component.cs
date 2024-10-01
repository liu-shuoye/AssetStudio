using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    /// <summary>
    /// 抽象类Component，继承自EditorExtension，用于表示场景中的组件。
    /// 该类的主要作用是关联一个GameObject，并在构造过程中初始化这个关联。
    /// </summary>
    public abstract class Component : EditorExtension
    {
        /// <summary>
        /// 关联的GameObject指针。
        /// 这个字段存储了组件所属的GameObject的信息，用于在编辑器中进行各种操作和关联数据。
        /// </summary>
        public PPtr<GameObject> m_GameObject;

        /// <summary>
        /// 构造函数，用于初始化Component对象。
        /// 在对象构造时，通过ObjectReader读取并初始化关联的GameObject信息。
        /// </summary>
        /// <param name="reader">用于读取构造信息的ObjectReader对象。</param>
        protected Component(ObjectReader reader) : base(reader)
        {
            m_GameObject = new PPtr<GameObject>(reader);
        }
    }

}
