using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileManager.Models;
using FileManager.ViewModels;

namespace FileManager.Views
{
    public partial class TaskPanel : UserControl
    {
        private readonly TaskPanelViewModel _viewModel;

        public event EventHandler? CloseRequested;
        public event EventHandler<UploadLog>? RetryRequested;

        public TaskPanel()
        {
            InitializeComponent();
            _viewModel = new TaskPanelViewModel();
            DataContext = _viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ShowFailedOnly = FilterButton.IsChecked == true;
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is UploadLog log)
            {
                RetryRequested?.Invoke(this, log);
            }
        }

        private void MinioUrl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = textBlock.Text,
                    UseShellExecute = true
                });
            }
        }

        public void AddLog(UploadLog log)
        {
            _viewModel.AddLog(log);
        }

        public void UpdateLog(UploadLog log)
        {
            _viewModel.UpdateLog(log);
        }

        public void ClearLogs()
        {
            _viewModel.ClearLogs();
        }
    }
} 