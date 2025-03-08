using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FileManager.Models
{
    public class FileItem
    {
        private static readonly BitmapImage FolderIcon = new(new Uri("pack://application:,,,/Images/folder.png"));
        private static readonly BitmapImage FileIcon = new(new Uri("pack://application:,,,/Images/file.png"));

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string MinioUrl { get; set; } = string.Empty;
        public string Md5Hash { get; set; } = string.Empty;
        public string DisplayMd5 { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsFolder { get; set; }
        public int? ParentId { get; set; }
        public virtual FileItem? Parent { get; set; }
        public virtual ICollection<FileItem> Children { get; set; } = new List<FileItem>();

        public ImageSource IconSource => IsFolder ? FolderIcon : FileIcon;
    }
} 