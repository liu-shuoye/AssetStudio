namespace AssetStudio
{
    /// <summary>
    /// 资产分组选项枚举，用于指定导出资产时的分组方式。
    /// </summary>
    public enum AssetGroupOption
    {
        /// <summary>
        /// 按类型分组。选择此选项时，导出的资产将根据其类型进行分组，每种类型的资产都会被放置在以其类型命名的文件夹中。
        /// </summary>
        ByType,

        /// <summary>
        /// 按容器分组。选择此选项时，导出的资产将根据其所在容器路径进行分组，每组资产都会被放置在其容器路径命名的文件夹中。
        /// </summary>
        ByContainer,

        /// <summary>
        /// 按来源分组。选择此选项时，导出的资产将根据其来源进行分组，来自同一源（如相同的文件或容器）的资产会被放置在以其来源命名的文件夹中。
        /// </summary>
        BySource,

        Custom,
        
        None
    }
}