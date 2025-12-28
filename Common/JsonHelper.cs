using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace DynamicDbApi.Common
{
    /// <summary>
    /// JSON助手类，提供序列化和反序列化方法
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// 默认JSON序列化选项
        /// </summary>
        public static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
        {
            // 支持中文
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            // 美化输出
            WriteIndented = true,
            // 忽略循环引用
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            // 允许注释
            ReadCommentHandling = JsonCommentHandling.Skip,
            // 允许尾随逗号
            AllowTrailingCommas = true,
            // 设置日期时间格式
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // 忽略空值
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// 紧凑JSON序列化选项（无缩进）
        /// </summary>
        public static readonly JsonSerializerOptions CompactOptions = new JsonSerializerOptions
        {
            // 支持中文
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            // 紧凑输出
            WriteIndented = false,
            // 忽略循环引用
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            // 允许注释
            ReadCommentHandling = JsonCommentHandling.Skip,
            // 允许尾随逗号
            AllowTrailingCommas = true,
            // 设置日期时间格式
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // 忽略空值
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// JSON序列化
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">要序列化的对象</param>
        /// <param name="options">序列化选项</param>
        /// <returns>JSON字符串</returns>
        public static string Serialize<T>(T obj, JsonSerializerOptions? options = null)
        {
            if (obj == null)
                return string.Empty;

            return JsonSerializer.Serialize(obj, options ?? DefaultOptions);
        }

        /// <summary>
        /// 紧凑JSON序列化（无缩进）
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">要序列化的对象</param>
        /// <returns>紧凑的JSON字符串</returns>
        public static string SerializeCompact<T>(T obj)
        {
            return Serialize(obj, CompactOptions);
        }

        /// <summary>
        /// JSON反序列化
        /// </summary>
        /// <typeparam name="T">目标对象类型</typeparam>
        /// <param name="json">JSON字符串</param>
        /// <param name="options">反序列化选项</param>
        /// <returns>反序列化后的对象</returns>
        public static T? Deserialize<T>(string json, JsonSerializerOptions? options = null)
        {
            if (string.IsNullOrEmpty(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
        }

        /// <summary>
        /// 安全的JSON反序列化，带有异常处理
        /// </summary>
        /// <typeparam name="T">目标对象类型</typeparam>
        /// <param name="json">JSON字符串</param>
        /// <param name="result">反序列化后的对象</param>
        /// <param name="options">反序列化选项</param>
        /// <returns>是否反序列化成功</returns>
        public static bool TryDeserialize<T>(string json, out T? result, JsonSerializerOptions? options = null)
        {
            result = default;
            
            if (string.IsNullOrEmpty(json))
                return false;

            try
            {
                result = Deserialize<T>(json, options);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 将对象转换为JSON字节数组
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">要序列化的对象</param>
        /// <param name="options">序列化选项</param>
        /// <returns>JSON字节数组</returns>
        public static byte[] ToBytes<T>(T obj, JsonSerializerOptions? options = null)
        {
            if (obj == null)
                return Array.Empty<byte>();

            return JsonSerializer.SerializeToUtf8Bytes(obj, options ?? DefaultOptions);
        }

        /// <summary>
        /// 从JSON字节数组转换为对象
        /// </summary>
        /// <typeparam name="T">目标对象类型</typeparam>
        /// <param name="bytes">JSON字节数组</param>
        /// <param name="options">反序列化选项</param>
        /// <returns>反序列化后的对象</returns>
        public static T? FromBytes<T>(byte[] bytes, JsonSerializerOptions? options = null)
        {
            if (bytes == null || bytes.Length == 0)
                return default;

            return JsonSerializer.Deserialize<T>(bytes, options ?? DefaultOptions);
        }
    }
}