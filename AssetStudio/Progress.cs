using System;
namespace AssetStudio
{
    /// <summary> 进度条组件 </summary>
    public static class Progress
    {
        public static bool Silent = false;
        /// <summary> 默认进度条 </summary>
        public static IProgress<int> Default = new Progress<int>();
        private static int preValue;

        /// <summary> 重置进度 </summary>
        public static void Reset()
        {
            if (!Silent)
            {
                preValue = 0;
                Default.Report(0);
            }
        }

        /// <summary>
        /// 更新进度
        /// </summary>
        /// <param name="current">当前进度</param>
        /// <param name="total">总进度</param>
        public static void Report(int current, int total)
        {
            if (!Silent)
            {
                var value = (int)(current * 100f / total);
                Report(value);
            }
        }

        private static void Report(int value)
        {
            if (value > preValue)
            {
                preValue = value;
                Default.Report(value);
            }
        }
    }
}
