using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;
using Windows.Storage.Pickers;

namespace XIGUASecurity.UI.Dialogs
{
    public sealed partial class BackupFolderDialog : ContentDialog
    {
        private ObservableCollection<string> _backupFolders;
        private long _totalSize;
        private int _totalFileCount;

        public string CountText => $"已添加 {_backupFolders.Count} 个备份目录，共 {_totalFileCount} 个文件，总大小: {FormatFileSize(_totalSize)}";

        public BackupFolderDialog()
        {
            this.InitializeComponent();
            _backupFolders = new ObservableCollection<string>();
            LoadBackupFolders();
            
            // 绑定ListView数据源
            BackupFolderListView.ItemsSource = _backupFolders;
            
            // 初始化显示
            UpdateCountText();
            
            // 添加Opened事件处理程序
            this.Opened += async (sender, args) =>
            {
                // 异步计算总大小
                await CalculateTotalSizeAsync();
            };
        }

        private void LoadBackupFolders()
        {
            // 从DocumentProtection加载备份目录
            var folders = XIGUASecurity.Protection.DocumentProtection.GetMonitoredFolders();
            _backupFolders.Clear();
            
            foreach (var folder in folders)
            {
                _backupFolders.Add(folder);
            }

            CalculateTotalSize();
        }

        private async Task CalculateTotalSizeAsync()
        {
            _totalSize = 0;
            _totalFileCount = 0;
            
            foreach (var folder in _backupFolders)
            {
                if (Directory.Exists(folder))
                {
                    try
                    {
                        // 计算原始文件夹中可备份文件的大小和数量
                        var (size, count) = await CalculateFolderSizeAndCountAsync(folder);
                        _totalSize += size;
                        _totalFileCount += count;
                    }
                    catch (Exception)
                    {
                        // 忽略无法访问的文件夹
                    }
                }
            }
            
            // 确保在UI线程上更新文本
            if (this.DispatcherQueue != null)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateCountText();
                });
            }
            else if (App.MainWindow?.DispatcherQueue != null)
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateCountText();
                });
            }
        }

        private void CalculateTotalSize()
        {
            _totalSize = 0;
            _totalFileCount = 0;
            
            foreach (var folder in _backupFolders)
            {
                if (Directory.Exists(folder))
                {
                    try
                    {
                        // 计算原始文件夹中可备份文件的大小和数量
                        var (size, count) = CalculateFolderSizeAndCount(folder);
                        _totalSize += size;
                        _totalFileCount += count;
                    }
                    catch (Exception)
                    {
                        // 忽略无法访问的文件夹
                    }
                }
            }
        }

        private (long size, int count) CalculateFolderSizeAndCount(string path)
        {
            long size = 0;
            int count = 0;
            
            try
            {
                // 定义可备份的文件扩展名
                string[] backupExtensions = { 
                    ".txt", ".doc", ".docx", ".pdf", ".xls", ".xlsx", ".ppt", ".pptx",
                    ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp",
                    ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma",
                    ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm",
                    ".zip", ".rar", ".7z", ".tar", ".gz",
                    ".html", ".htm", ".css", ".js", ".xml", ".json", ".csv"
                };
                
                // 获取文件夹中的所有文件
                var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var extension = fileInfo.Extension.ToLower();
                        
                        // 只计算可备份的文件
                        if (backupExtensions.Contains(extension))
                        {
                            size += fileInfo.Length;
                            count++;
                        }
                    }
                    catch
                    {
                        // 忽略无法访问的文件
                    }
                }
            }
            catch
            {
                // 忽略无法访问的文件夹
            }

            return (size, count);
        }

        private async Task<(long size, int count)> CalculateFolderSizeAndCountAsync(string path)
        {
            return await System.Threading.Tasks.Task.Run(() => CalculateFolderSizeAndCount(path));
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }

        private void UpdateCountText()
        {
            CountTextBlock.Text = CountText;
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string folderPath)
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
                catch (Exception ex)
                {
                    ShowResultTeachingTip($"无法打开文件夹: {ex.Message}");
                }
            }
        }

        private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string folderPath)
            {
                _backupFolders.Remove(folderPath);
                SaveBackupFolders();
                await CalculateTotalSizeAsync();
                UpdateCountText();
                ShowResultTeachingTip($"已移除文件夹: {folderPath}");
            }
        }

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string folderPath = FolderPathTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(folderPath))
            {
                args.Cancel = true;
                ShowResultTeachingTip("请输入文件夹路径");
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                args.Cancel = true;
                ShowResultTeachingTip("文件夹不存在");
                return;
            }

            if (_backupFolders.Contains(folderPath))
            {
                args.Cancel = true;
                ShowResultTeachingTip("该文件夹已在备份列表中");
                return;
            }

            _backupFolders.Add(folderPath);
            SaveBackupFolders();
            await CalculateTotalSizeAsync();
            UpdateCountText();
            FolderPathTextBox.Text = "";
            ShowResultTeachingTip($"已添加文件夹: {folderPath}");
            
            // 不关闭对话框，允许继续添加
            args.Cancel = true;
        }

        private void SaveBackupFolders()
        {
            // 保存到DocumentProtection
            XIGUASecurity.Protection.DocumentProtection.SetMonitoredFolders(_backupFolders.ToList());
        }

        private void ShowResultTeachingTip(string message)
        {
            ResultTeachingTip.Subtitle = message;
            ResultTeachingTip.IsOpen = true;
        }
    }
}