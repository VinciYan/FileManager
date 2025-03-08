using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FileManager.Utils
{
    public static class FileUtils
    {
        public static string CalculateMd5(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static string GetUniqueFileName(string md5Hash, string originalExtension)
        {
            return $"{md5Hash}{originalExtension}";
        }
    }
} 