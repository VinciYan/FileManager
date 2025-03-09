using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileManager.Data;
using FileManager.Models;
using FileManager.Views;
using FileManager.Services;
using FileManager.Utils;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using static FileManager.Services.MinioService;

namespace FileManager.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly FileManagerDbContext _dbContext;
        private readonly FileListView _fileListView;
        private readonly MinioService _minioService;
        private List<FileItem>? _copiedItems;
        private bool _isCutOperation;

        [ObservableProperty]
        private ObservableCollection<FileItem> _folderStructure = new();

        [ObservableProperty]
        private ObservableCollection<FileItem> _currentFolderItems = new();

        [ObservableProperty]
        private ObservableCollection<PathItem> _pathItems = new();

        [ObservableProperty]
        private bool _isDetailView;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private FileItem? _selectedFolder;

        [ObservableProperty]
        private bool _showTaskPanel;

        [ObservableProperty]
        private TaskPanel? _taskPanel;

        public MainViewModel(FileManagerDbContext dbContext)
        {
            _dbContext = dbContext;
            _fileListView = new FileListView { DataContext = this };
            _fileListView.ItemDoubleClicked += FileListView_ItemDoubleClicked;
            
            try
            {
                var config = MinioConfig.LoadConfig();
                _minioService = new MinioService(config);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载MinIO配置时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
            
            // 初始化任务面板
            TaskPanel = new TaskPanel();
            TaskPanel.CloseRequested += (s, e) => ShowTaskPanel = false;
            TaskPanel.RetryRequested += TaskPanel_RetryRequested;
            
            CurrentView = _fileListView;
            InitializeData();
        }
        public MinioConfig GetMinioConfig()
        {
            return _minioService.Config;
        }

        private async void TaskPanel_RetryRequested(object? sender, UploadLog log)
        {
            ShowTaskPanel = true;
            await UploadFileAsync(log.LocalPath);
        }

        public async Task HandleFilesDroppedAsync(string[] files)
        {
            if (!ShowTaskPanel)
            {
                ShowTaskPanel = true;
                TaskPanel?.ClearLogs();
            }

            foreach (var filePath in files)
            {
                if (Directory.Exists(filePath))
                {
                    // 处理文件夹
                    await HandleDroppedFolderAsync(filePath);
                }
                else
                {
                    // 处理文件
                    await HandleDroppedFileAsync(filePath);
                }
            }

            // 刷新当前文件夹的内容
            LoadCurrentFolderItems(SelectedFolder?.Id);
        }

        private async Task HandleDroppedFolderAsync(string folderPath)
        {
            var folderName = Path.GetFileName(folderPath);
            var newFolder = new FileItem
            {
                Name = folderName,
                IsFolder = true,
                ParentId = SelectedFolder?.Id,
                Path = SelectedFolder == null ? folderName : Path.Combine(SelectedFolder.Path, folderName),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _dbContext.FileItems.Add(newFolder);
            await _dbContext.SaveChangesAsync();

            // 递归处理子文件和文件夹
            foreach (var filePath in Directory.GetFiles(folderPath))
            {
                await HandleDroppedFileAsync(filePath, newFolder);
            }

            foreach (var subFolderPath in Directory.GetDirectories(folderPath))
            {
                await HandleDroppedFolderAsync(subFolderPath);
            }

            LoadFolderStructure();
            LoadCurrentFolderItems(SelectedFolder?.Id);
        }

        private async Task HandleDroppedFileAsync(string filePath, FileItem? parentFolder = null)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var originalFileName = fileName + extension;

            // 计算文件的MD5值
            var md5Hash = FileUtils.CalculateMd5(filePath);

            var retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    // 检查是否存在相同MD5的文件
                    var existingFile = await _dbContext.FileItems
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => !x.IsFolder && x.Md5Hash == md5Hash);

                    var log = new UploadLog
                    {
                        LocalPath = filePath,
                        Status = existingFile != null ? "Skipped" : "Uploading",
                        Progress = 0,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        IsRetryable = existingFile == null,
                        Operation = OperationType.Upload
                    };

                    _dbContext.UploadLogs.Add(log);
                    await _dbContext.SaveChangesAsync();
                    TaskPanel?.AddLog(log);

                    if (existingFile != null)
                    {
                        // 文件已存在，创建新的文件记录但使用现有的MinIO URL
                        var newFile = new FileItem
                        {
                            Name = fileName,
                            Extension = extension,
                            IsFolder = false,
                            ParentId = parentFolder?.Id ?? SelectedFolder?.Id,
                            Path = parentFolder == null ? 
                                (SelectedFolder == null ? fileName : Path.Combine(SelectedFolder.Path, fileName)) :
                                Path.Combine(parentFolder.Path, fileName),
                            MinioUrl = existingFile.MinioUrl,
                            Md5Hash = md5Hash,
                            DisplayMd5 = md5Hash,
                            Notes = string.Empty,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };

                        _dbContext.FileItems.Add(newFile);
                        await _dbContext.SaveChangesAsync();

                        log.Status = "Success";
                        log.Message = "文件已存在，跳过上传";
                        log.MinioUrl = existingFile.MinioUrl;
                        log.Progress = 100;
                        log.IsRetryable = false;

                        await _dbContext.SaveChangesAsync();
                        TaskPanel?.UpdateLog(log);
                        
                        // 刷新当前文件夹的内容
                        LoadCurrentFolderItems(SelectedFolder?.Id);
                        return;
                    }

                    // 使用MD5值作为文件夹，原始文件名作为文件名
                    var minioPath = $"{md5Hash}/{originalFileName}";
                    var fileInfo = new FileInfo(filePath);
                    var (success, url, message) = fileInfo.Length > 10 * 1024 * 1024 // 大于10MB的文件使用分片上传
                        ? await _minioService.UploadLargeFileAsync(
                            filePath,
                            new Progress<UploadProgress>(progress =>
                            {
                                log.Progress = progress.Percentage;
                                log.Message = $"正在上传第 {progress.PartNumber}/{progress.TotalParts} 个分片";
                                TaskPanel?.UpdateLog(log);
                            }),
                            minioPath)
                        : await _minioService.UploadFileAsync(
                            filePath,
                            new Progress<int>(percent =>
                            {
                                log.Progress = percent;
                                log.Message = $"已上传 {percent}%";
                                TaskPanel?.UpdateLog(log);
                            }),
                            minioPath);

                    if (success)
                    {
                        var newFile = new FileItem
                        {
                            Name = fileName,
                            Extension = extension,
                            IsFolder = false,
                            ParentId = parentFolder?.Id ?? SelectedFolder?.Id,
                            Path = parentFolder == null ? 
                                (SelectedFolder == null ? fileName : Path.Combine(SelectedFolder.Path, fileName)) :
                                Path.Combine(parentFolder.Path, fileName),
                            MinioUrl = url,
                            Md5Hash = md5Hash,
                            DisplayMd5 = md5Hash,
                            Notes = string.Empty,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };

                        _dbContext.FileItems.Add(newFile);
                        await _dbContext.SaveChangesAsync();

                        log.Status = "Success";
                        log.Message = "上传成功";
                        log.MinioUrl = url;
                        log.Progress = 100;
                        log.IsRetryable = false;
                    }
                    else
                    {
                        log.Status = "Failed";
                        log.Message = message;
                        log.IsRetryable = true;
                    }

                    log.UpdatedAt = DateTime.Now;
                    await _dbContext.SaveChangesAsync();
                    TaskPanel?.UpdateLog(log);

                    // 刷新当前文件夹的内容
                    LoadCurrentFolderItems(SelectedFolder?.Id);
                    return; // 成功完成，退出重试循环
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        MessageBox.Show($"数据更新冲突，已重试{maxRetries}次仍然失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    await Task.Delay(100 * retryCount); // 每次重试增加延迟
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"上传文件时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
        }

        private async Task UploadFileAsync(string filePath)
        {
            await HandleDroppedFileAsync(filePath);
        }

        [RelayCommand]
        private void CopyItems(List<FileItem> items)
        {
            if (items.Any())
            {
                _copiedItems = items;
                _isCutOperation = false;
            }
        }

        [RelayCommand]
        private void CutItems(List<FileItem> items)
        {
            if (items.Any())
            {
                _copiedItems = items;
                _isCutOperation = true;
            }
        }

        [RelayCommand]
        private void PasteItems()
        {
            if (_copiedItems == null || !_copiedItems.Any())
                return;

            try
            {
                if (_isCutOperation)
                {
                    // 移动操作
                    if (SelectedFolder != null)
                    {
                        MoveItems(_copiedItems, SelectedFolder);
                    }
                    else
                    {
                        // 移动到根目录
                        foreach (var item in _copiedItems)
                        {
                            item.ParentId = null;
                            item.Path = item.Name;
                            item.UpdatedAt = DateTime.Now;
                            _dbContext.Update(item);
                        }
                        _dbContext.SaveChanges();
                        LoadFolderStructure();
                        LoadCurrentFolderItems(null);
                    }
                    _copiedItems = null; // 清除剪切板
                }
                else
                {
                    // 复制操作
                    if (SelectedFolder != null)
                    {
                        foreach (var sourceItem in _copiedItems)
                        {
                            CopyFileItemRecursively(sourceItem, SelectedFolder);
                        }
                    }
                    else
                    {
                        // 复制到根目录
                        foreach (var sourceItem in _copiedItems)
                        {
                            CopyFileItemToRoot(sourceItem);
                        }
                    }
                    _dbContext.SaveChanges();
                    LoadFolderStructure();
                    LoadCurrentFolderItems(SelectedFolder?.Id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"粘贴项目时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyFileItemToRoot(FileItem sourceItem)
        {
            // 创建新的文件项
            var newItem = new FileItem
            {
                Name = sourceItem.Name,
                Extension = sourceItem.Extension,
                IsFolder = sourceItem.IsFolder,
                ParentId = null,
                Path = sourceItem.Name,
                Notes = sourceItem.Notes,
                MinioUrl = sourceItem.MinioUrl,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _dbContext.FileItems.Add(newItem);
            _dbContext.SaveChanges(); // 保存以获取新项目的 Id

            // 如果是文件夹，递归复制其子项
            if (sourceItem.IsFolder)
            {
                var childItems = _dbContext.FileItems
                    .Where(x => x.ParentId == sourceItem.Id)
                    .ToList();

                foreach (var childItem in childItems)
                {
                    CopyFileItemRecursively(childItem, newItem);
                }
            }
        }

        private void CopyFileItemRecursively(FileItem sourceItem, FileItem targetFolder)
        {
            // 创建新的文件项
            var newItem = new FileItem
            {
                Name = sourceItem.Name,
                Extension = sourceItem.Extension,
                IsFolder = sourceItem.IsFolder,
                ParentId = targetFolder.Id,
                Path = System.IO.Path.Combine(targetFolder.Path, sourceItem.Name),
                Notes = sourceItem.Notes,
                MinioUrl = sourceItem.MinioUrl,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _dbContext.FileItems.Add(newItem);
            _dbContext.SaveChanges(); // 保存以获取新项目的 Id

            // 如果是文件夹，递归复制其子项
            if (sourceItem.IsFolder)
            {
                var childItems = _dbContext.FileItems
                    .Where(x => x.ParentId == sourceItem.Id)
                    .ToList();

                foreach (var childItem in childItems)
                {
                    CopyFileItemRecursively(childItem, newItem);
                }
            }
        }

        private void InitializeData()
        {
            try
            {
                LoadFolderStructure();
            }
            catch (Exception ex)
            {
                // 如果加载失败，至少初始化一个空的集合
                FolderStructure = new ObservableCollection<FileItem>();
                MessageBox.Show($"加载文件结构时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFolderStructure()
        {
            var rootItems = _dbContext.FileItems
                .Include(x => x.Children)
                .Where(x => x.ParentId == null)
                .ToList();

            FolderStructure = new ObservableCollection<FileItem>(rootItems);
            LoadCurrentFolderItems(null);
            UpdatePathItems(null);
        }

        private void LoadCurrentFolderItems(int? parentId)
        {
            var items = _dbContext.FileItems
                .Where(x => x.ParentId == parentId)
                .OrderByDescending(x => x.UpdatedAt)  // 仅按修改日期降序
                .ThenBy(x => x.Name)  // 同一时间的按名称升序
                .ToList();

            CurrentFolderItems = new ObservableCollection<FileItem>(items);
            UpdatePathItems(parentId);
        }

        private void UpdatePathItems(int? currentFolderId)
        {
            var pathItems = new List<PathItem>();
            pathItems.Add(new PathItem { Id = 0, Name = "根目录", IsFirst = false });

            if (currentFolderId.HasValue)
            {
                var currentFolder = _dbContext.FileItems.Find(currentFolderId.Value);
                var parentFolders = new List<FileItem>();

                while (currentFolder != null)
                {
                    parentFolders.Insert(0, currentFolder);
                    currentFolder = _dbContext.FileItems.Find(currentFolder.ParentId);
                }

                foreach (var folder in parentFolders)
                {
                    pathItems.Add(new PathItem 
                    { 
                        Id = folder.Id, 
                        Name = folder.Name,
                        IsFirst = true 
                    });
                }
            }

            PathItems = new ObservableCollection<PathItem>(pathItems);
        }

        private void FileListView_ItemDoubleClicked(object? sender, FileItem item)
        {
            if (item.IsFolder)
            {
                SelectedFolder = item;
                LoadCurrentFolderItems(item.Id);
            }
        }

        [RelayCommand]
        private void NavigateToFolder(int folderId)
        {
            if (folderId == 0)
            {
                SelectedFolder = null;
                LoadCurrentFolderItems(null);
            }
            else
            {
                var folder = _dbContext.FileItems.Find(folderId);
                if (folder != null)
                {
                    SelectedFolder = folder;
                    LoadCurrentFolderItems(folder.Id);
                }
            }
        }

        [RelayCommand]
        private void NavigateBack()
        {
            if (SelectedFolder?.ParentId != null)
            {
                NavigateToFolder(SelectedFolder.ParentId.Value);
            }
            else
            {
                NavigateToFolder(0);
            }
        }

        [RelayCommand]
        private void CopyCurrentPath()
        {
            var path = string.Join("\\", PathItems.Select(x => x.Name));
            try
            {
                Clipboard.SetText(path);
                MessageBox.Show("路径已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制路径时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CreateFolder()
        {
            var dialog = new CreateFolderDialog
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var newFolder = new FileItem
                    {
                        Name = dialog.FolderName,
                        IsFolder = true,
                        ParentId = SelectedFolder?.Id,
                        Path = SelectedFolder == null ? dialog.FolderName : System.IO.Path.Combine(SelectedFolder.Path, dialog.FolderName),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _dbContext.FileItems.Add(newFolder);
                    _dbContext.SaveChanges();

                    // 刷新文件夹结构和当前文件夹内容
                    LoadFolderStructure();
                    LoadCurrentFolderItems(SelectedFolder?.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建文件夹时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void CreateFile()
        {
            var dialog = new CreateFileDialog
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var newFile = new FileItem
                    {
                        Name = dialog.FileName,
                        Extension = dialog.Extension,
                        IsFolder = false,
                        ParentId = SelectedFolder?.Id,
                        Path = SelectedFolder == null ? dialog.FileName : System.IO.Path.Combine(SelectedFolder.Path, dialog.FileName),
                        Notes = dialog.Notes,
                        MinioUrl = dialog.MinioUrl,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _dbContext.FileItems.Add(newFile);
                    _dbContext.SaveChanges();

                    // 刷新文件夹结构和当前文件夹内容
                    LoadFolderStructure();
                    LoadCurrentFolderItems(SelectedFolder?.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建文件时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void UpdateItem(FileItem item)
        {
            try
            {
                _dbContext.Update(item);
                _dbContext.SaveChanges();
                LoadFolderStructure();
                LoadCurrentFolderItems(SelectedFolder?.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新项目时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async void DeleteItems(List<FileItem> items)
        {
            try
            {
                // 显示任务面板
                if (!ShowTaskPanel)
                {
                    ShowTaskPanel = true;
                    TaskPanel?.ClearLogs();
                }

                foreach (var item in items)
                {
                    // 创建删除日志
                    var log = new UploadLog
                    {
                        LocalPath = item.Path,
                        Status = "Deleting",
                        Progress = 0,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        IsRetryable = false,
                        MinioUrl = item.MinioUrl,
                        Operation = OperationType.Delete
                    };

                    _dbContext.UploadLogs.Add(log);
                    await _dbContext.SaveChangesAsync();
                    TaskPanel?.AddLog(log);

                    bool deleteFromDatabase = true;

                    // 如果是文件夹，需要递归删除所有子项
                    if (item.IsFolder)
                    {
                        // 获取所有子项
                        var childItems = await _dbContext.FileItems
                            .Where(x => x.Path.StartsWith(item.Path + "/"))
                            .ToListAsync();

                        // 先删除所有有 MinioUrl 的子文件
                        foreach (var childItem in childItems.Where(x => !string.IsNullOrEmpty(x.MinioUrl)))
                        {
                            // 检查是否有其他文件使用相同的MD5
                            var sameHashCount = await _dbContext.FileItems
                                .Where(x => x.Md5Hash == childItem.Md5Hash && x.Id != childItem.Id)
                                .CountAsync();

                            if (sameHashCount == 0)
                            {
                                var (success, message) = await _minioService.DeleteObjectAsync($"http://{_minioService.Config.Endpoint}/{item.MinioUrl}");
                                if (!success)
                                {
                                    log.Status = "Failed";
                                    log.Message = $"删除子文件失败: {message}";
                                    log.UpdatedAt = DateTime.Now;
                                    await _dbContext.SaveChangesAsync();
                                    TaskPanel?.UpdateLog(log);
                                    deleteFromDatabase = false;
                                    continue;
                                }
                            }
                        }

                        if (deleteFromDatabase)
                        {
                            // 从数据库中删除所有子项
                            _dbContext.RemoveRange(childItems);
                        }
                    }

                    // 如果当前项有 MinioUrl，检查是否可以删除 MinIO 中的对象
                    if (!string.IsNullOrEmpty(item.MinioUrl) && !string.IsNullOrEmpty(item.Md5Hash))
                    {
                        // 检查是否有其他文件使用相同的MD5
                        var sameHashCount = await _dbContext.FileItems
                            .Where(x => x.Md5Hash == item.Md5Hash && x.Id != item.Id)
                            .CountAsync();

                        if (sameHashCount == 0)
                        {
                            var (success, message) = await _minioService.DeleteObjectAsync($"http://{_minioService.Config.Endpoint}/{item.MinioUrl}");
                            if (!success)
                            {
                                log.Status = "Failed";
                                log.Message = message;
                                log.UpdatedAt = DateTime.Now;
                                await _dbContext.SaveChangesAsync();
                                TaskPanel?.UpdateLog(log);
                                deleteFromDatabase = false;
                                continue;
                            }
                        }
                    }

                    if (deleteFromDatabase)
                    {
                        // 从数据库中删除当前项
                        _dbContext.Remove(item);
                        await _dbContext.SaveChangesAsync();

                        // 更新日志状态
                        log.Status = "Success";
                        log.Message = item.IsFolder ? "文件夹及其内容已成功删除" : "文件已成功删除";
                        log.Progress = 100;
                    }
                    else
                    {
                        log.Status = "Failed";
                        log.Message = "MinIO文件删除失败，数据库记录未删除";
                        log.Progress = 0;
                    }

                    log.UpdatedAt = DateTime.Now;
                    await _dbContext.SaveChangesAsync();
                    TaskPanel?.UpdateLog(log);
                }

                // 刷新视图
                LoadFolderStructure();
                LoadCurrentFolderItems(SelectedFolder?.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除项目时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async void DeleteItem(FileItem item)
        {
            await Task.Run(() => DeleteItems(new List<FileItem> { item }));
        }

        public void MoveItems(List<FileItem> sourceItems, FileItem targetFolder)
        {
            try
            {
                // 检查是否有循环引用
                if (sourceItems.Any(item => IsAncestor(item, targetFolder)))
                {
                    MessageBox.Show("不能将文件夹移动到其子文件夹中", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                foreach (var item in sourceItems)
                {
                    item.ParentId = targetFolder.Id;
                    item.Path = System.IO.Path.Combine(targetFolder.Path, item.Name);
                    item.UpdatedAt = DateTime.Now;
                    _dbContext.Update(item);
                }

                _dbContext.SaveChanges();
                LoadFolderStructure();
                LoadCurrentFolderItems(SelectedFolder?.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"移动项目时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsAncestor(FileItem potentialAncestor, FileItem item)
        {
            var current = item;
            while (current != null)
            {
                if (current.Id == potentialAncestor.Id)
                    return true;
                current = _dbContext.FileItems.Find(current.ParentId);
            }
            return false;
        }

        [RelayCommand]
        private void Refresh()
        {
            LoadFolderStructure();
            LoadCurrentFolderItems(SelectedFolder?.Id);
        }

        public void SelectedFolderChanged(object? selectedItem)
        {
            if (selectedItem is FileItem folder)
            {
                SelectedFolder = folder;
                LoadCurrentFolderItems(folder.Id);
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    LoadCurrentFolderItems(SelectedFolder?.Id);
                    return;
                }

                var searchResults = _dbContext.FileItems
                    .Where(x => x.Name.Contains(value) ||
                               x.Notes.Contains(value) ||
                               x.MinioUrl.Contains(value))
                    .OrderByDescending(x => x.UpdatedAt)  // 仅按修改日期降序
                    .ThenBy(x => x.Name)  // 同一时间的按名称升序
                    .ToList();

                CurrentFolderItems = new ObservableCollection<FileItem>(searchResults);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        partial void OnIsDetailViewChanged(bool value)
        {
            _fileListView.SetViewMode(value);
        }
    }
} 