using System;
using System.IO;
using System.Text.Json;

namespace FileManager.Services
{
    public class MinioConfig
    {
        public string Endpoint { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
        public bool Secure { get; set; }

        public static MinioConfig LoadConfig()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException("配置文件不存在", configPath);
                }

                var jsonString = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<JsonElement>(jsonString);
                var minioConfig = config.GetProperty("MinioConfig").Deserialize<MinioConfig>();

                if (minioConfig == null)
                {
                    throw new InvalidOperationException("无法读取MinIO配置信息");
                }

                // 验证必要的配置项
                if (string.IsNullOrEmpty(minioConfig.Endpoint))
                    throw new InvalidOperationException("MinIO Endpoint 不能为空");
                if (string.IsNullOrEmpty(minioConfig.AccessKey))
                    throw new InvalidOperationException("MinIO AccessKey 不能为空");
                if (string.IsNullOrEmpty(minioConfig.SecretKey))
                    throw new InvalidOperationException("MinIO SecretKey 不能为空");
                if (string.IsNullOrEmpty(minioConfig.BucketName))
                    throw new InvalidOperationException("MinIO BucketName 不能为空");

                return minioConfig;
            }
            catch (Exception ex)
            {
                throw new Exception($"加载MinIO配置时出错: {ex.Message}", ex);
            }
        }
    }
} 