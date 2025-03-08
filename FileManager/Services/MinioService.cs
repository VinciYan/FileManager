using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace FileManager.Services
{
    public class MinioService
    {
        private readonly IMinioClient _minioClient;
        private readonly string _bucketName;
        private readonly MinioConfig _config;

        public MinioService(MinioConfig config)
        {
            _minioClient = new MinioClient()
                .WithEndpoint(config.Endpoint)
                .WithCredentials(config.AccessKey, config.SecretKey)
                .WithSSL(config.Secure)
                .Build();

            _bucketName = config.BucketName;
            _config = config;
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
                    var progressReport = new Progress<ProgressReport>(report =>
                    {
                        var percentage = (int)((report.TotalBytesTransferred * 100) / fileSize);
                        progress.Report(Math.Min(percentage, 100));
                    });
                    putObjectArgs = putObjectArgs.WithProgress(progressReport);
                }

                // 上传文件
                await _minioClient.PutObjectAsync(putObjectArgs);

                // 返回文件的URL
                var url = $"http://{_config.Endpoint}/{_bucketName}/{objectName}";                
                return (true, url, "上传成功");
            }
            catch (Exception ex)
            {
                return (false, string.Empty, ex.Message);
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

                // 跳过 bucketName，直接使用后面的部分作为对象名
                var objectName = string.Join("/", pathSegments.Skip(1));
                
                // 检查对象是否存在
                var statObjectArgs = new StatObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName);

                try
                {
                    await _minioClient.StatObjectAsync(statObjectArgs);
                }
                catch (Minio.Exceptions.ObjectNotFoundException)
                {
                    return (true, "文件已经不存在");
                }

                // 删除对象
                await _minioClient.RemoveObjectAsync(
                    new RemoveObjectArgs()
                        .WithBucket(_bucketName)
                        .WithObject(objectName));

                return (true, "删除成功");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
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

                // 跳过 document 目录，直接使用后面的部分作为文件夹路径
                var folderPath = string.Join("/", pathSegments.SkipWhile(x => x != "document").Skip(1));
                if (!folderPath.EndsWith("/"))
                {
                    folderPath += "/";
                }

                // 列出文件夹中的所有对象
                var listArgs = new ListObjectsArgs()
                    .WithBucket(_bucketName)
                    .WithPrefix(folderPath)
                    .WithRecursive(true);

                var objectsToDelete = new List<string>();
                var tcs = new TaskCompletionSource<bool>();
                var listObjects = _minioClient.ListObjectsAsync(listArgs);
                
                listObjects.Subscribe(
                    item => objectsToDelete.Add(item.Key),
                    error => tcs.SetException(error),
                    () => tcs.SetResult(true));

                await tcs.Task;

                if (!objectsToDelete.Any())
                {
                    return (true, "文件夹为空或已经不存在");
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