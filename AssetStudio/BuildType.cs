using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetStudio
{
    /// <summary> 表示构建类型的类 </summary>
    public class BuildType
    {
        // 定义构建类型的私有字段
        private string buildType;

        public BuildType(string type)
        {
            buildType = type;
        }

        /// <summary> 获取一个值，该值指示当前构建类型是否为Alpha版本 </summary>
        public bool IsAlpha => buildType == "a";

        /// <summary> 获取一个值，该值指示当前构建类型是否为补丁版本 </summary>
        public bool IsPatch => buildType == "p";
    }

}
