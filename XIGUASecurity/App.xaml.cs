using Compatibility.Windows.Storage;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinUI3Localizer;
using XIGUASecurity.Protection;
using XIGUASecurity.Services;
using static XIGUASecurity.Protection.CallBack;

namespace XIGUASecurity
{
    public record UpdateInfo
    {
        public string Version { get; set; } = string.Empty;
        public string Build { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Checksum { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public bool Mandatory { get; set; }
        public List<string> Changelog { get; set; } = [];
        public bool HasUpdate { get; set; }
        public bool IsCurrentVersionNewer { get; set; } // 当前版本是否高于或等于服务器版本

        // 兼容性属性
        public string Title => $"XIGUASecurity {Version} 更新";
        public string Content
        {
            get
            {
                var content = $"**版本:** {Version}  \n";
                content += $"**构建:** {Build}  \n";
                content += $"**发布日期:** {ReleaseDate}  \n";
                content += $"**描述:** {Description}  \n";
                content += $"**大小:** {Size / 1024 / 1024} MB  \n";

                if (Changelog.Count > 0)
                {
                    content += "\n**更新日志:**\n";
                    foreach (var item in Changelog)
                    {
                        content += $"• {item}\n\n";
                    }
                }

                return content;
            }
        }
    }

    public static class Updater
    {
        private static readonly HttpClient _httpClient = new();

        static Updater()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("XIGUASecurity/4.1");
        }

        /// <summary>
        /// 比较版本号
        /// </summary>
        /// <param name="currentVersion">当前版本</param>
        /// <param name="newVersion">新版本</param>
        /// <returns>如果新版本大于当前版本返回true，否则返回false</returns>
        private static bool IsNewerVersion(string currentVersion, string newVersion)
        {
            try
            {
                // 移除可能的非数字字符（如"-Dev"）
                Version current = Version.Parse(System.Text.RegularExpressions.Regex.Replace(currentVersion, @"[^\d\.]", ""));
                Version newer = Version.Parse(System.Text.RegularExpressions.Regex.Replace(newVersion, @"[^\d\.]", ""));

                return newer > current;
            }
            catch
            {
                // 如果版本解析失败，使用字符串比较
                return string.Compare(newVersion, currentVersion, StringComparison.Ordinal) > 0;
            }
        }

        public static async Task<UpdateInfo?> CheckUpdateAsync()
        {
            try
            {
                const string versionUrl = "http://103.118.245.82:7500/version";
                string json = await _httpClient.GetStringAsync(versionUrl);
                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 获取服务器信息
                var serverInfo = root.GetProperty("server_info");
                bool hasUpdate = serverInfo.GetProperty("has_update").GetBoolean();

                // 获取最新版本信息
                var latestVersion = root.GetProperty("latest_version");
                string latestVersionString = latestVersion.GetProperty("version").GetString() ?? string.Empty;

                UpdateInfo updateInfo = new()
                {
                    Version = latestVersionString,
                    Build = latestVersion.GetProperty("build").GetString() ?? string.Empty,
                    ReleaseDate = latestVersion.GetProperty("release_date").GetString() ?? string.Empty,
                    Description = latestVersion.GetProperty("description").GetString() ?? string.Empty,
                    Size = latestVersion.GetProperty("size").GetInt64(),
                    Checksum = latestVersion.GetProperty("checksum").GetString() ?? string.Empty,
                    DownloadUrl = "http://103.118.245.82:7000/download", // 使用正确的下载地址
                    Mandatory = latestVersion.GetProperty("mandatory").GetBoolean(),
                    HasUpdate = hasUpdate || IsNewerVersion(AppInfo.AppVersion, latestVersionString),
                    IsCurrentVersionNewer = !IsNewerVersion(AppInfo.AppVersion, latestVersionString) // 当前版本是否高于或等于服务器版本
                };

                // 如果当前版本与服务器版本相同，则不需要更新
                if (AppInfo.AppVersion == latestVersionString)
                {
                    updateInfo.HasUpdate = false;
                    updateInfo.IsCurrentVersionNewer = true;
                }

                // 获取更新日志
                if (latestVersion.TryGetProperty("changelog", out var changelog) && changelog.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in changelog.EnumerateArray())
                    {
                        updateInfo.Changelog.Add(item.GetString() ?? string.Empty);
                    }
                }

                return updateInfo;
            }
            catch
            {
                return null; // 或可抛出异常，依需求而定
            }
        }
    }
    public class AppInfo
    {
        public static readonly string AppName = "XIGUASecurity";
        public static readonly string AppVersion = "10.1";
        public static readonly string AppFeedback = "https://github.com/XTY64XTY12345/XIGUASecurity/issues/new/choose";
        public static readonly string AppWebsite = "https://xty64xty.netlify.app/";
    }
    public static class ProtectionManager
    {
        public static bool IsOpen()
        {
            return true;
        }

        public static InterceptCallBack interceptCallBack = (isSucceed, path, type) =>
        {
            string entityName = type == "Reg" ? "注册表项" : "文件";
            string actionName = type == "Reg" ? "修改" : "操作";

            LogText.AddNewLog(LogLevel.WARN, "Protection", isSucceed
                ? $"已拦截{entityName}{actionName}：{Path.GetFileName(path)}"
                : $"无法拦截{entityName}{actionName}：{Path.GetFileName(path)}");

            _ = (App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
            {
                InterceptWindow.ShowOrActivate(false, path, type);
            }));
        };

        public static bool Run(int RunID)
        {
            switch (RunID)
            {
                case 0:
                    if (ProcessProtection.IsEnabled())
                    {
                        LogText.AddNewLog(LogLevel.INFO, "Protection", $"Try to Disable ProcessProtection ...");
                        return XIGUASecurity.Protection.ProcessProtection.Disable();
                    }
                    else
                    {
                        LogText.AddNewLog(LogLevel.INFO, "Protection", $"Try to Enable ProcessProtection ...");
                        return XIGUASecurity.Protection.ProcessProtection.Enable(interceptCallBack);
                    }
                case 1:
                    if (FilesProtection.IsEnabled())
                    {
                        LogText.AddNewLog(LogLevel.INFO, "Protection", $"Try to Disable FilesProtection ...");
                        return XIGUASecurity.Protection.FilesProtection.Disable();
                    }
                    else
                    {
                        LogText.AddNewLog(LogLevel.INFO, "Protection", $"Try to Enable FilesProtection ...");
                        return XIGUASecurity.Protection.FilesProtection.Enable(interceptCallBack);
                    }
                case 4:
                    if (RegistryProtection.IsEnabled())
                    {
                        LogText.AddNewLog(LogLevel.INFO, "Protection", $"Try to Disable RegistryProtection ...");
                        return XIGUASecurity.Protection.RegistryProtection.Disable();
                    }
                    else
                    {
                        LogText.AddNewLog(LogLevel.INFO, "Protection", $"Try to Enable RegistryProtection ...");
                        return XIGUASecurity.Protection.RegistryProtection.Enable(interceptCallBack);
                    }
                case 5:
                    if (DocumentProtection.IsEnabled())
                    {
                        LogText.AddNewLog(LogLevel.INFO, "Protection", $"Try to Disable DocumentProtection ...");
                        return XIGUASecurity.Protection.DocumentProtection.Disable();
                    }
                    else
                    {
                        LogText.AddNewLog(LogLevel.INFO, "Protection", $"Try to Enable DocumentProtection ...");
                        return XIGUASecurity.Protection.DocumentProtection.Enable(App.MainWindow ?? null);
                    }
                default:
                    return false;
            }
        }
    }

    public static class Statistics
    {
        public static int ScansQuantity { get; set; } = 0;
        public static int VirusQuantity { get; set; } = 0;
    }
    /// <summary>
    /// 日志级别的枚举类型，定义了不同的日志级别。
    /// </summary>
    public enum LogLevel
    {
        DEBUG,  // 调试日志
        INFO,   // 信息日志
        WARN,   // 警告日志
        ERROR,  // 错误日志
        FATAL   // 致命错误日志
    }

    public static class LogText
    {
        #region 对外保持不变的接口
        public static event EventHandler? TextChanged;
        public static string Text => _hotCache.ToString();

        public static void ClearLog()
        {
            lock (_hotCache)
            {
                _ = _hotCache.Clear();
                _hotLines = 0;
            }

            AddNewLog(LogLevel.INFO, "LogSystem", "Log is cleared");
        }
        #endregion

        #region 配置（可抽出去读 JSON）
        private const int HOT_MAX_LINES = 500;
        private const int HOT_MAX_BYTES = 80_000;
        private const int BATCH_SIZE = 100;
        private static readonly TimeSpan RetainAge = TimeSpan.FromDays(7);
        #endregion

        #region 路径 & 文件
        private static readonly string BaseFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "XIGUASecurity");

        private static string CurrentFilePath =>
            Path.Combine(BaseFolder, $"logs-{DateTime.Now:yyyy-MM-dd}.txt");
        #endregion

        #region 并发容器
        private static readonly StringBuilder _hotCache = new();
        private static readonly ConcurrentQueue<LogRow> _pending = new();
        private static int _hotLines;
        private static readonly SemaphoreSlim _signal = new(0, int.MaxValue);
        #endregion

        #region 启动后台写盘
        static LogText()
        {
            _ = Directory.CreateDirectory(BaseFolder);
            _ = Task.Run(WritePump);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                AddNewLog(LogLevel.FATAL, "Unhandled", e.ExceptionObject.ToString()!);
        }
        #endregion

        #region 对外唯一写入口
        public static void AddNewLog(LogLevel level, string source, string info)
        {
            LogRow row = new()
            {
                Time = DateTime.Now,
                Level = (int)level,
                Source = source,
                Text = info
            };

            _pending.Enqueue(row);
            _ = _signal.Release();
            AppendToHotCache(row);
        }
        #endregion

        #region 热缓存（线程安全）
        private static void AppendToHotCache(LogRow row)
        {
            lock (_hotCache)
            {
                if (_hotLines >= HOT_MAX_LINES || _hotCache.Length >= HOT_MAX_BYTES)
                {
                    TrimHotHead();
                }

                _ = _hotCache.AppendLine(FormatRow(row));
                _hotLines++;
            }

            RaiseChangedThrottled();
        }

        private static void TrimHotHead()
        {
            int cut = _hotCache.ToString().IndexOf('\n') + 1;
            if (cut > 0)
            {
                _ = _hotCache.Remove(0, cut);
                _hotLines--;
            }
        }
        #endregion

        #region 事件节流
        private static Timer? _throttleTimer;
        private static void RaiseChangedThrottled()
        {
            if (XIGUASecurity.MainWindow.NowPage != "Home")
            {
                return;
            }

            _throttleTimer?.Dispose();
            _throttleTimer = new Timer(_ => TextChanged?.Invoke(null, EventArgs.Empty),
                                       null, 100, Timeout.Infinite);
        }
        #endregion

        #region 后台写盘泵
        private static async Task WritePump()
        {
            List<LogRow> batch = new(BATCH_SIZE);
            while (true)
            {
                await _signal.WaitAsync();
                while (_pending.TryDequeue(out var row))
                {
                    batch.Add(row);
                }

                if (batch.Count == 0)
                {
                    continue;
                }

                try
                {
                    await File.AppendAllTextAsync(CurrentFilePath,
                        string.Join(Environment.NewLine, batch.ConvertAll(FormatRow)) +
                        Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    var emergency = Path.Combine(BaseFolder, "emergency.log");
                    await File.AppendAllTextAsync(emergency,
                        string.Join(Environment.NewLine, batch.ConvertAll(FormatRow)) +
                        Environment.NewLine, Encoding.UTF8);
                }

                batch.Clear();
                RollIfNeeded();
            }
        }
        #endregion

        #region 工具
        private static string FormatRow(LogRow r)
        {
            return $"[{r.Time:yyyy-MM-dd HH:mm:ss}][{LevelToText(r.Level)}][{r.Source}][{Environment.CurrentManagedThreadId}]: {r.Text}";
        }

        private static string LevelToText(int l)
        {
            return l switch
            {
                0 => "DEBUG",
                1 => "INFO",
                2 => "WARN",
                3 => "ERROR",
                4 => "FATAL",
                _ => "UNKNOWN"
            };
        }

        private static void RollIfNeeded()
        {
            DirectoryInfo dir = new(BaseFolder);
            foreach (var f in dir.GetFiles("logs-*.txt"))
            {
                if (DateTime.UtcNow - f.LastWriteTimeUtc > RetainAge)
                {
                    f.Delete();
                }
            }
        }
        #endregion

        #region 内部行对象
        private record LogRow
        {
            public DateTime Time;
            public int Level;
            public string Source = "";
            public string Text = "";
        }
        #endregion
    }
    /// <summary>
    /// 应用程序的主入口类，负责启动和管理应用程序。
    /// </summary>
    public partial class App : Application
    {
        public static MainWindow? MainWindow { get; private set; } // 主窗口实例

        // 导入Windows API函数用于设置AUMID
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

        private const string AppUserModelID = "XIGUASecurity.App";

        public App()
        {
            LogText.AddNewLog(LogLevel.INFO, "UI Interface", "Attempting to load the MainWindow...");
            InitializeComponent();
        }

        /// <summary>
        /// 注册应用程序用户模型ID (AUMID)
        /// </summary>
        private static void RegisterAppUserModelID()
        {
            try
            {
                // 为未打包的应用程序注册AUMID，确保通知能正确更新
                _ = SetCurrentProcessExplicitAppUserModelID(AppUserModelID);
                LogText.AddNewLog(LogLevel.INFO, "App", $"已注册AUMID: {AppUserModelID}");
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "App", $"注册AUMID失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用程序启动时调用，处理启动参数。
        /// </summary>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // 注册AUMID，确保Toast通知能正确更新
                RegisterAppUserModelID();

                // 添加全局异常处理
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                // 初始化通知管理器
                ToastNotificationManagerCompat.OnActivated += NotificationActivated;

                // 初始化信任区管理器
                XIGUASecurity.Protection.TrustManager.Initialize();
                
                // 初始化隔离区管理器
                XIGUASecurity.Protection.QuarantineManager.Initialize();

                await InitializeLocalizer();
                InitializeMainWindow();

                // 初始化辅助模式管理器
                if (MainWindow != null)
                {
                    Core.AssistantModeManager.Initialize(MainWindow);
                }

                // 检查并显示公告
                // 延迟3秒，确保主窗口已完全加载
                await Task.Delay(3000);
                await CheckAndShowAnnouncement();
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "App", $"Error in OnLaunched: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理未捕获的异常
        /// </summary>
        private void OnUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception ?? new Exception("Unknown exception");
            LogText.AddNewLog(LogLevel.FATAL, "GlobalExceptionHandler",
                $"Unhandled exception: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");

            // 如果是插件加载相关的异常，记录更详细的信息
            if (ex.Message.Contains("Plugin") || ex.StackTrace?.Contains("Plugin") == true)
            {
                LogText.AddNewLog(LogLevel.ERROR, "PluginLoader",
                    $"Plugin loading error details: {ex}");
            }
        }

        /// <summary>
        /// 处理未观察到的任务异常
        /// </summary>
        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Exception ex = e.Exception;
            LogText.AddNewLog(LogLevel.ERROR, "GlobalExceptionHandler",
                $"Unobserved task exception: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");

            // 标记异常为已观察，避免进程终止
            e.SetObserved();
        }
        private void InitializeMainWindow()
        {
            try
            {
                MainWindow ??= new MainWindow();
                MainWindow.Activate();
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "App", $"Error initializing MainWindow: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理通知激活事件
        /// </summary>
        private void NotificationActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            try
            {
                // 获取通知参数
                ToastArguments args = ToastArguments.Parse(e.Argument);

                // 获取操作类型
                if (args.TryGetValue("action", out string action))
                {
                    // 获取文件路径，如果存在的话
                    _ = args.TryGetValue("path", out string filePath);

                    // 获取威胁类型，默认为文件类型
                    string threatType = args.TryGetValue("type", out string type) ? type : "File";

                    switch (action)
                    {
                        case "allow":
                            // 允许执行文件或注册表修改
                            if (!string.IsNullOrEmpty(filePath))
                            {
                                if (threatType == "Reg")
                                {
                                    AllowRegistryModification(filePath);
                                }
                                else
                                {
                                    AllowFile(filePath, "未知病毒");
                                }
                            }
                            break;
                        case "quarantine":
                            // 隔离文件或恢复注册表
                            if (!string.IsNullOrEmpty(filePath))
                            {
                                if (threatType == "Reg")
                                {
                                    RestoreRegistryValue(filePath);
                                }
                                else
                                {
                                    QuarantineFile(filePath, "未知病毒");
                                }
                            }
                            break;
                        case "delete":
                            // 删除文件或注册表项
                            if (!string.IsNullOrEmpty(filePath))
                            {
                                if (threatType == "Reg")
                                {
                                    DeleteRegistryValue(filePath);
                                }
                                else
                                {
                                    DeleteFile(filePath);
                                }
                            }
                            break;
                        case "restore":
                            // 恢复文档
                            if (!string.IsNullOrEmpty(filePath) && DocumentProtection.RestoreFile(filePath))
                            {
                                // 显示恢复成功通知
                                new ToastContentBuilder()
                                    .AddText("XIGUASecurity 文档保护")
                                    .AddText($"文档 {Path.GetFileName(filePath)} 已成功恢复")
                                    .Show();
                            }
                            else if (!string.IsNullOrEmpty(filePath))
                            {
                                // 显示恢复失败通知
                                new ToastContentBuilder()
                                    .AddText("XIGUASecurity 文档保护")
                                    .AddText($"文档 {Path.GetFileName(filePath)} 恢复失败")
                                    .Show();
                            }
                            break;
                        case "view":
                            // 查看新创建的文档
                            if (!string.IsNullOrEmpty(filePath))
                            {
                                try
                                {
                                    // 使用系统默认程序打开文件
                                    _ = Process.Start(new ProcessStartInfo
                                    {
                                        FileName = filePath,
                                        UseShellExecute = true
                                    });

                                    // 显示打开成功通知
                                    new ToastContentBuilder()
                                        .AddText("XIGUASecurity 文档保护")
                                        .AddText($"已打开文档 {Path.GetFileName(filePath)}")
                                        .Show();
                                }
                                catch (Exception ex)
                                {
                                    // 显示打开失败通知
                                    new ToastContentBuilder()
                                        .AddText("XIGUASecurity 文档保护")
                                        .AddText($"打开文档 {Path.GetFileName(filePath)} 失败: {ex.Message}")
                                        .Show();
                                }
                            }
                            break;
                        case "ignore":
                            // 忽略文档删除或修改，不做任何操作
                            break;
                        case "restoreBatch":
                            // 回滚当前批次的文件修改
                            _ = (MainWindow?.DispatcherQueue?.TryEnqueue(async () =>
                            {
                                try
                                {
                                    // 检查文档保护是否启用
                                    if (!XIGUASecurity.Protection.DocumentProtection.IsEnabled())
                                    {
                                        ContentDialog confirmDialog = new()
                                        {
                                            Title = "文档保护未启用",
                                            Content = "文档保护功能未启用，无法回滚修改。",
                                            CloseButtonText = "确定",
                                            DefaultButton = ContentDialogButton.Close,
                                            XamlRoot = MainWindow.Content?.XamlRoot ?? throw new InvalidOperationException("MainWindow.Content is null") ?? throw new InvalidOperationException("MainWindow.Content is null")
                                        };
                                        _ = await confirmDialog.ShowAsync();
                                        return;
                                    }

                                    // 获取传递的文件列表
                                    if (!args.TryGetValue("files", out string filesJson))
                                    {
                                        ContentDialog errorDialog = new()
                                        {
                                            Title = "回滚失败",
                                            Content = "无法获取文件列表信息。",
                                            CloseButtonText = "确定",
                                            XamlRoot = MainWindow.Content?.XamlRoot
                                        };
                                        _ = await errorDialog.ShowAsync();
                                        return;
                                    }

                                    // 解析文件列表
                                    List<string> batchFiles;
                                    try
                                    {
                                        batchFiles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(filesJson) ?? new List<string>();
                                    }
                                    catch
                                    {
                                        ContentDialog errorDialog = new()
                                        {
                                            Title = "回滚失败",
                                            Content = "文件列表信息格式错误。",
                                            CloseButtonText = "确定",
                                            XamlRoot = MainWindow.Content?.XamlRoot
                                        };
                                        _ = await errorDialog.ShowAsync();
                                        return;
                                    }

                                    if (batchFiles == null || batchFiles.Count == 0)
                                    {
                                        ContentDialog infoDialog = new()
                                        {
                                            Title = "无文件需要回滚",
                                            Content = "没有找到需要回滚的文件。",
                                            CloseButtonText = "确定",
                                            DefaultButton = ContentDialogButton.Close,
                                            XamlRoot = MainWindow.Content.XamlRoot
                                        };
                                        _ = await infoDialog.ShowAsync();
                                        return;
                                    }

                                    // 直接执行回滚操作，不显示进度对话框
                                    int successCount = 0;
                                    int failCount = 0;

                                    // 设置批量还原状态标志
                                    XIGUASecurity.Protection.DocumentProtection.SetBulkRestoreInProgress(true);

                                    try
                                    {
                                        // 直接执行还原任务
                                        foreach (var filePath in batchFiles)
                                        {
                                            try
                                            {
                                                if (XIGUASecurity.Protection.DocumentProtection.RestoreFileFromBackup(filePath))
                                                {
                                                    _ = System.Threading.Interlocked.Increment(ref successCount);
                                                }
                                                else
                                                {
                                                    _ = System.Threading.Interlocked.Increment(ref failCount);
                                                }
                                            }
                                            catch
                                            {
                                                _ = System.Threading.Interlocked.Increment(ref failCount);
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        // 清除批量还原状态标志
                                        XIGUASecurity.Protection.DocumentProtection.SetBulkRestoreInProgress(false);
                                    }

                                    // 只有在回滚失败时才显示通知
                                    if (failCount > 0)
                                    {
                                        new ToastContentBuilder()
                                            .AddText("XIGUASecurity 文档保护")
                                            .AddText($"回滚操作完成，但有 {failCount} 个文件恢复失败")
                                            .Show();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to restore batch files: {ex.Message}");

                                    ContentDialog errorDialog = new()
                                    {
                                        Title = "回滚失败",
                                        Content = $"回滚文件时发生错误：{ex.Message}",
                                        CloseButtonText = "确定",
                                        XamlRoot = MainWindow.Content.XamlRoot
                                    };
                                    _ = await errorDialog.ShowAsync();
                                }
                            }));
                            break;
                        case "restoreAll":
                            // 回滚所有修改
                            _ = (MainWindow?.DispatcherQueue?.TryEnqueue(async () =>
                            {
                                try
                                {
                                    // 检查文档保护是否启用
                                    if (!XIGUASecurity.Protection.DocumentProtection.IsEnabled())
                                    {
                                        ContentDialog confirmDialog = new()
                                        {
                                            Title = "文档保护未启用",
                                            Content = "文档保护功能未启用，无法回滚修改。",
                                            CloseButtonText = "确定",
                                            DefaultButton = ContentDialogButton.Close,
                                            XamlRoot = MainWindow.Content.XamlRoot
                                        };
                                        _ = await confirmDialog.ShowAsync();
                                        return;
                                    }

                                    // 获取最近更改的文件备份信息（排除初始备份）
                                    var recentBackups = XIGUASecurity.Protection.DocumentProtection.GetRecentChangedFiles();

                                    if (recentBackups == null || recentBackups.Count == 0)
                                    {
                                        ContentDialog infoDialog = new()
                                        {
                                            Title = "无最近更改",
                                            Content = "没有找到最近更改的文件，无需回滚。",
                                            CloseButtonText = "确定",
                                            DefaultButton = ContentDialogButton.Close,
                                            XamlRoot = MainWindow.Content.XamlRoot
                                        };
                                        _ = await infoDialog.ShowAsync();
                                        return;
                                    }

                                    // 确认对话框
                                    ContentDialog confirmRestoreDialog = new()
                                    {
                                        Title = "确认回滚",
                                        Content = $"确定要回滚最近的修改吗？这将恢复 {recentBackups.Count} 个最近修改的文件。",
                                        PrimaryButtonText = "确认回滚",
                                        CloseButtonText = "取消",
                                        DefaultButton = ContentDialogButton.Close,
                                        XamlRoot = MainWindow.Content.XamlRoot
                                    };

                                    var result = await confirmRestoreDialog.ShowAsync();
                                    if (result != ContentDialogResult.Primary)
                                    {
                                        return;
                                    }

                                    // 直接执行回滚操作，不显示进度对话框
                                    int successCount = 0;
                                    int failCount = 0;

                                    // 设置批量还原状态标志
                                    XIGUASecurity.Protection.DocumentProtection.SetBulkRestoreInProgress(true);

                                    try
                                    {
                                        // 直接执行还原任务
                                        foreach (var backup in recentBackups)
                                        {
                                            try
                                            {
                                                if (XIGUASecurity.Protection.DocumentProtection.RestoreFileFromBackup(backup.OriginalPath))
                                                {
                                                    _ = System.Threading.Interlocked.Increment(ref successCount);
                                                }
                                                else
                                                {
                                                    _ = System.Threading.Interlocked.Increment(ref failCount);
                                                }
                                            }
                                            catch
                                            {
                                                _ = System.Threading.Interlocked.Increment(ref failCount);
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        // 清除批量还原状态标志
                                        XIGUASecurity.Protection.DocumentProtection.SetBulkRestoreInProgress(false);
                                    }

                                    // 显示结果
                                    ContentDialog resultDialog = new()
                                    {
                                        Title = "回滚完成",
                                        Content = $"文件回滚操作完成。\n成功恢复：{successCount} 个文件\n失败：{failCount} 个文件",
                                        CloseButtonText = "确定",
                                        XamlRoot = MainWindow.Content.XamlRoot
                                    };
                                    _ = await resultDialog.ShowAsync();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to restore all files: {ex.Message}");

                                    ContentDialog errorDialog = new()
                                    {
                                        Title = "回滚失败",
                                        Content = $"回滚文件时发生错误：{ex.Message}",
                                        CloseButtonText = "确定",
                                        XamlRoot = MainWindow.Content.XamlRoot
                                    };
                                    _ = await errorDialog.ShowAsync();
                                }
                            }));
                            break;
                        case "viewRecent":
                            // 查看最近更改的文件列表功能已移除
                            _ = (MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                            {
                                try
                                {
                                    // 功能已移除，不再显示最近更改文件对话框
                                }
                                catch (Exception)
                                {

                                }
                            }));
                            break;
                        case "ignoreBatch":
                            // 忽略批量文件更改，不做任何操作
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "App", $"Error handling notification activation: {ex.Message}");
            }
        }

        /// <summary>
        /// 允许文件执行
        /// </summary>
        private async void AllowFile(string filePath, string type)
        {
            try
            {
                // 将文件添加到白名单
                if (!string.IsNullOrEmpty(filePath))
                {
                    string whitelistFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XdowsSecurity", "whitelist.txt");
                    string whitelistDir = Path.GetDirectoryName(whitelistFile) ?? string.Empty;
                    if (!string.IsNullOrEmpty(whitelistDir))
                    {
                        _ = Directory.CreateDirectory(whitelistDir);
                    }
                    File.AppendAllText(whitelistFile, $"{filePath}|{type}\n");

                    // 如果是.virus文件，恢复为原始文件名
                    if (filePath.EndsWith(".virus"))
                    {
                        string originalPath = filePath[..^6];
                        File.Move(filePath, originalPath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "App - AllowFile", ex.Message);
            }
        }

        /// <summary>
        /// 隔离文件
        /// </summary>
        private void QuarantineFile(string filePath, string type)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    // 使用Protection项目中的QuarantineManager
                    _ = XIGUASecurity.Protection.QuarantineManager.AddToQuarantine(filePath, type);
                }
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "App - QuarantineFile", ex.Message);
            }
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        private void DeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "App - DeleteFile", ex.Message);
            }
        }

        /// <summary>
        /// 允许注册表修改
        /// </summary>
        private void AllowRegistryModification(string regPath)
        {
            try
            {
                // 创建一个临时文件来存储注册表路径，作为信任项
                // 由于TrustManager目前只支持文件和文件夹，我们使用一个临时文件来代表注册表项
                string tempTrustFile = Path.Combine(Path.GetTempPath(), $"xdows_trust_{Guid.NewGuid()}.tmp");
                File.WriteAllText(tempTrustFile, regPath);

                // 将临时文件添加到信任区
                bool success = TrustManager.AddFileToTrust(tempTrustFile, "注册表信任项");

                // 删除临时文件
                if (File.Exists(tempTrustFile))
                {
                    File.Delete(tempTrustFile);
                }

                if (success)
                {
                    LogText.AddNewLog(LogLevel.INFO, "App", $"已允许注册表修改: {regPath}");
                }
                else
                {
                    LogText.AddNewLog(LogLevel.WARN, "App", $"无法添加注册表项到信任区: {regPath}");
                }
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "App - AllowRegistryModification", ex.Message);
            }
        }

        /// <summary>
        /// 恢复注册表值
        /// </summary>
        private void RestoreRegistryValue(string regPath)
        {
            try
            {
                // 这里应该从备份中恢复注册表值
                // 由于当前代码中没有注册表备份功能，这里只是示例实现
                // 实际实现需要与注册表防护系统配合
                LogText.AddNewLog(LogLevel.INFO, "App", $"已恢复注册表值: {regPath}");
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "App - RestoreRegistryValue", ex.Message);
            }
        }

        /// <summary>
        /// 删除注册表项
        /// </summary>
        private void DeleteRegistryValue(string regPath)
        {
            try
            {
                // 删除注册表项或值
                // 这里需要解析regPath来确定是删除整个项还是只删除值
                // 实际实现需要与注册表防护系统配合
                LogText.AddNewLog(LogLevel.INFO, "App", $"已删除注册表项: {regPath}");
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "App - DeleteRegistryValue", ex.Message);
            }
        }
        // 定义主题属性
        public static ElementTheme Theme { get; set; } = ElementTheme.Default;

        // 获取云API密钥
        public static string GetCzkCloudApiKey()
        {
            return string.Empty;
        }

        // 检查是否以管理员身份运行
        public static bool IsRunAsAdmin()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        private async Task InitializeLocalizer()
        {
            string stringsPath = Path.Combine(AppContext.BaseDirectory, "Strings");

            var settings = ApplicationData.Current.LocalSettings;
            string lastLang = settings.Values["AppLanguage"] as string ?? "en-US";

            ILocalizer localizer = await new LocalizerBuilder()
                .AddStringResourcesFolderForLanguageDictionaries(stringsPath)
                .SetOptions(o => o.DefaultLanguage = lastLang)
                .Build();
            // ApplicationLanguages.PrimaryLanguageOverride = "en-US";
            await localizer.SetLanguage(lastLang);
        }
        // Windows 版本获取
        public static string OsName => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? (Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : "Windows 10")
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" : "Linux";

        public static string OsVersion => Environment.OSVersion.ToString();
    

    /// <summary>
    /// 检查并显示公告
    /// </summary>
    private static async Task CheckAndShowAnnouncement()
    {
        try
        {
            var announcement = await AnnouncementService.Instance.GetLatestAnnouncementAsync();
            if (announcement != null)
            {
                await ShowAnnouncementDialog(announcement);
                AnnouncementService.Instance.MarkAsRead(announcement.Id!);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"检查公告失败: {ex.Message}");
        }
    }

        /// <summary>
        /// 显示公告对话框
        /// </summary>
        private static async Task ShowAnnouncementDialog(Services.Announcement? announcement)
        {
            try
            {
                var contentDialog = new ContentDialog
                {
                    Title = "系统公告",
                    PrimaryButtonText = "我知道了",
                    CloseButtonText = "稍后查看",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = MainWindow.Content?.XamlRoot ?? throw new InvalidOperationException("MainWindow.Content is null")
                };

                // 创建滚动查看器
                var scrollViewer = new ScrollViewer
                {
                    MaxHeight = 400,
                    HorizontalScrollMode = ScrollMode.Disabled,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollMode = ScrollMode.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                // 创建内容布局
                var stackPanel = new StackPanel
                {
                    Spacing = 16,
                    MaxWidth = 600
                };

                // 标题
                var titleTextBlock = new TextBlock
                {
                    Text = announcement!.Title,
                    FontSize = 20,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                };
                stackPanel.Children.Add(titleTextBlock);

                // 发布日期
                var dateTextBlock = new TextBlock
                {
                    Text = $"发布时间: {announcement!.PublishDate}",
                    FontSize = 12,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                };
                stackPanel.Children.Add(dateTextBlock);

                // 分隔线
                var separator = new Microsoft.UI.Xaml.Shapes.Rectangle
                {
                    Height = 1,
                    Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray),
                    Margin = new Microsoft.UI.Xaml.Thickness(0, 8, 0, 8)
                };
                stackPanel.Children.Add(separator);

                // 内容 - 使用RichTextBlock支持HTML格式
                var richTextBlock = new RichTextBlock
                {
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap
                };
                
                // 解析HTML内容并添加到RichTextBlock
                var paragraph = new Microsoft.UI.Xaml.Documents.Paragraph();
                
                // 简单的HTML解析，支持基本标签
                var content = announcement!.Content;
                var isBold = false;
                var isItalic = false;
                var currentRun = new Microsoft.UI.Xaml.Documents.Run();
                
                // 使用更简单的方法解析HTML
                var tagPattern = new System.Text.RegularExpressions.Regex(@"<(/?)(b|i|br)>");
                var matches = tagPattern.Matches(content);
                int lastIndex = 0;
                
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    // 添加标签前的文本
                    if (match.Index > lastIndex)
                    {
                        var textBefore = content.Substring(lastIndex, match.Index - lastIndex);
                        if (!string.IsNullOrEmpty(textBefore))
                        {
                            if (isBold)
                            {
                                var bold = new Microsoft.UI.Xaml.Documents.Bold();
                                bold.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = textBefore });
                                paragraph.Inlines.Add(bold);
                            }
                            else if (isItalic)
                            {
                                var italic = new Microsoft.UI.Xaml.Documents.Italic();
                                italic.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = textBefore });
                                paragraph.Inlines.Add(italic);
                            }
                            else
                            {
                                paragraph.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = textBefore });
                            }
                        }
                    }
                    
                    // 处理标签
                    var isClosingTag = match.Groups[1].Value == "/";
                    var tagName = match.Groups[2].Value;
                    
                    if (tagName == "br")
                    {
                        paragraph.Inlines.Add(new Microsoft.UI.Xaml.Documents.LineBreak());
                    }
                    else if (tagName == "b")
                    {
                        isBold = !isClosingTag;
                    }
                    else if (tagName == "i")
                    {
                        isItalic = !isClosingTag;
                    }
                    
                    lastIndex = match.Index + match.Length;
                }
                
                // 添加最后的文本
                if (lastIndex < content.Length)
                {
                    var remainingText = content.Substring(lastIndex);
                    if (!string.IsNullOrEmpty(remainingText))
                    {
                        if (isBold)
                        {
                            var bold = new Microsoft.UI.Xaml.Documents.Bold();
                            bold.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = remainingText });
                            paragraph.Inlines.Add(bold);
                        }
                        else if (isItalic)
                        {
                            var italic = new Microsoft.UI.Xaml.Documents.Italic();
                            italic.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = remainingText });
                            paragraph.Inlines.Add(italic);
                        }
                        else
                        {
                            paragraph.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = remainingText });
                        }
                    }
                }
                
                richTextBlock.Blocks.Add(paragraph);
                stackPanel.Children.Add(richTextBlock);

                scrollViewer.Content = stackPanel;
                contentDialog.Content = scrollViewer;
                await contentDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "App", $"显示公告对话框失败: {ex.Message}");
            }
        }
    } }
