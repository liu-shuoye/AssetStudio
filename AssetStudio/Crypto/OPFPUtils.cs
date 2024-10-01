using System;
using System.IO;
using System.Linq;

namespace AssetStudio
{
    public static class OPFPUtils
    {
        private static readonly string BaseFolder = "BundleResources";
        private static readonly string[] V0_Prefixes = { "UI/", "Atlas/", "UITexture/" };
        private static readonly string[] V1_Prefixes = { "DynamicAtlas/", "Atlas/Skill", "Atlas/PlayerTitle", "UITexture/HeroCardEP12", "UITexture/HeroCardEP13" };

        public static void Decrypt(Span<byte> data, string path)
        {
            Logger.Verbose($"正在尝试使用 OPFP 加密解密块...");
            if (IsEncryptionBundle(path, out var key, out var version))
            {
                switch (version)
                {
                    case 0:
                        data[0] ^= key;
                        for (int i = 1; i < data.Length; i++)
                        {
                            data[i] ^= data[i - 1];
                        }
                        break;
                    case 1:
                        for (int i = 1; i < data.Length; i++)
                        {
                            var idx = (i + data.Length + key * key) % (i + 1);
                            (data[i], data[idx]) = (data[idx], data[i]);
                            data[i] ^= key;
                            data[idx] ^= key;
                        }
                        break;
                }
            }
        }
        private static bool IsEncryptionBundle(string path, out byte key, out int version) 
        {
            if (IsFixedPath(path, out var relativePath))
            {
                if (V1_Prefixes.Any(prefix => relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    Logger.Verbose("路径与V1前缀匹配，正在生成密钥...");
                    key = (byte)Path.GetFileName(relativePath).Length;
                    version = 1;
                    Logger.Verbose($"版本: {version}, 密钥: {key}");
                    return true;
                }
                else if (V0_Prefixes.Any(prefix => relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    Logger.Verbose("路径与V2前缀匹配，正在生成密钥...");

                    key = (byte)relativePath.Length;
                    version = 0;
                    Logger.Verbose($"版本: {version}, 密钥: {key}");
                    return true;
                }
            }
            Logger.Verbose($"未知加密类型");
            key = 0x00;
            version = 0;
            return false;
        }
        private static bool IsFixedPath(string path, out string fixedPath)
        {
            Logger.Verbose($"修复路径后再检查...");
            var dirs = path.Split(Path.DirectorySeparatorChar);
            if (dirs.Contains(BaseFolder))
            {
                var idx = Array.IndexOf(dirs, BaseFolder);
                Logger.Verbose($"在索引 {idx} 处找到分隔符。");
                fixedPath = string.Join(Path.DirectorySeparatorChar, dirs[(idx+1)..]).Replace("\\", "/");
                return true;
            }
            Logger.Verbose($"未知路径");
            fixedPath = string.Empty;
            return false;
        }
    }
}
