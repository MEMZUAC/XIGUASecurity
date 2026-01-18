using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.System;

namespace XIGUASecurity
{
    public sealed partial class BugReportPage : Page
    {
        private FeedbackTCPClient? _tcpClient;
        private readonly Dictionary<string, string> _userAvatars = [];
        private string _currentUsername = "";
        private readonly Dictionary<string, ProgressBar> _uploadProgressBars = [];
        private readonly Dictionary<string, TextBlock> _uploadStatusTexts = [];
        private readonly Dictionary<string, ProgressBar> _downloadProgressBars = [];
        private readonly Dictionary<string, TextBlock> _downloadProgressTexts = [];
        private readonly Dictionary<string, TextBlock> _downloadStatusTexts = [];
        private DispatcherTimer? _refreshTimer; // 添加定时器用于自动刷新消息
        private bool _isAutoRefresh = false; // 标记是否是自动刷新操作
        private DateTime _lastServerMessageTime = DateTime.Now; // 记录最后一次收到服务器消息的时间
        private readonly Queue<string> _pendingMessages = []; // 待发送的消息队列


        public BugReportPage()
        {
            InitializeComponent();
            Loaded += async (_, __) =>
            {
                try
                {
                    await InitializeTCPClientAsync();
                    InitializeRefreshTimer();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"初始化TCP客户端失败: {ex.Message}");
                    AddSystemMessage($"初始化失败: {ex.Message}");
                }
            };
            Unloaded += (_, __) => Cleanup();
        }

        #region TCP客户端
        private async Task InitializeTCPClientAsync()
        {
            Cleanup();
            _tcpClient = new FeedbackTCPClient();

            // 使用系统账户名作为用户名
            string systemUsername = Environment.UserName;
            if (_tcpClient != null && (string.IsNullOrEmpty(_tcpClient.Username) || _tcpClient.Username != systemUsername))
            {
                try
                {
                    await _tcpClient.SetUsernameAsync(systemUsername);
                }
                catch (Exception ex)
                {
                    AddSystemMessage($"设置用户名失败: {ex.Message}");
                    StatusTxt.Text = WinUI3Localizer.Localizer.Get().GetLocalizedString("BugReportPage_NotConnected");
                    return;
                }
            }

            // 订阅事件
            if (_tcpClient != null)
            {
                _tcpClient.OnConnected += (sender, message) => 
                {
                    // 自动刷新时不显示连接状态
                    // 完全不处理，避免UI显示任何状态变化
                };

                _tcpClient.OnDisconnected += (sender, message) =>
                {
                    // 自动刷新时不显示断开连接状态
                // 完全不处理，避免UI显示任何状态变化
            };

                _tcpClient.OnMessageReceived += async (sender, messageDict) =>
                {
                    await HandleReceivedMessageAsync(messageDict);
                };

                _tcpClient.OnError += async (sender, error) =>
                {
                    // 只有在非自动刷新时才显示连接错误
                    if (!_isAutoRefresh)
                    {
                        // 检查是否是解码错误
                        if (error.Contains("解码消息异常"))
                        {
                            // 解码错误已经在TCP客户端中处理，这里不显示
                            return;
                        }
                    
                    // 检查是否是正常的连接关闭消息
                    if (error.Contains("线程退出") || error.Contains("应用程序请求") || error.Contains("已中止 I/O 操作"))
                    {
                        // 正常的连接关闭，不显示错误
                        return;
                    }
                    
                    // 检查是否是重连失败消息
                    if (error.Contains("未连接到服务器，重连失败"))
                    {
                        // 重连失败消息，显示但不更新状态
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            AddSystemMessage("连接已断开，正在尝试重新连接...");
                        });
                        return;
                    }
                    
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusTxt.Text = WinUI3Localizer.Localizer.Get().GetLocalizedString("BugReportPage_ConnectionFailed");
                        AddSystemMessage(WinUI3Localizer.Localizer.Get().GetLocalizedString("BugReportPage_ConnectionFailedMessage") + error);
                    });
                    
                    // 如果是连接错误，等待600毫秒后尝试重连
                    if (error.Contains("连接") || error.Contains("断开"))
                    {
                        await Task.Delay(600);
                        // 不自动重连，让用户手动点击连接按钮
                    }
                }
                };
            }

            // 尝试连接
            if (_tcpClient != null)
            {
                await _tcpClient.ConnectAsync();
            }
        }

        private async Task HandleReceivedMessageAsync(Dictionary<string, object> messageDict)
        {
            try
            {
                if (messageDict == null)
                {
                    System.Diagnostics.Debug.WriteLine("收到空消息，忽略");
                    return;
                }

                if (!messageDict.TryGetValue("type", out var typeObj))
                {
                    System.Diagnostics.Debug.WriteLine("消息中没有type字段，忽略");
                    return;
                }

                string type = typeObj?.ToString() ?? "";
                if (string.IsNullOrEmpty(type))
                {
                    System.Diagnostics.Debug.WriteLine("消息type为空，忽略");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"收到消息类型: {type}");

            // 更新最后收到服务器消息的时间
            _lastServerMessageTime = DateTime.Now;

            DispatcherQueue.TryEnqueue(() =>
            {
                System.Diagnostics.Debug.WriteLine($"收到消息类型: {type}, 内容: {System.Text.Json.JsonSerializer.Serialize(messageDict)}");
                switch (type)
                {
                    case "register_success":
                        System.Diagnostics.Debug.WriteLine("处理register_success消息");
                        HandleRegisterSuccess(messageDict);
                        break;

                    case "new_message":
                        HandleNewMessage(messageDict);
                        break;

                    case "user_online":
                        HandleUserOnline(messageDict);
                        break;

                    case "user_offline":
                        HandleUserOffline(messageDict);
                        break;

                    case "file":
                        HandleFileMessage(messageDict);
                        break;

                    case "file_download_url":
                        System.Diagnostics.Debug.WriteLine("收到file_download_url消息，准备处理");
                        HandleFileDownloadUrl(messageDict);
                        break;

                    case "file_download_start":
                        HandleFileDownloadStart(messageDict);
                        break;

                    case "file_chunk":
                        HandleFileChunk(messageDict);
                        break;

                    case "file_download_complete":
                        HandleFileDownloadComplete(messageDict);
                        break;

                    case "read_status_update":
                        HandleReadStatusUpdate(messageDict);
                        break;

                    case "system_message":
                        HandleSystemMessage(messageDict);
                        break;

                    case "refresh_trigger":
                        // 处理服务端发送的刷新触发消息
                        HandleRefreshTrigger();
                        break;

                    case "error":
                        HandleErrorMessage(messageDict);
                        break;

                    default:
                        System.Diagnostics.Debug.WriteLine($"未知消息类型: {type}");
                        break;
                }
            });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理接收到的消息时出错: {ex.Message}");
                // 不向用户显示错误，避免干扰用户体验
            }
        }

        private void AddFileUploadMessage(string fileName, long fileSize, string username, string messageId)
        {
            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => AddFileUploadMessage(fileName, fileSize, username, messageId));
                return;
            }

            // 创建消息容器
            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // 添加头像
            var avatar = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 8)
            };

            // 在实际应用中，这里可以加载头像图片
            // 由于是示例，我们使用首字母作为头像
            var avatarText = new TextBlock
            {
                Text = username.Length > 0 ? username[0].ToString().ToUpper() : "?",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 16
            };

            avatar.Child = avatarText;

            // 创建文件上传卡片
            var card = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8),
                MaxWidth = 300,
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ControlElevationBorderBrush"],
                BorderThickness = new Thickness(1)
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 文件信息行
            var topGrid = new Grid { ColumnSpacing = 8 };
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var icon = new FontIcon
            {
                Glyph = "\uE8A5",
                FontSize = 32,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            var tbName = new TextBlock
            {
                Text = fileName,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 14
            };
            var tbSize = new TextBlock
            {
                Text = $"{fileSize / 1024.0 / 1024.0:F2} MB",
                FontSize = 12,
                Opacity = 0.7
            };
            stack.Children.Add(tbName);
            stack.Children.Add(tbSize);

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(stack, 1);
            topGrid.Children.Add(icon);
            topGrid.Children.Add(stack);

            // 进度条
            var progressBar = new ProgressBar
            {
                MinHeight = 4,
                Margin = new Thickness(0, 8, 0, 0)
            };

            // 状态文本
            var statusText = new TextBlock
            {
                Text = "正在上传...",
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            };

            Grid.SetRow(topGrid, 0);
            Grid.SetRow(progressBar, 1);
            Grid.SetRow(statusText, 2);
            rootGrid.Children.Add(topGrid);
            rootGrid.Children.Add(progressBar);
            rootGrid.Children.Add(statusText);

            card.Child = rootGrid;

            // 自己发送的消息：先添加消息卡片，再添加头像（头像在右边）
            container.Children.Add(card);
            container.Children.Add(avatar);

            // 存储消息ID和进度条、状态文本的引用，以便后续更新
            _uploadProgressBars[messageId] = progressBar;
            _uploadStatusTexts[messageId] = statusText;

            MessagesPanel.Children.Add(container);
            ScrollToBottom();
        }

        private void UpdateFileUploadProgress(string messageId, object progress)
        {
            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => UpdateFileUploadProgress(messageId, progress));
                return;
            }

            if (_uploadProgressBars.TryGetValue(messageId, out var progressBar) &&
                _uploadStatusTexts.TryGetValue(messageId, out var statusText))
            {
                if (progress is int percentage)
                {
                    progressBar.Value = percentage;
                    statusText.Text = $"正在上传... {percentage}%";
                }
                else if (progress is string status)
                {
                    if (status == "上传完成")
                    {
                        progressBar.Value = 100;
                        statusText.Text = status;
                    }
                    else
                    {
                        statusText.Text = status;
                    }
                }
            }
        }

        private void AddFileDownloadMessage(string fileName, long fileSize, string username, string messageId)
        {
            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => AddFileDownloadMessage(fileName, fileSize, username, messageId));
                return;
            }

            // 创建消息容器
            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // 添加头像
            var avatar = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 8)
            };

            // 在实际应用中，这里可以加载头像图片
            // 由于是示例，我们使用首字母作为头像
            var avatarText = new TextBlock
            {
                Text = username.Length > 0 ? username[0].ToString().ToUpper() : "?",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 16
            };

            avatar.Child = avatarText;

            // 创建文件下载卡片
            var card = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8),
                MaxWidth = 300,
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ControlElevationBorderBrush"],
                BorderThickness = new Thickness(1)
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 文件信息行
            var topGrid = new Grid { ColumnSpacing = 8 };
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var icon = new FontIcon
            {
                Glyph = "\uE8A5",
                FontSize = 32,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            var tbName = new TextBlock
            {
                Text = fileName,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 14
            };
            var tbSize = new TextBlock
            {
                Text = $"{fileSize / 1024.0 / 1024.0:F2} MB",
                FontSize = 12,
                Opacity = 0.7
            };
            stack.Children.Add(tbName);
            stack.Children.Add(tbSize);

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(stack, 1);
            topGrid.Children.Add(icon);
            topGrid.Children.Add(stack);

            // 进度条
            var progressBar = new ProgressBar
            {
                MinHeight = 4,
                Margin = new Thickness(0, 8, 0, 0)
            };

            // 状态文本
            var statusText = new TextBlock
            {
                Text = "正在下载...",
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            };

            Grid.SetRow(topGrid, 0);
            Grid.SetRow(progressBar, 1);
            Grid.SetRow(statusText, 2);
            rootGrid.Children.Add(topGrid);
            rootGrid.Children.Add(progressBar);
            rootGrid.Children.Add(statusText);

            card.Child = rootGrid;

            // 他人发送的消息：先添加头像，再添加消息卡片（头像在左边）
            container.Children.Add(avatar);
            container.Children.Add(card);

            // 存储消息ID和进度条、状态文本的引用，以便后续更新
            _downloadProgressBars[messageId] = progressBar;
            _downloadStatusTexts[messageId] = statusText;

            MessagesPanel.Children.Add(container);
            ScrollToBottom();
        }

        private void UpdateFileDownloadProgress(string messageId, object progress)
        {
            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => UpdateFileDownloadProgress(messageId, progress));
                return;
            }

            if (_downloadProgressBars.TryGetValue(messageId, out var progressBar) &&
                _downloadProgressTexts.TryGetValue(messageId, out var progressText))
            {
                if (progress is int percentage)
                {
                    progressBar.Value = percentage;
                    progressText.Text = $"正在下载... {percentage}%";
                }
                else if (progress is string status)
                {
                    if (status == "下载完成")
                    {
                        progressBar.Value = 100;
                        progressText.Text = status;

                        // 找到对应的下载按钮并启用它
                        foreach (var child in MessagesPanel.Children)
                        {
                            if (child is StackPanel container)
                            {
                                foreach (var element in container.Children)
                                {
                                    if (element is Border card && card.Child is Grid grid)
                                    {
                                        // 查找下载按钮
                                        foreach (var gridChild in grid.Children)
                                        {
                                            if (gridChild is Button btn && btn.Tag is string fileId && fileId == messageId)
                                            {
                                                btn.IsEnabled = true;
                                                btn.Content = "已完成";
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        progressText.Text = status;
                    }
                }
            }
        }

        private void HandleRegisterSuccess(Dictionary<string, object> messageDict)
        {
            System.Diagnostics.Debug.WriteLine($"HandleRegisterSuccess被调用，消息内容: {System.Text.Json.JsonSerializer.Serialize(messageDict)}");

            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => HandleRegisterSuccess(messageDict));
                return;
            }

            try
            {
                StatusTxt.Text = WinUI3Localizer.Localizer.Get().GetLocalizedString("BugReportPage_Connected");
                AddSystemMessage("已加入反馈频道");

                // 获取用户信息
                if (messageDict.TryGetValue("user", out var userObj) &&
                    userObj is JsonElement userElement)
                {
                    if (userElement.TryGetProperty("username", out var usernameElement))
                    {
                        _currentUsername = usernameElement.GetString() ?? "";
                    }

                    if (userElement.TryGetProperty("avatar", out var avatarElement))
                    {
                        string avatar = avatarElement.GetString() ?? "";
                        if (!string.IsNullOrEmpty(avatar) && !string.IsNullOrEmpty(_currentUsername))
                        {
                            _userAvatars[_currentUsername] = avatar;
                        }
                    }
                }

                // 显示历史消息
                if (messageDict.TryGetValue("recent_messages", out var messagesObj) &&
                    messagesObj is JsonElement messagesElement &&
                    messagesElement.ValueKind == JsonValueKind.Array)
                {
                    System.Diagnostics.Debug.WriteLine($"收到 {messagesElement.GetArrayLength()} 条历史消息");

                    // 先清空当前消息显示
                    MessagesPanel.Children.Clear();

                    // 处理历史消息
                    foreach (var msgElement in messagesElement.EnumerateArray())
                    {
                        try
                        {
                            var msgDict = JsonSerializer.Deserialize<Dictionary<string, object>>(msgElement.GetRawText());
                            if (msgDict != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"处理历史消息: {System.Text.Json.JsonSerializer.Serialize(msgDict)}");

                                // 检查消息类型
                                if (msgDict.TryGetValue("type", out var typeObj))
                                {
                                    System.Diagnostics.Debug.WriteLine($"历史消息类型: {typeObj}");
                                }

                                HandleNewMessage(msgDict, isHistory: true);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"处理历史消息失败: {ex.Message}");
                        }
                    }

                    // 滚动到底部
                    ScrollToBottom();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("没有收到历史消息");

                    // 添加系统消息提示
                    AddSystemMessage("已连接到服务器，但没有历史消息");
                }
                
                // 处理待发送的消息队列
                if (_pendingMessages.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"连接成功，处理 {_pendingMessages.Count} 条待发送消息");
                    _ = Task.Run(async () =>
                    {
                        // 等待一小段时间确保连接完全稳定
                        await Task.Delay(500);
                        await ProcessPendingMessagesAsync();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HandleRegisterSuccess处理失败: {ex.Message}");
                AddSystemMessage($"处理注册成功消息失败: {ex.Message}");
            }
        }

        private void HandleNewMessage(Dictionary<string, object> messageDict, bool isHistory = false)
        {
            System.Diagnostics.Debug.WriteLine($"HandleNewMessage被调用，isHistory={isHistory}, 消息内容: {System.Text.Json.JsonSerializer.Serialize(messageDict)}");

            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => HandleNewMessage(messageDict, isHistory));
                return;
            }

            try
            {
                if (!messageDict.TryGetValue("username", out var usernameObj) ||
                    !messageDict.TryGetValue("type", out var typeObj))
                    return;

                string username = usernameObj.ToString() ?? "";
                string type = typeObj.ToString() ?? "";

                // 根据消息类型处理
                if (type == "file")
                {
                    // 处理文件消息
                    if (!messageDict.TryGetValue("name", out var nameObj) ||
                        !messageDict.TryGetValue("size", out var sizeObj))
                        return;

                    string fileName = nameObj.ToString() ?? "";
                    long fileSize = long.TryParse(sizeObj.ToString(), out long size) ? size : 0;

                    // 获取用户信息
                    if (messageDict.TryGetValue("user_info", out var fileUserInfoObj) &&
                        fileUserInfoObj is JsonElement fileUserInfoElement)
                    {
                        if (fileUserInfoElement.TryGetProperty("username", out var infoUsernameElement) &&
                            fileUserInfoElement.TryGetProperty("avatar", out var avatarElement))
                        {
                            string infoUsername = infoUsernameElement.GetString() ?? "";
                            string avatar = avatarElement.GetString() ?? "";

                            if (!string.IsNullOrEmpty(infoUsername) && !string.IsNullOrEmpty(avatar))
                            {
                                _userAvatars[infoUsername] = avatar;
                            }
                        }
                    }

                    // 获取消息ID
                    string fileId = "";
                    if (messageDict.TryGetValue("id", out var fileIdObj))
                    {
                        fileId = fileIdObj.ToString() ?? "";
                    }

                    // 判断是否是自己发送的文件
                    bool fileIsMe = username == _currentUsername;

                    // 显示文件消息
                    AddFileMessage(fileName, fileSize, username, fileIsMe, fileId);
                    return;
                }
                else if (type == "file_download_url")
                {
                    // 处理文件下载链接消息
                    if (!messageDict.TryGetValue("name", out var nameObj) ||
                        !messageDict.TryGetValue("size", out var sizeObj) ||
                        !messageDict.TryGetValue("file_id", out var fileIdObj))
                        return;

                    string fileName = nameObj.ToString() ?? "";
                    long fileSize = long.TryParse(sizeObj.ToString(), out long size) ? size : 0;
                    string fileId = fileIdObj.ToString() ?? "";

                    // 获取下载URL
                    string downloadUrl = "";
                    if (messageDict.TryGetValue("url", out var urlObj))
                    {
                        downloadUrl = urlObj.ToString() ?? "";
                    }

                    // 获取用户信息
                    if (messageDict.TryGetValue("user_info", out var fileUserInfoObj) &&
                        fileUserInfoObj is JsonElement fileUserInfoElement)
                    {
                        if (fileUserInfoElement.TryGetProperty("username", out var infoUsernameElement) &&
                            fileUserInfoElement.TryGetProperty("avatar", out var avatarElement))
                        {
                            string infoUsername = infoUsernameElement.GetString() ?? "";
                            string avatar = avatarElement.GetString() ?? "";

                            if (!string.IsNullOrEmpty(infoUsername) && !string.IsNullOrEmpty(avatar))
                            {
                                _userAvatars[infoUsername] = avatar;
                            }
                        }
                    }

                    // 判断是否是自己发送的文件
                    bool fileIsMe = username == _currentUsername;

                    // 显示文件消息
                    AddFileMessage(fileName, fileSize, username, fileIsMe, fileId, downloadUrl);
                    return;
                }

                // 处理普通文本消息
                if (!messageDict.TryGetValue("content", out var contentObj))
                    return;

                string content = contentObj.ToString() ?? "";

                // 获取用户信息
                if (messageDict.TryGetValue("user_info", out var textUserInfoObj) &&
                    textUserInfoObj is JsonElement textUserInfoElement)
                {
                    if (textUserInfoElement.TryGetProperty("username", out var infoUsernameElement) &&
                        textUserInfoElement.TryGetProperty("avatar", out var avatarElement))
                    {
                        string infoUsername = infoUsernameElement.GetString() ?? "";
                        string avatar = avatarElement.GetString() ?? "";

                        if (!string.IsNullOrEmpty(infoUsername) && !string.IsNullOrEmpty(avatar))
                        {
                            _userAvatars[infoUsername] = avatar;
                        }
                    }
                }

                // 判断是否是自己发送的消息
                bool textIsMe = username == _currentUsername;

                // 如果是自己发送的消息且不是历史消息，则不显示（因为已经在发送时显示了）
                if (textIsMe && !isHistory)
                {
                    System.Diagnostics.Debug.WriteLine($"跳过显示自己发送的消息: {content}");
                    return;
                }

                // 获取已读状态
                int readByCount = 0;
                int totalUsers = 0;

                if (messageDict.TryGetValue("read_by_count", out var readByObj) &&
                    int.TryParse(readByObj.ToString(), out int readCount))
                {
                    readByCount = readCount;
                }

                if (messageDict.TryGetValue("total_users", out var totalUsersObj) &&
                    int.TryParse(totalUsersObj.ToString(), out int totalCount))
                {
                    totalUsers = totalCount;
                }

                // 添加消息到UI
                System.Diagnostics.Debug.WriteLine($"准备添加消息到UI: username={username}, isMe={textIsMe}, isHistory={isHistory}");
                AddMessageWithUser(content, username, textIsMe, isHistory, readByCount, totalUsers);

                // 标记消息为已读
                if (!isHistory && messageDict.TryGetValue("id", out var idObj))
                {
                    string messageId = idObj.ToString() ?? "";
                    if (!string.IsNullOrEmpty(messageId))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _tcpClient?.MarkMessageReadAsync(messageId)!;
                            }
                            catch { }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HandleNewMessage处理失败: {ex.Message}");
            }
        }

        private void HandleUserOnline(Dictionary<string, object> messageDict)
        {
            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => HandleUserOnline(messageDict));
                return;
            }

            if (!messageDict.TryGetValue("username", out var usernameObj))
                return;

            string username = usernameObj.ToString() ?? "";
            AddSystemMessage($"{username} 已上线");
        }

        private void HandleUserOffline(Dictionary<string, object> messageDict)
        {
            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => HandleUserOffline(messageDict));
                return;
            }

            if (!messageDict.TryGetValue("username", out var usernameObj))
                return;

            string username = usernameObj.ToString() ?? "";
            AddSystemMessage($"{username} 已下线");
        }

        private void HandleFileMessage(Dictionary<string, object> messageDict)
        {
            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => HandleFileMessage(messageDict));
                return;
            }

            if (!messageDict.TryGetValue("id", out var idObj) ||
                !messageDict.TryGetValue("name", out var nameObj) ||
                !messageDict.TryGetValue("size", out var sizeObj) ||
                !messageDict.TryGetValue("username", out var usernameObj))
                return;

            string fileId = idObj.ToString() ?? "";
            string fileName = nameObj.ToString() ?? "";
            long fileSize = long.TryParse(sizeObj.ToString(), out long size) ? size : 0;
            string username = usernameObj.ToString() ?? "";

            // 获取用户信息
            if (messageDict.TryGetValue("user_info", out var userInfoObj) &&
                userInfoObj is JsonElement userInfoElement)
            {
                if (userInfoElement.TryGetProperty("username", out var infoUsernameElement) &&
                    userInfoElement.TryGetProperty("avatar", out var avatarElement))
                {
                    string infoUsername = infoUsernameElement.GetString() ?? "";
                    string avatar = avatarElement.GetString() ?? "";

                    if (!string.IsNullOrEmpty(infoUsername) && !string.IsNullOrEmpty(avatar))
                    {
                        _userAvatars[infoUsername] = avatar;
                    }
                }
            }

            // 判断是否是自己发送的文件
            bool isMe = username == _currentUsername;

            // 显示文件消息
            AddFileMessage(fileName, fileSize, username, isMe, fileId);
        }

        private void HandleReadStatusUpdate(Dictionary<string, object> messageDict)
        {
            // 在实际应用中，这里可以更新特定消息的已读状态显示
            if (messageDict.TryGetValue("message_id", out var idObj) &&
                messageDict.TryGetValue("read_by_count", out var readCountObj) &&
                messageDict.TryGetValue("total_users", out var totalUsersObj))
            {
                string messageId = idObj.ToString() ?? "";
                int readCount = int.TryParse(readCountObj.ToString(), out int rc) ? rc : 0;
                int totalUsers = int.TryParse(totalUsersObj.ToString(), out int tu) ? tu : 0;

                // 可以在这里更新UI显示已读状态
                // 例如：UpdateMessageReadStatus(messageId, readCount, totalUsers);
            }
        }

        private void HandleSystemMessage(Dictionary<string, object> messageDict)
        {
            if (messageDict.TryGetValue("content", out var contentObj))
            {
                string content = contentObj.ToString() ?? "";
                string sender = "系统";
                
                // 检查是否有发送者信息
                if (messageDict.TryGetValue("sender", out var senderObj))
                {
                    sender = senderObj.ToString() ?? "系统";
                }
                
                // 如果是服务器消息，显示发送者名称
                if (sender == "Server")
                {
                    AddSystemMessage($"[Server] {content}");
                }
                else
                {
                    AddSystemMessage(content);
                }
            }
        }

        private void HandleRefreshTrigger()
        {
            // 处理服务端发送的刷新触发消息
            // 标记为自动刷新操作，避免显示连接状态
            _isAutoRefresh = true;
            
            // 断开并重新连接以获取最新消息
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_tcpClient != null)
                    {
                        await _tcpClient.DisconnectAsync();
                        await Task.Delay(600); // 等待600毫秒
                        await _tcpClient.ConnectAsync();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"刷新触发重连失败: {ex.Message}");
                }
                finally
                {
                    // 重置标志
                    DispatcherQueue.TryEnqueue(() => _isAutoRefresh = false);
                }
            });
            
            // 模拟收到register_success消息来刷新历史消息
            var refreshMessageDict = new Dictionary<string, object>
            {
                ["type"] = "register_success",
                ["users"] = new List<Dictionary<string, object>>(),
                ["messages"] = new List<Dictionary<string, object>>()
            };
            
            // 调用HandleRegisterSuccess来刷新历史消息
            HandleRegisterSuccess(refreshMessageDict);
        }

        private void HandleErrorMessage(Dictionary<string, object> messageDict)
        {
            if (messageDict.TryGetValue("content", out var msgObj))
            {
                string errorMessage = msgObj.ToString() ?? "";
                AddSystemMessage(errorMessage);
            }
            else if (messageDict.TryGetValue("message", out var msgObj2))
            {
                string errorMessage = msgObj2.ToString() ?? "";
                AddSystemMessage($"错误: {errorMessage}");
            }
        }

        private void HandleFileDownloadUrl(Dictionary<string, object> messageDict)
        {
            System.Diagnostics.Debug.WriteLine($"HandleFileDownloadUrl被调用: {System.Text.Json.JsonSerializer.Serialize(messageDict)}");

            if (!messageDict.TryGetValue("file_id", out var fileIdObj) ||
                !messageDict.TryGetValue("name", out var nameObj) ||
                !messageDict.TryGetValue("url", out var urlObj))
            {
                System.Diagnostics.Debug.WriteLine("HandleFileDownloadUrl: 缺少必要的字段");
                return;
            }

            string fileId = fileIdObj.ToString() ?? "";
            string fileName = nameObj.ToString() ?? "";
            string downloadUrl = urlObj.ToString() ?? "";

            System.Diagnostics.Debug.WriteLine($"准备在浏览器中打开下载链接: {fileName}, URL: {downloadUrl}");

            try
            {
                // 直接在浏览器中打开下载链接
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = downloadUrl,
                    UseShellExecute = true
                });

                AddSystemMessage($"已在浏览器中打开下载链接: {fileName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开浏览器异常: {ex.Message}");
                AddSystemMessage($"打开浏览器失败: {ex.Message}");
            }
        }

        private void HandleFileDownloadStart(Dictionary<string, object> messageDict)
        {
            // 这个方法现在不需要做任何事情，因为进度条已经内嵌在文件消息中
            // 下载进度由HandleFileChunk方法更新
            System.Diagnostics.Debug.WriteLine($"文件下载开始: {System.Text.Json.JsonSerializer.Serialize(messageDict)}");
        }

        private void HandleFileChunk(Dictionary<string, object> messageDict)
        {
            System.Diagnostics.Debug.WriteLine($"HandleFileChunk被调用: {System.Text.Json.JsonSerializer.Serialize(messageDict)}");

            if (!messageDict.TryGetValue("file_id", out var fileIdObj) ||
                !messageDict.TryGetValue("chunk_index", out var indexObj) ||
                !messageDict.TryGetValue("total_chunks", out var totalObj) ||
                !messageDict.TryGetValue("content", out var contentObj))
                return;

            string fileId = fileIdObj.ToString() ?? "";
            int chunkIndex = int.TryParse(indexObj.ToString(), out int index) ? index : 0;
            int totalChunks = int.TryParse(totalObj.ToString(), out int total) ? total : 1;
            string base64Content = contentObj.ToString() ?? "";

            System.Diagnostics.Debug.WriteLine($"处理文件块: {fileId}, 块索引: {chunkIndex}/{totalChunks}");

            // 解码文件块
            byte[] chunkData = Convert.FromBase64String(base64Content);

            // 存储文件块
            if (!_fileChunks.TryGetValue(fileId, out var chunks))
            {
                chunks = [];
                _fileChunks[fileId] = chunks;
            }

            // 确保列表大小足够
            while (chunks.Count <= chunkIndex)
            {
                chunks.Add([]);
            }

            // 替换文件块
            chunks[chunkIndex] = chunkData;

            // 计算下载进度
            int progress = (int)(((double)(chunkIndex + 1) / totalChunks) * 100);
            if (progress > 100) progress = 100;

            System.Diagnostics.Debug.WriteLine($"更新下载进度: {fileId}, {progress}%");

            // 更新下载进度
            UpdateFileDownloadProgress(fileId, progress);
        }

        private void HandleFileDownloadComplete(Dictionary<string, object> messageDict)
        {
            if (!messageDict.TryGetValue("file_id", out var fileIdObj))
                return;

            string fileId = fileIdObj.ToString() ?? "";

            // 更新下载进度为完成
            UpdateFileDownloadProgress(fileId, "下载完成");

            // 合并文件块并保存
            MergeAndSaveFile(fileId);
        }

        private readonly Dictionary<string, List<byte[]>> _fileChunks = [];

        private void MergeAndSaveFile(string fileId)
        {
            if (!_fileChunks.TryGetValue(fileId, out var chunks))
                return;

            try
            {
                // 合并所有文件块
                using var fileStream = new MemoryStream();
                foreach (var chunk in chunks)
                {
                    fileStream.Write(chunk, 0, chunk.Length);
                }

                byte[] fileBytes = fileStream.ToArray();

                // 获取文件名（从下载进度消息中获取）
                string fileName = "downloaded_file";
                if (_downloadStatusTexts.TryGetValue(fileId, out var statusTextBlock))
                {
                    // 从父容器中查找文件名
                    var parent = statusTextBlock.Parent as Grid;
                    if (parent != null && parent.Parent is Border border && border.Parent is StackPanel container)
                    {
                        foreach (var child in container.Children)
                        {
                            if (child is StackPanel innerPanel && innerPanel.Children.Count > 0)
                            {
                                var firstChild = innerPanel.Children[0];
                                if (firstChild is TextBlock nameBlock && !string.IsNullOrEmpty(nameBlock.Text))
                                {
                                    fileName = nameBlock.Text;
                                    break;
                                }
                            }
                        }
                    }
                }

                // 保存文件到下载目录
                string downloadsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads");
                Directory.CreateDirectory(downloadsFolder);
                string filePath = Path.Combine(downloadsFolder, fileName);
                File.WriteAllBytes(filePath, fileBytes);

                AddSystemMessage($"文件已下载到: {filePath}");

                // 打开文件所在目录
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                };
                var safePath = filePath.Replace("\"", "\\\"");
                psi.Arguments = $"/select,\"{safePath}\"";
                System.Diagnostics.Process.Start(psi);

                // 清理文件块
                _fileChunks.Remove(fileId);
            }
            catch (Exception ex)
            {
                AddSystemMessage($"保存文件失败: {ex.Message}");
            }
        }
        #endregion

        #region UI消息处理
        private async Task<bool> ShowUsernameDialogAsync()
        {
            // 创建用户名输入对话框
            var dialog = new ContentDialog
            {
                Title = "反馈频道设置",
                Content = "请输入您的用户名以连接到反馈频道",
                CloseButtonText = "取消",
                PrimaryButtonText = "连接",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            // 创建输入框
            var stackPanel = new StackPanel { Spacing = 12 };
            stackPanel.Children.Add(new TextBlock { Text = "用户名:" });
            var usernameBox = new TextBox { PlaceholderText = "请输入用户名" };
            stackPanel.Children.Add(usernameBox);

            dialog.Content = stackPanel;

            // 处理连接按钮点击
            bool result = false;
            dialog.PrimaryButtonClick += async (_, __) =>
            {
                string username = usernameBox.Text.Trim();
                if (string.IsNullOrEmpty(username))
                {
                    usernameBox.PlaceholderText = "用户名不能为空";
                    return;
                }

                try
                {
                    if (_tcpClient != null)
                    {
                        await _tcpClient.SetUsernameAsync(username);
                        result = true;
                    }
                }
                catch (Exception ex)
                {
                    AddSystemMessage($"设置用户名失败: {ex.Message}");
                }
            };

            // 显示对话框
            await dialog.ShowAsync();
            return result;
        }

        private void AddMessageWithUser(string content, string username, bool isMe, bool isHistory = false, int readByCount = 0, int totalUsers = 0)
        {
            System.Diagnostics.Debug.WriteLine($"AddMessageWithUser被调用: content={content}, username={username}, isMe={isMe}, isHistory={isHistory}");

            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => AddMessageWithUser(content, username, isMe, isHistory));
                return;
            }

            // 创建消息容器
            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            // 添加头像
            var avatar = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = (Brush)Application.Current.Resources["SystemControlBackgroundAccentBrush"],
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 8)
            };
            var avatarText = new TextBlock
            {
                Text = username.Length > 0 ? username[0].ToString().ToUpper() : "?",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 16,
                Foreground = (Brush)Application.Current.Resources["SystemControlForegroundChromeWhiteBrush"]
            };

            avatar.Child = avatarText;

            // 创建消息气泡
            var messageBubble = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8),
                MaxWidth = 400,
                Background = (Brush)Application.Current.Resources["LayerOnMicaBaseAltFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ControlElevationBorderBrush"],
                BorderThickness = new Thickness(1)
            };

            var messageStack = new StackPanel { Spacing = 4 };

            // 添加用户名
            var usernameText = new TextBlock
            {
                Text = username,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            messageStack.Children.Add(usernameText);

            // 添加消息内容
            var contentText = new TextBlock
            {
                Text = content,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
            };
            messageStack.Children.Add(contentText);

            // 添加已读状态（如果有已读信息）
            if (totalUsers > 0)
            {
                var readStatusText = new TextBlock
                {
                    Text = readByCount >= totalUsers ? "全部已读" : $"{readByCount}人已读",
                    FontSize = 10,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorDisabledBrush"],
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                messageStack.Children.Add(readStatusText);
            }

            messageBubble.Child = messageStack;

            // 根据是否是自己发送的消息决定头像和消息气泡的顺序
            if (isMe)
            {
                // 自己发送的消息：先添加消息气泡，再添加头像（头像在右边）
                container.Children.Add(messageBubble);
                container.Children.Add(avatar);
            }
            else
            {
                // 他人发送的消息：先添加头像，再添加消息气泡（头像在左边）
                container.Children.Add(avatar);
                container.Children.Add(messageBubble);
            }

            System.Diagnostics.Debug.WriteLine($"准备将消息添加到MessagesPanel，当前子元素数量: {MessagesPanel.Children.Count}");
            MessagesPanel.Children.Add(container);
            System.Diagnostics.Debug.WriteLine($"消息已添加到MessagesPanel，新的子元素数量: {MessagesPanel.Children.Count}");

            ScrollToBottom();
        }

        private void AddSystemMessage(string message)
        {
            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => AddSystemMessage(message));
                return;
            }

            var container = new StackPanel
            {
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var systemMessage = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 6, 12, 6),
                Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"]
            };

            var text = new TextBlock
            {
                Text = message,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };

            systemMessage.Child = text;
            container.Children.Add(systemMessage);

            MessagesPanel.Children.Add(container);
            ScrollToBottom();
        }

        private void AddFileMessage(string fileName, long fileSize, string username, bool isMe, string fileId, string? downloadUrl = null)
        {
            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => AddFileMessage(fileName, fileSize, username, isMe, fileId, downloadUrl));
                return;
            }

            // 创建消息容器
            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            // 添加头像
            var avatar = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 8)
            };


            var avatarText = new TextBlock
            {
                Text = username.Length > 0 ? username[0].ToString().ToUpper() : "?",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 16
            };

            avatar.Child = avatarText;

            // 创建文件卡片
            var card = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 8, 10, 8),
                MaxWidth = 280,
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ControlElevationBorderBrush"],
                BorderThickness = new Thickness(1)
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var topGrid = new Grid { ColumnSpacing = 8 };
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var icon = new FontIcon
            {
                Glyph = "\uE8A5",
                FontSize = 32,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            var tbName = new TextBlock
            {
                Text = fileName,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 14
            };
            var tbSize = new TextBlock
            {
                Text = $"{fileSize / 1024.0 / 1024.0:F2} MB",
                FontSize = 12,
                Opacity = 0.7
            };
            stack.Children.Add(tbName);
            stack.Children.Add(tbSize);

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(stack, 1);
            topGrid.Children.Add(icon);
            topGrid.Children.Add(stack);

            var btn = new Button
            {
                Content = "下载",
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 6, 0, 0),
                Tag = fileId // 保存文件ID
            };

            void DownloadFileHandler()
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"下载文件按钮被点击: {fileName}, fileId: {fileId}");
                    System.Diagnostics.Debug.WriteLine($"下载URL: {downloadUrl}");

                    // 直接在浏览器中打开下载链接
                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        System.Diagnostics.Debug.WriteLine($"在浏览器中打开下载链接: {downloadUrl}");

                        try
                        {
                            // 使用默认浏览器打开下载链接
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = downloadUrl,
                                UseShellExecute = true
                            });

                            AddSystemMessage($"下载链接已在浏览器中打开: {fileName}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"打开浏览器异常: {ex.Message}");
                            AddSystemMessage($"打开下载链接失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        // 如果没有URL，显示错误
                        AddSystemMessage("下载失败：无法获取下载链接");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"打开下载链接异常: {ex.Message}");
                    AddSystemMessage($"打开下载链接失败: {ex.Message}");
                }
            }

            btn.Click += (_, _) => DownloadFileHandler();

            Grid.SetRow(topGrid, 0);
            Grid.SetRow(btn, 1);
            rootGrid.Children.Add(topGrid);
            rootGrid.Children.Add(btn);

            card.Child = rootGrid;

            // 根据是否是自己发送的消息决定头像和消息卡片的顺序
            if (isMe)
            {
                // 自己发送的消息：先添加消息卡片，再添加头像（头像在右边）
                container.Children.Add(card);
                container.Children.Add(avatar);
            }
            else
            {
                // 他人发送的消息：先添加头像，再添加消息卡片（头像在左边）
                container.Children.Add(avatar);
                container.Children.Add(card);
            }

            MessagesPanel.Children.Add(container);
            ScrollToBottom();
        }

        private void ScrollToBottom(bool force = false)
        {
            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => ScrollToBottom(force));
                return;
            }

            // 总是滚动到底部，确保新消息可见
            ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null);
        }
        
        private void RemoveLastMessage()
        {
            // 确保在UI线程上执行
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(RemoveLastMessage);
                return;
            }
            
            // 移除最后一条消息
            if (MessagesPanel.Children.Count > 0)
            {
                MessagesPanel.Children.RemoveAt(MessagesPanel.Children.Count - 1);
            }
        }
        #endregion

        #region 事件处理
        private async void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            await SendBtn_ClickAsync();
        }

        private async Task SendBtn_ClickAsync()
        {
            if (_tcpClient == null)
            {
                AddSystemMessage("TCP客户端未初始化");
                return;
            }

            string text = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            InputBox.Text = "";
            string displayUsername = !string.IsNullOrEmpty(_currentUsername) ? _currentUsername : "我";
            AddMessageWithUser(text, displayUsername, isMe: true, readByCount: 1, totalUsers: 1);

            // 将消息添加到待发送队列
            _pendingMessages.Enqueue(text);
            
            // 尝试发送队列中的消息
            await ProcessPendingMessagesAsync();
        }

        private async Task ProcessPendingMessagesAsync()
        {
            // 如果TCP客户端未连接，不尝试发送
            if (_tcpClient == null || !_tcpClient.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine($"TCP客户端未连接，消息已加入队列，当前队列中有 {_pendingMessages.Count} 条消息");
                return;
            }

            // 处理队列中的所有消息
            while (_pendingMessages.Count > 0)
            {
                string message = _pendingMessages.Peek();
                try
                {
                    await _tcpClient.SendMessageAsync(message);
                    // 发送成功，从队列中移除
                    _pendingMessages.Dequeue();
                    System.Diagnostics.Debug.WriteLine($"消息发送成功，队列中剩余 {_pendingMessages.Count} 条消息");
                }
                catch (Exception ex)
                {
                    // 发送失败，不从队列中移除，等待下次重试
                    System.Diagnostics.Debug.WriteLine($"消息发送失败: {ex.Message}");
                    break;
                }
            }
        }

        private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && !e.KeyStatus.IsMenuKeyDown)
            {
                e.Handled = true;
                _ = SendBtn_ClickAsync();
            }
        }

        private async void ReconnectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_tcpClient != null)
            {
                await _tcpClient.DisconnectAsync();
            }

            // 确保这不是自动刷新操作
            _isAutoRefresh = false;
            await InitializeTCPClientAsync();
        }

        private async void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            // 创建设置对话框
            var dialog = new ContentDialog
            {
                Title = "反馈频道设置",
                CloseButtonText = "取消",
                PrimaryButtonText = "保存",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            // 创建设置UI
            var stackPanel = new StackPanel { Spacing = 12 };

            // 用户名设置
            var usernamePanel = new StackPanel { Spacing = 4 };
            usernamePanel.Children.Add(new TextBlock { Text = "用户名:" });
            var usernameBox = new TextBox { Text = _tcpClient?.Username ?? "" };
            usernamePanel.Children.Add(usernameBox);

            // 服务器地址设置
            var hostPanel = new StackPanel { Spacing = 4 };
            hostPanel.Children.Add(new TextBlock { Text = "服务器地址:" });
            var hostBox = new TextBox { Text = _tcpClient?.ServerHost ?? "" };
            hostPanel.Children.Add(hostBox);

            // 端口设置
            var portPanel = new StackPanel { Spacing = 4 };
            portPanel.Children.Add(new TextBlock { Text = "端口:" });
            var portBox = new TextBox { Text = _tcpClient?.ServerPort.ToString() ?? "" };
            portPanel.Children.Add(portBox);

            stackPanel.Children.Add(usernamePanel);
            stackPanel.Children.Add(hostPanel);
            stackPanel.Children.Add(portPanel);

            dialog.Content = stackPanel;

            // 处理保存按钮点击
            dialog.PrimaryButtonClick += async (_, __) =>
            {
                try
                {
                    if (_tcpClient != null)
                    {
                        // 先断开连接
                        if (_tcpClient.IsConnected)
                        {
                            await _tcpClient.DisconnectAsync();
                        }

                        // 保存设置
                        await _tcpClient.SetUsernameAsync(usernameBox.Text);

                        if (int.TryParse(portBox.Text, out int port))
                        {
                            await _tcpClient.SetServerAsync(hostBox.Text, port);
                        }

                        AddSystemMessage("设置已保存，正在重新连接...");

                        // 重新连接
                        await _tcpClient.ConnectAsync();
                    }
                }
                catch (Exception ex)
                {
                    AddSystemMessage($"保存设置失败: {ex.Message}");
                }
                return;
            };

            await dialog.ShowAsync();
        }
        #endregion

        #region 自动刷新
        private void InitializeRefreshTimer()
        {
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4) // 每4秒钟执行一次
            };
            
            _refreshTimer.Tick += async (sender, e) =>
            {
                try
                {
                    // 只刷新消息，不显示连接状态变化
                    if (_tcpClient != null)
                    {
                        // 标记为自动刷新操作
                        _isAutoRefresh = true;
                        
                        // 尝试连接以刷新消息
                        await _tcpClient.ConnectAsync();
                        
                        // 如果连接成功且有待发送消息，处理它们
                        if (_tcpClient.IsConnected && _pendingMessages.Count > 0)
                        {
                            await ProcessPendingMessagesAsync();
                        }
                        
                        // 重置标志
                        _isAutoRefresh = false;
                    }
                }
                catch (Exception ex)
                {
                    // 忽略连接错误，不显示错误消息
                    System.Diagnostics.Debug.WriteLine($"自动刷新连接失败: {ex.Message}");
                    _isAutoRefresh = false; // 确保重置标志
                }
            };
            
            _refreshTimer.Start();
            
            // 添加连接状态检测定时器
            var connectionCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(3000) // 每3秒检查一次
            };
            
            // 连接失败计数器
            int connectionFailureCount = 0;
            
            // 连接状态锁，防止状态快速切换
            bool isUpdatingStatus = false;
            
            connectionCheckTimer.Tick += (sender, e) =>
            {
                // 防止状态更新冲突
                if (isUpdatingStatus)
                    return;
                    
                // 每3秒执行一次重新连接，模拟点击重新连接按钮
                if (_tcpClient != null)
                {
                    isUpdatingStatus = true;
                    
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _tcpClient.DisconnectAsync();
                            await Task.Delay(1000); // 增加等待时间到1秒
                            await _tcpClient.ConnectAsync();
                            
                            // 连接成功，重置失败计数器
                            connectionFailureCount = 0;
                            
                            // 如果有待发送消息，处理它们
                            if (_pendingMessages.Count > 0)
                            {
                                await ProcessPendingMessagesAsync();
                            }
                            
                            // 更新状态为已连接
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                StatusTxt.Text = WinUI3Localizer.Localizer.Get().GetLocalizedString("BugReportPage_Connected");
                                isUpdatingStatus = false;
                            });
                        }
                        catch (Exception ex)
                        {
                            connectionFailureCount++;
                            System.Diagnostics.Debug.WriteLine($"自动重连失败 {connectionFailureCount} 次: {ex.Message}");
                            
                            // 只有连续失败3次才显示连接失败状态
                            if (connectionFailureCount >= 3)
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    StatusTxt.Text = WinUI3Localizer.Localizer.Get().GetLocalizedString("BugReportPage_ConnectionFailed");
                                    AddSystemMessage("连接失败，请检查网络或稍后再试");
                                    isUpdatingStatus = false;
                                });
                            }
                            else
                            {
                                // 失败但未达到3次，不更新状态
                                isUpdatingStatus = false;
                            }
                        }
                    });
                }
            };
            
            connectionCheckTimer.Start();
        }
        #endregion

        #region 清理
        private void Cleanup()
        {
            try
            {
                // 停止定时器
                _refreshTimer?.Stop();
                
                // 如果之前已连接，显示离开反馈频道的提示
                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    AddSystemMessage("已离开反馈频道");
                }

                _tcpClient?.DisconnectAsync().Wait(1000);
            }
            catch { }

            _tcpClient = null;
        }
        #endregion

    }
}