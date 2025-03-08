namespace FileManager.Models
{
    public class PathItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsFirst { get; set; }

        public bool ShowSeparator => IsFirst;
    }
} 