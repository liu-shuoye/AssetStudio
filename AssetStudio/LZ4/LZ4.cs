using System;

namespace AssetStudio;
/// <summary>
/// 表示LZ4压缩算法的工具类。
/// </summary>
public class LZ4
{
    /// <summary>
    /// 获取LZ4实例。
    /// </summary>
    public static LZ4 Instance => new();

    /// <summary>
    /// 解压缩数据。
    /// </summary>
    /// <param name="cmp">压缩的数据。</param>
    /// <param name="dec">解压缩后的数据。</param>
    /// <returns>解压缩后的数据长度。</returns>
    public virtual int Decompress(ReadOnlySpan<byte> cmp, Span<byte> dec)
    {
        int cmpPos = 0;
        int decPos = 0;

        do
        {
            var (encCount, litCount) = GetLiteralToken(cmp, ref cmpPos);

            //Copy literal chunk
            litCount = GetLength(litCount, cmp, ref cmpPos);

            cmp.Slice(cmpPos, litCount).CopyTo(dec.Slice(decPos));

            cmpPos += litCount;
            decPos += litCount;

            if (cmpPos >= cmp.Length)
            {
                break;
            }

            //Copy compressed chunk
            int back = GetChunkEnd(cmp, ref cmpPos);

            encCount = GetLength(encCount, cmp, ref cmpPos) + 4;

            int encPos = decPos - back;

            if (encCount <= back)
            {
                dec.Slice(encPos, encCount).CopyTo(dec.Slice(decPos));

                decPos += encCount;
            }
            else
            {
                while (encCount-- > 0)
                {
                    dec[decPos++] = dec[encPos++];
                }
            }
        } while (cmpPos < cmp.Length &&
                 decPos < dec.Length);

        return decPos;
    }

    /// <summary>
    /// 获取解压缩数据中的字面量令牌。
    /// </summary>
    /// <param name="cmp">压缩的数据。</param>
    /// <param name="cmpPos">压缩数据的当前位置。</param>
    /// <returns>返回编码数据块和字面量数据块的长度。</returns>
    protected virtual (int encCount, int litCount) GetLiteralToken(ReadOnlySpan<byte> cmp, ref int cmpPos) => ((cmp[cmpPos] >> 0) & 0xf, (cmp[cmpPos++] >> 4) & 0xf);

    /// <summary>
    /// 获取压缩数据块的结束位置。
    /// </summary>
    /// <param name="cmp">压缩的数据。</param>
    /// <param name="cmpPos">压缩数据的当前位置。</param>
    /// <returns>返回压缩数据块的结束位置。</returns>
    protected virtual int GetChunkEnd(ReadOnlySpan<byte> cmp, ref int cmpPos) => cmp[cmpPos++] << 0 | cmp[cmpPos++] << 8;

    /// <summary>
    /// 获取数据块的实际长度。
    /// </summary>
    /// <param name="length">初始长度。</param>
    /// <param name="cmp">压缩的数据。</param>
    /// <param name="cmpPos">压缩数据的当前位置。</param>
    /// <returns>返回数据块的实际长度。</returns>
    protected virtual int GetLength(int length, ReadOnlySpan<byte> cmp, ref int cmpPos)
    {
        byte sum;

        if (length == 0xf)
        {
            do
            {
                length += sum = cmp[cmpPos++];
            } while (sum == 0xff);
        }

        return length;
    }
}
