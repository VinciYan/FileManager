using System.Windows;

namespace FileManager.Views
{
    public partial class CreateFolderDialog : Window
    {
        public string FolderName { get; private set; } = string.Empty;

        public CreateFolderDialog()
        {
            InitializeComponent();
            FolderNameTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FolderNameTextBox.Text))
            {
                MessageBox.Show("请输入文件夹名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FolderName = FolderNameTextBox.Text.Trim();
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
} 