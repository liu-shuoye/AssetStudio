using System;
using System.Buffers.Binary;

namespace AssetStudio
{
    /// <summary>
    /// XORShift128是一种基于XORShift算法的随机数生成器，提供随机数生成相关方法。
    /// </summary>
    public static class XORShift128
    {
        /// <summary>
        /// 定义了一个常量SEED，用于初始化随机数生成器的种子。
        /// </summary>
        private const long SEED = 0x61C8864E7A143579;
        
        /// <summary>
        /// 定义了一个常量MT19937，它是MT19937算法中的一个常量，用于随机数生成。
        /// </summary>
        private const uint MT19937 = 0x6C078965;
        
        /// <summary>
        /// x、y、z、w是用于存储随机数生成状态的变量，initseed用于存储初始化种子。
        /// </summary>
        private static uint x = 0, y = 0, z = 0, w = 0, initseed = 0;
        
        /// <summary>
        /// 表示随机数生成器是否已初始化的标志。
        /// </summary>
        public static bool Init = false;

        /// <summary>
        /// 初始化随机数生成器。
        /// </summary>
        /// <param name="seed">初始化种子。</param>
        public static void InitSeed(uint seed)
        {
            initseed = seed;
            x = seed;
            y = MT19937 * x + 1;
            z = MT19937 * y + 1;
            w = MT19937 * z + 1;
            Init = true;
        }

        /// <summary>
        /// 执行XORShift算法生成随机数。
        /// </summary>
        /// <returns>返回生成的随机数。</returns>
        public static uint XORShift()
        {
            uint t = x ^ (x << 11);
            x = y; y = z; z = w;
            return w = w ^ (w >> 19) ^ t ^ (t >> 8);
        }

        /// <summary>
        /// 生成一个随机的uint值。
        /// </summary>
        /// <returns>返回生成的随机uint值。</returns>
        public static uint NextUInt32()
        {
            return XORShift();
        }

        /// <summary>
        /// 生成一个随机的int值，用于解密操作。
        /// </summary>
        /// <returns>返回生成的随机int值。</returns>
        public static int NextDecryptInt() => BinaryPrimitives.ReadInt32LittleEndian(NextDecrypt(4));

        /// <summary>
        /// 生成一个随机的uint值，用于解密操作。
        /// </summary>
        /// <returns>返回生成的随机uint值。</returns>
        public static uint NextDecryptUInt() => BinaryPrimitives.ReadUInt32LittleEndian(NextDecrypt(4));

        /// <summary>
        /// 生成一个随机的long值，用于解密操作。
        /// </summary>
        /// <returns>返回生成的随机long值。</returns>
        public static long NextDecryptLong() => BinaryPrimitives.ReadInt64LittleEndian(NextDecrypt(8));

        /// <summary>
        /// 生成指定长度的随机字节数组，用于解密操作。
        /// </summary>
        /// <param name="size">数组长度。</param>
        /// <returns>返回生成的随机字节数组。</returns>
        public static byte[] NextDecrypt(int size)
        {
            var valueBytes = new byte[size];
            var key = size * initseed - SEED;
            var keyBytes = BitConverter.GetBytes(key);
            for (int i = 0; i < size; i++)
            {
                var val = NextUInt32();
                valueBytes[i] = keyBytes[val % 8];
            }
            return valueBytes;
        }
    }

}
