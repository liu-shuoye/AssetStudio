namespace AssetStudio
{
    /// <summary> 导出类型 </summary>
    public enum ExportType
    {
        /// <summary> 转换为Unity的AssetBundle格式 </summary>
        Convert,

        /// <summary> 导出为原始格式 </summary>
        Raw,

        /// <summary> 导出为文本 </summary>
        Dump,

        /// <summary> 导出为JSON </summary>
        JSON
    }
}