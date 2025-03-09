using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Reactive.Linq;
using Minio.ApiEndpoints;
using System.Web;
using Minio.Handlers;

namespace FileManager.Services
{
    public class MinioService
    {
        private readonly IMinioClient _minioClient;
        private readonly string _bucketName;
        private readonly MinioConfig _config;
        public readonly MinioConfig Config;

        public MinioService(MinioConfig config)
        {
            _config = config;
            Config = config;
            _bucketName = config.BucketName;

            try
            {
                _minioClient = new MinioClient()
                    .WithEndpoint(config.Endpoint)
                    .WithCredentials(config.AccessKey, config.SecretKey)
                    .WithSSL(config.Secure)
                    .Build();

                // 确保 bucket 存在
                CheckAndCreateBucketAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw new Exception($"初始化 MinIO 客户端失败: {ex.Message}", ex);
            }
        }

        private async Task CheckAndCreateBucketAsync()
        {
            try
            {
                // 检查存储桶是否存在
                var beArgs = new BucketExistsArgs()
                    .WithBucket(_bucketName);
                bool found = await _minioClient.BucketExistsAsync(beArgs).ConfigureAwait(false);
                if (!found)
                {
                    // 创建存储桶
                    var mbArgs = new MakeBucketArgs()
                        .WithBucket(_bucketName);
                    await _minioClient.MakeBucketAsync(mbArgs).ConfigureAwait(false);

                    // 设置存储桶策略
                    var policy = $@"{{
                        ""Version"": ""2012-10-17"",
                        ""Statement"": [
                            {{
                                ""Action"": [""s3:GetObject""],
                                ""Effect"": ""Allow"",
                                ""Principal"": {{""AWS"": [""*""]}},
                                ""Resource"": [""arn:aws:s3:::{_bucketName}/*""],
                                ""Sid"": """"
                            }}
                        ]
                    }}";

                    var spArgs = new SetPolicyArgs()
                        .WithBucket(_bucketName)
                        .WithPolicy(policy);
                    await _minioClient.SetPolicyAsync(spArgs).ConfigureAwait(false);
                }
            }
            catch (MinioException ex)
            {
                throw new Exception($"确保存储桶存在时出错: {ex.Message}", ex);
            }
        }

        public async Task<(bool success, string url, string message)> UploadFileAsync(
            string filePath, 
            IProgress<int>? progress = null,
            string? uniqueFileName = null)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    return (false, string.Empty, "文件不存在");
                }

                // 生成对象名
                var objectName = uniqueFileName ?? $"{Guid.NewGuid()}/{fileInfo.Name}";

                // 设置上传参数
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName)
                    .WithFileName(filePath)
                    .WithContentType(GetMimeType(Path.GetExtension(filePath)));

                if (progress != null)
                {
                    var fileSize = fileInfo.Length;
                    putObjectArgs = putObjectArgs.WithProgress(new Progress<ProgressReport>(report =>
                    {
                        if (fileSize > 0)
                        {
                            var percentage = (int)((report.TotalBytesTransferred * 100) / fileSize);
                            progress.Report(Math.Min(percentage, 100));
                        }
                    }));
                }

                // 上传文件
                await _minioClient.PutObjectAsync(putObjectArgs).ConfigureAwait(false);

                // 验证文件是否成功上传
                try
                {
                    var statObjectArgs = new StatObjectArgs()
                        .WithBucket(_bucketName)
                        .WithObject(objectName);

                    var objStat = await _minioClient.StatObjectAsync(statObjectArgs).ConfigureAwait(false);

                    if (objStat.Size != fileInfo.Length)
                    {
                        throw new Exception($"文件大小不匹配：本地 {fileInfo.Length} 字节，服务器 {objStat.Size} 字节");
                    }

                    // 返回文件的URL
                    var url = $"{_bucketName}/{objectName}";
                    return (true, url, "上传成功，已验证文件完整性");
                }
                catch (MinioException ex)
                {
                    return (false, string.Empty, $"文件上传后验证失败: {ex.Message}");
                }
                // 返回文件的URL
                //var url = $"http://{_config.Endpoint}/{_bucketName}/{objectName}";
                //var url = $"{_bucketName}/{objectName}";
                //return (true, url, "上传成功");
            }
            catch (MinioException ex)
            {
                return (false, string.Empty, $"MinIO 错误: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                return (false, string.Empty, $"网络错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"上传失败: {ex.Message}");
            }
        }

        public async Task<(bool success, string message)> DeleteObjectAsync(string objectUrl)
        {
            try
            {
                // 从 URL 中提取对象名称
                var uri = new Uri(objectUrl);
                var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (pathSegments.Length < 2)
                {
                    return (false, "无效的 MinIO URL");
                }

                // 跳过 bucketName，直接使用后面的部分作为对象名，并进行 URL 解码
                var objectName = string.Join("/", pathSegments.Skip(1).Select(System.Web.HttpUtility.UrlDecode));
                
                // 检查对象是否存在
                var statObjectArgs = new StatObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName);

                try
                {
                    await _minioClient.StatObjectAsync(statObjectArgs);
                }
                catch (ObjectNotFoundException)
                {
                    return (false, "文件不存在");
                }

                try
                {
                    // 删除对象
                    await _minioClient.RemoveObjectAsync(
                        new RemoveObjectArgs()
                            .WithBucket(_bucketName)
                            .WithObject(objectName));

                    return (true, "删除成功");
                }
                catch (Exception ex)
                {
                    return (false, $"删除失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"删除失败: {ex.Message}");
            }
        }

        public async Task<(bool success, string message)> DeleteFolderAsync(string folderUrl)
        {
            try
            {
                // 从 URL 中提取文件夹路径
                var uri = new Uri(folderUrl);
                var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (pathSegments.Length < 2)
                {
                    return (false, "无效的 MinIO URL");
                }

                // 跳过 bucketName，直接使用后面的部分作为文件夹路径
                var folderPath = string.Join("/", pathSegments.Skip(1));
                if (!folderPath.EndsWith("/"))
                {
                    folderPath += "/";
                }

                var objectsToDelete = new List<string>();

                try
                {
                    // 获取所有对象
                    var listArgs = new ListObjectsArgs()
                        .WithBucket(_bucketName)
                        .WithPrefix(folderPath)
                        .WithRecursive(true);

                    await foreach (var item in _minioClient.ListObjectsEnumAsync(listArgs))
                    {
                        objectsToDelete.Add(item.Key);
                    }

                    if (!objectsToDelete.Any())
                    {
                        return (true, "文件夹为空或已经不存在");
                    }
                }
                catch (MinioException ex)
                {
                    return (false, $"列举对象失败: {ex.Message}");
                }

                // 删除所有对象
                foreach (var objectName in objectsToDelete)
                {
                    await _minioClient.RemoveObjectAsync(
                        new RemoveObjectArgs()
                            .WithBucket(_bucketName)
                            .WithObject(objectName));
                }

                return (true, $"成功删除文件夹及其中的 {objectsToDelete.Count} 个文件");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool success, string url, string message)> UploadLargeFileAsync(
            string filePath,
            IProgress<UploadProgress>? progress = null,
            string? uniqueFileName = null,
            int partSize = 30 * 1024 * 1024) // 默认分片大小为30MB
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    return (false, string.Empty, "文件不存在");
                }

                // 生成对象名
                var objectName = uniqueFileName ?? $"{Guid.NewGuid()}/{fileInfo.Name}";

                // 使用普通上传方法，但是通过进度回调来模拟分片上传的进度显示
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName)
                    .WithFileName(filePath)
                    .WithContentType(GetMimeType(Path.GetExtension(filePath)));

                if (progress != null)
                {
                    var fileSize = fileInfo.Length;
                    var totalParts = (int)Math.Ceiling((double)fileInfo.Length / partSize);
                    var progressReport = new Progress<ProgressReport>(report =>
                    {
                        var currentBytes = report.TotalBytesTransferred;
                        var currentPart = (int)(currentBytes / partSize) + 1;
                        progress.Report(new UploadProgress
                        {
                            TotalBytes = fileSize,
                            UploadedBytes = currentBytes,
                            PartNumber = Math.Min(currentPart, totalParts),
                            TotalParts = totalParts,
                            Percentage = (int)((double)currentBytes * 100 / fileSize)
                        });
                    });
                    putObjectArgs = putObjectArgs.WithProgress(progressReport);
                }

                // 上传文件
                await _minioClient.PutObjectAsync(putObjectArgs);

                // 验证文件是否成功上传
                try
                {
                    var statObjectArgs = new StatObjectArgs()
                        .WithBucket(_bucketName)
                        .WithObject(objectName);

                    var objStat = await _minioClient.StatObjectAsync(statObjectArgs).ConfigureAwait(false);

                    if (objStat.Size != fileInfo.Length)
                    {
                        throw new Exception($"文件大小不匹配：本地 {fileInfo.Length} 字节，服务器 {objStat.Size} 字节");
                    }

                    // 返回文件的URL
                    var url = $"{_bucketName}/{objectName}";
                    return (true, url, "上传成功，已验证文件完整性");
                }
                catch (MinioException ex)
                {
                    return (false, string.Empty, $"文件上传后验证失败: {ex.Message}");
                }
                // 返回文件的URL
                //var url = $"http://{_config.Endpoint}/{_bucketName}/{objectName}";
                //var url = $"{_bucketName}/{objectName}";
                //return (true, url, "上传成功");
            }
            catch (MinioException ex)
            {
                return (false, string.Empty, $"MinIO 错误: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                return (false, string.Empty, $"网络错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"上传失败: {ex.Message}");
            }
        }

        public class UploadProgress
        {
            public long TotalBytes { get; set; }
            public long UploadedBytes { get; set; }
            public int PartNumber { get; set; }
            public int TotalParts { get; set; }
            public int Percentage { get; set; }
        }

        private string GetMimeType(string extension)
        {
            return extension.ToLower() switch
            {
                ".txt" => "text/plain",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".7z" => "application/x-7z-compressed",
                _ => "application/octet-stream"
            };
        }
    }
} 