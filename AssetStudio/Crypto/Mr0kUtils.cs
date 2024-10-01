using System;
using System.Buffers;
using System.Buffers.Binary;

namespace AssetStudio
{
    /// <summary>
    /// 提供与Mr0k加密相关的工具方法。
    /// </summary>
    public static class Mr0kUtils
    {
        /// <summary>
        /// 定义块大小常量。
        /// </summary>
        private const int BlockSize = 0x400;

        /// <summary>
        /// 定义Mr0k标志字节序列，用于识别Mr0k加密数据。
        /// </summary>
        private static readonly byte[] mr0kMagic = { 0x6D, 0x72, 0x30, 0x6B };

        /// <summary>
        /// 解密Mr0k加密的数据。
        /// </summary>
        /// <param name="data">待解密的数据段。</param>
        /// <param name="mr0k">包含Mr0k加密相关信息的实例。</param>
        /// <returns>解密后的数据段。</returns>
        public static Span<byte> Decrypt(Span<byte> data, Mr0k mr0k)
        {
            // 初始化三个密钥数组
            var key1 = new byte[0x10];
            var key2 = new byte[0x10];
            var key3 = new byte[0x10];

            // 从数据中提取密钥
            data.Slice(4, 0x10).CopyTo(key1);
            data.Slice(0x74, 0x10).CopyTo(key2);
            data.Slice(0x84, 0x10).CopyTo(key3);

            // 计算加密块的大小
            var encryptedBlockSize = Math.Min(0x10 * ((data.Length - 0x94) >> 7), BlockSize);

            // 日志输出加密块大小
            Logger.Verbose($"加密块大小: {encryptedBlockSize}");

            // 如果初始向量不为空，将其与key2进行异或操作
            if (!mr0k.InitVector.IsNullOrEmpty())
            {
                for (int i = 0; i < mr0k.InitVector.Length; i++)
                    key2[i] ^= mr0k.InitVector[i];
            }

            // 如果S盒不为空，根据S盒规则更新key1
            if (!mr0k.SBox.IsNullOrEmpty())
            {
                for (int i = 0; i < 0x10; i++)
                    key1[i] = mr0k.SBox[(i % 4 * 0x100) | key1[i]];
            }

            // 使用AES解密key1和key3
            AES.Decrypt(key1, mr0k.ExpansionKey);
            AES.Decrypt(key3, mr0k.ExpansionKey);

            // 将解密后的key1和key3进行异或操作
            for (int i = 0; i < key1.Length; i++)
            {
                key1[i] ^= key3[i];
            }

            // 将处理后的key1复制回数据中的相应位置
            key1.CopyTo(data.Slice(0x84, 0x10));

            // 从key2和key3中分别提取出64位的种子，并计算最终的种子值
            var seed1 = BinaryPrimitives.ReadUInt64LittleEndian(key2);
            var seed2 = BinaryPrimitives.ReadUInt64LittleEndian(key3);
            var seed = seed2 ^ seed1 ^ (seed1 + (uint)data.Length - 20);

            // 日志输出种子值
            Logger.Verbose($"种子: 0x{seed:X8}");

            // 对加密块进行解密操作
            var encryptedBlock = data.Slice(0x94, encryptedBlockSize);
            var seedSpan = BitConverter.GetBytes(seed);
            for (var i = 0; i < encryptedBlockSize; i++)
            {
                encryptedBlock[i] ^= (byte)(seedSpan[i % seedSpan.Length] ^ mr0k.BlockKey[i % mr0k.BlockKey.Length]);
            }

            // 移除解密过程中的前导数据
            data = data[0x14..];

            // 如果后处理密钥不为空，使用其对数据进行异或操作
            if (!mr0k.PostKey.IsNullOrEmpty())
            {
                for (int i = 0; i < 0xC00; i++)
                {
                    data[i] ^= mr0k.PostKey[i % mr0k.PostKey.Length];
                }
            }

            // 返回解密后的数据段
            return data;
        }

        /// <summary>
        /// 检查数据是否为Mr0k加密格式。
        /// </summary>
        /// <param name="data">待检查的数据段。</param>
        /// <returns>如果数据段以Mr0k标志字节序列开头，返回true；否则返回false。</returns>
        public static bool IsMr0k(ReadOnlySpan<byte> data) => data[..4].SequenceEqual(mr0kMagic);
    }
}
