using CommunityToolkit.WinUI.Controls;
using Compatibility.Windows.Storage;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.Security.Credentials.UI;
using WinUI3Localizer;
using XIGUASecurity.Protection;
using XIGUASecurity.UI.Dialogs;

namespace XIGUASecurity
{
    public sealed partial class SettingsPage : Page
    {
        private readonly bool IsInitialize = true;
        public SettingsPage()
        {
            this.InitializeComponent();
            LoadLanguageSetting();
            LoadThemeSetting();
            LoadBackdropSetting();
            LoadScanSetting();
            LoadBackgroundImageSetting();
            Settings_About_Name.Text = AppInfo.AppName;
            Settings_About_Version.Text = AppInfo.AppVersion;
            Settings_About_Feedback.NavigateUri = new Uri(AppInfo.AppFeedback);
            Settings_About_Website.NavigateUri = new Uri(AppInfo.AppWebsite);
            if (App.GetCzkCloudApiKey() == string.Empty)
            {
                CzkCloudScanToggle.IsOn = false;
                CzkCloudScanToggle.IsEnabled = false;
            }

            // 检查AX_API.exe是否存在
            string axApiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AX_API", "AX_API.exe");
            if (!File.Exists(axApiPath))
            {
                JiSuSafeAXToggle.IsOn = false;
                JiSuSafeAXToggle.IsEnabled = false;
            }

            //if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "model.onnx")) {
            //SouXiaoScanToggle.IsOn = false;
            //SouXiaoScanToggle.IsEnabled = false;
            //ProcessToggle.IsOn = false;
            //ProcessToggle.IsEnabled = false;
            //FilesToggle.IsOn = false;
            //FilesToggle.IsEnabled = false;
            //}
            if (!App.IsRunAsAdmin())
            {
                RegistryToggle.IsEnabled = false;
                RegistryToggle.IsOn = false;
            }

            // 初始化辅助模式设置
            try
            {
                AssistantModeToggle.IsOn = Core.AssistantModeManager.IsAssistantMode;
                // UpdateAssistantModeStatus(); // 已移除状态文本显示
                
                // 订阅辅助模式变更事件
                Core.AssistantModeManager.AssistantModeChanged += OnAssistantModeChanged;
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "SettingsPage", $"初始化辅助模式设置时发生错误: {ex.Message}");
            }

            IsInitialize = false;
        }
        private void RunProtectionWithToggle(ToggleSwitch toggle, int runId)
        {
            toggle.Toggled -= RunProtection;
            if (!XIGUASecurity.ProtectionManager.Run(runId)) { return; }
            toggle.IsOn = !toggle.IsOn;
            if (runId == 0)
                toggle.IsOn = ProcessProtection.IsEnabled();
            if (runId == 1)
                toggle.IsOn = FilesProtection.IsEnabled();
            if (runId == 4)
                toggle.IsOn = RegistryProtection.IsEnabled();
            if (runId == 5)
                toggle.IsOn = DocumentProtection.IsEnabled();
            toggle.Toggled += RunProtection;
        }
        private void Settings_Feedback_Click(object sender, RoutedEventArgs e)
        {
            App.MainWindow?.GoToBugReportPage(SettingsPage_Other_Feedback.Header.ToString());
        }
        private void RunProtection(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch toggle || IsInitialize) return;
            int runId = toggle.Tag switch
            {
                "Progress" => 0,
                "Files" => 1,
                "Registry" => 4,
                "Document" => 5,
                _ => 0
            };
            RunProtectionWithToggle(toggle, runId);
        }
        private async void Toggled_SaveToggleData(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch toggle || IsInitialize) return;

            string key = toggle.Tag as string ?? toggle.Name;
            if (string.IsNullOrWhiteSpace(key)) return;
            if (toggle.IsOn && (key == "CzkCloudScan" || key == "CloudScan"))
            {
                _ = new ContentDialog
                {
                    Title = Localizer.Get().GetLocalizedString("SettingsPage_Scan_Cloud_Disclaimer_Title"),
                    Content = Localizer.Get().GetLocalizedString("SettingsPage_Scan_Cloud_Disclaimer_Text"),
                    PrimaryButtonText = Localizer.Get().GetLocalizedString("Button_Confirm"),
                    XamlRoot = this.XamlRoot,
                    RequestedTheme = (XamlRoot.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default,
                    PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"]
                }.ShowAsync();
            }
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values[key] = toggle.IsOn;
        }
        private void LoadScanSetting()
        {
            var settings = ApplicationData.Current.LocalSettings;

            var toggles = new List<ToggleSwitch>
              {
                 ScanProgressToggle,
                 DeepScanToggle,
                 ExtraDataToggle,
                 LocalScanToggle,
                 CzkCloudScanToggle,
                 JiSuSafeAXToggle,
                 SouXiaoScanToggle,
                 CloudScanToggle,
                 TrayVisibleToggle,
                 DisabledVerifyToggle
               };

            foreach (var toggle in toggles)
            {
                if (toggle == null) continue;

                if (toggle.Tag is string key && !string.IsNullOrWhiteSpace(key) &&
                    settings.Values.TryGetValue(key, out object raw) && raw is bool isOn)
                {
                    toggle.IsOn = isOn;
                }
            }

            if (settings.Values.TryGetValue("AppBackdropOpacity", out object opacityRaw) &&
                opacityRaw is double opacity)
            {
                Appearance_Backdrop_Opacity.Value = opacity;
            }
            else
            {
                Appearance_Backdrop_Opacity.Value = 100;
            }

            ProcessToggle.IsOn = ProcessProtection.IsEnabled();
            FilesToggle.IsOn = FilesProtection.IsEnabled();
            RegistryToggle.IsOn = RegistryProtection.IsEnabled();
            DocumentToggle.IsOn = DocumentProtection.IsEnabled();
        }

        private void LoadLanguageSetting()
        {
            var settings = ApplicationData.Current.LocalSettings;

            if (!settings.Values.TryGetValue("AppLanguage", out object langRaw) ||
                langRaw is not string savedLanguage)
            {
                savedLanguage = "en-US";
            }

            foreach (ComboBoxItem item in LanguageComboBox.Items.Cast<ComboBoxItem>())
            {
                if (item.Tag as string == savedLanguage)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }
        }
        private void LoadThemeSetting()
        {
            var settings = ApplicationData.Current.LocalSettings;

            if (!settings.Values.TryGetValue("AppTheme", out object themeRaw) ||
                themeRaw is not string themeString ||
                !Enum.TryParse(themeString, out ElementTheme themeValue))
            {
                themeValue = ElementTheme.Default;
            }

            ThemeComboBox.SelectedIndex = themeValue switch
            {
                ElementTheme.Light => 1,
                ElementTheme.Dark => 2,
                _ => 0
            };

            NavComboBox.SelectedIndex =
                settings.Values.TryGetValue("AppNavTheme", out object raw) && raw is double d ?
                (int)d : 0;
        }
        private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsInitialize) return;
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var currentLanguage = Localizer.Get().GetCurrentLanguage();
                if (selectedItem.Tag is not string newLanguage) return;
                if (newLanguage != currentLanguage)
                {
                    ApplicationData.Current.LocalSettings.Values["AppLanguage"] = newLanguage;
                    await Localizer.Get().SetLanguage(newLanguage);
                }
            }
        }

        private async void UpdateButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateButton.IsEnabled = false;
                UpdateProgressRing.IsActive = true;
                UpdateProgressRing.Visibility = Visibility.Visible;

                var update = await Updater.CheckUpdateAsync();
                if (update == null)
                {
                    UpdateButton.IsEnabled = true;
                    UpdateProgressRing.IsActive = false;
                    UpdateProgressRing.Visibility = Visibility.Collapsed;
                    UpdateTeachingTip.ActionButtonContent = Localizer.Get().GetLocalizedString("Button_Confirm");
                    UpdateTeachingTip.IsOpen = !UpdateTeachingTip.IsOpen;
                    return;
                }
                var box = new CommunityToolkit.WinUI.UI.Controls.MarkdownTextBlock
                {
                    Text = update.Content,
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
                    Title = update.Title,
                    Content = scrollViewer,
                    PrimaryButtonText = update.IsCurrentVersionNewer ? "下载最新(当前已是最新)" : Localizer.Get().GetLocalizedString("Button_Download"),
                    SecondaryButtonText = Localizer.Get().GetLocalizedString("Button_Cancel"),
                    XamlRoot = this.XamlRoot,
                    RequestedTheme = (XamlRoot.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default,
                    PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                    DefaultButton = ContentDialogButton.Primary
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(update.DownloadUrl));
                }
            }
            catch
            {
                try
                {
                    UpdateTeachingTip.ActionButtonContent = Localizer.Get().GetLocalizedString("Button_Confirm");
                    UpdateTeachingTip.IsOpen = !UpdateTeachingTip.IsOpen;
                }
                catch { }
            }
            finally
            {
                UpdateButton.IsEnabled = true;
                UpdateProgressRing.IsActive = false;
                UpdateProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateTeachingTipClose(TeachingTip sender, object args)
        {
            sender.IsOpen = false;
        }
        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsInitialize || ThemeComboBox.SelectedIndex == -1) return;

            ElementTheme selectedTheme = ThemeComboBox.SelectedIndex switch
            {
                0 => ElementTheme.Default,
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["AppTheme"] = selectedTheme.ToString();
            if (App.MainWindow == null) return;
            if (App.MainWindow.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = selectedTheme;
            }
            MainWindow.UpdateTheme(selectedTheme);
        }

        private void LoadBackdropSetting()
        {
            var settings = ApplicationData.Current.LocalSettings;

            var savedBackdrop = settings.Values["AppBackdrop"] as string;

            Appearance_Backdrop_Opacity.IsEnabled = !(savedBackdrop == "Solid");
            MicaOption.IsEnabled = MicaController.IsSupported();
            MicaAltOption.IsEnabled = MicaController.IsSupported();

            bool found = false;

            foreach (ComboBoxItem item in BackdropComboBox.Items.Cast<ComboBoxItem>())
            {
                if (item.Tag as string == savedBackdrop)
                {
                    BackdropComboBox.SelectedItem = item;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                BackdropComboBox.SelectedIndex = MicaController.IsSupported() ? 1 : 3;
            }
        }

        private async void LoadBackgroundImageSetting()
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings;
                var backdropType = settings.Values["AppBackdrop"] as string ?? "Solid";
                var opacityValue = settings.Values["AppBackgroundImageOpacity"] as double? ?? 30.0;
                BackgroundImageOpacitySlider.Value = opacityValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载背景图片设置失败: {ex.Message}");
            }
        }

        private void BackdropComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsInitialize) return;
            if (BackdropComboBox.SelectedItem is ComboBoxItem selected)
            {
                try
                {
                    string backdropType = selected.Tag as string ?? ElementTheme.Default.ToString();
                    var settings = ApplicationData.Current.LocalSettings;
                    settings.Values["AppBackdrop"] = backdropType;

                    // 应用新背景
                    App.MainWindow?.ApplyBackdrop(backdropType, false);
                    Appearance_Backdrop_Opacity.IsEnabled = !(backdropType == "Solid");
                }
                catch { }
            }
        }

        private void OpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (IsInitialize || sender is not Slider slider) return;
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["AppBackdropOpacity"] = slider.Value;
            if (App.MainWindow == null) return;
            App.MainWindow.ApplyBackdrop(settings.Values["AppBackdrop"] as string ?? "Mica", false);
        }

        private void AssistantModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!IsInitialize) // 确保不是初始化阶段
                {
                    bool isEnabled = AssistantModeToggle.IsOn;
                    Core.AssistantModeManager.SetAssistantMode(isEnabled);
                    // UpdateAssistantModeStatus(); // 已移除状态文本显示
                }
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "SettingsPage", $"切换辅助模式时发生错误: {ex.Message}");
                // 恢复切换前的状态
                AssistantModeToggle.IsOn = !AssistantModeToggle.IsOn;
            }
        }

        private void UpdateAssistantModeStatus()
        {
            try
            {
                // AssistantModeStatus.Text = Core.AssistantModeManager.IsAssistantMode 
                //     ? "已启用辅助模式：当前程序仅提供防护和应用商店支持，扫描功能由Xdows-Security提供。" 
                //     : "已禁用辅助模式：当前程序提供所有功能，包括防护、扫描和应用商店支持。";
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "SettingsPage", $"更新辅助模式状态文本时发生错误: {ex.Message}");
            }
        }

        private void OnAssistantModeChanged(object? sender, bool isAssistantMode)
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    AssistantModeToggle.IsOn = isAssistantMode;
                    // UpdateAssistantModeStatus(); // 已移除状态文本显示
                });
            }
            catch (Exception ex)
            {
                LogText.AddNewLog(LogLevel.ERROR, "SettingsPage", $"处理辅助模式变更事件时发生错误: {ex.Message}");
            }
        }

        private void NavComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsInitialize) return;
            try
            {
                int index = NavComboBox.SelectedIndex;
                var settings = ApplicationData.Current.LocalSettings;
                settings.Values["AppNavTheme"] = index;
                App.MainWindow?.UpdateNavTheme(index);
            }
            catch { }
        }

        /// <summary>
        /// 隔离区查看按钮点击事件
        /// </summary>
        private async void Quarantine_ViewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new QuarantineDialog
                {
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open quarantine dialog: {ex.Message}");
            }
        }

        /// <summary>
        /// 隔离区清空按钮点击事件
        /// </summary>
        private async void Quarantine_ClearButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int count = QuarantineManager.GetQuarantineCount();

                if (count == 0)
                {
                    var emptyDialog = new ContentDialog
                    {
                        Title = Localizer.Get().GetLocalizedString("SettingsPage_Quarantine_Empty_Title"),
                        Content = Localizer.Get().GetLocalizedString("SettingsPage_Quarantine_Empty_Content"),
                        CloseButtonText = Localizer.Get().GetLocalizedString("Button_Confirm"),
                        XamlRoot = this.XamlRoot
                    };
                    await emptyDialog.ShowAsync();
                    return;
                }

                var confirmDialog = new ContentDialog
                {
                    Title = Localizer.Get().GetLocalizedString("SettingsPage_Quarantine_ClearConfirm_Title"),
                    Content = string.Format(Localizer.Get().GetLocalizedString("SettingsPage_Quarantine_ClearConfirm_Content"), count),
                    PrimaryButtonText = Localizer.Get().GetLocalizedString("SettingsPage_Quarantine_ClearConfirm_PrimaryButton"),
                    CloseButtonText = Localizer.Get().GetLocalizedString("Button_Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                if (await confirmDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    bool success = QuarantineManager.ClearQuarantine();

                    if (success)
                    {
                        var successDialog = new ContentDialog
                        {
                            Title = Localizer.Get().GetLocalizedString("SettingsPage_Quarantine_ClearSuccess_Title"),
                            Content = Localizer.Get().GetLocalizedString("SettingsPage_Quarantine_ClearSuccess_Content"),
                            CloseButtonText = Localizer.Get().GetLocalizedString("Button_Confirm"),
                            XamlRoot = this.XamlRoot
                        };
                        await successDialog.ShowAsync();
                    }
                    else
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = Localizer.Get().GetLocalizedString("SettingsPage_Quarantine_ClearFailed_Title"),
                            Content = Localizer.Get().GetLocalizedString("SettingsPage_Quarantine_ClearFailed_Content"),
                            CloseButtonText = Localizer.Get().GetLocalizedString("Button_Confirm"),
                            XamlRoot = this.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear quarantine: {ex.Message}");

                var errorDialog = new ContentDialog
                {
                    Title = Localizer.Get().GetLocalizedString("SettingsPage_Quarantine_ClearFailed_Title"),
                    Content = Localizer.Get().GetLocalizedString("SettingsPage_Quarantine_ClearFailed_Content"),
                    CloseButtonText = Localizer.Get().GetLocalizedString("Button_Confirm"),
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        /// <summary>
        /// 备份目录设置按钮点击事件
        /// </summary>
        private async void BackupFolderSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new XIGUASecurity.UI.Dialogs.BackupFolderDialog
                {
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open backup folder settings: {ex.Message}");

                var errorDialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "无法打开备份目录设置",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        /// <summary>
        /// 一键还原所有文件按钮点击事件
        /// </summary>
        private async void RestoreAllFiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查文档保护是否启用
                if (!XIGUASecurity.Protection.DocumentProtection.IsEnabled())
                {
                    var confirmDialog = new ContentDialog
                    {
                        Title = Localizer.Get().GetLocalizedString("SettingsPage_Protection_Document_RestoreAll_Disabled_Title"),
                        Content = Localizer.Get().GetLocalizedString("SettingsPage_Protection_Document_RestoreAll_Disabled_Content"),
                        CloseButtonText = Localizer.Get().GetLocalizedString("Common_Ok"),
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };
                    await confirmDialog.ShowAsync();
                    return;
                }

                // 获取所有备份文件信息
                var allBackups = XIGUASecurity.Protection.DocumentProtection.GetAllFileBackups();
                
                if (allBackups == null || allBackups.Count == 0)
                {
                    var infoDialog = new ContentDialog
                    {
                        Title = Localizer.Get().GetLocalizedString("SettingsPage_Protection_Document_RestoreAll_NoBackups_Title"),
                        Content = Localizer.Get().GetLocalizedString("SettingsPage_Protection_Document_RestoreAll_NoBackups_Content"),
                        CloseButtonText = Localizer.Get().GetLocalizedString("Common_Ok"),
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };
                    await infoDialog.ShowAsync();
                    return;
                }

                // 确认对话框
                var confirmRestoreDialog = new ContentDialog
                {
                    Title = Localizer.Get().GetLocalizedString("SettingsPage_Protection_Document_RestoreAll_Confirm_Title"),
                    Content = Localizer.Get().GetLocalizedString("SettingsPage_Protection_Document_RestoreAll_Confirm_Content").Replace("{0}", allBackups.Count.ToString()),
                    PrimaryButtonText = Localizer.Get().GetLocalizedString("SettingsPage_Protection_Document_RestoreAll_Confirm_Button_Restore"),
                    CloseButtonText = Localizer.Get().GetLocalizedString("Common_Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmRestoreDialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                {
                    return;
                }

                // 创建进度对话框
                var progressDialog = new ContentDialog
                {
                    Title = "正在还原文件",
                    Content = "请稍候，正在还原所有备份文件...",
                    CloseButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };

                // 使用异步任务执行还原操作
                int successCount = 0;
                int failCount = 0;
                bool isCancelled = false;
                
                // 设置批量还原状态标志
                XIGUASecurity.Protection.DocumentProtection.SetBulkRestoreInProgress(true);
                
                try
                {
                    // 启动还原任务
                    var restoreTask = System.Threading.Tasks.Task.Run(() =>
                    {
                        foreach (var backup in allBackups)
                        {
                            // 检查是否已取消
                            if (isCancelled) break;
                            
                            try
                            {
                                if (XIGUASecurity.Protection.DocumentProtection.RestoreFileFromBackup(backup.OriginalPath))
                                {
                                    System.Threading.Interlocked.Increment(ref successCount);
                                }
                                else
                                {
                                    System.Threading.Interlocked.Increment(ref failCount);
                                }
                            }
                            catch
                            {
                                System.Threading.Interlocked.Increment(ref failCount);
                            }
                        }
                    });

                    // 显示进度对话框并等待结果
                    var progressResult = await progressDialog.ShowAsync();
                    
                    // 如果用户点击了取消
                    if (progressResult == ContentDialogResult.None)
                    {
                        isCancelled = true;
                    }
                    
                    // 等待还原任务完成
                    await restoreTask;
                }
                finally
                {
                    // 清除批量还原状态标志
                    XIGUASecurity.Protection.DocumentProtection.SetBulkRestoreInProgress(false);
                }

                // 关闭进度对话框
                progressDialog.Hide();

                // 显示结果
                var resultDialog = new ContentDialog
                {
                    Title = isCancelled ? "已取消" : "还原完成",
                    Content = isCancelled ? 
                        "文件还原操作已取消。" :
                        $"文件还原操作完成。\n成功还原：{successCount} 个文件\n失败：{failCount} 个文件",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await resultDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restore all files: {ex.Message}");

                var errorDialog = new ContentDialog
                {
                    Title = "还原失败",
                    Content = $"还原文件时发生错误：{ex.Message}",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }



        /// <summary>
        /// 信任区查看按钮点击事件
        /// </summary>
        private async void Trust_ViewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new TrustDialog
                {
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open trust dialog: {ex.Message}");
            }
        }

        /// <summary>
        /// 信任区添加按钮点击事件
        /// </summary>
        private async void Trust_AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new AddTrustDialog
                {
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open add trust dialog: {ex.Message}");
            }
        }

        private void TrayVisibleToggle_Toggled(object sender, RoutedEventArgs e)
        {
            Toggled_SaveToggleData(sender, e);
            App.MainWindow?.manager?.IsVisibleInTray = TrayVisibleToggle.IsEnabled;
        }

        private void SettingsSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                string searchText = sender.Text.ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    ShowAllSettingsItems();
                    return;
                }

                FilterSettingsItems(searchText);
            }
        }

        private void ShowAllSettingsItems()
        {
            var scrollViewer = this.Content as ScrollViewer;
            if (scrollViewer == null) return;

            var stackPanel = scrollViewer.Content as StackPanel;
            if (stackPanel == null) return;

            foreach (var child in stackPanel.Children)
            {
                if (child is AutoSuggestBox) continue;

                if (child is FrameworkElement element)
                {
                    element.Visibility = Visibility.Visible;

                    if (element is SettingsExpander expander)
                    {
                        foreach (var expanderChild in expander.Items)
                        {
                            if (expanderChild is SettingsCard card)
                            {
                                card.Visibility = Visibility.Visible;
                            }
                        }
                    }
                }
            }
        }
        private void FilterSettingsItems(string searchText)
        {
            var scrollViewer = this.Content as ScrollViewer;
            if (scrollViewer == null) return;

            var stackPanel = scrollViewer.Content as StackPanel;
            if (stackPanel == null) return;

            foreach (var child in stackPanel.Children)
            {
                if (child is AutoSuggestBox) continue;

                if (child is FrameworkElement element)
                {
                    element.Visibility = Visibility.Collapsed;

                    if (element is SettingsExpander expander)
                    {
                        foreach (var expanderChild in expander.Items)
                        {
                            if (expanderChild is SettingsCard card)
                            {
                                card.Visibility = Visibility.Collapsed;
                            }
                        }
                    }
                }
            }

            bool currentHeaderMatched = false;

            for (int i = 0; i < stackPanel.Children.Count; i++)
            {
                var child = stackPanel.Children[i];

                if (child is AutoSuggestBox) continue;

                if (child is FrameworkElement element)
                {
                    if (element is TextBlock textBlock)
                    {
                        currentHeaderMatched = IsSettingsItemMatched(textBlock, searchText);

                        if (currentHeaderMatched)
                        {
                            textBlock.Visibility = Visibility.Visible;
                        }
                    }
                    else if (element is SettingsCard || element is SettingsExpander)
                    {
                        bool shouldShow = false;

                        if (IsSettingsItemMatched(element, searchText))
                        {
                            shouldShow = true;
                        }

                        if (!shouldShow && currentHeaderMatched)
                        {
                            shouldShow = true;
                        }

                        if (element is SettingsExpander expander)
                        {
                            foreach (var expanderChild in expander.Items)
                            {
                                if (expanderChild is SettingsCard card)
                                {
                                    if (IsSettingsItemMatched(card, searchText) || currentHeaderMatched)
                                    {
                                        shouldShow = true;
                                        card.Visibility = Visibility.Visible;
                                    }
                                }
                            }
                        }

                        if (shouldShow)
                        {
                            element.Visibility = Visibility.Visible;
                        }
                    }
                }
            }
        }
        private static bool IsSettingsItemMatched(FrameworkElement item, string searchText)
        {
            string itemText = GetSettingsItemText(item);

            if (string.IsNullOrEmpty(itemText))
                return false;

            return itemText.Contains(searchText, StringComparison.InvariantCultureIgnoreCase);
        }
        private static string GetSettingsItemText(FrameworkElement item)
        {
            if (item is TextBlock textBlock)
            {
                return textBlock.Text;
            }
            else if (item is SettingsCard card)
            {
                return card.Header?.ToString() ?? string.Empty;
            }
            else if (item is SettingsExpander expander)
            {
                return expander.Header?.ToString() ?? string.Empty;
            }

            return string.Empty;
        }
        private bool DisabledVerifyToggleVerify = true;
        private async void DisabledVerifyToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!DisabledVerifyToggleVerify || IsInitialize)
            {
                return;
            }
            if (DisabledVerifyToggle.IsOn)
            {
                DisabledVerifyToggleVerify = false;
                DisabledVerifyToggle.IsOn = false;
                var result = await UserConsentVerifier.RequestVerificationAsync(string.Empty);
                if (result == UserConsentVerificationResult.DeviceNotPresent ||
                result == UserConsentVerificationResult.DisabledByPolicy ||
                result == UserConsentVerificationResult.NotConfiguredForUser ||
                result == UserConsentVerificationResult.Verified)
                {
                    DisabledVerifyToggle.IsOn = true;
                    Toggled_SaveToggleData(sender, e);
                }
                DisabledVerifyToggleVerify = true;
            }
            else
            {
                Toggled_SaveToggleData(sender, e);
            }
        }
        private async void SelectBackgroundImageButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog
            {
                Title = Localizer.Get().GetLocalizedString("SettingsPage_BackgroundImage_SelectDialog_Title"),
                Filters =
                {
                    new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter(Localizer.Get().GetLocalizedString("SettingsPage_BackgroundImage_ImageFiles"), "*.jpg;*.jpeg;*.png;*.bmp;*.gif"),
                    new Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogFilter(Localizer.Get().GetLocalizedString("SettingsPage_BackgroundImage_AllFiles"), "*.*")
                },
                EnsureFileExists = true
            };

            if (dlg.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
            {
                try
                {
                    string imagePath = dlg.FileName;
                    string key = "background_image";
                    await ApplicationData.WriteFileAsync(key, imagePath);

                    // 应用背景图片
                    App.MainWindow?.ApplyBackgroundImage(imagePath);
                }
                catch (Exception ex)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = Localizer.Get().GetLocalizedString("SettingsPage_BackgroundImage_Error_Title"),
                        Content = string.Format(Localizer.Get().GetLocalizedString("SettingsPage_BackgroundImage_SelectError_Content"), ex.Message),
                        CloseButtonText = Localizer.Get().GetLocalizedString("Button_Confirm"),
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }

        private async void ClearBackgroundImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ApplicationData.HasFile("background_image"))
                {
                    await ApplicationData.DeleteFileAsync("background_image");
                    App.MainWindow?.ClearBackgroundImage();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = Localizer.Get().GetLocalizedString("SettingsPage_BackgroundImage_Error_Title"),
                    Content = string.Format(Localizer.Get().GetLocalizedString("SettingsPage_BackgroundImage_ClearError_Content"), ex.Message),
                    CloseButtonText = Localizer.Get().GetLocalizedString("Button_Confirm"),
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private async void OpenConfigLocationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = ApplicationData.LocalFolder.Path;
                await Windows.System.Launcher.LaunchFolderPathAsync(path);
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = Localizer.Get().GetLocalizedString("SettingsPage_Other_Config_Location_OpenFailed_Title"),
                    Content = string.Format(Localizer.Get().GetLocalizedString("SettingsPage_Other_Config_Location_OpenFailed_Content"), ex.Message),
                    CloseButtonText = Localizer.Get().GetLocalizedString("Button_Confirm"),
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private async void ResetConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var confirmDialog = new ContentDialog
            {
                Title = Localizer.Get().GetLocalizedString("SettingsPage_Other_Config_Reset_Confirm_Title"),
                Content = Localizer.Get().GetLocalizedString("SettingsPage_Other_Config_Reset_Confirm_Content"),
                PrimaryButtonText = Localizer.Get().GetLocalizedString("Button_Confirm"),
                CloseButtonText = Localizer.Get().GetLocalizedString("Button_Cancel"),
                XamlRoot = this.XamlRoot,
                PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"]
            };

            if (await confirmDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    var path = ApplicationData.LocalFolder.Path;
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                }
                catch (Exception ex)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = Localizer.Get().GetLocalizedString("SettingsPage_Other_Config_Reset_DeleteFailed_Title"),
                        Content = string.Format(Localizer.Get().GetLocalizedString("SettingsPage_Other_Config_Reset_DeleteFailed_Content"), ex.Message),
                        CloseButtonText = Localizer.Get().GetLocalizedString("Button_Confirm"),
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    return;
                }

                try
                {
                    var current = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(current))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = current,
                            UseShellExecute = true
                        });
                    }
                }
                catch { }

                App.MainWindow?.Close();
                Environment.Exit(0);
            }
        }

        private void BackgroundImageOpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (IsInitialize || sender is not Slider slider) return;

            // 保存透明度设置
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["AppBackgroundImageOpacity"] = slider.Value;

            // 应用新的透明度
            App.MainWindow?.UpdateBackgroundImageOpacity(slider.Value / 100.0);
        }
    }
}