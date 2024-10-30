using System.Collections.Generic;

namespace AssetStudio
{
    /// <summary> 资源管理器 </summary>
    public class ResourceManager : Object
    {
        public List<KeyValuePair<string, PPtr<Object>>> Container;

        public ResourceManager(ObjectReader reader) : base(reader)
        {
            var mContainerSize = reader.ReadInt32();
            Container = new List<KeyValuePair<string, PPtr<Object>>>();
            for (int i = 0; i < mContainerSize; i++)
            {
                Container.Add(new KeyValuePair<string, PPtr<Object>>(reader.ReadAlignedString(), new PPtr<Object>(reader)));
            }
        }
    }
}
