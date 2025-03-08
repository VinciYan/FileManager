using System.Windows;

namespace FileManager.Views
{
    public partial class CreateFileDialog : Window
    {
        public string FileName { get; private set; } = string.Empty;
        public string Extension { get; private set; } = string.Empty;
        public string MinioUrl { get; private set; } = string.Empty;
        public string Notes { get; private set; } = string.Empty;

        public CreateFileDialog()
        {
            InitializeComponent();
            FileNameTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FileNameTextBox.Text))
            {
                MessageBox.Show("请输入文件名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FileName = FileNameTextBox.Text.Trim();
            Extension = ExtensionTextBox.Text.Trim();
            MinioUrl = MinioUrlTextBox.Text.Trim();
            Notes = NotesTextBox.Text.Trim();

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
} 