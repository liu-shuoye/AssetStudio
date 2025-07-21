using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AssetStudio.Utils;

public static class JsonUtils
{
    private static readonly Dictionary<string, JObject> JsonData = new();


    private const string PathHashDataFile = @"D:\Config\fieldHash\pathHashData.json"; //JSON文件路径
    private const string FieldHashDatafile = @"D:\Config\fieldHash\fieldHashData.json"; //JSON文件路径

    /// <summary>
    /// 从指定的JSON文件中读取数据并返回一个JObject对象。
    /// </summary>
    /// <param name="jsonfile">要读取的JSON文件的路径。</param>
    /// <returns>如果成功读取，则返回包含JSON数据的JObject对象；如果文件不存在或读取失败，则返回null。</returns>
    public static JObject ReadJson(string jsonfile)
    {
        if (JsonData.TryGetValue(jsonfile, out var hashData))
        {
            return hashData;
        }

        // 判断文件是否存在
        if (!File.Exists(jsonfile)) return null;

        using var file = File.OpenText(jsonfile);
        using var reader = new JsonTextReader(file);

        hashData = (JObject)JToken.ReadFrom(reader);
        JsonData[jsonfile] = hashData;
        return hashData;
    }

    /// <summary>
    /// 获取指定键对应的JSON值。
    /// </summary>
    /// <param name="key">要获取的键。</param>
    /// <param name="jsonfile">包含JSON数据的文件路径。</param>
    /// <returns>指定键对应的JSON值，如果不存在则返回"unknown_key"。</returns>
    public static string GetJsonValue(string key, string jsonfile)
    {
        var jObject = ReadJson(jsonfile);
        var unknown = $"unknown_{key}";
        if (jObject == null)
        {
            return unknown;
        }

        var value = jObject[key]?.ToString() ?? unknown;
        return value;
    }

    /// <summary>
    /// 根据给定的哈希值获取对应的字段名称。
    /// </summary>
    /// <param name="key">要查找的字段哈希值。</param>
    /// <returns>如果找到，则返回与哈希值关联的字段名称；如果未找到则返回"unknown_key"形式的字符串，其中key是原始哈希值。</returns>
    public static string GetFieldByHash(long key)
    {
        return GetJsonValue(key.ToString(), FieldHashDatafile);
    }

    /// <summary>
    /// 根据给定的哈希值获取对应的路径字符串。
    /// </summary>
    /// <param name="key">用于查找路径的哈希值。</param>
    /// <returns>如果找到，则返回与哈希值关联的路径字符串；如果未找到或键为0，则返回空字符串。</returns>
    public static string GetPathByHash(long key)
    {
        if (key == 0)
        {
            return string.Empty;
        }

        return GetJsonValue(key.ToString(), PathHashDataFile);
    }
}