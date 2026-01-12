using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Reflection;
using Windows.Storage;
using XIGUASecurity.Utils;

namespace XIGUASecurity.Core
{
    /// <summary>
    /// 辅助模式管理器
    /// </summary>
    public static class AssistantModeManager
    {
        private const string AssistantModeSettingKey = "AssistantModeEnabled";
        
        /// <summary>
        /// 当前是否处于辅助模式
        /// </summary>
        public static bool IsAssistantMode { get; private set; } = false;
        
        /// <summary>
        /// 辅助模式状态变更事件
        /// </summary>
        public static event EventHandler<bool>? AssistantModeChanged;
        
        /// <summary>
        /// 初始化辅助模式管理器
        /// </summary>
        /// <param name="owner">主窗口</param>
        public static void Initialize(Window? owner)
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings;
                
                // 从设置中读取辅助模式状态
                if (settings.Values.TryGetValue(AssistantModeSettingKey, out var isAssistantMode))
                {
                    IsAssistantMode = Convert.ToBoolean(isAssistantMode);
                }
                
                // 订阅辅助模式变更事件
                AssistantModeChanged?.Invoke(null, IsAssistantMode);
            }
            catch
            {
                // 静默处理错误，不显示给用户
            }
        }
        
        /// <summary>
        /// 设置辅助模式状态
        /// </summary>
        /// <param name="isAssistantMode">是否启用辅助模式</param>
        public static void SetAssistantMode(bool isAssistantMode)
        {
            try
            {
                if (IsAssistantMode != isAssistantMode)
                {
                    IsAssistantMode = isAssistantMode;
                    
                    // 触发状态变更事件
                    AssistantModeChanged?.Invoke(null, IsAssistantMode);
                    
                    // 更新窗口标题
                    var mainWindow = App.MainWindow;
                    if (mainWindow != null)
                    {
                        UpdateWindowTitle(mainWindow);
                    }
                    
                    // 保存设置
                    try
                    {
                        var settings = ApplicationData.Current.LocalSettings;
                        settings.Values[AssistantModeSettingKey] = isAssistantMode;
                    }
                    catch
                    {
                        // 静默处理错误，不显示给用户
                    }
                }
            }
            catch
            {
                // 静默处理错误，不显示给用户
            }
        }
        
        /// <summary>
        /// 更新窗口标题
        /// </summary>
        /// <param name="window">要更新的窗口</param>
        private static void UpdateWindowTitle(Window? window)
        {
            try
            {
                if (window != null)
                {
                    // 更新窗口标题
                    window.Title = IsAssistantMode ? "XIGUASecurity[Xdows辅助模式]" : "XIGUASecurity";
                    
                    // 更新标题文本
                    if (window is MainWindow mainWindow)
                    {
                        // 使用反射获取TitleText控件并更新文本
                        var titleTextProperty = typeof(MainWindow).GetField("TitleText", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (titleTextProperty?.GetValue(mainWindow) is TextBlock titleText)
                        {
                            titleText.Text = IsAssistantMode ? "XIGUASecurity[Xdows辅助模式]" : AppInfo.AppName;
                        }
                    }
                }
            }
            catch
            {
                // 静默处理错误，不显示给用户
            }
        }
    }
}