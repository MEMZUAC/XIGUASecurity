using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Text;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.System;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace Code_Editor
{
    public sealed partial class CodeEditorPage : Page
    {
        private SyntaxHighlighter _syntaxHighlighter;
        private bool _isUpdating = false;
        private StorageFile _currentFile;
        
        public CodeEditorPage()
        {
            this.InitializeComponent();
            _syntaxHighlighter = new SyntaxHighlighter();
            Loaded += CodeEditorPage_Loaded;
        }
        
        private void CodeEditorPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始化界面
            CodeRichEditBox.Document.SetText(TextSetOptions.None, "");
            UpdateLineNumbers();
            UpdateStatus();
            
            // 绑定按钮事件
            BindButtonEvents();
        }
        
        private void BindButtonEvents()
        {
            // 查找工具栏中的按钮并绑定事件
            var appBar = this.FindName("TopAppBar") as AppBar;
            if (appBar != null)
            {
                // 遍历AppBar中的按钮
                var panel = appBar.Content as Panel;
                if (panel != null)
                {
                    foreach (var child in panel.Children)
                    {
                        if (child is StackPanel stackPanel)
                        {
                            foreach (var item in stackPanel.Children)
                            {
                                if (item is AppBarButton button)
                                {
                                    switch (button.Label)
                                    {
                                        case "新建":
                                            button.Click += NewFile_Click;
                                            break;
                                        case "打开":
                                            button.Click += OpenFile_Click;
                                            break;
                                        case "保存":
                                            button.Click += SaveFile_Click;
                                            break;
                                        case "另存为":
                                            button.Click += SaveAsFile_Click;
                                            break;
                                        case "撤销":
                                            button.Click += Undo_Click;
                                            break;
                                        case "重做":
                                            button.Click += Redo_Click;
                                            break;
                                        case "复制":
                                            button.Click += Copy_Click;
                                            break;
                                        case "剪切":
                                            button.Click += Cut_Click;
                                            break;
                                        case "粘贴":
                                            button.Click += Paste_Click;
                                            break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        private async void NewFile_Click(object sender, RoutedEventArgs e)
        {
            // 检查当前文件是否有未保存的更改
            if (!await CheckForUnsavedChanges())
                return;
                
            // 创建新文件
            _currentFile = null;
            CodeRichEditBox.Document.SetText(TextSetOptions.None, "");
            FilePathTextBox.Text = "未打开文件";
            UpdateLineNumbers();
            UpdateStatus();
            StatusText.Text = "已创建新文件";
        }
        
        private async void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            // 检查当前文件是否有未保存的更改
            if (!await CheckForUnsavedChanges())
                return;
                
            // 打开文件选择器
            var filePicker = new FileOpenPicker();
            filePicker.ViewMode = PickerViewMode.List;
            filePicker.FileTypeFilter.Add(".cs");
            filePicker.FileTypeFilter.Add(".js");
            filePicker.FileTypeFilter.Add(".py");
            filePicker.FileTypeFilter.Add(".xml");
            filePicker.FileTypeFilter.Add(".json");
            filePicker.FileTypeFilter.Add(".txt");
            filePicker.FileTypeFilter.Add("*");
            
            // 获取窗口句柄
            var window = this.XamlRoot.Content as FrameworkElement;
            if (window != null)
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hWnd);
            }
            
            var file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    // 读取文件内容
                    var content = await FileIO.ReadTextAsync(file);
                    CodeRichEditBox.Document.SetText(TextSetOptions.None, content);
                    
                    // 更新当前文件和UI
                    _currentFile = file;
                    FilePathTextBox.Text = file.Path;
                    
                    // 根据文件扩展名设置语言
                    SetLanguageByFileExtension(file.FileType);
                    
                    UpdateLineNumbers();
                    UpdateStatus();
                    StatusText.Text = $"已打开文件: {file.Name}";
                    
                    // 应用语法高亮
                    var language = GetSelectedLanguage();
                    await _syntaxHighlighter.ApplyHighlightingAsync(CodeRichEditBox, language);
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"打开文件失败: {ex.Message}";
                }
            }
        }
        
        private async void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFile != null)
            {
                await SaveFile(_currentFile);
            }
            else
            {
                // 如果当前没有文件，则执行另存为操作
                await SaveAsFile();
            }
        }
        
        private async void SaveAsFile_Click(object sender, RoutedEventArgs e)
        {
            await SaveAsFile();
        }
        
        private async Task SaveAsFile()
        {
            var filePicker = new FileSavePicker();
            filePicker.SuggestedFileName = "未命名文件";
            
            // 根据当前选择的语言设置默认文件扩展名
            var language = GetSelectedLanguage();
            switch (language)
            {
                case "C#":
                    filePicker.FileTypeChoices.Add("C# 文件", new List<string> { ".cs" });
                    break;
                case "JavaScript":
                    filePicker.FileTypeChoices.Add("JavaScript 文件", new List<string> { ".js" });
                    break;
                case "Python":
                    filePicker.FileTypeChoices.Add("Python 文件", new List<string> { ".py" });
                    break;
                case "XML":
                    filePicker.FileTypeChoices.Add("XML 文件", new List<string> { ".xml" });
                    break;
                case "JSON":
                    filePicker.FileTypeChoices.Add("JSON 文件", new List<string> { ".json" });
                    break;
                default:
                    filePicker.FileTypeChoices.Add("文本文件", new List<string> { ".txt" });
                    break;
            }
            filePicker.FileTypeChoices.Add("所有文件", new List<string> { "." });
            
            // 获取窗口句柄
            var window = this.XamlRoot.Content as FrameworkElement;
            if (window != null)
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hWnd);
            }
            
            var file = await filePicker.PickSaveFileAsync();
            if (file != null)
            {
                await SaveFile(file);
            }
        }
        
        private async Task SaveFile(StorageFile file)
        {
            try
            {
                // 获取文档内容
                string content;
                CodeRichEditBox.Document.GetText(TextGetOptions.None, out content);
                
                // 保存到文件
                await FileIO.WriteTextAsync(file, content);
                
                // 更新当前文件和UI
                _currentFile = file;
                FilePathTextBox.Text = file.Path;
                StatusText.Text = $"已保存文件: {file.Name}";
                
                // 根据文件扩展名设置语言
                SetLanguageByFileExtension(file.FileType);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"保存文件失败: {ex.Message}";
            }
        }
        
        private void SetLanguageByFileExtension(string extension)
        {
            switch (extension.ToLower())
            {
                case ".cs":
                    LanguageComboBox.SelectedIndex = 0; // C#
                    break;
                case ".js":
                    LanguageComboBox.SelectedIndex = 1; // JavaScript
                    break;
                case ".py":
                    LanguageComboBox.SelectedIndex = 2; // Python
                    break;
                case ".xml":
                    LanguageComboBox.SelectedIndex = 3; // XML
                    break;
                case ".json":
                    LanguageComboBox.SelectedIndex = 4; // JSON
                    break;
                default:
                    LanguageComboBox.SelectedIndex = 5; // 文本
                    break;
            }
        }
        
        private async Task<bool> CheckForUnsavedChanges()
        {
            // 检查当前文件是否有未保存的更改
            if (_currentFile != null)
            {
                try
                {
                    // 获取当前编辑器内容
                    string currentContent;
                    CodeRichEditBox.Document.GetText(TextGetOptions.None, out currentContent);
                    
                    // 获取文件原始内容
                    var originalContent = await FileIO.ReadTextAsync(_currentFile);
                    
                    // 比较内容
                    if (currentContent != originalContent)
                    {
                        // 显示确认对话框
                        var dialog = new ContentDialog
                        {
                            Title = "未保存的更改",
                            Content = "当前文件有未保存的更改，是否要保存？",
                            PrimaryButtonText = "保存",
                            SecondaryButtonText = "不保存",
                            CloseButtonText = "取消",
                            XamlRoot = this.XamlRoot
                        };
                        
                        var result = await dialog.ShowAsync();
                        
                        if (result == ContentDialogResult.Primary)
                        {
                            // 保存文件
                            await SaveFile(_currentFile);
                            return true;
                        }
                        else if (result == ContentDialogResult.Secondary)
                        {
                            // 不保存，继续操作
                            return true;
                        }
                        else
                        {
                            // 取消操作
                            return false;
                        }
                    }
                }
                catch (Exception)
                {
                    // 如果无法读取文件，假设有未保存的更改
                    var dialog = new ContentDialog
                    {
                        Title = "未保存的更改",
                        Content = "当前文件可能有未保存的更改，是否要保存？",
                        PrimaryButtonText = "保存",
                        SecondaryButtonText = "不保存",
                        CloseButtonText = "取消",
                        XamlRoot = this.XamlRoot
                    };
                    
                    var result = await dialog.ShowAsync();
                    
                    if (result == ContentDialogResult.Primary)
                    {
                        await SaveAsFile();
                        return true;
                    }
                    else if (result == ContentDialogResult.Secondary)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            CodeRichEditBox.Document.Undo();
            StatusText.Text = "已撤销";
        }
        
        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            CodeRichEditBox.Document.Redo();
            StatusText.Text = "已重做";
        }
        
        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            CodeRichEditBox.Document.Selection.Copy();
            StatusText.Text = "已复制";
        }
        
        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            CodeRichEditBox.Document.Selection.Cut();
            StatusText.Text = "已剪切";
        }
        
        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            CodeRichEditBox.Document.Selection.Paste(0);
            StatusText.Text = "已粘贴";
        }
        
        private async void CodeRichEditBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;
            
            // 获取文本内容
            string text;
            CodeRichEditBox.Document.GetText(TextGetOptions.None, out text);
            
            UpdateLineNumbers();
            UpdateStatus();
            
            // 应用语法高亮
            var language = GetSelectedLanguage();
            await _syntaxHighlighter.ApplyHighlightingAsync(CodeRichEditBox, language);
        }
        
        private void CodeRichEditBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateLineColumnStatus();
        }
        
        private void CodeRichEditBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // 处理快捷键
            if (e.Key == VirtualKey.Tab)
            {
                // 插入缩进而不是切换焦点
                e.Handled = true;
                
                // 获取当前选择
                CodeRichEditBox.Document.Selection.GetText(TextGetOptions.None, out string selectedText);
                
                // 替换为缩进
                var indent = new string(' ', 4); // 4个空格作为缩进
                CodeRichEditBox.Document.Selection.SetText(TextSetOptions.None, indent);
            }
        }
        
        private string GetSelectedLanguage()
        {
            var selectedItem = LanguageComboBox.SelectedItem as ComboBoxItem;
            return selectedItem?.Content.ToString() ?? "文本";
        }
        
        private void UpdateLineNumbers()
        {
            // 获取文本内容
            string text;
            CodeRichEditBox.Document.GetText(TextGetOptions.None, out text);
            
            var lines = text.Split('\n');
            
            // 清除现有内容
            LineNumbersText.Blocks.Clear();
            
            // 添加行号
            for (int i = 1; i <= lines.Length; i++)
            {
                var paragraph = new Microsoft.UI.Xaml.Documents.Paragraph();
                var run = new Microsoft.UI.Xaml.Documents.Run();
                run.Text = i.ToString();
                paragraph.Inlines.Add(run);
                LineNumbersText.Blocks.Add(paragraph);
            }
        }
        
        private void UpdateStatus()
        {
            // 获取文本内容
            string text;
            CodeRichEditBox.Document.GetText(TextGetOptions.None, out text);
            
            LengthText.Text = $"{text.Length} 字符";
            
            if (string.IsNullOrEmpty(FilePathTextBox.Text) || FilePathTextBox.Text == "未打开文件")
            {
                StatusText.Text = "新文件";
            }
            else
            {
                StatusText.Text = "已打开文件";
            }
        }
        
        private void UpdateLineColumnStatus()
        {
            var selection = CodeRichEditBox.Document.Selection;
            int position = selection.StartPosition;
            
            // 获取文本内容
            string text;
            CodeRichEditBox.Document.GetText(TextGetOptions.None, out text);
            
            // 计算行号和列号
            int line = 1;
            int column = 1;
            
            for (int i = 0; i < position && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }
            
            LineColumnText.Text = $"行 {line}, 列 {column}";
        }
    }
}