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
using XIGUASecurity.Services;
using Microsoft.UI.Xaml.Documents;

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
                await LoadAnnouncementsAsync();
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
                // 防护状态卡片已替换为公告卡片，不再需要初始化ToggleSwitch
                /*
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
                */
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
            // 防护状态卡片已替换为公告卡片，不再需要更新防护状态
            /*
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
            */
        }

        // 实时防护开关事件
        private void OnRealTimeProtectionToggled(object sender, RoutedEventArgs e)
        {
            // 防护状态卡片已替换为公告卡片，不再需要处理ToggleSwitch事件
            /*
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
            */
        }

        // 文件防护开关事件
        private void OnFileProtectionToggled(object sender, RoutedEventArgs e)
        {
            // 防护状态卡片已替换为公告卡片，不再需要处理ToggleSwitch事件
            /*
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
            */
        }

        // 同步防护状态到设置页面
        private void SyncProtectionToSettings(string protectionType, bool isEnabled)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values[$"Protection_{protectionType}"] = isEnabled;
        }

        // 显示更新对话框
        private async Task ShowUpdateDialog(UpdateInfo update)
        {
            var contentDialog = new ContentDialog
            {
                Title = "发现新版本",
                Content = $"发现新版本 {update.Version}\n\n{update.Description}",
                PrimaryButtonText = "立即更新",
                CloseButtonText = "稍后提醒",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await contentDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // 用户点击了立即更新
                await Windows.System.Launcher.LaunchUriAsync(new Uri(update.DownloadUrl));
            }
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

        // 加载公告
        private async Task LoadAnnouncementsAsync()
        {
            try
            {
                var announcement = await AnnouncementService.Instance.GetLatestAnnouncementAsync();
                
                if (announcement != null)
                {
                    // 更新主公告面板
                    UpdateMainAnnouncementPanel(announcement);
                }
                else
                {
                    // 没有公告时显示提示
                    // 清空主公告面板
                    MainAnnouncementPanel.Children.Clear();
                    var noAnnouncementText = new TextBlock
                    {
                        Text = "暂无公告",
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                        TextWrapping = TextWrapping.Wrap
                    };
                    MainAnnouncementPanel.Children.Add(noAnnouncementText);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAnnouncementsAsync error: {ex.Message}");
                
                // 清空主公告面板
                MainAnnouncementPanel.Children.Clear();
                var errorText = new TextBlock
                {
                    Text = "加载公告失败",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                    TextWrapping = TextWrapping.Wrap
                };
                MainAnnouncementPanel.Children.Add(errorText);
            }
        }

        // 更新主公告面板
        private void UpdateMainAnnouncementPanel(Services.Announcement announcement)
        {
            // 清空现有内容
            MainAnnouncementPanel.Children.Clear();
            
            // 添加标题
            var titleTextBlock = new TextBlock
            {
                Text = announcement.Title,
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            MainAnnouncementPanel.Children.Add(titleTextBlock);
            
            // 添加发布日期
            var dateTextBlock = new TextBlock
            {
                Text = $"发布时间: {announcement.PublishDate}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                Margin = new Thickness(0, 4, 0, 8)
            };
            MainAnnouncementPanel.Children.Add(dateTextBlock);
            
            // 添加分隔线
            var separator = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Height = 1,
                Fill = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                Margin = new Thickness(0, 0, 0, 8)
            };
            MainAnnouncementPanel.Children.Add(separator);
            
            // 添加内容 - 使用RichTextBlock支持HTML格式
            var richTextBlock = new RichTextBlock
            {
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };
            
            // 解析HTML内容并添加到RichTextBlock
            var paragraph = new Paragraph();
            
            // 简单的HTML解析，支持基本标签
            var content = announcement.Content;
            var isBold = false;
            var isItalic = false;
            
            // 使用正则表达式解析HTML
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
                            var bold = new Bold();
                            bold.Inlines.Add(new Run { Text = textBefore });
                            paragraph.Inlines.Add(bold);
                        }
                        else if (isItalic)
                        {
                            var italic = new Italic();
                            italic.Inlines.Add(new Run { Text = textBefore });
                            paragraph.Inlines.Add(italic);
                        }
                        else
                        {
                            paragraph.Inlines.Add(new Run { Text = textBefore });
                        }
                    }
                }
                
                // 处理标签
                var isClosingTag = match.Groups[1].Value == "/";
                var tagName = match.Groups[2].Value;
                
                if (tagName == "br")
                {
                    paragraph.Inlines.Add(new LineBreak());
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
                        var bold = new Bold();
                        bold.Inlines.Add(new Run { Text = remainingText });
                        paragraph.Inlines.Add(bold);
                    }
                    else if (isItalic)
                    {
                        var italic = new Italic();
                        italic.Inlines.Add(new Run { Text = remainingText });
                        paragraph.Inlines.Add(italic);
                    }
                    else
                    {
                        paragraph.Inlines.Add(new Run { Text = remainingText });
                    }
                }
            }
            
            richTextBlock.Blocks.Add(paragraph);
            MainAnnouncementPanel.Children.Add(richTextBlock);
        }
    }
}
        
