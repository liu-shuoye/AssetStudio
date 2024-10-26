using System;
namespace AssetStudio
{
    public static class Progress
    {
        public static IProgress<int> Default = new Progress<int>();
        private static int preValue;

        public static void Reset()
        {
            preValue = 0;
            Default.Report(0);
        }

        public static void Report(int current, int total)
        {
            var value = (int)(current * 100f / total);
            Report(value);
        }

        private static void Report(int value)
        {
            preValue = value;
            Default.Report(value);
        }
    }
}
