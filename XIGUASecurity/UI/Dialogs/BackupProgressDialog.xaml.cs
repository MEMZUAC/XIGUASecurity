using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace XIGUASecurity.UI.Dialogs
{
    public sealed partial class BackupProgressDialog : ContentDialog
    {
        private int _lastReportedProgress = -1;
        private readonly object _updateLock = new object();
        
        public BackupProgressDialog()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// 更新备份进度
        /// </summary>
        /// <param name="current">当前已备份文件数</param>
        /// <param name="total">总文件数</param>
        /// <param name="currentFile">当前正在备份的文件名</param>
        public void UpdateProgress(int current, int total, string currentFile)
        {
            // 只有进度变化超过1%或是最后一个文件时才更新UI，减少UI更新频率
            double progressPercentage = total > 0 ? (double)current / total * 100 : 0;
            int progressPercentageInt = (int)progressPercentage;
            
            lock (_updateLock)
            {
                // 如果进度变化小于1%且不是最后一个文件，则跳过更新
                if (progressPercentageInt <= _lastReportedProgress && current < total)
                {
                    return;
                }
                
                _lastReportedProgress = progressPercentageInt;
            }
            
            if (DispatcherQueue != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ProgressText.Text = $"{current}/{total} 文件已备份";
                    StatusText.Text = $"正在备份: {currentFile}";
                    
                    // 如果总文件数大于0，使用确定进度
                    if (total > 0)
                    {
                        BackupProgressBar.IsIndeterminate = false;
                        BackupProgressBar.Value = progressPercentage;
                    }
                });
            }
        }

        /// <summary>
        /// 设置为不确定进度模式
        /// </summary>
        /// <param name="status">状态文本</param>
        public void SetIndeterminate(string status)
        {
            if (DispatcherQueue != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    StatusText.Text = status;
                    BackupProgressBar.IsIndeterminate = true;
                    ProgressText.Text = "正在处理...";
                });
            }
        }

        /// <summary>
        /// 显示备份完成状态
        /// </summary>
        /// <param name="totalFiles">总备份文件数</param>
        public void ShowCompletion(int totalFiles)
        {
            if (DispatcherQueue != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    StatusText.Text = "备份完成";
                    BackupProgressBar.IsIndeterminate = false;
                    BackupProgressBar.Value = 100;
                    ProgressText.Text = $"共备份 {totalFiles} 个文件";
                    
                    // 设置关闭按钮
                    CloseButtonText = "确定";
                });
            }
        }

        /// <summary>
        /// 异步显示对话框并等待关闭
        /// </summary>
        /// <returns>等待任务</returns>
        public new async Task<ContentDialogResult> ShowAsync()
        {
            return await base.ShowAsync();
        }
    }
}