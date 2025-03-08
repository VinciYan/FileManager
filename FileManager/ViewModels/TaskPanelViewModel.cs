using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FileManager.Models;

namespace FileManager.ViewModels
{
    public partial class TaskPanelViewModel : ObservableObject
    {
        private ObservableCollection<UploadLog> _allLogs = new();

        [ObservableProperty]
        private ObservableCollection<UploadLog> _logs = new();

        [ObservableProperty]
        private bool _showFailedOnly;

        partial void OnShowFailedOnlyChanged(bool value)
        {
            UpdateLogsList();
        }

        public void AddLog(UploadLog log)
        {
            _allLogs.Add(log);
            UpdateLogsList();
        }

        public void UpdateLog(UploadLog log)
        {
            var existingLog = _allLogs.FirstOrDefault(l => l.Id == log.Id);
            if (existingLog != null)
            {
                var index = _allLogs.IndexOf(existingLog);
                _allLogs[index] = log;
                UpdateLogsList();
            }
        }

        public void ClearLogs()
        {
            _allLogs.Clear();
            UpdateLogsList();
        }

        private void UpdateLogsList()
        {
            var filteredLogs = ShowFailedOnly
                ? _allLogs.Where(l => l.Status == "Failed")
                : _allLogs;

            Logs = new ObservableCollection<UploadLog>(filteredLogs);
        }
    }
} 