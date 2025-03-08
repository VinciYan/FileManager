using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileManager.Models
{
    public enum OperationType
    {
        Upload = 0,
        Delete = 1
    }

    [Table("UploadLogs")]
    public class UploadLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string LocalPath { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string MinioUrl { get; set; } = string.Empty;

        public int Progress { get; set; }

        public bool IsRetryable { get; set; }

        [Required]
        [Column(TypeName = "INTEGER")]
        public OperationType Operation { get; set; }

        [NotMapped]
        public string OperationDisplay => Operation switch
        {
            OperationType.Upload => "上传",
            OperationType.Delete => "删除",
            _ => Operation.ToString()
        };

        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; }
    }
} 