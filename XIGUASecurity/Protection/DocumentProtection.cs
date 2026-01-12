using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Notifications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace XIGUASecurity.Protection
{
    /// <summary>
    /// 文档保护系统，监控重要文档并在文件被修改或删除时提供恢复选项
    /// </summary>
    public static class DocumentProtection
    {
        // 通知标识符
        private const string InitialBackupNotificationTag = "InitialBackup";
        private const string InitialBackupNotificationGroup = "DocumentProtection";
        private const string AppUserModelID = "XIGUASecurity.DocumentProtection";
        
        // 保存当前通知的引用，用于替换
        private static ToastNotification? _currentBackupNotification = null;
        // 通知更新序列号，确保按顺序更新
        private static uint _notificationSequenceNumber = 1;
        
        // 备份进度对话框引用
        private static UI.Dialogs.BackupProgressDialog? _backupProgressDialog = null;
        private static readonly object _lockObject = new object();
        private static bool _isEnabled = false;
        private static bool _isInitialBackupComplete = false;
        private static bool _isBackupCancelled = false;
        private static bool _isBulkRestoreInProgress = false;
        private static FileSystemWatcher[]? _watchers = null;
        private static readonly ConcurrentDictionary<string, FileBackup> _fileBackups = new ConcurrentDictionary<string, FileBackup>();
        private static readonly ConcurrentDictionary<string, DateTime> _recentlyModifiedFiles = new ConcurrentDictionary<string, DateTime>();
        private static readonly ConcurrentDictionary<string, DateTime> _recentlyNotifiedFiles = new ConcurrentDictionary<string, DateTime>();
        private static Timer? _backupTimer = null;
        private static Timer? _notificationBatchTimer = null;
        private static int _recentChangeCount = 0;
        private static readonly List<string> _currentBatchFiles = new List<string>();
        private static readonly object _notificationLock = new object();
        private static readonly string _backupInfoFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XIGUASecurity", "backup_info.json");
        private static readonly string _backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XIGUASecurity", "DocumentBackups");
        
        // 监控的文档类型
        private static readonly List<string> _documentExtensions = new List<string>
        {
            ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".txt", ".rtf",
            ".odt", ".ods", ".odp", ".csv", ".html", ".htm", ".md", ".jpg", ".jpeg", 
            ".png", ".gif", ".bmp", ".tiff", ".svg", ".mp3", ".wav", ".mp4", ".avi", 
            ".mov", ".zip", ".rar", ".7z", ".tar", ".gz"
        };
        
        // 监控的文件夹
        private static readonly List<string> _monitoredFolders = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        };
        
        // 文件大小限制（100MB），超过此大小的文件不进行哈希计算
        private const long MaxFileSizeForHash = 100 * 1024 * 1024;
        
        /// <summary>
        /// 格式化文件大小显示
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 文件备份信息
        /// </summary>
        internal class FileBackup
        {
            public string OriginalPath { get; set; } = string.Empty;
            public string BackupPath { get; set; } = string.Empty;
            public DateTime CreatedTime { get; set; }
            public long FileSize { get; set; }
            public string FileHash { get; set; } = string.Empty;
            public int Version { get; set; } = 1;
            public BackupType Type { get; set; } = BackupType.Initial;
        }
        
        internal enum BackupType
        {
            Initial,    // 初始备份（启用文档保护时创建）
            Modified,   // 文件修改后的备份
            Created     // 新创建文件的备份
        }

        /// <summary>
        /// 获取所有备份文件信息
        /// </summary>
        /// <returns>所有备份文件信息的列表</returns>
        internal static List<FileBackup> GetAllFileBackups()
        {
            lock (_lockObject)
            {
                return new List<FileBackup>(_fileBackups.Values);
            }
        }

        /// <summary>
        /// 获取最近更改的文件备份（排除初始备份）
        /// </summary>
        /// <returns>最近更改的文件备份列表</returns>
        internal static List<FileBackup> GetRecentChangedFiles()
        {
            lock (_lockObject)
            {
                return _fileBackups.Values
                    .Where(backup => backup.Type != BackupType.Initial)
                    .OrderByDescending(b => b.CreatedTime)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取最近通知的文件列表
        /// </summary>
        /// <returns>最近通知的文件列表</returns>
        internal static List<(string FilePath, DateTime NotificationTime)> GetRecentNotifiedFiles()
        {
            lock (_lockObject)
            {
                return _recentlyNotifiedFiles
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(100)
                    .Select(kvp => (kvp.Key, kvp.Value))
                    .ToList();
            }
        }

        /// <summary>
        /// 设置批量还原状态
        /// </summary>
        /// <param name="isInProgress">是否正在进行批量还原</param>
        internal static void SetBulkRestoreInProgress(bool isInProgress)
        {
            _isBulkRestoreInProgress = isInProgress;
            LogEvent($"批量还原状态已设置为: {isInProgress}");
        }

        /// <summary>
        /// 检查文档保护是否已启用
        /// </summary>
        public static bool IsEnabled()
        {
            lock (_lockObject)
            {
                return _isEnabled;
            }
        }

        /// <summary>
        /// 强制重新备份所有文件
        /// </summary>
        public static async Task ForceRebackupAsync(Window? mainWindow = null)
        {
            if (!_isEnabled)
            {
                LogEvent("文档保护未启用，无法执行强制重新备份");
                return;
            }

            try
            {
                LogEvent("开始强制重新备份所有文件...");
                
                // 显示备份进度对话框
                ShowBackupProgressDialog(mainWindow);
                
                // 执行强制重新备份
                await CreateInitialBackupWithProgress(true);
                
                LogEvent("强制重新备份完成");
            }
            catch (Exception ex)
            {
                LogEvent($"强制重新备份失败: {ex.Message}");
            }
            finally
            {
                // 关闭备份进度对话框
                CloseBackupProgressDialog(_fileBackups.Count);
            }
        }

        /// <summary>
        /// 启用文档保护
        /// </summary>
        public static bool Enable(Window? mainWindow = null)
        {
            lock (_lockObject)
            {
                if (_isEnabled)
                    return true;

                try
                {
                    // 创建备份目录
                    if (!Directory.Exists(_backupDirectory))
                    {
                        Directory.CreateDirectory(_backupDirectory);
                    }
                    
                    // 加载已存在的备份信息
                    LoadBackupInfo();
                    
                    // 设置文件系统监控
                    SetupFileWatchers();
                    
                    // 设置定期备份计时器（每6小时备份一次文件变更记录）
                    _backupTimer = new Timer(CreatePeriodicBackup, null, TimeSpan.FromHours(6), TimeSpan.FromHours(6));
                    
                    _isEnabled = true;
                    LogEvent("文档保护已启用");
                    
                    // 启用后自动创建初始备份
                    LogEvent("开始创建初始文档备份...");
                    
                    // 显示备份进度对话框
                    ShowBackupProgressDialog(mainWindow);
                    
                    // 在后台线程中运行备份，避免阻塞UI
                    var backupTask = Task.Run(async () => 
                    {
                        try
                        {
                            await CreateInitialBackupWithProgress();
                        }
                        catch (Exception ex)
                        {
                            LogEvent($"启用文档保护后自动备份失败: {ex.Message}");
                        }
                    });
                    
                    return true;
                }
                catch (Exception ex)
                {
                    LogEvent($"启用文档保护失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 禁用文档保护
        /// </summary>
        public static bool Disable()
        {
            lock (_lockObject)
            {
                if (!_isEnabled)
                    return true;

                try
                {
                    // 停止文件系统监控
                    if (_watchers != null)
                    {
                        foreach (var watcher in _watchers)
                        {
                            watcher?.Dispose();
                        }
                        _watchers = null;
                    }
                    
                    // 停止定期备份计时器
                    _backupTimer?.Dispose();
                    _backupTimer = null;
                    _notificationBatchTimer?.Dispose();
                    _notificationBatchTimer = null;
                    
                    _isEnabled = false;
                    LogEvent("文档保护已禁用");
                    return true;
                }
                catch (Exception ex)
                {
                    LogEvent($"禁用文档保护失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 获取当前监控的文件夹列表
        /// </summary>
        public static List<string> GetMonitoredFolders()
        {
            lock (_lockObject)
            {
                return new List<string>(_monitoredFolders);
            }
        }

        /// <summary>
        /// 获取指定文件夹的备份路径
        /// </summary>
        public static string GetBackupFolderPath(string originalPath)
        {
            if (string.IsNullOrEmpty(originalPath))
                return string.Empty;
                
            // 将原始路径转换为有效的备份文件夹名称
            string folderName = Path.GetFileName(originalPath);
            if (string.IsNullOrEmpty(folderName))
                folderName = "Root";
                
            // 替换无效字符
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                folderName = folderName.Replace(c, '_');
            }
            
            return Path.Combine(_backupDirectory, folderName);
        }

        /// <summary>
        /// 设置监控的文件夹列表
        /// </summary>
        public static void SetMonitoredFolders(List<string> folders)
        {
            lock (_lockObject)
            {
                _monitoredFolders.Clear();
                if (folders != null)
                {
                    foreach (var folder in folders)
                    {
                        if (Directory.Exists(folder) && !_monitoredFolders.Contains(folder))
                        {
                            _monitoredFolders.Add(folder);
                        }
                    }
                }

                // 如果文档保护已启用，重新初始化监控
                if (_isEnabled)
                {
                    // 先停止当前监控
                    if (_watchers != null)
                    {
                        foreach (var watcher in _watchers)
                        {
                            watcher?.Dispose();
                        }
                        _watchers = null;
                    }
                    
                    // 重新初始化监控
                    SetupFileWatchers();
                    LogEvent($"已更新监控文件夹列表，共 {_monitoredFolders.Count} 个文件夹");
                }
            }
        }

        /// <summary>
        /// 等待用户对备份决定的响应
        /// </summary>
        private static async Task WaitForUserBackupDecision()
        {
            // 创建一个任务完成源，用于等待用户响应
            var tcs = new TaskCompletionSource<bool>();
            
            // 注册Toast通知激活事件处理程序
            ToastNotificationManagerCompat.OnActivated += (toastArgs) =>
            {
                try
                {
                    // 解析Toast参数
                    var arguments = ToastArguments.Parse(toastArgs.Argument);
                    
                    // 检查是否是备份相关的操作
                    if (arguments.TryGetValue("action", out string action))
                    {
                        LogEvent($"用户选择了操作: {action}");
                        
                        if (action == "continue-backup")
                        {
                            LogEvent("用户选择继续备份");
                            _isBackupCancelled = false;
                            tcs.SetResult(true);
                        }
                        else if (action == "cancel-backup")
                        {
                            LogEvent("用户选择取消备份，将关闭文档保护");
                            _isBackupCancelled = true;
                            
                            // 在UI线程中关闭文档保护
                            App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                            {
                                try
                                {
                                    // 调用DocumentProtection.Disable()关闭文档保护
                                    DocumentProtection.Disable();
                                    
                                    // 更新UI中的文档保护按钮状态
                                    var settingsPage = App.MainWindow?.Content as SettingsPage;
                                    if (settingsPage != null)
                                    {
                                        // 找到文档保护的ToggleSwitch并更新其状态
                                        var documentToggle = settingsPage.FindName("DocumentToggle") as ToggleSwitch;
                                        if (documentToggle != null)
                                        {
                                            documentToggle.IsOn = false;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogEvent($"关闭文档保护时出错: {ex.Message}");
                                }
                            });
                            
                            tcs.SetResult(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogEvent($"处理Toast通知激活事件失败: {ex.Message}");
                }
            };
            
            // 等待用户响应，设置超时时间为60秒
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(1));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                LogEvent("用户未在规定时间内响应，默认取消备份");
                tcs.SetResult(false);
            }
        }

        /// <summary>
        /// 显示磁盘空间警告通知
        /// </summary>
        private static async Task ShowDiskSpaceWarningNotification(long backupSize, long freeSpace)
        {
            try
            {
                string backupSizeString = FormatFileSize(backupSize);
                string freeSpaceString = FormatFileSize(freeSpace);
                double usagePercentage = (double)backupSize / freeSpace * 100;
                
                // 简化通知内容，只保留最基本的信息
                var builder = new ToastContentBuilder()
                    .AddText("磁盘空间不足警告")
                    .AddText($"备份大小 {backupSizeString} 占用了可用空间的 {usagePercentage:F1}%")
                    .AddButton(new ToastButton()
                        .SetContent("继续备份")
                        .AddArgument("action", "continue-backup"))
                    .AddButton(new ToastButton()
                        .SetContent("取消备份")
                        .AddArgument("action", "cancel-backup"));

                // 显示通知
                builder.Show();
            }
            catch (Exception ex)
            {
                LogEvent($"显示磁盘空间警告通知失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 显示备份取消通知
        /// </summary>
        private static void ShowBackupCancelledNotification()
        {
            try
            {
                // 使用和其他通知一样的方式创建取消通知
                var builder = new ToastContentBuilder()
                    .AddText("XIGUASecurity 文档保护")
                    .AddText("备份已取消")
                    .AddText("用户取消了文档备份操作");

                // 显示通知
                builder.Show();
            }
            catch (Exception ex)
            {
                LogEvent($"显示备份取消通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示初始备份开始通知
        /// </summary>
        private static void ShowInitialBackupStartNotification(int totalFiles, string totalSize)
        {
            try
            {
                // 使用ToastContentBuilder创建通知内容，使用数据绑定字段
                var content = new ToastContentBuilder()
                    .AddText("XIGUASecurity 文档保护")
                    .AddText($"正在创建初始文档备份... (共 {totalFiles} 个文件，{totalSize})")
                    .AddText("完成前，文件备份功能将不会正常工作")
                        
                    // 添加数据绑定的进度条
                    .AddVisualChild(new AdaptiveProgressBar()
                    {
                        Title = "备份进度",
                        Value = new BindableProgressBarValue("progressValue"),
                        ValueStringOverride = new BindableString("progressValueString"),
                        Status = new BindableString("progressStatus")
                    })
                    .GetToastContent();

                // 创建新的通知
                var toast = new ToastNotification(content.GetXml())
                {
                    Tag = InitialBackupNotificationTag,
                    Group = InitialBackupNotificationGroup
                };
                
                // 添加通知数据，设置初始值
                var notificationData = new NotificationData();
                notificationData.Values["progressValue"] = "0";
                notificationData.Values["progressValueString"] = "0%";
                notificationData.Values["progressStatus"] = "正在初始化";
                notificationData.SequenceNumber = _notificationSequenceNumber++;
                toast.Data = notificationData;

                // 保存通知引用
                _currentBackupNotification = toast;

                // 确保在UI线程上显示通知
                try
                {
                    // 使用App.MainWindow的DispatcherQueue确保在UI线程上执行
                    if (App.MainWindow?.DispatcherQueue != null)
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
                            }
                            catch (Exception ex)
                            {
                                LogEvent($"在UI线程上显示通知失败: {ex.Message}");
                            }
                        });
                    }
                    else
                    {
                        // 如果无法访问UI线程，直接显示通知
                        ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
                    }
                }
                catch (Exception ex)
                {
                    // 如果访问UI线程失败，直接显示通知
                    LogEvent($"访问UI线程失败，直接显示通知: {ex.Message}");
                    ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
                }
                
                LogEvent("已显示初始备份开始通知");
            }
            catch (Exception ex)
            {
                LogEvent($"显示初始备份开始通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新初始备份进度通知
        /// </summary>
        private static void UpdateInitialBackupProgressNotification(int currentProgress, int totalProgress, string currentFolder)
        {
            try
            {
                string progressText = totalProgress > 0 ? $"{currentProgress}/{totalProgress}" : "0/0";
                double progressValue = totalProgress > 0 ? (double)currentProgress / totalProgress : 0;
                string progressPercent = $"{(progressValue * 100):F0}";
                string statusText = $"正在备份 {Path.GetFileName(currentFolder)}";
                
                // 创建NotificationData对象，只更新需要改变的数据
                var data = new NotificationData
                {
                    SequenceNumber = _notificationSequenceNumber++
                };
                
                // 设置新的进度值
                data.Values["progressValue"] = progressValue.ToString("F2");
                data.Values["progressValueString"] = $"{progressPercent}%";
                data.Values["progressStatus"] = statusText;

                // 确保在UI线程上更新通知
                try
                {
                    // 使用App.MainWindow的DispatcherQueue确保在UI线程上执行
                    if (App.MainWindow?.DispatcherQueue != null)
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                // 使用Update方法更新现有通知的数据，而不是创建新通知
                                ToastNotificationManagerCompat.CreateToastNotifier().Update(data, InitialBackupNotificationTag, InitialBackupNotificationGroup);
                            }
                            catch (Exception ex)
                            {
                                LogEvent($"在UI线程上更新通知失败: {ex.Message}");
                            }
                        });
                    }
                    else
                    {
                        // 如果无法访问UI线程，直接更新通知
                        ToastNotificationManagerCompat.CreateToastNotifier().Update(data, InitialBackupNotificationTag, InitialBackupNotificationGroup);
                    }
                }
                catch (Exception ex)
                {
                    // 如果访问UI线程失败，直接更新通知
                    LogEvent($"访问UI线程失败，直接更新通知: {ex.Message}");
                    ToastNotificationManagerCompat.CreateToastNotifier().Update(data, InitialBackupNotificationTag, InitialBackupNotificationGroup);
                }
            }
            catch (Exception ex)
            {
                LogEvent($"更新初始备份进度通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示初始备份完成通知
        /// </summary>
        private static void ShowInitialBackupCompleteNotification(int totalFiles)
        {
            try
            {
                // 创建NotificationData对象，更新进度为100%
                var data = new NotificationData
                {
                    SequenceNumber = _notificationSequenceNumber++
                };
                
                // 设置完成状态
                data.Values["progressValue"] = "1.0";
                data.Values["progressValueString"] = "100%";
                data.Values["progressStatus"] = "备份完成";

                // 确保在UI线程上更新通知
                try
                {
                    // 使用App.MainWindow的DispatcherQueue确保在UI线程上执行
                    if (App.MainWindow?.DispatcherQueue != null)
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                // 使用Update方法更新现有通知的数据，而不是创建新通知
                                ToastNotificationManagerCompat.CreateToastNotifier().Update(data, InitialBackupNotificationTag, InitialBackupNotificationGroup);
                                
                                // 等待一下再显示完成通知，让用户看到100%进度
                                Task.Delay(1000).ContinueWith(_ =>
                                {
                                    try
                                    {
                                        // 创建完成通知
                                        var content = new ToastContentBuilder()
                                            .AddText("XIGUASecurity 文档保护")
                                            .AddText($"初始备份已完成！")
                                            .AddText($"共备份 {totalFiles} 个文件，文档保护功能现已正常工作")
                                            .GetToastContent();

                                        var toast = new ToastNotification(content.GetXml())
                                        {
                                            Tag = InitialBackupNotificationTag + "_complete",
                                            Group = InitialBackupNotificationGroup
                                        };
                                        
                                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                                        {
                                            try
                                            {
                                                ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
                                            }
                                            catch (Exception ex)
                                            {
                                                LogEvent($"在UI线程上显示完成通知失败: {ex.Message}");
                                            }
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        LogEvent($"创建完成通知失败: {ex.Message}");
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                LogEvent($"在UI线程上更新通知失败: {ex.Message}");
                            }
                        });
                    }
                    else
                    {
                        // 如果无法访问UI线程，直接更新通知
                        ToastNotificationManagerCompat.CreateToastNotifier().Update(data, InitialBackupNotificationTag, InitialBackupNotificationGroup);
                    }
                }
                catch (Exception ex)
                {
                    // 如果访问UI线程失败，直接更新通知
                    LogEvent($"访问UI线程失败，直接更新通知: {ex.Message}");
                    ToastNotificationManagerCompat.CreateToastNotifier().Update(data, InitialBackupNotificationTag, InitialBackupNotificationGroup);
                }
                
                LogEvent("已显示初始备份完成通知");
            }
            catch (Exception ex)
            {
                LogEvent($"显示初始备份完成通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除旧备份文件（带进度显示）
        /// </summary>
        private static async Task DeleteOldBackupsWithProgress()
        {
            try
            {
                LogEvent("正在清理旧备份文件...");
                
                // 清空备份记录
                _fileBackups.Clear();
                
                if (!Directory.Exists(_backupDirectory))
                {
                    LogEvent("备份目录不存在，无需清理");
                    return;
                }
                
                // 获取所有备份文件
                var backupFiles = Directory.GetFiles(_backupDirectory, "*", SearchOption.AllDirectories).ToList();
                int totalFiles = backupFiles.Count;
                
                if (totalFiles == 0)
                {
                    LogEvent("没有找到旧备份文件");
                    return;
                }
                
                // 显示删除进度通知
                ShowDeleteProgressStartNotification(totalFiles);
                
                int processedFiles = 0;
                int lastReportedProgress = 0;
                double lastReportedProgressPercentage = 0;
                
                // 使用并行处理提高删除效率
                await Task.Run(() => 
                {
                    Parallel.ForEach(backupFiles, new ParallelOptions { MaxDegreeOfParallelism = 8 }, file =>
                    {
                        try
                        {
                            File.Delete(file);
                            int currentCount = Interlocked.Increment(ref processedFiles);
                            
                            // 减少通知更新频率：每处理100个文件或完成一个文件夹时更新一次进度
                            // 同时确保至少有5%的进度变化才更新通知
                            double progressPercentage = totalFiles > 0 ? (double)currentCount / totalFiles * 100 : 0;
                            bool shouldUpdateByCount = currentCount - lastReportedProgress >= 100;
                            bool shouldUpdateByProgress = progressPercentage - lastReportedProgressPercentage >= 5;
                            bool isLastFile = currentCount == totalFiles;
                            
                            if (shouldUpdateByCount || shouldUpdateByProgress || isLastFile)
                            {
                                int newReportedProgress = Interlocked.Exchange(ref lastReportedProgress, currentCount);
                                double newReportedPercentage = Interlocked.Exchange(ref lastReportedProgressPercentage, progressPercentage);
                                
                                if (newReportedProgress != currentCount) // 确保只有一个线程更新UI
                                {
                                    UpdateDeleteProgressNotification(currentCount, totalFiles, $"正在删除: {Path.GetFileName(file)}");
                                }
                            }
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            LogEvent($"无权限删除文件 {file}: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            LogEvent($"删除文件 {file} 时出错: {ex.Message}");
                        }
                    });
                });
                
                LogEvent($"已删除 {processedFiles} 个旧备份文件");
                ShowDeleteProgressCompleteNotification(processedFiles);
            }
            catch (Exception ex)
            {
                LogEvent($"删除旧备份文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示删除进度开始通知
        /// </summary>
        private static void ShowDeleteProgressStartNotification(int totalFiles)
        {
            try
            {
                // 使用ToastContentBuilder创建通知内容，使用数据绑定字段
                var content = new ToastContentBuilder()
                    .AddText("XIGUASecurity 文档保护")
                    .AddText($"正在清理旧备份文件... (共 {totalFiles} 个文件)")
                    .AddText("为新备份腾出空间")
                        
                    // 添加数据绑定的进度条
                    .AddVisualChild(new AdaptiveProgressBar()
                    {
                        Title = "清理进度",
                        Value = new BindableProgressBarValue("progressValue"),
                        ValueStringOverride = new BindableString("progressValueString"),
                        Status = new BindableString("progressStatus")
                    })
                    .GetToastContent();

                // 创建新的通知
                var toast = new ToastNotification(content.GetXml())
                {
                    Tag = "DeleteProgress",
                    Group = InitialBackupNotificationGroup
                };
                
                // 添加通知数据，设置初始值
                var notificationData = new NotificationData();
                notificationData.Values["progressValue"] = "0";
                notificationData.Values["progressValueString"] = "0%";
                notificationData.Values["progressStatus"] = "正在初始化";
                notificationData.SequenceNumber = _notificationSequenceNumber++;
                toast.Data = notificationData;

                // 确保在UI线程上显示通知
                try
                {
                    // 使用App.MainWindow的DispatcherQueue确保在UI线程上执行
                    if (App.MainWindow?.DispatcherQueue != null)
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
                            }
                            catch (Exception ex)
                            {
                                LogEvent($"在UI线程上显示删除进度通知失败: {ex.Message}");
                            }
                        });
                    }
                    else
                    {
                        // 如果无法访问UI线程，直接显示通知
                        ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
                    }
                }
                catch (Exception ex)
                {
                    // 如果访问UI线程失败，直接显示通知
                    LogEvent($"访问UI线程失败，直接显示删除进度通知: {ex.Message}");
                    ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
                }
                
                LogEvent("已显示删除进度开始通知");
            }
            catch (Exception ex)
            {
                LogEvent($"显示删除进度开始通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新删除进度通知
        /// </summary>
        private static void UpdateDeleteProgressNotification(int currentProgress, int totalProgress, string currentFile)
        {
            try
            {
                string progressText = totalProgress > 0 ? $"{currentProgress}/{totalProgress}" : "0/0";
                double progressValue = totalProgress > 0 ? (double)currentProgress / totalProgress : 0;
                string progressPercent = $"{(progressValue * 100):F0}";
                string statusText = $"正在删除: {currentFile}";
                
                // 创建NotificationData对象，只更新需要改变的数据
                var data = new NotificationData
                {
                    SequenceNumber = _notificationSequenceNumber++
                };
                
                // 设置新的进度值
                data.Values["progressValue"] = progressValue.ToString("F2");
                data.Values["progressValueString"] = $"{progressPercent}%";
                data.Values["progressStatus"] = statusText;

                // 确保在UI线程上更新通知
                try
                {
                    // 使用App.MainWindow的DispatcherQueue确保在UI线程上执行
                    if (App.MainWindow?.DispatcherQueue != null)
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                // 使用Update方法更新现有通知的数据，而不是创建新通知
                                ToastNotificationManagerCompat.CreateToastNotifier().Update(data, "DeleteProgress", InitialBackupNotificationGroup);
                            }
                            catch (Exception ex)
                            {
                                LogEvent($"在UI线程上更新删除进度通知失败: {ex.Message}");
                            }
                        });
                    }
                    else
                    {
                        // 如果无法访问UI线程，直接更新通知
                        ToastNotificationManagerCompat.CreateToastNotifier().Update(data, "DeleteProgress", InitialBackupNotificationGroup);
                    }
                }
                catch (Exception ex)
                {
                    // 如果访问UI线程失败，直接更新通知
                    LogEvent($"访问UI线程失败，直接更新删除进度通知: {ex.Message}");
                    ToastNotificationManagerCompat.CreateToastNotifier().Update(data, "DeleteProgress", InitialBackupNotificationGroup);
                }
            }
            catch (Exception ex)
            {
                LogEvent($"更新删除进度通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示删除进度完成通知
        /// </summary>
        private static void ShowDeleteProgressCompleteNotification(int totalFiles)
        {
            try
            {
                // 创建NotificationData对象，更新进度为100%
                var data = new NotificationData
                {
                    SequenceNumber = _notificationSequenceNumber++
                };
                
                // 设置完成状态
                data.Values["progressValue"] = "1.0";
                data.Values["progressValueString"] = "100%";
                data.Values["progressStatus"] = "清理完成";

                // 确保在UI线程上更新通知
                try
                {
                    // 使用App.MainWindow的DispatcherQueue确保在UI线程上执行
                    if (App.MainWindow?.DispatcherQueue != null)
                    {
                        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                // 使用Update方法更新现有通知的数据，而不是创建新通知
                                ToastNotificationManagerCompat.CreateToastNotifier().Update(data, "DeleteProgress", InitialBackupNotificationGroup);
                                
                                // 等待一下再显示完成通知，让用户看到100%进度
                                Task.Delay(1000).ContinueWith(_ =>
                                {
                                    try
                                    {
                                        // 创建完成通知
                                        var content = new ToastContentBuilder()
                                            .AddText("XIGUASecurity 文档保护")
                                            .AddText($"旧备份文件清理完成")
                                            .AddText($"已删除 {totalFiles} 个文件，为新备份腾出空间")
                                            .GetToastContent();

                                        var toast = new ToastNotification(content.GetXml())
                                        {
                                            Tag = "DeleteComplete",
                                            Group = InitialBackupNotificationGroup
                                        };

                                        // 显示通知
                                        ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
                                    }
                                    catch (Exception ex)
                                    {
                                        LogEvent($"显示删除完成通知失败: {ex.Message}");
                                    }
                                });
                            }
                            catch (Exception ex)
                            {
                                LogEvent($"在UI线程上更新删除进度通知失败: {ex.Message}");
                            }
                        });
                    }
                    else
                    {
                        // 如果无法访问UI线程，直接更新通知
                        ToastNotificationManagerCompat.CreateToastNotifier().Update(data, "DeleteProgress", InitialBackupNotificationGroup);
                        
                        // 等待一下再显示完成通知，让用户看到100%进度
                        Task.Delay(1000).ContinueWith(_ =>
                        {
                            try
                            {
                                // 创建完成通知
                                var content = new ToastContentBuilder()
                                    .AddText("XIGUASecurity 文档保护")
                                    .AddText($"旧备份文件清理完成")
                                    .AddText($"已删除 {totalFiles} 个文件，为新备份腾出空间")
                                    .GetToastContent();

                                var toast = new ToastNotification(content.GetXml())
                                {
                                    Tag = "DeleteComplete",
                                    Group = InitialBackupNotificationGroup
                                };

                                // 显示通知
                                ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
                            }
                            catch (Exception ex)
                            {
                                LogEvent($"显示删除完成通知失败: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    // 如果访问UI线程失败，直接更新通知
                    LogEvent($"访问UI线程失败，直接更新删除进度通知: {ex.Message}");
                    ToastNotificationManagerCompat.CreateToastNotifier().Update(data, "DeleteProgress", InitialBackupNotificationGroup);
                    
                    // 等待一下再显示完成通知，让用户看到100%进度
                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        try
                        {
                            // 创建完成通知
                            var content = new ToastContentBuilder()
                                .AddText("XIGUASecurity 文档保护")
                                .AddText($"旧备份文件清理完成")
                                .AddText($"已删除 {totalFiles} 个文件，为新备份腾出空间")
                                .GetToastContent();

                            var toast = new ToastNotification(content.GetXml())
                            {
                                Tag = "DeleteComplete",
                                Group = InitialBackupNotificationGroup
                            };

                            // 显示通知
                            ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
                        }
                        catch (Exception ex)
                        {
                            LogEvent($"显示删除完成通知失败: {ex.Message}");
                        }
                    });
                }
                
                LogEvent("已显示删除进度完成通知");
            }
            catch (Exception ex)
            {
                LogEvent($"显示删除进度完成通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 递归获取文件夹中的所有文档文件，跳过无法访问的文件
        /// </summary>
        /// <param name="folder">要搜索的文件夹</param>
        /// <param name="files">用于存储找到的文件的列表</param>
        private static void GetFilesRecursively(string folder, List<string> files)
        {
            try
            {
                // 获取当前目录中的所有文档文件
                try
                {
                    var allFiles = Directory.GetFiles(folder).ToList();
                    LogEvent($"在 {folder} 中找到 {allFiles.Count} 个文件");
                    
                    var currentFiles = allFiles
                        .Where(file => _documentExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                        .ToList();
                    
                    LogEvent($"在 {folder} 中过滤出 {currentFiles.Count} 个文档文件");
                    
                    // 记录被过滤掉的文件（仅记录前10个，避免日志过多）
                    var filteredFiles = allFiles.Except(currentFiles).Take(10);
                    if (filteredFiles.Any())
                    {
                        string filteredFileList = string.Join(", ", filteredFiles.Select(Path.GetFileName));
                        LogEvent($"被过滤掉的文件（部分）: {filteredFileList}...");
                    }
                    
                    files.AddRange(currentFiles);
                }
                catch (UnauthorizedAccessException ex)
                {
                    LogEvent($"无权限访问文件夹 {folder} 中的文件: {ex.Message}");
                }
                catch (Exception ex)
                {
                    LogEvent($"获取文件夹 {folder} 中的文件时出错: {ex.Message}");
                }
                
                // 递归处理子目录
                try
                {
                    var subdirectories = Directory.GetDirectories(folder);
                    foreach (var subdirectory in subdirectories)
                    {
                        try
                        {
                            GetFilesRecursively(subdirectory, files);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            LogEvent($"无权限访问子目录 {subdirectory}: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            LogEvent($"处理子目录 {subdirectory} 时出错: {ex.Message}");
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    LogEvent($"无权限访问文件夹 {folder} 的子目录: {ex.Message}");
                }
                catch (Exception ex)
                {
                    LogEvent($"获取文件夹 {folder} 的子目录时出错: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LogEvent($"处理文件夹 {folder} 时发生未预期的错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建初始备份（带进度显示）
        /// </summary>
        private static async Task CreateInitialBackupWithProgress(bool forceRebackup = false)
        {
            try
            {
                LogEvent("正在创建初始文档备份...");
                
                // 重置取消标志
                _isBackupCancelled = false;
                
                // 计算总文件数和总大小
                int totalFiles = 0;
                long totalSize = 0;
                var folderFileCounts = new Dictionary<string, int>();
                var folderFileSizes = new Dictionary<string, long>();
                var allFiles = new List<string>();
                
                // 首先枚举所有文件并计算总大小
                LogEvent($"开始扫描监控目录，共 {_monitoredFolders.Count} 个目录:");
                foreach (string folder in _monitoredFolders)
                {
                    LogEvent($"监控目录: {folder}");
                    
                    // 尝试创建目录（如果不存在）
                    try
                    {
                        if (!Directory.Exists(folder))
                        {
                            Directory.CreateDirectory(folder);
                            LogEvent($"创建备份目录: {folder}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogEvent($"无法创建目录 {folder}: {ex.Message}");
                        continue;
                    }
                        
                    try
                    {
                        LogEvent($"正在枚举 {folder} 中的文件...");
                        
                        var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                            .Where(file => _documentExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                            .ToList();
                        
                        LogEvent($"在 {folder} 中找到 {files.Count} 个文档文件");
                        
                        long folderSize = 0;
                        foreach (var file in files)
                        {
                            try
                            {
                                var fileInfo = new FileInfo(file);
                                folderSize += fileInfo.Length;
                                allFiles.Add(file);
                            }
                            catch (Exception ex)
                            {
                                LogEvent($"获取文件大小失败 {file}: {ex.Message}");
                            }
                        }
                        
                        folderFileCounts[folder] = files.Count;
                        folderFileSizes[folder] = folderSize;
                        totalFiles += files.Count;
                        totalSize += folderSize;
                        
                        LogEvent($"目录 {folder} 统计: {files.Count} 个文件，总大小 {FormatFileSize(folderSize)}");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        LogEvent($"无权限访问文件夹 {folder}: {ex.Message}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        LogEvent($"访问文件夹 {folder} 时出错: {ex.Message}");
                        continue;
                    }
                }
                
                if (totalFiles == 0)
                {
                    LogEvent("没有找到需要备份的文件");
                    ShowInitialBackupCompleteNotification(0);
                    _isInitialBackupComplete = true;
                    return;
                }
                
                // 格式化总大小显示
                string totalSizeString = FormatFileSize(totalSize);
                LogEvent($"找到 {totalFiles} 个文件，总大小: {totalSizeString}");
                
                // 在备份前删除旧备份文件
                await DeleteOldBackupsWithProgress();
                
                // 检查磁盘空间
                string? backupDrive = Path.GetPathRoot(_backupDirectory);
                LogEvent($"备份目录: {_backupDirectory}");
                LogEvent($"备份驱动器根目录: {backupDrive}");
                
                if (!string.IsNullOrEmpty(backupDrive))
                {
                    try
                    {
                        var driveInfo = new DriveInfo(backupDrive);
                        long freeSpace = driveInfo.AvailableFreeSpace;
                        long totalSpace = driveInfo.TotalSize;
                        double usagePercentage = totalSize > 0 ? (double)totalSize / freeSpace * 100 : 0;
                        
                        LogEvent($"驱动器: {driveInfo.Name}");
                        LogEvent($"可用空间: {FormatFileSize(freeSpace)}");
                        LogEvent($"总空间: {FormatFileSize(totalSpace)}");
                        LogEvent($"备份大小: {FormatFileSize(totalSize)}");
                        LogEvent($"占用百分比: {usagePercentage:F2}%");
                        
                        // 如果备份大小超过可用空间的5%，提示用户
                        if (totalSize > freeSpace * 0.05)
                        {
                            LogEvent($"警告: 备份大小 {totalSizeString} 超过可用磁盘空间的5% ({FormatFileSize(freeSpace)})");
                            
                            // 确保删除进度通知已完成
                            await Task.Delay(2000); // 等待2秒确保删除完成通知显示完成
                            
                            // 显示空间不足警告通知
                            await ShowDiskSpaceWarningNotification(totalSize, freeSpace);
                            
                            // 等待用户响应
                            LogEvent("等待用户确认是否继续备份...");
                            await WaitForUserBackupDecision();
                            
                            // 检查用户是否取消了备份
                            if (_isBackupCancelled)
                            {
                                LogEvent("用户取消了备份操作");
                                ShowBackupCancelledNotification();
                                // 关闭备份进度对话框
                                CloseBackupProgressDialog(0);
                                return;
                            }
                        }
                        else
                        {
                            LogEvent($"磁盘空间充足，备份大小 {totalSizeString} 未超过可用磁盘空间的5%");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogEvent($"检查磁盘空间失败: {ex.Message}");
                    }
                }
                
                // 显示初始进度，包含总大小信息
                ShowInitialBackupStartNotification(totalFiles, totalSizeString);
                
                int processedFiles = 0;
                int lastReportedProgress = 0;
                double lastReportedProgressPercentage = 0; // 记录上次报告的进度，避免过于频繁的更新
                
                // 为每个文件夹中的文档创建备份
                foreach (var kvp in folderFileCounts)
                {
                    // 检查用户是否取消了备份
                    if (_isBackupCancelled)
                    {
                        LogEvent("备份过程中用户取消了操作");
                        ShowBackupCancelledNotification();
                        return;
                    }
                    
                    string folder = kvp.Key;
                    int folderFileCount = kvp.Value;
                    
                    // 获取文件夹中的所有文档文件
                    try
                    {
                        LogEvent($"开始处理目录: {folder} (预计 {folderFileCount} 个文件)");
                        
                        var files = new List<string>();
                        
                        // 使用递归方式获取所有文件，避免因单个文件无法访问而导致整个目录被跳过
                        try
                        {
                            GetFilesRecursively(folder, files);
                            LogEvent($"从 {folder} 递归获取到 {files.Count} 个文件");
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            LogEvent($"无权限访问文件夹 {folder}: {ex.Message}");
                            continue;
                        }
                        catch (Exception ex)
                        {
                            LogEvent($"获取文件夹 {folder} 中的文件时出错: {ex.Message}");
                            continue;
                        }
                        
                        // 使用并行处理提高备份效率
                await Task.Run(() => 
                {
                    Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async file =>
                    {
                        // 检查用户是否取消了备份
                        if (_isBackupCancelled)
                        {
                            return;
                        }
                        
                        try
                        {
                            // 使用同步方式调用，避免异步开销
                            CreateFileBackupAsync(file, BackupType.Initial, forceRebackup).GetAwaiter().GetResult();
                            int currentCount = Interlocked.Increment(ref processedFiles);
                            
                            // 记录成功备份的文件（减少日志频率）
                            if (currentCount % 100 == 0 || currentCount == totalFiles)
                            {
                                LogEvent($"已备份 {currentCount}/{totalFiles} 个文件");
                            }
                            
                            // 每处理一个文件就更新一次进度
                            UpdateBackupProgress(currentCount, totalFiles, Path.GetFileName(file));
                            
                            // 减少通知更新频率：每处理100个文件或完成一个文件夹时更新一次进度
                            // 同时确保至少有5%的进度变化才更新通知
                            double progressPercentage = totalFiles > 0 ? (double)currentCount / totalFiles * 100 : 0;
                            bool shouldUpdateByCount = currentCount - lastReportedProgress >= 100;
                            bool shouldUpdateByProgress = progressPercentage - lastReportedProgressPercentage >= 5;
                            bool isLastFile = currentCount == totalFiles;
                            
                            if (shouldUpdateByCount || shouldUpdateByProgress || isLastFile)
                            {
                                int newReportedProgress = Interlocked.Exchange(ref lastReportedProgress, currentCount);
                                double newReportedPercentage = Interlocked.Exchange(ref lastReportedProgressPercentage, progressPercentage);
                                
                                if (newReportedProgress != currentCount) // 确保只有一个线程更新UI
                                {
                                    UpdateInitialBackupProgressNotification(currentCount, totalFiles, $"正在备份: {Path.GetFileName(file)}");
                                }
                            }
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            LogEvent($"无权限访问文件 {file}: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            LogEvent($"处理文件 {file} 时出错: {ex.Message}");
                        }
                    });
                });
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        LogEvent($"无权限访问文件夹 {folder}: {ex.Message}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        LogEvent($"访问文件夹 {folder} 时出错: {ex.Message}");
                        continue;
                    }
                    
                    // 只在文件夹处理完成且有显著进度变化时更新进度
                    double folderProgressPercentage = totalFiles > 0 ? (double)processedFiles / totalFiles * 100 : 0;
                    if (folderProgressPercentage - lastReportedProgressPercentage >= 5 || processedFiles == totalFiles)
                    {
                        UpdateInitialBackupProgressNotification(processedFiles, totalFiles, $"已完成 {Path.GetFileName(folder)}");
                        lastReportedProgressPercentage = folderProgressPercentage;
                    }
                }
                
                // 标记初始备份完成
                _isInitialBackupComplete = true;
                
                LogEvent($"初始文档备份创建完成，共备份 {_fileBackups.Count} 个文件");
                
                // 批量保存备份信息
                SaveBackupInfo();
                
                ShowInitialBackupCompleteNotification(_fileBackups.Count);
                
                // 关闭备份进度对话框
                CloseBackupProgressDialog(_fileBackups.Count);
            }
            catch (Exception ex)
            {
                LogEvent($"创建初始备份失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建初始备份（复制文件到备份目录）
        /// </summary>
        private static void CreateInitialBackup()
        {
            try
            {
                LogEvent("正在创建初始文档备份...");
                
                // 创建任务列表
                var backupTasks = new List<Task>();
                
                // 为每个文件夹中的文档创建备份
                foreach (string folder in _monitoredFolders)
                {
                    if (!Directory.Exists(folder))
                        continue;
                        
                    // 使用并行处理提高备份效率
                    var task = Task.Run(() => CreateBackupForFolder(folder));
                    backupTasks.Add(task);
                }
                
                // 等待所有备份任务完成
                Task.WaitAll(backupTasks.ToArray(), TimeSpan.FromMinutes(10));
                
                LogEvent($"初始文档备份创建完成，共备份 {_fileBackups.Count} 个文件");
            }
            catch (Exception ex)
            {
                LogEvent($"创建初始备份失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 为文件夹中的文档创建备份
        /// </summary>
        private static void CreateBackupForFolder(string folderPath)
        {
            try
            {
                // 获取文件夹中的所有文档文件
                var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)
                    .Where(file => _documentExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToList();
                
                LogEvent($"正在备份文件夹 {folderPath} 中的 {files.Count} 个文件");
                
                // 使用并行处理提高备份效率
                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 8 }, file =>
                {
                    try
                    {
                        // 使用同步方式调用，避免异步开销
                        CreateFileBackupAsync(file).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        LogEvent($"处理文件 {file} 时出错: {ex.Message}");
                    }
                });
                
                LogEvent($"文件夹 {folderPath} 备份完成");
            }
            catch (Exception ex)
            {
                LogEvent($"为文件夹 {folderPath} 创建备份时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建文件备份
        /// </summary>
        private static async Task CreateFileBackupAsync(string filePath, BackupType backupType = BackupType.Initial, bool forceRebackup = false)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    LogEvent($"文件不存在，跳过备份: {filePath}");
                    return;
                }
                
                var fileInfo = new FileInfo(filePath);
                LogEvent($"开始备份文件: {filePath} (大小: {FormatFileSize(fileInfo.Length)})");
                
                // 简化逻辑：只检查是否已有备份且文件修改时间未变化（除非强制重新备份）
                if (!forceRebackup && _fileBackups.TryGetValue(filePath, out var existingBackup))
                {
                    // 检查备份文件是否存在
                    if (File.Exists(existingBackup.BackupPath))
                    {
                        try
                        {
                            // 只比较修改时间，不做复杂的哈希计算
                            var existingBackupInfo = new FileInfo(existingBackup.BackupPath);
                            if (existingBackupInfo.LastWriteTime >= fileInfo.LastWriteTime)
                            {
                                LogEvent($"文件未更新，跳过备份: {filePath}");
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogEvent($"比较文件时出错，将创建新备份: {ex.Message}");
                            // 出错时继续创建新备份
                        }
                    }
                }
                
                // 直接创建备份，不检查哈希值（全量备份策略）
                // 创建备份文件名，使用相对路径和文件名哈希，确保唯一性但不过于复杂
                string relativePath = GetRelativePath(filePath);
                string fileName = Path.GetFileName(filePath);
                string fileExtension = Path.GetExtension(filePath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                
                // 使用文件路径的哈希值作为目录名，避免文件名冲突
                string pathHash = ComputePathHash(relativePath);
                string backupSubDir = Path.Combine(_backupDirectory, pathHash);
                
                // 确保备份子目录存在
                if (!Directory.Exists(backupSubDir))
                {
                    Directory.CreateDirectory(backupSubDir);
                }
                
                string backupPath = Path.Combine(backupSubDir, fileName);
                
                // 使用更大的缓冲区提高性能，并使用同步复制避免异步开销
                const int bufferSize = 1048576; // 1MB缓冲区
                
                using (var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: bufferSize))
                using (var destStream = new FileStream(backupPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: bufferSize))
                {
                    sourceStream.CopyTo(destStream, bufferSize);
                }
                LogEvent($"文件已复制到备份目录: {backupPath}");
                
                // 更新备份信息
                var backup = new FileBackup
                {
                    OriginalPath = filePath,
                    BackupPath = backupPath,
                    CreatedTime = DateTime.Now,
                    FileSize = fileInfo.Length,
                    FileHash = "", // 不再计算哈希，提高效率
                    Version = 1,
                    Type = backupType
                };
                
                _fileBackups.AddOrUpdate(filePath, backup, (key, oldValue) => backup);
                
                // 不在每次备份后保存信息，改为批量保存
                // SaveBackupInfo();
                
                LogEvent($"已创建备份: {filePath} -> {backupPath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                LogEvent($"无权限访问文件 {filePath}: {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                LogEvent($"找不到目录 {filePath}: {ex.Message}");
            }
            catch (FileNotFoundException ex)
            {
                LogEvent($"找不到文件 {filePath}: {ex.Message}");
            }
            catch (IOException ex)
            {
                LogEvent($"IO错误，无法备份文件 {filePath}: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogEvent($"创建文件备份失败 {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理旧版本备份
        /// </summary>
        private static void CleanupOldBackups(string originalPath, int maxVersions)
        {
            try
            {
                // 获取同一原始文件的所有备份
                var backups = _fileBackups.Where(kvp => kvp.Key == originalPath).ToList();
                if (backups.Count <= maxVersions)
                    return;
                
                // 按创建时间排序，保留最新的版本
                var sortedBackups = backups.OrderByDescending(kvp => kvp.Value.CreatedTime).ToList();
                var toDelete = sortedBackups.Skip(maxVersions);
                
                // 删除旧备份
                foreach (var kvp in toDelete)
                {
                    try
                    {
                        if (File.Exists(kvp.Value.BackupPath))
                        {
                            File.Delete(kvp.Value.BackupPath);
                        }
                        _fileBackups.TryRemove(kvp.Key, out _);
                    }
                    catch (Exception ex)
                    {
                        LogEvent($"删除旧备份失败 {kvp.Value.BackupPath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogEvent($"清理旧备份失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置文件系统监控
        /// </summary>
        private static void SetupFileWatchers()
        {
            try
            {
                _watchers = new FileSystemWatcher[_monitoredFolders.Count];
                
                for (int i = 0; i < _monitoredFolders.Count; i++)
                {
                    string folder = _monitoredFolders[i];
                    
                    // 尝试创建目录（如果不存在）
                    try
                    {
                        if (!Directory.Exists(folder))
                        {
                            Directory.CreateDirectory(folder);
                            LogEvent($"创建监控目录: {folder}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogEvent($"无法创建目录 {folder}: {ex.Message}");
                        continue;
                    }
                        
                    var watcher = new FileSystemWatcher(folder)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                    };
                    
                    // 监控文件创建
                    watcher.Created += OnFileCreated;
                    
                    // 监控文件删除
                    watcher.Deleted += OnFileDeleted;
                    
                    // 监控文件修改
                    watcher.Changed += OnFileChanged;
                    
                    // 启用监控
                    watcher.EnableRaisingEvents = true;
                    _watchers[i] = watcher;
                    
                    LogEvent($"已设置文件夹监控: {folder}");
                }
            }
            catch (Exception ex)
            {
                LogEvent($"设置文件监控失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理文件创建事件
        /// </summary>
        private static void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                // 如果正在进行批量还原，跳过所有文件系统事件
                if (_isBulkRestoreInProgress)
                {
                    LogEvent($"批量还原进行中，跳过文件创建事件: {e.FullPath}");
                    return;
                }
                
                string extension = Path.GetExtension(e.FullPath).ToLowerInvariant();
                if (!_documentExtensions.Contains(extension))
                    return;
                
                // 检查文件是否是系统最近修改的（避免无限循环）
                if (_recentlyModifiedFiles.TryGetValue(e.FullPath, out DateTime modificationTime) && 
                    (DateTime.Now - modificationTime).TotalSeconds < 5)
                {
                    // 移除跟踪并跳过处理
                    _recentlyModifiedFiles.TryRemove(e.FullPath, out _);
                    LogEvent($"跳过系统创建的文件: {e.FullPath}");
                    return;
                }
                
                // 如果初始备份未完成，不处理文件创建事件
                if (!_isInitialBackupComplete)
                    return;
                
                LogEvent($"检测到文档创建: {e.FullPath}");
                
                // 异步处理文件创建，避免阻塞
                _ = Task.Run(async () => await HandleFileCreatedAsync(e.FullPath));
            }
            catch (Exception ex)
            {
                LogEvent($"处理文件创建事件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步处理文件创建
        /// </summary>
        private static async Task HandleFileCreatedAsync(string filePath)
        {
            try
            {
                // 检查是否最近已经通知过这个文件（防抖机制）
                if (_recentlyNotifiedFiles.TryGetValue(filePath, out DateTime lastNotificationTime))
                {
                    // 如果在30秒内已经通知过，则跳过
                    if ((DateTime.Now - lastNotificationTime).TotalSeconds < 30)
                    {
                        LogEvent($"跳过重复创建通知: {filePath} (距离上次通知 {(DateTime.Now - lastNotificationTime).TotalSeconds:F1} 秒)");
                        return;
                    }
                }
                
                // 标记为已通知
                _recentlyNotifiedFiles.AddOrUpdate(filePath, DateTime.Now, (key, oldValue) => DateTime.Now);
                
                // 等待文件创建完成，避免访问未完全创建的文件
                await Task.Delay(500);
                
                // 检查文件是否仍然存在
                if (!File.Exists(filePath))
                    return;
                
                // 创建新文件的备份
                LogEvent($"为新创建的文档创建备份: {filePath}");
                await CreateFileBackupAsync(filePath, BackupType.Created);
                
                // 显示恢复通知
                ShowFileRecoveryNotification(filePath, "created");
                
                // 设置一个定时器，在24小时后移除通知记录
                _ = Task.Run(async () =>
                {
                    await Task.Delay(86400000); // 等待24小时 (24 * 60 * 60 * 1000)
                    _recentlyNotifiedFiles.TryRemove(filePath, out _);
                    LogEvent($"移除创建通知记录: {filePath}");
                });
            }
            catch (Exception ex)
            {
                LogEvent($"处理文件创建失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理文件删除事件
        /// </summary>
        private static void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                // 如果正在进行批量还原，跳过所有文件系统事件
                if (_isBulkRestoreInProgress)
                {
                    LogEvent($"批量还原进行中，跳过文件删除事件: {e.FullPath}");
                    return;
                }
                
                string extension = Path.GetExtension(e.FullPath).ToLowerInvariant();
                if (!_documentExtensions.Contains(extension))
                    return;
                
                // 检查文件是否是系统最近修改的（避免无限循环）
                if (_recentlyModifiedFiles.TryGetValue(e.FullPath, out DateTime modificationTime) && 
                    (DateTime.Now - modificationTime).TotalSeconds < 5)
                {
                    // 移除跟踪并跳过处理
                    _recentlyModifiedFiles.TryRemove(e.FullPath, out _);
                    LogEvent($"跳过系统删除的文件: {e.FullPath}");
                    return;
                }
                
                LogEvent($"检测到文档删除: {e.FullPath}");
                
                // 异步处理文件删除，避免阻塞
                _ = Task.Run(async () => await HandleFileDeletedAsync(e.FullPath));
            }
            catch (Exception ex)
            {
                LogEvent($"处理文件删除事件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步处理文件删除
        /// </summary>
        private static async Task HandleFileDeletedAsync(string filePath)
        {
            try
            {
                // 检查是否最近已经通知过这个文件（防抖机制）
                if (_recentlyNotifiedFiles.TryGetValue(filePath, out DateTime lastNotificationTime))
                {
                    // 如果在30秒内已经通知过，则跳过
                    if ((DateTime.Now - lastNotificationTime).TotalSeconds < 30)
                    {
                        LogEvent($"跳过重复删除通知: {filePath} (距离上次通知 {(DateTime.Now - lastNotificationTime).TotalSeconds:F1} 秒)");
                        return;
                    }
                }
                
                // 标记为已通知
                _recentlyNotifiedFiles.AddOrUpdate(filePath, DateTime.Now, (key, oldValue) => DateTime.Now);
                
                // 检查是否已有备份，如果没有则尝试从回收站或其他位置恢复
                if (!_fileBackups.ContainsKey(filePath))
                {
                    LogEvent($"文件 {filePath} 没有备份记录，尝试查找最近的备份");
                    
                    // 尝试在备份目录中查找同名文件的备份
                    await Task.Run(() => FindAndRegisterExistingBackup(filePath));
                }
                
                // 显示恢复通知
                ShowFileRecoveryNotification(filePath, "deleted");
                
                // 设置一个定时器，在24小时后移除通知记录
                _ = Task.Run(async () =>
                {
                    await Task.Delay(86400000); // 等待24小时 (24 * 60 * 60 * 1000)
                    _recentlyNotifiedFiles.TryRemove(filePath, out _);
                    LogEvent($"移除删除通知记录: {filePath}");
                });
            }
            catch (Exception ex)
            {
                LogEvent($"处理文件删除失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 查找并注册已存在的备份文件
        /// </summary>
        private static void FindAndRegisterExistingBackup(string originalPath)
        {
            try
            {
                if (!Directory.Exists(_backupDirectory))
                    return;
                
                string fileName = Path.GetFileName(originalPath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
                string extension = Path.GetExtension(originalPath);
                
                // 在备份目录中查找匹配的文件
                var backupFiles = Directory.GetFiles(_backupDirectory, $"{fileNameWithoutExt}_*{extension}")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .ToList();
                
                if (backupFiles.Any())
                {
                    // 使用最新的备份文件
                    string latestBackup = backupFiles.First();
                    var fileInfo = new FileInfo(latestBackup);
                    
                    // 创建备份记录
                    var backup = new FileBackup
                    {
                        OriginalPath = originalPath,
                        BackupPath = latestBackup,
                        CreatedTime = fileInfo.CreationTime,
                        FileSize = fileInfo.Length,
                        FileHash = "", // 不再计算哈希，提高效率
                        Version = 1
                    };
                    
                    _fileBackups.TryAdd(originalPath, backup);
                    LogEvent($"已找到并注册现有备份: {latestBackup}");
                }
            }
            catch (Exception ex)
            {
                LogEvent($"查找现有备份失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理文件修改事件
        /// </summary>
        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // 如果正在进行批量还原，跳过所有文件系统事件
                if (_isBulkRestoreInProgress)
                {
                    LogEvent($"批量还原进行中，跳过文件修改事件: {e.FullPath}");
                    return;
                }
                
                string extension = Path.GetExtension(e.FullPath).ToLowerInvariant();
                if (!_documentExtensions.Contains(extension))
                    return;
                
                // 检查文件是否存在（可能是修改后立即删除）
                if (!File.Exists(e.FullPath))
                    return;
                
                // 如果初始备份未完成，不处理文件修改事件
                if (!_isInitialBackupComplete)
                    return;
                
                // 检查文件是否是系统最近修改的（避免无限循环）
                if (_recentlyModifiedFiles.TryGetValue(e.FullPath, out DateTime modificationTime) && 
                    (DateTime.Now - modificationTime).TotalSeconds < 5)
                {
                    // 移除跟踪并跳过处理
                    _recentlyModifiedFiles.TryRemove(e.FullPath, out _);
                    LogEvent($"跳过系统修改的文件: {e.FullPath}");
                    return;
                }
                
                // 异步处理文件修改，避免阻塞
                _ = Task.Run(async () => await HandleFileChangedAsync(e.FullPath));
            }
            catch (Exception ex)
            {
                LogEvent($"处理文件修改事件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步处理文件修改
        /// </summary>
        private static async Task HandleFileChangedAsync(string filePath)
        {
            try
            {
                // 检查是否最近已经通知过这个文件（防抖机制）
                if (_recentlyNotifiedFiles.TryGetValue(filePath, out DateTime lastNotificationTime))
                {
                    // 如果在30秒内已经通知过，则跳过
                    if ((DateTime.Now - lastNotificationTime).TotalSeconds < 30)
                    {
                        LogEvent($"跳过重复通知: {filePath} (距离上次通知 {(DateTime.Now - lastNotificationTime).TotalSeconds:F1} 秒)");
                        return;
                    }
                }
                
                // 标记为已通知
                _recentlyNotifiedFiles.AddOrUpdate(filePath, DateTime.Now, (key, oldValue) => DateTime.Now);
                
                // 直接创建备份，不检查文件内容变化（全量备份策略）
                LogEvent($"检测到文档修改: {filePath}");
                
                // 创建新的备份
                await CreateFileBackupAsync(filePath, BackupType.Modified);
                
                // 显示恢复通知
                ShowFileRecoveryNotification(filePath, "modified");
                
                // 设置一个定时器，在24小时后移除通知记录
                _ = Task.Run(async () =>
                {
                    await Task.Delay(86400000); // 等待24小时 (24 * 60 * 60 * 1000)
                    _recentlyNotifiedFiles.TryRemove(filePath, out _);
                    LogEvent($"移除通知记录: {filePath}");
                });
            }
            catch (Exception ex)
            {
                LogEvent($"处理文件修改失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示文件恢复通知
        /// </summary>
        private static void ShowFileRecoveryNotification(string filePath, string changeType)
        {
            try
            {
                lock (_notificationLock)
                {
                    _recentChangeCount++;
                    
                    // 将文件添加到当前批次列表
                    if (!_currentBatchFiles.Contains(filePath))
                    {
                        _currentBatchFiles.Add(filePath);
                    }
                    
                    // 重置计时器（如果存在）
                    if (_notificationBatchTimer != null)
                    {
                        _notificationBatchTimer.Dispose();
                    }
                    
                    // 设置计时器，5秒后检查是否需要显示通知
                    _notificationBatchTimer = new Timer((state) =>
                    {
                        lock (_notificationLock)
                        {
                            if (_recentChangeCount >= 5)
                            {
                                // 显示批量通知，传递当前批次的文件列表
                                ShowBatchFileRecoveryNotification(_recentChangeCount, new List<string>(_currentBatchFiles));
                            }
                            _recentChangeCount = 0;
                            _currentBatchFiles.Clear();
                            _notificationBatchTimer?.Dispose();
                            _notificationBatchTimer = null;
                        }
                    }, state: null, dueTime: 5000, period: Timeout.Infinite);
                }
                
                LogEvent($"记录文件更改: {Path.GetFileName(filePath)} ({changeType}), 当前计数: {_recentChangeCount}");
            }
            catch (Exception ex)
            {
                LogEvent($"记录文件更改失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 显示批量文件恢复通知
        /// </summary>
        private static void ShowBatchFileRecoveryNotification(int changeCount, List<string> batchFiles)
        {
            try
            {
                // 将文件列表转换为JSON字符串传递
                string filesJson = System.Text.Json.JsonSerializer.Serialize(batchFiles);
                
                // 使用ToastContentBuilder创建批量通知
                var builder = new ToastContentBuilder()
                    .AddText("XIGUASecurity 文档保护")
                    .AddText($"检测到 {changeCount} 个文档可能被篡改")
                    .AddText("点击回滚所有修改")
                    
                    // 添加回滚按钮，传递文件列表
                    .AddButton(new ToastButton()
                        .SetContent("回滚所有修改")
                        .AddArgument("action", "restoreBatch")
                        .AddArgument("files", filesJson)
                        .SetBackgroundActivation())
                        
                    // 添加忽略按钮
                    .AddButton(new ToastButton()
                        .SetContent("忽略")
                        .AddArgument("action", "ignoreBatch")
                        .SetBackgroundActivation());

                // 显示通知
                builder.Show();
                
                LogEvent($"已显示批量文件恢复通知: {changeCount} 个文件");
            }
            catch (Exception ex)
            {
                LogEvent($"显示批量文件恢复通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取文件的备份路径
        /// </summary>
        public static string? GetBackupPath(string originalPath)
        {
            try
            {
                if (_fileBackups.TryGetValue(originalPath, out var backup))
                {
                    return backup.BackupPath;
                }
                return null;
            }
            catch (Exception ex)
            {
                LogEvent($"获取备份路径失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从备份恢复文件（新实现，解决COMException问题）
        /// </summary>
        public static bool RestoreFileFromBackupNew(string originalPath)
        {
            try
            {
                if (!_fileBackups.TryGetValue(originalPath, out var backup))
                {
                    LogEvent($"找不到文件的备份信息: {originalPath}");
                    return false;
                }
                
                // 检查备份文件是否存在
                if (!File.Exists(backup.BackupPath))
                {
                    LogEvent($"备份文件不存在: {backup.BackupPath}");
                    return false;
                }
                
                // 确保目标目录存在
                string? targetDirectory = Path.GetDirectoryName(originalPath);
                if (!string.IsNullOrEmpty(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }
                
                // 标记文件为系统修改，避免触发无限循环
                _recentlyModifiedFiles[originalPath] = DateTime.Now;
                
                // 创建临时备份，以防恢复失败
                string tempBackup = "";
                if (File.Exists(originalPath))
                {
                    tempBackup = Path.Combine(Path.GetTempPath(), $"xdows_backup_{Path.GetFileName(originalPath)}_{DateTime.Now:yyyyMMdd_HHmmss}");
                    try
                    {
                        File.Copy(originalPath, tempBackup, true);
                    }
                    catch (Exception ex)
                    {
                        LogEvent($"创建临时备份失败: {ex.Message}");
                        // 继续尝试恢复，即使临时备份失败
                    }
                }
                
                try
                {
                    // 使用更安全的文件复制方法
                    using (var source = new FileStream(backup.BackupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (var destination = new FileStream(originalPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            // 复制文件内容
                            source.CopyTo(destination);
                            
                            // 确保所有数据写入磁盘
                            destination.Flush(true);
                        }
                    }
                    
                    LogEvent($"已从备份恢复文件: {originalPath}");
                    
                    // 删除临时备份
                    if (!string.IsNullOrEmpty(tempBackup) && File.Exists(tempBackup))
                    {
                        try
                        {
                            File.Delete(tempBackup);
                        }
                        catch (Exception ex)
                        {
                            LogEvent($"删除临时备份失败: {ex.Message}");
                        }
                    }
                    
                    return true;
                }
                catch (Exception ex)
                {
                    LogEvent($"恢复文件失败: {ex.Message}");
                    
                    // 如果恢复失败，尝试恢复临时备份
                    if (!string.IsNullOrEmpty(tempBackup) && File.Exists(tempBackup))
                    {
                        try
                        {
                            File.Copy(tempBackup, originalPath, true);
                            LogEvent($"已从临时备份恢复原文件: {originalPath}");
                        }
                        catch (Exception restoreEx)
                        {
                            LogEvent($"从临时备份恢复失败: {restoreEx.Message}");
                        }
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogEvent($"恢复文件失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从备份恢复文件
        /// </summary>
        public static bool RestoreFileFromBackup(string originalPath)
        {
            try
            {
                if (!_fileBackups.TryGetValue(originalPath, out var backup))
                {
                    LogEvent($"找不到文件的备份信息: {originalPath}");
                    return false;
                }
                
                // 检查备份文件是否存在
                if (!File.Exists(backup.BackupPath))
                {
                    LogEvent($"备份文件不存在: {backup.BackupPath}");
                    return false;
                }
                
                // 确保目标目录存在
                string? targetDirectory = Path.GetDirectoryName(originalPath);
                if (!string.IsNullOrEmpty(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }
                
                // 检查文件是否被占用，如果被占用则尝试解锁
                if (File.Exists(originalPath))
                {
                    // 尝试获取文件的读写权限
                    try
                    {
                        // 检查文件是否可写
                        using (var fs = new FileStream(originalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            // 如果能打开文件，说明没有被占用
                            fs.Close();
                        }
                    }
                    catch (IOException)
                    {
                        LogEvent($"文件被占用，无法恢复: {originalPath}");
                        return false;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        LogEvent($"无权限访问文件: {originalPath}");
                        return false;
                    }
                }
                
                // 标记文件为系统修改，避免触发无限循环
                _recentlyModifiedFiles[originalPath] = DateTime.Now;
                
                // 创建临时备份，以防恢复失败
                string tempBackup = "";
                if (File.Exists(originalPath))
                {
                    tempBackup = Path.Combine(Path.GetTempPath(), $"xdows_backup_{Path.GetFileName(originalPath)}_{DateTime.Now:yyyyMMdd_HHmmss}");
                    try
                    {
                        File.Copy(originalPath, tempBackup, true);
                    }
                    catch (Exception ex)
                    {
                        LogEvent($"创建临时备份失败: {ex.Message}");
                        // 继续尝试恢复，即使临时备份失败
                    }
                }
                
                try
                {
                    // 从备份恢复文件
                    File.Copy(backup.BackupPath, originalPath, true);
                    LogEvent($"已从备份恢复文件: {originalPath}");
                    
                    // 删除临时备份
                    if (!string.IsNullOrEmpty(tempBackup) && File.Exists(tempBackup))
                    {
                        try
                        {
                            File.Delete(tempBackup);
                        }
                        catch (Exception ex)
                        {
                            LogEvent($"删除临时备份失败: {ex.Message}");
                        }
                    }
                    
                    return true;
                }
                catch (Exception ex)
                {
                    LogEvent($"恢复文件失败: {ex.Message}");
                    
                    // 如果恢复失败，尝试恢复临时备份
                    if (!string.IsNullOrEmpty(tempBackup) && File.Exists(tempBackup))
                    {
                        try
                        {
                            File.Copy(tempBackup, originalPath, true);
                            LogEvent($"已从临时备份恢复原文件: {originalPath}");
                        }
                        catch (Exception restoreEx)
                        {
                            LogEvent($"从临时备份恢复失败: {restoreEx.Message}");
                        }
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogEvent($"恢复文件失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 删除文件备份
        /// </summary>
        public static bool DeleteFileBackup(string originalPath)
        {
            try
            {
                if (!_fileBackups.TryRemove(originalPath, out var backup))
                {
                    LogEvent($"找不到文件的备份信息: {originalPath}");
                    return false;
                }
                
                // 删除备份文件
                if (File.Exists(backup.BackupPath))
                {
                    File.Delete(backup.BackupPath);
                    LogEvent($"已删除备份文件: {backup.BackupPath}");
                }
                
                // 保存更新后的备份信息
                SaveBackupInfo();
                
                LogEvent($"已删除文件备份记录: {originalPath}");
                return true;
            }
            catch (Exception ex)
            {
                LogEvent($"删除文件备份失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 恢复文件
        /// </summary>
        public static bool RestoreFile(string filePath)
        {
            try
            {
                if (!_fileBackups.TryGetValue(filePath, out var backup))
                {
                    LogEvent($"找不到文件的备份信息: {filePath}");
                    return false;
                }
                
                // 检查备份文件是否存在
                if (!File.Exists(backup.BackupPath))
                {
                    LogEvent($"备份文件不存在: {backup.BackupPath}");
                    return false;
                }
                
                // 检查文件是否被占用
                if (File.Exists(filePath))
                {
                    try
                    {
                        // 检查文件是否可写
                        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            // 如果能打开文件，说明没有被占用
                            fs.Close();
                        }
                    }
                    catch (IOException)
                    {
                        LogEvent($"文件被占用，无法恢复: {filePath}");
                        return false;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        LogEvent($"无权限访问文件: {filePath}");
                        return false;
                    }
                }
                
                // 创建临时备份，以防恢复失败
                string tempBackup = "";
                if (File.Exists(filePath))
                {
                    tempBackup = Path.Combine(Path.GetTempPath(), $"xdows_backup_{Path.GetFileName(filePath)}_{DateTime.Now:yyyyMMdd_HHmmss}");
                    try
                    {
                        File.Copy(filePath, tempBackup, true);
                    }
                    catch (Exception ex)
                    {
                        LogEvent($"创建临时备份失败: {ex.Message}");
                        // 继续尝试恢复，即使临时备份失败
                    }
                }
                
                // 标记文件为系统修改，避免触发无限循环
                _recentlyModifiedFiles[filePath] = DateTime.Now;
                
                try
                {
                    // 从备份恢复文件
                    File.Copy(backup.BackupPath, filePath, true);
                    LogEvent($"已从备份恢复文件: {filePath}");
                    
                    // 删除临时备份
                    if (!string.IsNullOrEmpty(tempBackup) && File.Exists(tempBackup))
                    {
                        try
                        {
                            File.Delete(tempBackup);
                        }
                        catch (Exception ex)
                        {
                            LogEvent($"删除临时备份失败: {ex.Message}");
                        }
                    }
                    
                    return true;
                }
                catch (Exception ex)
                {
                    LogEvent($"恢复文件失败: {ex.Message}");
                    
                    // 如果恢复失败，尝试恢复临时备份
                    if (!string.IsNullOrEmpty(tempBackup) && File.Exists(tempBackup))
                    {
                        try
                        {
                            File.Copy(tempBackup, filePath, true);
                            LogEvent($"已从临时备份恢复原文件: {filePath}");
                        }
                        catch (Exception restoreEx)
                        {
                            LogEvent($"从临时备份恢复失败: {restoreEx.Message}");
                        }
                    }
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogEvent($"恢复文件失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 创建定期备份（保存文件变更记录）
        /// </summary>
        private static void CreatePeriodicBackup(object? state)
        {
            try
            {
                if (!_isEnabled)
                    return;
                
                LogEvent("正在保存文件变更记录...");
                
                // 清理过期的备份记录（保留最近7天）
                CleanupExpiredBackups();
                
                // 清理过期的文件跟踪记录（超过1分钟的记录）
                CleanupExpiredFileTracking();
                
                LogEvent($"文件变更记录已保存，共记录 {_fileBackups.Count} 个文件");
            }
            catch (Exception ex)
            {
                LogEvent($"创建定期备份失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 计算文件哈希值
        /// </summary>
        private static string CalculateFileHash(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return string.Empty;
                
                var fileInfo = new FileInfo(filePath);
                
                // 如果文件太大，跳过哈希计算
                if (fileInfo.Length > MaxFileSizeForHash)
                {
                    LogEvent($"文件过大，跳过哈希计算: {filePath} ({fileInfo.Length / 1024 / 1024} MB)");
                    return $"large_file_{fileInfo.Length}_{fileInfo.LastWriteTime:yyyyMMddHHmmss}";
                }
                
                // 使用文件流读取，设置FileShare.ReadWrite以允许其他进程访问
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var md5 = MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                LogEvent($"计算文件哈希失败: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 清理过期的备份记录（保留最近7天）
        /// </summary>
        private static void CleanupExpiredBackups()
        {
            try
            {
                DateTime cutoffDate = DateTime.Now.AddDays(-7);
                var expiredBackups = _fileBackups.Where(kvp => kvp.Value.CreatedTime < cutoffDate).ToList();
                
                foreach (var expired in expiredBackups)
                {
                    try
                    {
                        // 删除备份文件
                        if (!string.IsNullOrEmpty(expired.Value.BackupPath) && File.Exists(expired.Value.BackupPath))
                        {
                            File.Delete(expired.Value.BackupPath);
                        }
                        
                        // 从记录中移除
                        if (_fileBackups.TryRemove(expired.Key, out _))
                        {
                            LogEvent($"已移除过期备份记录: {expired.Key}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogEvent($"清理过期备份失败 {expired.Key}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogEvent($"清理过期备份时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理过期的文件跟踪记录
        /// </summary>
        private static void CleanupExpiredFileTracking()
        {
            try
            {
                // 清理超过1分钟的跟踪记录
                DateTime cutoffTime = DateTime.Now.AddMinutes(-1);
                var expiredEntries = _recentlyModifiedFiles.Where(kvp => kvp.Value < cutoffTime).ToList();
                
                foreach (var expired in expiredEntries)
                {
                    if (_recentlyModifiedFiles.TryRemove(expired.Key, out _))
                    {
                        LogEvent($"已移除过期文件跟踪记录: {expired.Key}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogEvent($"清理过期文件跟踪记录时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存备份信息到文件
        /// </summary>
        private static void SaveBackupInfo()
        {
            try
            {
                // 确保目录存在
                string? directory = Path.GetDirectoryName(_backupInfoFile);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // 创建备份信息列表
                var backupInfoList = _fileBackups.Values.ToList();
                
                // 序列化为JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                string json = JsonSerializer.Serialize(backupInfoList, options);
                
                // 写入文件
                File.WriteAllText(_backupInfoFile, json);
                
                LogEvent($"已保存备份信息到文件: {_backupInfoFile}");
            }
            catch (Exception ex)
            {
                LogEvent($"保存备份信息失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从文件加载备份信息
        /// </summary>
        private static void LoadBackupInfo()
        {
            try
            {
                // 检查文件是否存在
                if (!File.Exists(_backupInfoFile))
                {
                    LogEvent("备份信息文件不存在，跳过加载");
                    return;
                }
                
                // 读取文件内容
                string json = File.ReadAllText(_backupInfoFile);
                
                // 反序列化
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var backupInfoList = JsonSerializer.Deserialize<List<FileBackup>>(json, options);
                
                if (backupInfoList != null)
                {
                    // 清空当前备份信息
                    _fileBackups.Clear();
                    
                    // 加载备份信息
                    foreach (var backup in backupInfoList)
                    {
                        // 检查备份文件是否仍然存在
                        if (File.Exists(backup.BackupPath))
                        {
                            _fileBackups[backup.OriginalPath] = backup;
                        }
                        else
                        {
                            LogEvent($"备份文件不存在，跳过加载: {backup.BackupPath}");
                        }
                    }
                    
                    LogEvent($"已加载 {_fileBackups.Count} 个备份信息");
                }
            }
            catch (Exception ex)
            {
                LogEvent($"加载备份信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 记录事件日志
        /// </summary>
        /// <summary>
        /// 显示备份进度对话框
        /// </summary>
        private static void ShowBackupProgressDialog(Window? mainWindow)
        {
            try
            {
                if (mainWindow != null)
                {
                    mainWindow.DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            _backupProgressDialog = new UI.Dialogs.BackupProgressDialog();
                            _backupProgressDialog.XamlRoot = mainWindow.Content.XamlRoot;
                            
                            // 异步显示对话框，不阻塞当前线程
                            _ = _backupProgressDialog.ShowAsync();
                        }
                        catch (Exception ex)
                        {
                            LogEvent($"显示备份进度对话框失败: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogEvent($"创建备份进度对话框失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新备份进度
        /// </summary>
        private static void UpdateBackupProgress(int current, int total, string currentFile)
        {
            try
            {
                if (_backupProgressDialog != null)
                {
                    _backupProgressDialog.UpdateProgress(current, total, currentFile);
                }
            }
            catch (Exception ex)
            {
                LogEvent($"更新备份进度失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 关闭备份进度对话框
        /// </summary>
        private static void CloseBackupProgressDialog(int totalFiles)
        {
            try
            {
                if (_backupProgressDialog != null)
                {
                    _backupProgressDialog.ShowCompletion(totalFiles);
                    
                    // 延迟关闭对话框，让用户看到完成状态
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000); // 等待2秒
                        if (_backupProgressDialog != null)
                        {
                            _backupProgressDialog.DispatcherQueue.TryEnqueue(() =>
                            {
                                _backupProgressDialog.Hide();
                                _backupProgressDialog = null;
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogEvent($"关闭备份进度对话框失败: {ex.Message}");
                // 如果关闭失败，强制清除引用
                _backupProgressDialog = null;
            }
        }

        private static void LogEvent(string message)
        {
            LogText.AddNewLog(LogLevel.INFO, "DocumentProtection", message);
        }

        /// <summary>
        /// 获取文件相对于监控目录的路径
        /// </summary>
        private static string GetRelativePath(string fullPath)
        {
            foreach (string folder in _monitoredFolders)
            {
                if (fullPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                {
                    return fullPath.Substring(folder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
            }
            return fullPath;
        }

        /// <summary>
        /// 计算路径的哈希值，用于创建备份子目录
        /// </summary>
        private static string ComputePathHash(string path)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(path));
                return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 16);
            }
        }
    }
}