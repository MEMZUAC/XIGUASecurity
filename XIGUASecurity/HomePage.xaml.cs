using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Linq;
using XIGUASecurity.ViewModel;
using XIGUASecurity.Protection;
using Windows.Storage;
using System.Threading.Tasks;
using System;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml.Media;

namespace XIGUASecurity
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            /* 1. 页面加载完成后刷新数据，更新 UI */
            Loaded += async (_, _) => 
            {
                await (DataContext as HomeViewModel)!.LoadOnUiThread();
                InitializeProtectionToggles();
                await CheckForUpdatesAsync();
            };
        }

        /* 2. 日志筛选，保留原有功能 */
        private void LogLevelFilter_MenuClick(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleMenuFlyoutItem item) return;
            var flyout = LogLevelFilter.Flyout as MenuFlyout;
            var selected = flyout!.Items
                                  .OfType<ToggleMenuFlyoutItem>()
                                  .Where(t => t.Tag.ToString() != "All" && t.IsChecked)
                                  .Select(t => t.Tag.ToString()!)
                                  .ToArray();
            (DataContext as HomeViewModel)!.LogLevelFilterCommand.Execute(selected);
        }

        // 快速扫描按钮点击事件
        private void QuickScanButton_Click(object sender, RoutedEventArgs e)
        {
            // 获取MainWindow实例并使用其GoToPage方法
            if (App.MainWindow != null)
            {
                App.MainWindow.GoToPage("Security");
            }
        }

        // 设置按钮点击事件
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // 获取MainWindow实例并使用其GoToPage方法
            if (App.MainWindow != null)
            {
                App.MainWindow.GoToPage("Settings");
            }
        }

        // 初始化防护开关状态
        private void InitializeProtectionToggles()
        {
            try
            {
                // 确保在UI线程上执行
                if (!DispatcherQueue.HasThreadAccess)
                {
                    DispatcherQueue.TryEnqueue(() => InitializeProtectionToggles());
                    return;
                }

                var settings = ApplicationData.Current.LocalSettings;
                
                // 读取防护状态，如果不存在则使用默认值
                if (settings.Values.TryGetValue("Protection_Process", out var processProtectionValue))
                {
                    if (RealTimeProtectionToggle != null)
                        RealTimeProtectionToggle.IsOn = (bool)processProtectionValue;
                }
                else
                {
                    // 使用默认值，避免在UI线程上直接调用防护状态检查
                    if (RealTimeProtectionToggle != null)
                        RealTimeProtectionToggle.IsOn = false;
                }
                
                if (settings.Values.TryGetValue("Protection_Files", out var filesProtectionValue))
                {
                    if (FileProtectionToggle != null)
                        FileProtectionToggle.IsOn = (bool)filesProtectionValue;
                }
                else
                {
                    // 使用默认值，避免在UI线程上直接调用防护状态检查
                    if (FileProtectionToggle != null)
                        FileProtectionToggle.IsOn = false;
                }
                
                // 异步获取实际防护状态
                _ = UpdateProtectionStatusAsync();
            }
            catch (Exception ex)
            {
                // 记录异常但不中断程序执行
                System.Diagnostics.Debug.WriteLine($"InitializeProtectionToggles error: {ex.Message}");
            }
        }
        
        // 异步更新防护状态
        private async Task UpdateProtectionStatusAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        // 在后台线程检查防护状态
                        bool processEnabled = ProcessProtection.IsEnabled();
                        bool filesEnabled = FilesProtection.IsEnabled();
                        
                        // 切换回UI线程更新UI
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                // 只有当设置中没有保存值时才更新
                                var settings = ApplicationData.Current.LocalSettings;
                                if (!settings.Values.ContainsKey("Protection_Process") && RealTimeProtectionToggle != null)
                                {
                                    RealTimeProtectionToggle.IsOn = processEnabled;
                                }
                                if (!settings.Values.ContainsKey("Protection_Files") && FileProtectionToggle != null)
                                {
                                    FileProtectionToggle.IsOn = filesEnabled;
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Update protection status UI error: {ex.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Check protection status error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateProtectionStatusAsync error: {ex.Message}");
            }
        }

        // 实时防护开关事件
        private void OnRealTimeProtectionToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                if (toggle.IsOn)
                {
                    ProcessProtection.Enable(ProtectionManager.interceptCallBack);
                }
                else
                {
                    ProcessProtection.Disable();
                }
                
                // 同步到设置页面
                SyncProtectionToSettings("Process", toggle.IsOn);
            }
        }

        // 文件防护开关事件
        private void OnFileProtectionToggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                if (toggle.IsOn)
                {
                    FilesProtection.Enable(ProtectionManager.interceptCallBack);
                }
                else
                {
                    FilesProtection.Disable();
                }
                
                // 同步到设置页面
                SyncProtectionToSettings("Files", toggle.IsOn);
            }
        }

        // 同步防护状态到设置页面
        private void SyncProtectionToSettings(string protectionType, bool isEnabled)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values[$"Protection_{protectionType}"] = isEnabled;
        }
        
        // 检查更新
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var update = await Updater.CheckUpdateAsync();
                if (update != null && update.HasUpdate)
                {
                    // 有新版本，显示更新提示
                    await ShowUpdateDialog(update);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckForUpdatesAsync error: {ex.Message}");
            }
        }
        
        // 显示更新提示对话框
        private async Task ShowUpdateDialog(UpdateInfo updateInfo)
        {
            try
            {
                var box = new MarkdownTextBlock
                {
                    Text = updateInfo.Content,
                    IsTextSelectionEnabled = true,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12),
                    Background = null
                };
                
                var scrollViewer = new ScrollViewer
                {
                    Content = box,
                    MaxHeight = 320,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
                
                var dialog = new ContentDialog
                {
                    Title = updateInfo.Title,
                    Content = scrollViewer,
                    PrimaryButtonText = "立即下载",
                    SecondaryButtonText = "稍后提醒",
                    XamlRoot = this.XamlRoot,
                    RequestedTheme = (XamlRoot.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default,
                    PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                    DefaultButton = ContentDialogButton.Primary
                };
                
                var result = await dialog.ShowAsync();
                
                if (result == ContentDialogResult.Primary)
                {
                    // 打开下载页面
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(updateInfo.DownloadUrl));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowUpdateDialog error: {ex.Message}");
            }
        }
    }
}