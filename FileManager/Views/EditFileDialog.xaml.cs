using System.Windows;
using FileManager.Models;

namespace FileManager.Views
{
    public partial class EditFileDialog : Window
    {
        public string FileName { get; private set; }
        public string Extension { get; private set; }
        public string MinioUrl { get; private set; }
        public string Notes { get; private set; }
        public string DisplayMd5 { get; private set; }

        public EditFileDialog(FileItem fileItem)
        {
            InitializeComponent();

            // 初始化文件信息
            FileNameTextBox.Text = fileItem.Name;
            ExtensionTextBox.Text = fileItem.Extension;
            MinioUrlTextBox.Text = fileItem.MinioUrl;
            NotesTextBox.Text = fileItem.Notes;
            DisplayMd5TextBox.Text = fileItem.DisplayMd5;

            // 如果是文件夹，禁用后缀名输入
            if (fileItem.IsFolder)
            {
                ExtensionTextBox.IsEnabled = false;
                MinioUrlTextBox.IsEnabled = false;
                DisplayMd5TextBox.IsEnabled = false;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FileNameTextBox.Text))
            {
                MessageBox.Show("请输入名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FileName = FileNameTextBox.Text.Trim();
            Extension = ExtensionTextBox.Text.Trim();
            MinioUrl = MinioUrlTextBox.Text.Trim();
            Notes = NotesTextBox.Text.Trim();
            DisplayMd5 = DisplayMd5TextBox.Text.Trim();

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
} 