using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FileManager.Models;
using FileManager.ViewModels;

namespace FileManager.Views
{
    public partial class FileListView : UserControl
    {
        public event EventHandler<FileItem>? ItemDoubleClicked;

        public FileListView()
        {
            InitializeComponent();
        }

        private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ItemsListView.SelectedItem is FileItem selectedItem)
            {
                ItemDoubleClicked?.Invoke(this, selectedItem);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.NavigateBackCommand.Execute(null);
            }
        }

        private void PathButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is MainViewModel viewModel)
            {
                if (button.Tag is int folderId)
                {
                    viewModel.NavigateToFolderCommand.Execute(folderId);
                }
            }
        }

        private void CopyPathButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.CopyCurrentPathCommand.Execute(null);
            }
        }

        private void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.CreateFolderCommand.Execute(null);
            }
        }

        private void CreateFile_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.CreateFileCommand.Execute(null);
            }
        }

        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListView.SelectedItem is FileItem selectedItem && DataContext is MainViewModel viewModel)
            {
                var dialog = new EditFileDialog(selectedItem)
                {
                    Owner = Window.GetWindow(this)
                };

                if (dialog.ShowDialog() == true)
                {
                    selectedItem.Name = dialog.FileName;
                    selectedItem.Extension = dialog.Extension;
                    selectedItem.MinioUrl = dialog.MinioUrl;
                    selectedItem.Notes = dialog.Notes;
                    selectedItem.DisplayMd5 = dialog.DisplayMd5;
                    selectedItem.UpdatedAt = DateTime.Now;

                    viewModel.UpdateItem(selectedItem);
                }
            }
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                var selectedItems = ItemsListView.SelectedItems.Cast<FileItem>().ToList();
                if (selectedItems.Any())
                {
                    var message = selectedItems.Count == 1
                        ? $"确定要删除 {selectedItems[0].Name} 吗？"
                        : $"确定要删除选中的 {selectedItems.Count} 个项目吗？";

                    var result = MessageBox.Show(message, "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        viewModel.DeleteItems(selectedItems);
                    }
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.RefreshCommand.Execute(null);
            }
        }

        private void CopyItems_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                var selectedItems = ItemsListView.SelectedItems.Cast<FileItem>().ToList();
                viewModel.CopyItemsCommand.Execute(selectedItems);
            }
        }

        private void PasteItems_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.PasteItemsCommand.Execute(null);
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            ItemsListView.SelectAll();
        }

        private void ItemsListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    ItemsListView.SelectAll();
                    e.Handled = true;
                }
                else if (e.Key == Key.X && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    var selectedItems = ItemsListView.SelectedItems.Cast<FileItem>().ToList();
                    viewModel.CutItemsCommand.Execute(selectedItems);
                    e.Handled = true;
                }
                else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    var selectedItems = ItemsListView.SelectedItems.Cast<FileItem>().ToList();
                    viewModel.CopyItemsCommand.Execute(selectedItems);
                    e.Handled = true;
                }
                else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    viewModel.PasteItemsCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void ItemsListView_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void ItemsListView_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private async void ItemsListView_Drop(object sender, DragEventArgs e)
        {
            // 处理从外部拖入的文件
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (DataContext is MainViewModel vm)
                {
                    await vm.HandleFilesDroppedAsync(files);
                }
            }
        }

        private void CopyMinioUrl_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListView.SelectedItem is FileItem selectedItem && !string.IsNullOrEmpty(selectedItem.MinioUrl))
            {
                try
                {
                    Clipboard.SetText(selectedItem.MinioUrl);
                    MessageBox.Show("MinIO链接已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"复制MinIO链接时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void SetViewMode(bool isDetailView)
        {
            ItemsListView.View = isDetailView ? ItemsListView.View : null;
        }

        private void CutItems_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                var selectedItems = ItemsListView.SelectedItems.Cast<FileItem>().ToList();
                viewModel.CutItemsCommand.Execute(selectedItems);
            }
        }
    }
} 