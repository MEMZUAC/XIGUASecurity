using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WinUI3Localizer;
using WinRT.Interop;
using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Runtime.InteropServices;

namespace XIGUASecurity
{
    public sealed partial class InterceptWindow : Window
    {
        private string? _originalFilePath;
        private string? _virusFilePath;
        private readonly string? _type;

        // 静态字典用于跟踪已打开的窗口
        private static readonly Dictionary<string, InterceptWindow> _openWindows = new Dictionary<string, InterceptWindow>();

        public static void ShowOrActivate(bool isSucceed, string path, string type)
        {
            try
            {
                // 只显示通知
                ShowThreatNotificationOnly(path, type);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示通知失败: {ex.Message}");
                LogText.AddNewLog(LogLevel.ERROR, "InterceptWindow - ShowOrActivate", ex.Message);
            }
        }
        


        private void SetFileInfo(string path)
        {
            _originalFilePath = path;
            if (File.Exists(path))
                _virusFilePath = path;
            else if (File.Exists(path + ".virus"))
                _virusFilePath = path + ".virus";
            else
                _virusFilePath = path;
            
            System.Diagnostics.Debug.WriteLine(_virusFilePath);
            
            // 设置新界面的文件信息
            FileName.Text = System.IO.Path.GetFileName(path);
            FilePath.Text = path;

            try
            {
                if (File.Exists(_virusFilePath))
                {
                    var fileInfo = new FileInfo(_virusFilePath);
                    ModifyDate.Text = fileInfo.LastWriteTime.ToString("yyyy-MM-dd");
                    
                    // 设置文件大小
                    long fileSize = fileInfo.Length;
                    if (fileSize < 1024)
                        FileSize.Text = $"{fileSize} B";
                    else if (fileSize < 1024 * 1024)
                        FileSize.Text = $"{fileSize / 1024.0:F1} KB";
                    else
                        FileSize.Text = $"{fileSize / (1024.0 * 1024):F1} MB";
                    
                    // 设置文件类型
                    string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
                    switch (extension)
                    {
                        case ".exe":
                        case ".com":
                            FileType.Text = "可执行文件";
                            break;
                        case ".dll":
                            FileType.Text = "动态链接库";
                            break;
                        case ".bat":
                        case ".cmd":
                            FileType.Text = "批处理文件";
                            break;
                        case ".ps1":
                            FileType.Text = "PowerShell脚本";
                            break;
                        case ".js":
                        case ".vbs":
                            FileType.Text = "脚本文件";
                            break;
                        case ".reg":
                            FileType.Text = "注册表文件";
                            break;
                        default:
                            FileType.Text = "未知文件类型";
                            break;
                    }
                }
                else
                {
                    ModifyDate.Text = Localizer.Get().GetLocalizedString("AllPage_Undefined");
                    FileSize.Text = Localizer.Get().GetLocalizedString("AllPage_Undefined");
                    FileType.Text = Localizer.Get().GetLocalizedString("AllPage_Undefined");
                }
            }
            catch (Exception ex)
            {
                ModifyDate.Text = Localizer.Get().GetLocalizedString("AllPage_Undefined");
                FileSize.Text = Localizer.Get().GetLocalizedString("AllPage_Undefined");
                FileType.Text = Localizer.Get().GetLocalizedString("AllPage_Undefined");
                LogText.AddNewLog(LogLevel.ERROR, "InterceptWindow - SetFileInfo", ex.Message);
            }

            try
            {
                if (File.Exists(_virusFilePath))
                {
                    FileIcon.Source = SetIcon(_virusFilePath);
                }
                else
                {
                    // fallback: keep default or placeholder icon
                }
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "InterceptWindow - LoadIcon", ex.Message);
            }
        }

        public Microsoft.UI.Xaml.Media.Imaging.BitmapImage SetIcon(string path)
        {
            var fileIcon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (fileIcon == null) return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            using (var bitmap = fileIcon.ToBitmap())
            {
                using (var memoryStream = new System.IO.MemoryStream())
                {
                    bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    memoryStream.Position = 0;

                    var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    bitmapImage.SetSource(memoryStream.AsRandomAccessStream());
                    return bitmapImage;
                }
            }
        }

        private InterceptWindow(bool isSucceed, string path, string type, string key)
        {
            this.InitializeComponent();
            var manager = WinUIEx.WindowManager.Get(this);
            manager.MinWidth = 600;
            manager.MinHeight = 500;
            manager.Width = 700;
            manager.Height = 650;

            // 将窗口添加到静态字典
            _openWindows[key] = this;

            try
            {
                Localizer.Get().LanguageChanged += Localizer_LanguageChanged;
                UpdateWindowTitle();
            }
            catch { }

            // 注册窗口关闭事件，从字典中移除
            this.Closed += (sender, e) =>
            {
                Localizer.Get().LanguageChanged -= Localizer_LanguageChanged;
                _openWindows.Remove(key);
            };

            SetFileInfo(path);
            _type = type;

            // 根据类型设置威胁等级和行为
            SetThreatLevel(type);

            // 根据类型设置图标
            if (_type == "Reg")
            {
                // 使用系统注册表编辑器图标
                try
                {
                    string regeditPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "regedit.exe");
                    if (File.Exists(regeditPath))
                    {
                        FileIcon.Source = SetIcon(regeditPath);
                    }
                    else
                    {
                        FileIcon.Source = SetIcon(path);
                    }
                }
                catch
                {
                    // 如果获取失败，使用默认图标
                    if (File.Exists(path))
                    {
                        FileIcon.Source = SetIcon(path);
                    }
                }
            }
            else
            {
                // 原有的动态获取图标逻辑
                if (File.Exists(path))
                {
                    FileIcon.Source = SetIcon(path);
                }
            }

            // 设置扫描引擎信息
            EngineNameText.Text = "XIGUASecurity Engine v3.2";

            // 设置威胁类型
            if (_type == "Reg")
            {
                ThreatType.Text = "注册表修改";
                ThreatBehavior.Text = "尝试修改系统注册表关键项，可能导致系统不稳定或安全漏洞";
                SecurityAdvice.Text = "建议阻止此注册表修改操作，以防止系统设置被恶意更改。如果这是您信任的程序的操作，可以选择允许执行。";
            }
            else if (_type == "Music")
            {
                ThreatType.Text = "音乐播放程序";
                ThreatBehavior.Text = "正在尝试访问音频设备或播放音乐文件";
                SecurityAdvice.Text = "这是一个音乐播放程序，通常不会对系统安全构成威胁。如果您不认识此程序，建议谨慎处理。";
            }
            else
            {
                // 根据文件扩展名设置威胁类型
                string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
                switch (extension)
                {
                    case ".exe":
                    case ".com":
                        ThreatType.Text = "可疑可执行程序";
                        ThreatBehavior.Text = "尝试执行可能包含恶意代码的程序，可能危害系统安全";
                        SecurityAdvice.Text = "建议立即隔离此文件，以防止对系统造成潜在损害。此文件可能包含恶意代码，会危害您的计算机安全。";
                        break;
                    case ".dll":
                        ThreatType.Text = "可疑动态链接库";
                        ThreatBehavior.Text = "尝试加载可能包含恶意代码的库文件，可能被用于注入恶意代码";
                        SecurityAdvice.Text = "建议隔离此文件，以防止被恶意程序利用。如果您确信此文件安全，可以选择允许执行。";
                        break;
                    case ".bat":
                    case ".cmd":
                        ThreatType.Text = "可疑批处理脚本";
                        ThreatBehavior.Text = "尝试执行可能包含恶意命令的脚本，可能对系统造成损害";
                        SecurityAdvice.Text = "建议检查脚本内容后再决定是否执行。如果来源不明，建议隔离此文件。";
                        break;
                    case ".ps1":
                        ThreatType.Text = "可疑PowerShell脚本";
                        ThreatBehavior.Text = "尝试执行可能包含恶意命令的PowerShell脚本，可能被用于绕过安全措施";
                        SecurityAdvice.Text = "PowerShell脚本具有强大的系统操作能力，建议仅在确认脚本安全后执行。";
                        break;
                    case ".js":
                    case ".vbs":
                        ThreatType.Text = "可疑脚本文件";
                        ThreatBehavior.Text = "尝试执行可能包含恶意代码的脚本，可能被用于下载或执行其他恶意软件";
                        SecurityAdvice.Text = "脚本文件可能被用于传播恶意软件，建议在确认来源安全后再执行。";
                        break;
                    default:
                        ThreatType.Text = "可疑文件";
                        ThreatBehavior.Text = "检测到可疑文件活动，可能对系统安全构成威胁";
                        SecurityAdvice.Text = "建议谨慎处理此文件，如非必要请勿执行或打开。";
                        break;
                }
            }

            // 更新本地化标题
            UpdateWindowTitle();

            // 初始化其他UI元素
    InitializeUI(path);
    
    // 显示系统通知
    ShowThreatNotification(path, type);
    
    // 设置窗口位置到右下角
    this.Activated += InterceptWindow_Activated;
        }

        private void InterceptWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            // 只在第一次激活时设置位置
            if (args.WindowActivationState == WindowActivationState.CodeActivated)
            {
                this.SetWindowPositionToBottomRight();
                // 移除事件处理器，避免重复设置
                this.Activated -= InterceptWindow_Activated;
            }
        }

        private void SetWindowPositionToBottomRight()
        {
            try
            {
                // 获取窗口句柄
                var hWnd = WindowNative.GetWindowHandle(this);
                
                // 使用Win32 API获取屏幕工作区域
                var screenWidth = (int)Microsoft.UI.Windowing.DisplayArea.Primary.WorkArea.Width;
                var screenHeight = (int)Microsoft.UI.Windowing.DisplayArea.Primary.WorkArea.Height;
                
                // 获取窗口大小
                var windowWidth = 700; // 我们设置的窗口宽度
                var windowHeight = 650; // 我们设置的窗口高度
                
                // 计算窗口位置（右下角，留出一些边距）
                int margin = 20;
                int x = screenWidth - windowWidth - margin;
                int y = screenHeight - windowHeight - margin;
                
                // 设置窗口位置
                if (x >= 0 && y >= 0)
                {
                    var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    appWindow?.Move(new Windows.Graphics.PointInt32(x, y));
                }
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "InterceptWindow - SetWindowPosition", ex.Message);
            }
        }

        private void SetThreatLevel(string type)
        {
            // 根据类型设置威胁等级
            if (type == "Reg")
            {
                // 注册表修改设为中等威胁
                ThreatLevelBadge.Text = "中等";
                ThreatLevelText.Text = "潜在风险";
                // 使用系统默认的强调色
            }
            else if (type == "Music")
            {
                // 音乐程序设为低威胁
                ThreatLevelBadge.Text = "低危";
                ThreatLevelText.Text = "低风险程序";
                // 使用系统默认的强调色
            }
            else
            {
                // 可执行文件默认为高危
                ThreatLevelBadge.Text = "高危";
                ThreatLevelText.Text = "高危威胁";
                // 使用系统默认的强调色
            }
        }

        private void Localizer_LanguageChanged(object? sender, WinUI3Localizer.LanguageChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() => UpdateWindowTitle());
        }

        private void UpdateWindowTitle()
        {
            try
            {
                var title = Localizer.Get().GetLocalizedString("InterceptWindow_WindowTitle");
                if (!string.IsNullOrEmpty(title))
                    this.Title = title;
            }
            catch { }
        }

        private void InitializeUI(string path)
        {
            // 初始化UI元素
            // 大部分UI元素已经在SetFileInfo中设置
        }

        private async void AllowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 将文件添加到白名单
                if (!string.IsNullOrEmpty(_virusFilePath))
                {
                    string whitelistFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XdowsSecurity", "whitelist.txt");
                    string whitelistDir = Path.GetDirectoryName(whitelistFile) ?? string.Empty;
                    if (!string.IsNullOrEmpty(whitelistDir))
                    {
                        Directory.CreateDirectory(whitelistDir);
                    }
                    File.AppendAllText(whitelistFile, $"{_virusFilePath}|{_type}\n");
                    
                    // 如果是.virus文件，恢复为原始文件名
                    if (_virusFilePath.EndsWith(".virus"))
                    {
                        string originalPath = _virusFilePath.Substring(0, _virusFilePath.Length - 6);
                        File.Move(_virusFilePath, originalPath);
                    }
                }
                
                await ShowMessageDialog("允许执行", "文件已被添加到白名单，将允许执行。");
                this.Close();
            }
            catch (Exception ex)
            {
                await ShowMessageDialog("操作失败", $"允许文件执行时出错: {ex.Message}");
                LogText.AddNewLog(LogLevel.ERROR, "InterceptWindow - AllowFile", ex.Message);
            }
        }

        private void AllowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AllowButton_Click(sender, e);
        }

        private async void QuarantineButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_virusFilePath) && File.Exists(_virusFilePath))
                {
                    // 创建隔离目录
                    string quarantineDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XdowsSecurity", "Quarantine");
                    Directory.CreateDirectory(quarantineDir);
                    
                    // 生成隔离文件名
                    string quarantineFileName = $"{Path.GetFileNameWithoutExtension(_virusFilePath)}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(_virusFilePath)}";
                    string quarantineFilePath = Path.Combine(quarantineDir, quarantineFileName);
                    
                    // 移动文件到隔离目录
                    File.Move(_virusFilePath, quarantineFilePath);
                    
                    // 记录隔离信息
                    string logFile = Path.Combine(quarantineDir, "quarantine.log");
                    File.AppendAllText(logFile, $"{DateTime.Now}: {quarantineFileName} | Original: {_virusFilePath} | Type: {_type}\n");
                    
                    await ShowMessageDialog("隔离成功", "文件已被成功隔离到安全区域。");
                }
                else
                {
                    await ShowMessageDialog("隔离失败", "找不到要隔离的文件。");
                }
                
                this.Close();
            }
            catch (Exception ex)
            {
                await ShowMessageDialog("隔离失败", $"隔离文件时出错: {ex.Message}");
                LogText.AddNewLog(LogLevel.ERROR, "InterceptWindow - QuarantineFile", ex.Message);
            }
        }

        private void QuarantineMenuItem_Click(object sender, RoutedEventArgs e)
        {
            QuarantineButton_Click(sender, e);
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(_virusFilePath))
                {
                    File.Delete(_virusFilePath);
                    await ShowMessageDialog("删除成功", "文件已被永久删除。");
                    this.Close();
                }
                else
                {
                    await ShowMessageDialog("删除失败", "找不到要删除的文件。");
                }
            }
            catch (Exception ex)
            {
                await ShowMessageDialog("删除失败", $"删除文件时出错: {ex.Message}");
                LogText.AddNewLog(LogLevel.ERROR, "InterceptWindow - DeleteFile", ex.Message);
            }
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DeleteButton_Click(sender, e);
        }

        private async Task ShowMessageDialog(string title, string message)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "确定",
                PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                XamlRoot = this.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        /// <summary>
        /// 只显示威胁拦截通知，不创建窗口
        /// </summary>
        /// <param name="filePath">被拦截的文件路径</param>
        /// <param name="threatType">威胁类型</param>
        public static void ShowThreatNotificationOnly(string filePath, string threatType)
        {
            try
            {
                // 检查文件路径是否有效
                if (string.IsNullOrEmpty(filePath))
                {
                    System.Diagnostics.Debug.WriteLine("文件路径为空，无法显示通知");
                    return;
                }

                string fileName = System.IO.Path.GetFileName(filePath);
                string notificationId = Guid.NewGuid().ToString();
                
                // 根据威胁类型设置不同的消息
                string entityName = threatType == "Reg" ? "注册表项" : "文件";
                string actionName = threatType == "Reg" ? "修改" : "执行";
                string threatMessage = threatType == "Reg" 
                    ? $"检测到注册表项 {fileName} 可能危害系统安全"
                    : $"检测到文件 {fileName} 可能危害安全";
                string interceptMessage = threatType == "Reg"
                    ? $"已拦截此注册表项的修改"
                    : $"已拦截此文件的执行";
                
                // 使用ToastContentBuilder创建通知（不包含按钮）
                var builder = new ToastContentBuilder()
                    .AddArgument("action", "view")
                    .AddArgument("id", notificationId)
                    .AddArgument("path", filePath)
                    .AddArgument("type", threatType)
                    
                    .AddText("XIGUASecurity 安全防护")
                    .AddText(threatMessage)
                    .AddText(interceptMessage);

                // 显示通知
                builder.Show();
                
                System.Diagnostics.Debug.WriteLine($"已显示威胁拦截通知: {fileName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示通知失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
                // 不抛出异常，避免影响主程序
            }
        }

        /// <summary>
        /// 显示威胁拦截通知
        /// </summary>
        /// <param name="filePath">被拦截的文件路径</param>
        /// <param name="threatType">威胁类型</param>
        private void ShowThreatNotification(string filePath, string threatType)
        {
            try
            {
                string fileName = System.IO.Path.GetFileName(filePath);
                string notificationId = Guid.NewGuid().ToString();
                
                // 使用ToastContentBuilder创建通知
                var builder = new ToastContentBuilder()
                    .AddArgument("action", "view")
                    .AddArgument("id", notificationId)
                    .AddArgument("path", filePath)
                    
                    .AddText("XIGUASecurity 安全防护")
                    .AddText($"检测到文件 {fileName} 可能危害安全")
                    .AddText($"已拦截此文件的执行，请确认是否允许运行")
                    
                    .AddButton(new ToastButton()
                        .SetContent("允许执行")
                        .AddArgument("action", "allow")
                        .AddArgument("path", filePath)
                        .AddArgument("id", notificationId))
                    .AddButton(new ToastButton()
                        .SetContent("隔离文件")
                        .AddArgument("action", "quarantine")
                        .AddArgument("path", filePath)
                        .AddArgument("id", notificationId))
                    .AddButton(new ToastButton()
                        .SetContent("删除文件")
                        .AddArgument("action", "delete")
                        .AddArgument("path", filePath)
                        .AddArgument("id", notificationId));

                // 显示通知
                builder.Show();
                
                System.Diagnostics.Debug.WriteLine($"已显示威胁拦截通知: {fileName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示通知失败: {ex.Message}");
            }
        }
    }
}