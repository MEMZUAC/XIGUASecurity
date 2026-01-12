using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using XIGUASecurity.Models;
using Windows.System;

namespace XIGUASecurity
{
    public sealed partial class AppStorePage : Page
    {
        private ObservableCollection<AppStoreItem> _apps;
        private ObservableCollection<AppStoreItem> _allApps;
        private List<AppStoreItem> _allAppsList;
        private List<AppStoreItem> _filteredAppsList; // 当前过滤后的应用列表
        private int _pageSize = 12; // 每页加载的应用数量
        private int _currentPage = 0;
        private bool _isLoading = false;
        private bool _allAppsLoaded = false;
        private string _currentCategory = "All"; // 当前选择的分类

        public AppStorePage()
        {
            this.InitializeComponent();
            _apps = new ObservableCollection<AppStoreItem>();
            _allApps = new ObservableCollection<AppStoreItem>();
            _allAppsList = new List<AppStoreItem>();
            _filteredAppsList = new List<AppStoreItem>(); // 初始化过滤列表
            AppsGridView.ItemsSource = _apps;

            // 初始化所有应用数据，但不加载到UI
            InitializeAllApps();

            // 默认选中"全部应用"
            CategoriesListView.SelectedIndex = 0;

            // 加载第一页应用
            _ = LoadFirstPageAsync();
        }

        // 加载第一页应用
        private async Task LoadFirstPageAsync()
        {
            // 等待一小段时间确保UI初始化完成
            await Task.Delay(100);
            await LoadAppsAsync();
        }

        private async Task LoadAppsAsync()
        {
            if (_isLoading || _allAppsLoaded) return;

            // 确定要加载的应用列表
            List<AppStoreItem> sourceList = _currentCategory == "All" ? _allAppsList : _filteredAppsList;

            _isLoading = true;
            
            // 只有在第一页加载时才显示"加载应用中..."文本
            if (_currentPage == 0)
            {
                ShowLoadingProgress();
            }

            try
            {
                // 计算当前页的起始和结束索引
                int startIndex = _currentPage * _pageSize;
                int endIndex = Math.Min(startIndex + _pageSize, sourceList.Count);

                // 加载当前页的应用
                for (int i = startIndex; i < endIndex; i++)
                {
                    var app = sourceList[i];
                    _apps.Add(app);

                    // 异步加载图标
                    _ = app.LoadIconAsync();

                    // 更新进度，使用当前分类的应用总数
                    UpdateLoadingProgress(_apps.Count, sourceList.Count);

                    // 添加更短的延迟以避免UI阻塞
                    await Task.Delay(20);
                }

                _currentPage++;

                // 检查是否所有应用都已加载
                if (_apps.Count >= sourceList.Count)
                {
                    _allAppsLoaded = true;
                }
            }
            finally
            {
                _isLoading = false;

                // 如果所有应用都已加载，隐藏进度条
                if (_allAppsLoaded)
                {
                    HideLoadingProgress();
                }
            }
            
            // 更新页码显示
            UpdatePageIndicator();
        }

        private void UpdatePageIndicator()
        {
            // 确定要加载的应用列表
            List<AppStoreItem> sourceList = _currentCategory == "All" ? _allAppsList : _filteredAppsList;
            
            // 计算总页数
            int totalPages = (int)Math.Ceiling((double)sourceList.Count / _pageSize);
            
            // 确保至少有一页
            if (totalPages == 0) totalPages = 1;
            
            // 获取本地化字符串并更新页码显示
            string pageIndicatorText = WinUI3Localizer.Localizer.Get().GetLocalizedString("AppStorePage_PageIndicator.Text") ?? "Page 1/1";
            
            // 替换页码占位符
            pageIndicatorText = pageIndicatorText.Replace("1/1", $"{_currentPage}/{totalPages}");
            
            // 更新UI
            PageIndicatorText.Text = pageIndicatorText;
        }

        private void InitializeAllApps()
        {
            // 开发工具
            _allAppsList.Add(new AppStoreItem
            {
                Name = "Visual Studio",
                Description = "功能强大的集成开发环境，支持多种编程语言和平台开发。",
                Category = "开发工具",
                Version = "2022",
                Developer = "Microsoft",
                WebsiteUrl = "https://visualstudio.microsoft.com/",
                Rating = 4.8,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Visual Studio", "https://visualstudio.microsoft.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Visual Studio Code",
                Description = "轻量级但功能强大的源代码编辑器，支持多种编程语言。",
                Category = "开发工具",
                Version = "1.85.0",
                Developer = "Microsoft",
                WebsiteUrl = "https://code.visualstudio.com/",
                Rating = 4.9,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Visual Studio Code", "https://code.visualstudio.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "IntelliJ IDEA",
                Description = "强大的Java集成开发环境，支持多种编程语言。",
                Category = "开发工具",
                Version = "2024.1",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/idea/",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("IntelliJ IDEA", "https://www.jetbrains.com/idea/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "PyCharm",
                Description = "专为Python开发设计的集成开发环境。",
                Category = "开发工具",
                Version = "2024.1",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/pycharm/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("PyCharm", "https://www.jetbrains.com/pycharm/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Android Studio",
                Description = "官方Android应用开发环境，基于IntelliJ IDEA。",
                Category = "开发工具",
                Version = "2024.1.1",
                Developer = "Google",
                WebsiteUrl = "https://developer.android.com/studio",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Android Studio", "https://developer.android.com/studio"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Unity",
                Description = "跨平台游戏引擎，用于创建2D和3D游戏。",
                Category = "开发工具",
                Version = "2023.2.20",
                Developer = "Unity Technologies",
                WebsiteUrl = "https://unity.com/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Unity", "https://unity.com/"))
            });

            // 网络工具
            _allAppsList.Add(new AppStoreItem
            {
                Name = "Google Chrome",
                Description = "快速、安全的网页浏览器，提供丰富的扩展和同步功能。",
                Category = "网络工具",
                Version = "124.0.6367.91",
                Developer = "Google",
                WebsiteUrl = "https://www.google.com/chrome/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Google Chrome", "https://www.google.com/chrome/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Microsoft Edge",
                Description = "基于Chromium的现代化网页浏览器，集成Windows系统。",
                Category = "网络工具",
                Version = "124.0.2478.80",
                Developer = "Microsoft",
                WebsiteUrl = "https://www.microsoft.com/edge",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Microsoft Edge", "https://www.microsoft.com/edge"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Firefox",
                Description = "开源、注重隐私的网页浏览器。",
                Category = "网络工具",
                Version = "125.0.3",
                Developer = "Mozilla",
                WebsiteUrl = "https://www.mozilla.org/firefox/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Firefox", "https://www.mozilla.org/firefox/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "哔哩哔哩",
                Description = "中国领先的年轻人文化社区和视频平台。",
                Category = "网络工具",
                Version = "2.47.0",
                Developer = "哔哩哔哩",
                WebsiteUrl = "https://www.bilibili.com/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("哔哩哔哩", "https://www.bilibili.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "YouTube",
                Description = "全球最大的视频分享平台。",
                Category = "网络工具",
                Version = "网页版",
                Developer = "Google",
                WebsiteUrl = "https://www.youtube.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("YouTube", "https://www.youtube.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Telegram",
                Description = "注重隐私的即时通讯应用，支持端到端加密。",
                Category = "网络工具",
                Version = "5.1.3",
                Developer = "Telegram FZ-LLC",
                WebsiteUrl = "https://telegram.org/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Telegram", "https://telegram.org/"))
            });

            // 系统工具
            _allAppsList.Add(new AppStoreItem
            {
                Name = "7-Zip",
                Description = "高压缩比的文件压缩工具，支持多种压缩格式。",
                Category = "系统工具",
                Version = "23.01",
                Developer = "Igor Pavlov",
                WebsiteUrl = "https://www.7-zip.org/",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("7-Zip", "https://www.7-zip.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "WinRAR",
                Description = "强大的压缩文件管理器，支持RAR和ZIP格式。",
                Category = "系统工具",
                Version = "6.24",
                Developer = "win.rar GmbH",
                WebsiteUrl = "https://www.win-rar.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("WinRAR", "https://www.win-rar.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "CCleaner",
                Description = "系统清理和优化工具，提高计算机性能。",
                Category = "系统工具",
                Version = "6.20",
                Developer = "Piriform",
                WebsiteUrl = "https://www.ccleaner.com/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("CCleaner", "https://www.ccleaner.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Driver Booster",
                Description = "自动检测和更新过时的驱动程序。",
                Category = "系统工具",
                Version = "11.1.0",
                Developer = "IObit",
                WebsiteUrl = "https://www.iobit.com/driver-booster.php",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Driver Booster", "https://www.iobit.com/driver-booster.php"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Bandizip",
                Description = "全功能的压缩工具，支持多种格式。",
                Category = "系统工具",
                Version = "7.32",
                Developer = "Bandisoft",
                WebsiteUrl = "https://en.bandisoft.com/bandizip/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Bandizip", "https://en.bandisoft.com/bandizip/"))
            });

            // 多媒体
            _allAppsList.Add(new AppStoreItem
            {
                Name = "VLC Media Player",
                Description = "免费开源的多媒体播放器，支持几乎所有音视频格式。",
                Category = "多媒体",
                Version = "3.0.20",
                Developer = "VideoLAN",
                WebsiteUrl = "https://www.videolan.org/vlc/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("VLC Media Player", "https://www.videolan.org/vlc/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "PotPlayer",
                Description = "功能强大的多媒体播放器，支持各种格式和硬件加速。",
                Category = "多媒体",
                Version = "1.7.21523",
                Developer = "Kakao Corp",
                WebsiteUrl = "https://potplayer.daum.net/",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("PotPlayer", "https://potplayer.daum.net/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "OBS Studio",
                Description = "免费开源的视频录制和直播软件。",
                Category = "多媒体",
                Version = "30.1.2",
                Developer = "OBS Project",
                WebsiteUrl = "https://obsproject.com/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("OBS Studio", "https://obsproject.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Audacity",
                Description = "免费开源的音频编辑软件。",
                Category = "多媒体",
                Version = "3.4.2",
                Developer = "Audacity Team",
                WebsiteUrl = "https://www.audacityteam.org/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Audacity", "https://www.audacityteam.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "GIMP",
                Description = "免费开源的图像编辑软件，可替代Photoshop。",
                Category = "多媒体",
                Version = "2.10.36",
                Developer = "GIMP Team",
                WebsiteUrl = "https://www.gimp.org/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("GIMP", "https://www.gimp.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Krita",
                Description = "专业的数字绘画软件，特别适合概念艺术和纹理绘制。",
                Category = "多媒体",
                Version = "5.2.2",
                Developer = "Krita Foundation",
                WebsiteUrl = "https://krita.org/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Krita", "https://krita.org/"))
            });

            // 通讯工具
            _allAppsList.Add(new AppStoreItem
            {
                Name = "Discord",
                Description = "专为游戏玩家设计的语音、视频和文字聊天应用。",
                Category = "通讯工具",
                Version = "1.0.9013",
                Developer = "Discord Inc.",
                WebsiteUrl = "https://discord.com/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Discord", "https://discord.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "微信",
                Description = "腾讯开发的即时通讯软件，支持文字、语音和视频通话。",
                Category = "通讯工具",
                Version = "3.9.9.35",
                Developer = "Tencent",
                WebsiteUrl = "https://windows.weixin.qq.com/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("微信", "https://windows.weixin.qq.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "QQ",
                Description = "腾讯开发的即时通讯软件，中国最流行的社交平台之一。",
                Category = "通讯工具",
                Version = "9.7.23",
                Developer = "Tencent",
                WebsiteUrl = "https://im.qq.com/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("QQ", "https://im.qq.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "钉钉",
                Description = "阿里巴巴开发的企业级智能移动办公平台。",
                Category = "通讯工具",
                Version = "7.0.40",
                Developer = "Alibaba",
                WebsiteUrl = "https://www.dingtalk.com/",
                Rating = 3.9,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("钉钉", "https://www.dingtalk.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Zoom",
                Description = "高质量的视频会议和在线协作平台。",
                Category = "通讯工具",
                Version = "5.16.10",
                Developer = "Zoom Video Communications",
                WebsiteUrl = "https://zoom.us/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Zoom", "https://zoom.us/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Slack",
                Description = "企业级团队协作和通讯平台。",
                Category = "通讯工具",
                Version = "4.35.132",
                Developer = "Slack Technologies",
                WebsiteUrl = "https://slack.com/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Slack", "https://slack.com/"))
            });

            // 办公软件
            _allAppsList.Add(new AppStoreItem
            {
                Name = "WPS Office",
                Description = "金山办公软件，兼容Microsoft Office格式。",
                Category = "办公软件",
                Version = "11.1.0",
                Developer = "Kingsoft",
                WebsiteUrl = "https://www.wps.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("WPS Office", "https://www.wps.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Microsoft Office",
                Description = "微软办公套件，包含Word、Excel、PowerPoint等。",
                Category = "办公软件",
                Version = "2024",
                Developer = "Microsoft",
                WebsiteUrl = "https://www.office.com/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Microsoft Office", "https://www.office.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Notion",
                Description = "集笔记、知识库、任务管理于一体的协作工具。",
                Category = "办公软件",
                Version = "网页版",
                Developer = "Notion Labs",
                WebsiteUrl = "https://www.notion.so/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Notion", "https://www.notion.so/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "XMind",
                Description = "专业的思维导图和头脑风暴软件。",
                Category = "办公软件",
                Version = "23.12",
                Developer = "XMind Ltd",
                WebsiteUrl = "https://www.xmind.net/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("XMind", "https://www.xmind.net/"))
            });

            // 设计软件
            _allAppsList.Add(new AppStoreItem
            {
                Name = "Figma",
                Description = "基于云端的协作式界面设计工具。",
                Category = "设计软件",
                Version = "网页版",
                Developer = "Figma Inc",
                WebsiteUrl = "https://www.figma.com/",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Figma", "https://www.figma.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Sketch",
                Description = "Mac平台上的数字设计工具。",
                Category = "设计软件",
                Version = "98",
                Developer = "Sketch B.V",
                WebsiteUrl = "https://www.sketch.com/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Sketch", "https://www.sketch.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Adobe Photoshop",
                Description = "专业的图像编辑和设计软件。",
                Category = "设计软件",
                Version = "2024",
                Developer = "Adobe",
                WebsiteUrl = "https://www.adobe.com/products/photoshop.html",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Adobe Photoshop", "https://www.adobe.com/products/photoshop.html"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Blender",
                Description = "免费开源的3D创作套件，支持建模、动画、渲染等。",
                Category = "设计软件",
                Version = "4.1.1",
                Developer = "Blender Foundation",
                WebsiteUrl = "https://www.blender.org/",
                Rating = 4.8,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Blender", "https://www.blender.org/"))
            });

            // 添加50个新应用程序

            // 开发工具 (10个)
            _allAppsList.Add(new AppStoreItem
            {
                Name = "Sublime Text",
                Description = "轻量级、高性能的文本编辑器，支持多种编程语言。",
                Category = "开发工具",
                Version = "4.0",
                Developer = "Sublime HQ",
                WebsiteUrl = "https://www.sublimetext.com/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Sublime Text", "https://www.sublimetext.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Atom",
                Description = "GitHub开发的可定制文本编辑器。",
                Category = "开发工具",
                Version = "1.60.0",
                Developer = "GitHub",
                WebsiteUrl = "https://atom.io/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Atom", "https://atom.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Eclipse",
                Description = "开源的集成开发环境，主要用于Java开发。",
                Category = "开发工具",
                Version = "2024-03",
                Developer = "Eclipse Foundation",
                WebsiteUrl = "https://www.eclipse.org/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Eclipse", "https://www.eclipse.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "NetBeans",
                Description = "免费开源的集成开发环境，支持多种语言。",
                Category = "开发工具",
                Version = "17",
                Developer = "Apache",
                WebsiteUrl = "https://netbeans.apache.org/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("NetBeans", "https://netbeans.apache.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Xcode",
                Description = "苹果官方的集成开发环境，用于macOS和iOS应用开发。",
                Category = "开发工具",
                Version = "15.3",
                Developer = "Apple",
                WebsiteUrl = "https://developer.apple.com/xcode/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Xcode", "https://developer.apple.com/xcode/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Flutter",
                Description = "谷歌开发的跨平台移动应用开发框架。",
                Category = "开发工具",
                Version = "3.19.0",
                Developer = "Google",
                WebsiteUrl = "https://flutter.dev/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Flutter", "https://flutter.dev/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "React Native",
                Description = "Facebook开发的跨平台移动应用开发框架。",
                Category = "开发工具",
                Version = "0.73.4",
                Developer = "Facebook",
                WebsiteUrl = "https://reactnative.dev/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("React Native", "https://reactnative.dev/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Postman",
                Description = "API开发和测试工具。",
                Category = "开发工具",
                Version = "10.24.0",
                Developer = "Postman",
                WebsiteUrl = "https://www.postman.com/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Postman", "https://www.postman.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Docker Desktop",
                Description = "容器化应用程序开发和部署工具。",
                Category = "开发工具",
                Version = "4.28.0",
                Developer = "Docker",
                WebsiteUrl = "https://www.docker.com/products/docker-desktop/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Docker Desktop", "https://www.docker.com/products/docker-desktop/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Git",
                Description = "分布式版本控制系统。",
                Category = "开发工具",
                Version = "2.44.0",
                Developer = "Git Community",
                WebsiteUrl = "https://git-scm.com/",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Git", "https://git-scm.com/"))
            });

            // 系统工具 (8个)
            _allAppsList.Add(new AppStoreItem
            {
                Name = "Process Explorer",
                Description = "高级进程管理和实用工具，可查看系统中运行的进程。",
                Category = "系统工具",
                Version = "17.01",
                Developer = "Microsoft",
                WebsiteUrl = "https://docs.microsoft.com/en-us/sysinternals/downloads/process-explorer",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Process Explorer", "https://docs.microsoft.com/en-us/sysinternals/downloads/process-explorer"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Process Monitor",
                Description = "实时监视文件系统、注册表和进程/线程活动的工具。",
                Category = "系统工具",
                Version = "3.96",
                Developer = "Microsoft",
                WebsiteUrl = "https://docs.microsoft.com/en-us/sysinternals/downloads/procmon",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Process Monitor", "https://docs.microsoft.com/en-us/sysinternals/downloads/procmon"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Autoruns",
                Description = "管理Windows启动程序的强大工具。",
                Category = "系统工具",
                Version = "14.21",
                Developer = "Microsoft",
                WebsiteUrl = "https://docs.microsoft.com/en-us/sysinternals/downloads/autoruns",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Autoruns", "https://docs.microsoft.com/en-us/sysinternals/downloads/autoruns"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Defraggler",
                Description = "快速安全的硬盘碎片整理工具。",
                Category = "系统工具",
                Version = "2.35.1398",
                Developer = "Piriform",
                WebsiteUrl = "https://www.ccleaner.com/defraggler",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Defraggler", "https://www.ccleaner.com/defraggler"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Recuva",
                Description = "免费文件恢复工具，可恢复已删除的文件。",
                Category = "系统工具",
                Version = "1.53.2087",
                Developer = "Piriform",
                WebsiteUrl = "https://www.ccleaner.com/recuva",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Recuva", "https://www.ccleaner.com/recuva"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Speccy",
                Description = "详细的系统信息工具，显示硬件和软件配置。",
                Category = "系统工具",
                Version = "1.32.803",
                Developer = "Piriform",
                WebsiteUrl = "https://www.ccleaner.com/speccy",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Speccy", "https://www.ccleaner.com/speccy"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "HWMonitor",
                Description = "硬件监控工具，可查看系统温度、电压和风扇速度。",
                Category = "系统工具",
                Version = "1.53",
                Developer = "CPUID",
                WebsiteUrl = "https://www.cpuid.com/softwares/hwmonitor.html",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("HWMonitor", "https://www.cpuid.com/softwares/hwmonitor.html"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "CrystalDiskInfo",
                Description = "硬盘健康状态监控工具。",
                Category = "系统工具",
                Version = "9.2.1",
                Developer = "Crystal Dew World",
                WebsiteUrl = "https://crystalmark.info/en/software/crystaldiskinfo/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("CrystalDiskInfo", "https://crystalmark.info/en/software/crystaldiskinfo/"))
            });

            // 网络工具 (8个)
            _allAppsList.Add(new AppStoreItem
            {
                Name = "Wireshark",
                Description = "网络协议分析器，用于捕获和分析网络流量。",
                Category = "网络工具",
                Version = "4.2.5",
                Developer = "Wireshark Foundation",
                WebsiteUrl = "https://www.wireshark.org/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Wireshark", "https://www.wireshark.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "FileZilla",
                Description = "免费开源的FTP客户端。",
                Category = "网络工具",
                Version = "3.66.1",
                Developer = "FileZilla Project",
                WebsiteUrl = "https://filezilla-project.org/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("FileZilla", "https://filezilla-project.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "PuTTY",
                Description = "免费的SSH和Telnet客户端。",
                Category = "网络工具",
                Version = "0.81",
                Developer = "Simon Tatham",
                WebsiteUrl = "https://www.putty.org/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("PuTTY", "https://www.putty.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "WinSCP",
                Description = "Windows平台的免费SFTP、FTP和WebDAV客户端。",
                Category = "网络工具",
                Version = "6.3.1",
                Developer = "Martin Přikryl",
                WebsiteUrl = "https://winscp.net/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("WinSCP", "https://winscp.net/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Thunderbird",
                Description = "Mozilla开发的免费电子邮件客户端。",
                Category = "网络工具",
                Version = "115.9.0",
                Developer = "Mozilla",
                WebsiteUrl = "https://www.thunderbird.net/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Thunderbird", "https://www.thunderbird.net/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Opera",
                Description = "功能丰富的网页浏览器，内置VPN和广告拦截器。",
                Category = "网络工具",
                Version = "104.0.4944.54",
                Developer = "Opera Software",
                WebsiteUrl = "https://www.opera.com/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Opera", "https://www.opera.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Vivaldi",
                Description = "高度可定制的网页浏览器，注重用户隐私。",
                Category = "网络工具",
                Version = "6.6.3271.47",
                Developer = "Vivaldi Technologies",
                WebsiteUrl = "https://vivaldi.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Vivaldi", "https://vivaldi.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Brave",
                Description = "注重隐私的网页浏览器，自动拦截广告和跟踪器。",
                Category = "网络工具",
                Version = "1.68.132",
                Developer = "Brave Software",
                WebsiteUrl = "https://brave.com/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Brave", "https://brave.com/"))
            });

            // 多媒体 (8个)
            _allAppsList.Add(new AppStoreItem
            {
                Name = "DaVinci Resolve",
                Description = "专业的视频编辑软件，集编辑、调色、音频后期于一体。",
                Category = "多媒体",
                Version = "18.6.4",
                Developer = "Blackmagic Design",
                WebsiteUrl = "https://www.blackmagicdesign.com/products/davinciresolve",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("DaVinci Resolve", "https://www.blackmagicdesign.com/products/davinciresolve"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Shotcut",
                Description = "免费开源的跨平台视频编辑软件。",
                Category = "多媒体",
                Version = "24.02.28",
                Developer = "Meltytech",
                WebsiteUrl = "https://shotcut.org/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Shotcut", "https://shotcut.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Kdenlive",
                Description = "基于KDE框架的开源非线性视频编辑器。",
                Category = "多媒体",
                Version = "23.08.5",
                Developer = "KDE Community",
                WebsiteUrl = "https://kdenlive.org/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Kdenlive", "https://kdenlive.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "HandBrake",
                Description = "免费开源的视频转码工具。",
                Category = "多媒体",
                Version = "1.7.3",
                Developer = "HandBrake Team",
                WebsiteUrl = "https://handbrake.fr/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("HandBrake", "https://handbrake.fr/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Foobar2000",
                Description = "高度可定制的音频播放器，支持多种音频格式。",
                Category = "多媒体",
                Version = "2.1",
                Developer = "Peter Pawlowski",
                WebsiteUrl = "https://www.foobar2000.org/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Foobar2000", "https://www.foobar2000.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "MusicBee",
                Description = "功能强大的音乐管理和播放软件。",
                Category = "多媒体",
                Version = "3.5.8625",
                Developer = "Steven Mayall",
                WebsiteUrl = "https://getmusicbee.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("MusicBee", "https://getmusicbee.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Inkscape",
                Description = "免费开源的矢量图形编辑器，可替代Adobe Illustrator。",
                Category = "多媒体",
                Version = "1.3.2",
                Developer = "Inkscape Team",
                WebsiteUrl = "https://inkscape.org/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Inkscape", "https://inkscape.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Paint.NET",
                Description = "免费且功能强大的图像和照片编辑软件。",
                Category = "多媒体",
                Version = "5.0.11",
                Developer = "dotPDN LLC",
                WebsiteUrl = "https://www.getpaint.net/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Paint.NET", "https://www.getpaint.net/"))
            });

            // 通讯工具 (8个)
            _allAppsList.Add(new AppStoreItem
            {
                Name = "Skype",
                Description = "微软开发的视频通话和即时通讯应用。",
                Category = "通讯工具",
                Version = "8.110.0.215",
                Developer = "Microsoft",
                WebsiteUrl = "https://www.skype.com/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Skype", "https://www.skype.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "WhatsApp",
                Description = "Facebook开发的跨平台即时通讯应用。",
                Category = "通讯工具",
                Version = "2.2401.4.0",
                Developer = "Meta",
                WebsiteUrl = "https://www.whatsapp.com/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("WhatsApp", "https://www.whatsapp.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Microsoft Teams",
                Description = "微软开发的团队协作和通讯平台。",
                Category = "通讯工具",
                Version = "24006.2402.2600.3647",
                Developer = "Microsoft",
                WebsiteUrl = "https://www.microsoft.com/teams",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Microsoft Teams", "https://www.microsoft.com/teams"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Viber",
                Description = "支持免费通话和消息的跨平台通讯应用。",
                Category = "通讯工具",
                Version = "22.3.0.0",
                Developer = "Viber Media",
                WebsiteUrl = "https://www.viber.com/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Viber", "https://www.viber.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Line",
                Description = "日本流行的即时通讯应用，提供多种服务。",
                Category = "通讯工具",
                Version = "7.19.1",
                Developer = "LINE Corporation",
                WebsiteUrl = "https://line.me/",
                Rating = 3.9,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Line", "https://line.me/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Signal",
                Description = "注重隐私的即时通讯应用，支持端到端加密。",
                Category = "通讯工具",
                Version = "6.42.0",
                Developer = "Signal Foundation",
                WebsiteUrl = "https://signal.org/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Signal", "https://signal.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "WeChat Work",
                Description = "企业微信，专为企业设计的通讯和协作平台。",
                Category = "通讯工具",
                Version = "4.1.18",
                Developer = "Tencent",
                WebsiteUrl = "https://work.weixin.qq.com/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("WeChat Work", "https://work.weixin.qq.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Mattermost",
                Description = "开源的自托管团队通讯平台。",
                Category = "通讯工具",
                Version = "9.5.1",
                Developer = "Mattermost",
                WebsiteUrl = "https://mattermost.com/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Mattermost", "https://mattermost.com/"))
            });

            // 办公软件 (8个)
            _allAppsList.Add(new AppStoreItem
            {
                Name = "LibreOffice",
                Description = "免费开源的办公套件，兼容Microsoft Office格式。",
                Category = "办公软件",
                Version = "7.6.5",
                Developer = "The Document Foundation",
                WebsiteUrl = "https://www.libreoffice.org/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("LibreOffice", "https://www.libreoffice.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "OpenOffice",
                Description = "Apache开源的办公套件，包含文字处理、电子表格等。",
                Category = "办公软件",
                Version = "4.1.15",
                Developer = "Apache Software Foundation",
                WebsiteUrl = "https://www.openoffice.org/",
                Rating = 3.9,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("OpenOffice", "https://www.openoffice.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "OnlyOffice",
                Description = "功能全面的办公套件，兼容多种文档格式。",
                Category = "办公软件",
                Version = "7.5.1",
                Developer = "Ascensio System SIA",
                WebsiteUrl = "https://www.onlyoffice.com/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("OnlyOffice", "https://www.onlyoffice.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Evernote",
                Description = "功能强大的笔记和任务管理应用。",
                Category = "办公软件",
                Version = "10.65.4",
                Developer = "Evernote Corporation",
                WebsiteUrl = "https://evernote.com/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Evernote", "https://evernote.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "OneNote",
                Description = "微软开发的数字笔记应用。",
                Category = "办公软件",
                Version = "2402",
                Developer = "Microsoft",
                WebsiteUrl = "https://www.onenote.com/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("OneNote", "https://www.onenote.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Todoist",
                Description = "简洁高效的任务管理和待办事项应用。",
                Category = "办公软件",
                Version = "网页版",
                Developer = "Doist",
                WebsiteUrl = "https://todoist.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Todoist", "https://todoist.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Trello",
                Description = "基于看板方法的项目管理和协作工具。",
                Category = "办公软件",
                Version = "网页版",
                Developer = "Atlassian",
                WebsiteUrl = "https://trello.com/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Trello", "https://trello.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Typora",
                Description = "简洁优雅的Markdown编辑器。",
                Category = "办公软件",
                Version = "1.8.10",
                Developer = "Typora",
                WebsiteUrl = "https://typora.io/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Typora", "https://typora.io/"))
            });

            // 设计软件 (8个)
            _allAppsList.Add(new AppStoreItem
            {
                Name = "Adobe Illustrator",
                Description = "专业的矢量图形设计软件。",
                Category = "设计软件",
                Version = "2024",
                Developer = "Adobe",
                WebsiteUrl = "https://www.adobe.com/products/illustrator.html",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Adobe Illustrator", "https://www.adobe.com/products/illustrator.html"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Adobe XD",
                Description = "用于设计和原型制作的协作平台。",
                Category = "设计软件",
                Version = "2024",
                Developer = "Adobe",
                WebsiteUrl = "https://www.adobe.com/products/xd.html",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Adobe XD", "https://www.adobe.com/products/xd.html"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "CorelDRAW",
                Description = "专业的矢量图形设计和页面布局软件。",
                Category = "设计软件",
                Version = "2023",
                Developer = "Corel",
                WebsiteUrl = "https://www.coreldraw.com/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("CorelDRAW", "https://www.coreldraw.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Affinity Designer",
                Description = "专业的矢量图形设计软件，Adobe Illustrator的替代品。",
                Category = "设计软件",
                Version = "2.5.5",
                Developer = "Serif",
                WebsiteUrl = "https://affinity.serif.com/designer/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Affinity Designer", "https://affinity.serif.com/designer/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Affinity Photo",
                Description = "专业的照片编辑软件，Adobe Photoshop的替代品。",
                Category = "设计软件",
                Version = "2.5.5",
                Developer = "Serif",
                WebsiteUrl = "https://affinity.serif.com/photo/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Affinity Photo", "https://affinity.serif.com/photo/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Canva",
                Description = "在线图形设计平台，提供丰富的模板和设计工具。",
                Category = "设计软件",
                Version = "网页版",
                Developer = "Canva",
                WebsiteUrl = "https://www.canva.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Canva", "https://www.canva.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Gravit Designer",
                Description = "免费的在线矢量图形设计工具。",
                Category = "设计软件",
                Version = "网页版",
                Developer = "Corel",
                WebsiteUrl = "https://www.designer.io/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Gravit Designer", "https://www.designer.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Lunacy",
                Description = "免费的矢量图形设计软件，支持Figma文件。",
                Category = "设计软件",
                Version = "9.5.0",
                Developer = "Icons8",
                WebsiteUrl = "https://icons8.com/lunacy",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Lunacy", "https://icons8.com/lunacy"))
            });

            // 常用办公软件
            _allAppsList.Add(new AppStoreItem
            {
                Name = "腾讯会议",
                Description = "高清流畅的云视频会议服务，支持多人在线会议。",
                Category = "办公软件",
                Version = "3.21.0",
                Developer = "腾讯",
                WebsiteUrl = "https://meeting.tencent.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("腾讯会议", "https://meeting.tencent.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "钉钉",
                Description = "阿里巴巴出品的企业级智能移动办公平台。",
                Category = "办公软件",
                Version = "6.5.40",
                Developer = "阿里巴巴",
                WebsiteUrl = "https://www.dingtalk.com/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("钉钉", "https://www.dingtalk.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "企业微信",
                Description = "腾讯出出的企业级沟通与协作工具。",
                Category = "办公软件",
                Version = "4.1.10",
                Developer = "腾讯",
                WebsiteUrl = "https://work.weixin.qq.com/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("企业微信", "https://work.weixin.qq.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "飞书",
                Description = "字节跳动出品的一站式企业协作平台。",
                Category = "办公软件",
                Version = "6.8.17",
                Developer = "字节跳动",
                WebsiteUrl = "https://www.feishu.cn/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("飞书", "https://www.feishu.cn/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "WPS Office",
                Description = "金山办公出品的办公软件套装，兼容Microsoft Office格式。",
                Category = "办公软件",
                Version = "11.1.0",
                Developer = "金山办公",
                WebsiteUrl = "https://www.wps.cn/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("WPS Office", "https://www.wps.cn/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Notion",
                Description = "功能强大的笔记和协作工具，支持数据库、看板等多种视图。",
                Category = "办公软件",
                Version = "网页版",
                Developer = "Notion Labs",
                WebsiteUrl = "https://www.notion.so/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Notion", "https://www.notion.so/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "XMind",
                Description = "专业的思维导图和头脑风暴软件。",
                Category = "办公软件",
                Version = "23.11",
                Developer = "XMind Ltd.",
                WebsiteUrl = "https://www.xmind.cn/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("XMind", "https://www.xmind.cn/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "有道云笔记",
                Description = "网易出品的云笔记应用，支持多平台同步。",
                Category = "办公软件",
                Version = "7.2.5",
                Developer = "网易",
                WebsiteUrl = "https://note.youdao.com/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("有道云笔记", "https://note.youdao.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "石墨文档",
                Description = "协作文档工具，支持多人实时编辑。",
                Category = "办公软件",
                Version = "网页版",
                Developer = "石墨文档",
                WebsiteUrl = "https://shimo.im/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("石墨文档", "https://shimo.im/"))
            });

            // 常用通讯社交软件
            _allAppsList.Add(new AppStoreItem
            {
                Name = "微信",
                Description = "腾讯出品的即时通讯软件，支持文字、语音、视频聊天。",
                Category = "通讯社交",
                Version = "3.9.8",
                Developer = "腾讯",
                WebsiteUrl = "https://windows.weixin.qq.com/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("微信", "https://windows.weixin.qq.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "QQ",
                Description = "腾讯出品的即时通讯软件，支持群聊、文件传输等功能。",
                Category = "通讯社交",
                Version = "9.7.8",
                Developer = "腾讯",
                WebsiteUrl = "https://im.qq.com/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("QQ", "https://im.qq.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Telegram",
                Description = "注重隐私的即时通讯应用，支持端到端加密。",
                Category = "通讯社交",
                Version = "4.11.3",
                Developer = "Telegram Messenger LLP",
                WebsiteUrl = "https://telegram.org/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Telegram", "https://telegram.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Discord",
                Description = "面向游戏玩家和社区的语音、视频和文字聊天应用。",
                Category = "通讯社交",
                Version = "1.0.9007",
                Developer = "Discord Inc.",
                WebsiteUrl = "https://discord.com/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Discord", "https://discord.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Slack",
                Description = "企业级团队协作工具，支持频道、文件共享等功能。",
                Category = "通讯社交",
                Version = "4.32.122",
                Developer = "Slack Technologies",
                WebsiteUrl = "https://slack.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Slack", "https://slack.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Zoom",
                Description = "高质量的视频会议和在线会议服务。",
                Category = "通讯社交",
                Version = "5.16.6",
                Developer = "Zoom Video Communications",
                WebsiteUrl = "https://zoom.us/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Zoom", "https://zoom.us/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Skype",
                Description = "微软出品的视频通话和即时通讯应用。",
                Category = "通讯社交",
                Version = "8.104.0.205",
                Developer = "Microsoft",
                WebsiteUrl = "https://www.skype.com/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Skype", "https://www.skype.com/"))
            });

            // 常用图形图像软件
            _allAppsList.Add(new AppStoreItem
            {
                Name = "Photoshop",
                Description = "Adobe出品的专业的图像编辑软件。",
                Category = "图形图像",
                Version = "2024",
                Developer = "Adobe",
                WebsiteUrl = "https://www.adobe.com/products/photoshop.html",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Photoshop", "https://www.adobe.com/products/photoshop.html"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "GIMP",
                Description = "免费开源的图像编辑软件，功能强大。",
                Category = "图形图像",
                Version = "2.10.34",
                Developer = "GIMP Team",
                WebsiteUrl = "https://www.gimp.org/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("GIMP", "https://www.gimp.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Paint.NET",
                Description = "免费易用的图像和照片编辑软件。",
                Category = "图形图像",
                Version = "5.0.10",
                Developer = "dotPDN LLC",
                WebsiteUrl = "https://www.getpaint.net/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Paint.NET", "https://www.getpaint.net/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Inkscape",
                Description = "免费开源的矢量图形编辑器，类似于Adobe Illustrator。",
                Category = "图形图像",
                Version = "1.3.2",
                Developer = "Inkscape Team",
                WebsiteUrl = "https://inkscape.org/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Inkscape", "https://inkscape.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Blender",
                Description = "免费开源的3D创作套件，支持建模、动画、渲染等功能。",
                Category = "图形图像",
                Version = "4.1.1",
                Developer = "Blender Foundation",
                WebsiteUrl = "https://www.blender.org/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Blender", "https://www.blender.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Krita",
                Description = "免费开源的数字绘画软件，专为概念艺术家和插画师设计。",
                Category = "图形图像",
                Version = "5.2.1",
                Developer = "Krita Foundation",
                WebsiteUrl = "https://krita.org/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Krita", "https://krita.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "DaVinci Resolve",
                Description = "专业的视频编辑和调色软件，提供免费版本。",
                Category = "图形图像",
                Version = "18.6",
                Developer = "Blackmagic Design",
                WebsiteUrl = "https://www.blackmagicdesign.com/products/davinciresolve",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("DaVinci Resolve", "https://www.blackmagicdesign.com/products/davinciresolve"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "OBS Studio",
                Description = "免费开源的视频录制和直播软件。",
                Category = "图形图像",
                Version = "30.1.2",
                Developer = "OBS Project",
                WebsiteUrl = "https://obsproject.com/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("OBS Studio", "https://obsproject.com/"))
            });

            // 常用系统工具
            _allAppsList.Add(new AppStoreItem
            {
                Name = "7-Zip",
                Description = "免费开源的压缩/解压缩软件，支持多种格式。",
                Category = "系统工具",
                Version = "23.01",
                Developer = "Igor Pavlov",
                WebsiteUrl = "https://www.7-zip.org/",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("7-Zip", "https://www.7-zip.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "WinRAR",
                Description = "强大的压缩/解压缩软件，支持RAR格式。",
                Category = "系统工具",
                Version = "6.24",
                Developer = "win.rar GmbH",
                WebsiteUrl = "https://www.win-rar.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("WinRAR", "https://www.win-rar.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "CCleaner",
                Description = "系统清理和优化工具，可以清理临时文件和注册表。",
                Category = "系统工具",
                Version = "6.17",
                Developer = "Piriform",
                WebsiteUrl = "https://www.ccleaner.com/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("CCleaner", "https://www.ccleaner.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Defraggler",
                Description = "免费的磁盘碎片整理工具。",
                Category = "系统工具",
                Version = "2.35",
                Developer = "Piriform",
                WebsiteUrl = "https://www.ccleaner.com/defraggler",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Defraggler", "https://www.ccleaner.com/defraggler"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Recuva",
                Description = "免费的文件恢复工具，可以恢复已删除的文件。",
                Category = "系统工具",
                Version = "1.72",
                Developer = "Piriform",
                WebsiteUrl = "https://www.ccleaner.com/recuva",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Recuva", "https://www.ccleaner.com/recuva"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Speccy",
                Description = "免费的系统信息工具，可以查看详细的硬件信息。",
                Category = "系统工具",
                Version = "1.32.804",
                Developer = "Piriform",
                WebsiteUrl = "https://www.ccleaner.com/speccy",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Speccy", "https://www.ccleaner.com/speccy"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Process Lasso",
                Description = "系统优化和自动化工具，可以优化CPU响应速度。",
                Category = "系统工具",
                Version = "12.4.2.44",
                Developer = "Bitsum Technologies",
                WebsiteUrl = "https://bitsum.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Process Lasso", "https://bitsum.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "HWMonitor",
                Description = "硬件监控工具，可以查看CPU、主板、显卡等硬件状态。",
                Category = "系统工具",
                Version = "1.52",
                Developer = "CPUID",
                WebsiteUrl = "https://www.cpuid.com/softwares/hwmonitor.html",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("HWMonitor", "https://www.cpuid.com/softwares/hwmonitor.html"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "CrystalDiskInfo",
                Description = "硬盘健康状态监测工具，支持HDD/SSD。",
                Category = "系统工具",
                Version = "8.17.11",
                Developer = "Crystal Dew World",
                WebsiteUrl = "https://crystalmark.info/en/software/crystaldiskinfo/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("CrystalDiskInfo", "https://crystalmark.info/en/software/crystaldiskinfo/"))
            });

            // 常用多媒体软件
            _allAppsList.Add(new AppStoreItem
            {
                Name = "VLC Media Player",
                Description = "免费开源的多媒体播放器，支持几乎所有音视频格式。",
                Category = "多媒体",
                Version = "3.0.20",
                Developer = "VideoLAN",
                WebsiteUrl = "https://www.videolan.org/vlc/",
                Rating = 4.8,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("VLC Media Player", "https://www.videolan.org/vlc/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "PotPlayer",
                Description = "功能强大的多媒体播放器，支持硬件加速和字幕。",
                Category = "多媒体",
                Version = "1.7.21523",
                Developer = "Kakao Corp.",
                WebsiteUrl = "https://potplayer.daum.net/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("PotPlayer", "https://potplayer.daum.net/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "foobar2000",
                Description = "高级音频播放器，支持多种音频格式和插件。",
                Category = "多媒体",
                Version = "2.0",
                Developer = "Peter Pawlowski",
                WebsiteUrl = "https://www.foobar2000.org/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("foobar2000", "https://www.foobar2000.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "AIMP",
                Description = "免费的音乐播放器，支持多种音频格式和插件。",
                Category = "多媒体",
                Version = "5.11",
                Developer = "AIMP DevTeam",
                WebsiteUrl = "https://www.aimp.ru/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("AIMP", "https://www.aimp.ru/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "MusicBee",
                Description = "功能丰富的音乐管理器和播放器。",
                Category = "多媒体",
                Version = "3.4.8033",
                Developer = "Steven Mayall",
                WebsiteUrl = "https://getmusicbee.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("MusicBee", "https://getmusicbee.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Audacity",
                Description = "免费开源的音频编辑软件，支持录音和编辑。",
                Category = "多媒体",
                Version = "3.4.2",
                Developer = "Audacity Team",
                WebsiteUrl = "https://www.audacityteam.org/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Audacity", "https://www.audacityteam.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "HandBrake",
                Description = "免费开源的视频转码工具，支持多种格式。",
                Category = "多媒体",
                Version = "1.7.2",
                Developer = "HandBrake Team",
                WebsiteUrl = "https://handbrake.fr/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("HandBrake", "https://handbrake.fr/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "K-Lite Codec Pack",
                Description = "全面的编解码器集合，支持播放各种音视频格式。",
                Category = "多媒体",
                Version = "17.9.0",
                Developer = "Codec Guide",
                WebsiteUrl = "https://codecguide.com/download_kl.htm",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("K-Lite Codec Pack", "https://codecguide.com/download_kl.htm"))
            });

            // 常用安全软件
            _allAppsList.Add(new AppStoreItem
            {
                Name = "Malwarebytes",
                Description = "专业的反恶意软件工具，可以检测和移除恶意软件。",
                Category = "安全软件",
                Version = "4.5.5",
                Developer = "Malwarebytes Inc.",
                WebsiteUrl = "https://www.malwarebytes.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Malwarebytes", "https://www.malwarebytes.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "AdwCleaner",
                Description = "免费的广告软件和恶意软件清理工具。",
                Category = "安全软件",
                Version = "8.4.0",
                Developer = "Malwarebytes Inc.",
                WebsiteUrl = "https://www.malwarebytes.com/adwcleaner/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("AdwCleaner", "https://www.malwarebytes.com/adwcleaner/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "VeraCrypt",
                Description = "免费开源的磁盘加密软件，可以创建加密的虚拟磁盘。",
                Category = "安全软件",
                Version = "1.26.7",
                Developer = "IDRIX",
                WebsiteUrl = "https://www.veracrypt.fr/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("VeraCrypt", "https://www.veracrypt.fr/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Bitwarden",
                Description = "免费开源的密码管理器，支持多平台同步。",
                Category = "安全软件",
                Version = "2024.6.2",
                Developer = "Bitwarden Inc.",
                WebsiteUrl = "https://bitwarden.com/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Bitwarden", "https://bitwarden.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "KeePass",
                Description = "免费开源的密码管理器，支持强密码生成和数据库加密。",
                Category = "安全软件",
                Version = "2.55",
                Developer = "Dominik Reichl",
                WebsiteUrl = "https://keepass.info/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("KeePass", "https://keepass.info/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Windows Terminal",
                Description = "微软出品的现代化终端工具，支持多标签页和自定义。",
                Category = "系统工具",
                Version = "1.19.2775",
                Developer = "Microsoft",
                WebsiteUrl = "https://learn.microsoft.com/en-us/windows/terminal/",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Windows Terminal", "https://learn.microsoft.com/en-us/windows/terminal/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "PowerToys",
                Description = "微软出品的系统实用工具集，提供多种增强功能。",
                Category = "系统工具",
                Version = "0.79.1",
                Developer = "Microsoft",
                WebsiteUrl = "https://learn.microsoft.com/en-us/windows/powertoys/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("PowerToys", "https://learn.microsoft.com/en-us/windows/powertoys/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "ShareX",
                Description = "免费开源的截图和屏幕录制工具，支持多种分享方式。",
                Category = "系统工具",
                Version = "16.0.0",
                Developer = "ShareX Team",
                WebsiteUrl = "https://getsharex.com/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("ShareX", "https://getsharex.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Everything",
                Description = "高速文件搜索工具，可以实时搜索文件名。",
                Category = "系统工具",
                Version = "1.4.1.1024",
                Developer = "voidtools",
                WebsiteUrl = "https://www.voidtools.com/",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Everything", "https://www.voidtools.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "SpaceSniffer",
                Description = "磁盘空间分析工具，可以可视化显示文件夹大小。",
                Category = "系统工具",
                Version = "1.3.0.2",
                Developer = "Uderzo Software",
                WebsiteUrl = "https://www.uderzo.it/main_products/space_sniffer/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("SpaceSniffer", "https://www.uderzo.it/main_products/space_sniffer/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "WizTree",
                Description = "高速磁盘空间分析工具，可以快速找到占用空间最大的文件。",
                Category = "系统工具",
                Version = "4.18",
                Developer = "DiskAnalyzer",
                WebsiteUrl = "https://www.diskanalyzer.com/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("WizTree", "https://www.diskanalyzer.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "AutoHotkey",
                Description = "免费的自动化脚本语言，可以创建热键和自动化任务。",
                Category = "系统工具",
                Version = "2.0.10",
                Developer = "AutoHotkey Foundation",
                WebsiteUrl = "https://www.autohotkey.com/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("AutoHotkey", "https://www.autohotkey.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "EarTrumpet",
                Description = "现代化的音量控制工具，可以单独控制每个应用的音量。",
                Category = "系统工具",
                Version = "2.2.0.0",
                Developer = "EarTrumpet Team",
                WebsiteUrl = "https://eartrumpet.app/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("EarTrumpet", "https://eartrumpet.app/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Rainmeter",
                Description = "免费的桌面自定义工具，可以创建漂亮的桌面小部件。",
                Category = "系统工具",
                Version = "4.5.18",
                Developer = "Rainmeter Team",
                WebsiteUrl = "https://www.rainmeter.net/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Rainmeter", "https://www.rainmeter.net/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "f.lux",
                Description = "自动调整屏幕色温的工具，可以减少蓝光对眼睛的伤害。",
                Category = "系统工具",
                Version = "4.130",
                Developer = "justgetflux",
                WebsiteUrl = "https://justgetflux.com/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("f.lux", "https://justgetflux.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "EarTrumpet",
                Description = "现代化的音量控制工具，可以单独控制每个应用的音量。",
                Category = "系统工具",
                Version = "2.2.0.0",
                Developer = "EarTrumpet Team",
                WebsiteUrl = "https://eartrumpet.app/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("EarTrumpet", "https://eartrumpet.app/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Rainmeter",
                Description = "免费的桌面自定义工具，可以创建漂亮的桌面小部件。",
                Category = "系统工具",
                Version = "4.5.18",
                Developer = "Rainmeter Team",
                WebsiteUrl = "https://www.rainmeter.net/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Rainmeter", "https://www.rainmeter.net/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Ditto",
                Description = "功能强大的剪贴板管理工具，支持剪贴板历史记录。",
                Category = "系统工具",
                Version = "3.24.214.0",
                Developer = "Scott Brogden",
                WebsiteUrl = "https://ditto-cp.sourceforge.io/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Ditto", "https://ditto-cp.sourceforge.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "QuickLook",
                Description = "类似于macOS的快速预览工具，可以按空格键预览文件。",
                Category = "系统工具",
                Version = "3.7.1000",
                Developer = "QL-Win",
                WebsiteUrl = "https://github.com/QL-Win/QuickLook",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("QuickLook", "https://github.com/QL-Win/QuickLook"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Files",
                Description = "现代化的文件管理器，支持标签页和多窗格。",
                Category = "系统工具",
                Version = "3.1.2.0",
                Developer = "files-community",
                WebsiteUrl = "https://files.community/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Files", "https://files.community/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "FendOSPE",
                Description = "群友制作的PE系统，支持系统维护、修复和安装。",
                Category = "系统工具",
                Version = "1.0.0",
                Developer = "FendOSPE Team",
                WebsiteUrl = "https://fendospext.mysxl.cn",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("FendOSPE", "https://fendospext.mysxl.cn"))
            });

            // 第一批新增应用 - 开发工具类
            _allAppsList.Add(new AppStoreItem
            {
                Name = "Visual Studio Code",
                Description = "轻量级但功能强大的源代码编辑器，支持多种编程语言。",
                Category = "开发工具",
                Version = "1.85.0",
                Developer = "Microsoft",
                WebsiteUrl = "https://code.visualstudio.com/",
                Rating = 4.8,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Visual Studio Code", "https://code.visualstudio.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Sublime Text",
                Description = "快速、功能丰富的文本编辑器，支持多种编程语言和标记语言。",
                Category = "开发工具",
                Version = "4.0",
                Developer = "Sublime HQ",
                WebsiteUrl = "https://www.sublimetext.com/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Sublime Text", "https://www.sublimetext.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Notepad++",
                Description = "免费开源的代码编辑器，支持多种语言和语法高亮。",
                Category = "开发工具",
                Version = "8.5.8",
                Developer = "Don Ho",
                WebsiteUrl = "https://notepad-plus-plus.org/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Notepad++", "https://notepad-plus-plus.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Atom",
                Description = "GitHub开发的可定制文本编辑器，支持多种插件和主题。",
                Category = "开发工具",
                Version = "1.63.1",
                Developer = "GitHub",
                WebsiteUrl = "https://atom.io/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Atom", "https://atom.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Brackets",
                Description = "Adobe开发的现代文本编辑器，专为Web开发设计。",
                Category = "开发工具",
                Version = "2.0.1",
                Developer = "Adobe",
                WebsiteUrl = "http://brackets.io/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Brackets", "http://brackets.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "JetBrains Rider",
                Description = "跨平台.NET IDE，支持C#、VB.NET和其他.NET语言。",
                Category = "开发工具",
                Version = "2023.3.2",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/rider/",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("JetBrains Rider", "https://www.jetbrains.com/rider/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Android Studio",
                Description = "Google官方Android开发IDE，基于IntelliJ IDEA。",
                Category = "开发工具",
                Version = "2023.2.1",
                Developer = "Google",
                WebsiteUrl = "https://developer.android.com/studio",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Android Studio", "https://developer.android.com/studio"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Xcode",
                Description = "Apple官方iOS和macOS应用开发IDE。",
                Category = "开发工具",
                Version = "15.0",
                Developer = "Apple",
                WebsiteUrl = "https://developer.apple.com/xcode/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Xcode", "https://developer.apple.com/xcode/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Eclipse",
                Description = "开源的集成开发环境，主要用于Java开发。",
                Category = "开发工具",
                Version = "2023-12",
                Developer = "Eclipse Foundation",
                WebsiteUrl = "https://www.eclipse.org/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Eclipse", "https://www.eclipse.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "NetBeans",
                Description = "Oracle开源的集成开发环境，支持多种编程语言。",
                Category = "开发工具",
                Version = "18",
                Developer = "Apache Software Foundation",
                WebsiteUrl = "https://netbeans.apache.org/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("NetBeans", "https://netbeans.apache.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "PyCharm",
                Description = "JetBrains开发的Python IDE，提供智能代码补全和调试功能。",
                Category = "开发工具",
                Version = "2023.3.2",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/pycharm/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("PyCharm", "https://www.jetbrains.com/pycharm/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "WebStorm",
                Description = "JetBrains开发的JavaScript和Web开发IDE。",
                Category = "开发工具",
                Version = "2023.3.2",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/webstorm/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("WebStorm", "https://www.jetbrains.com/webstorm/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "IntelliJ IDEA",
                Description = "JetBrains开发的Java IDE，支持多种JVM语言。",
                Category = "开发工具",
                Version = "2023.3.2",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/idea/",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("IntelliJ IDEA", "https://www.jetbrains.com/idea/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "CLion",
                Description = "JetBrains开发的C和C++跨平台IDE。",
                Category = "开发工具",
                Version = "2023.3.2",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/clion/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("CLion", "https://www.jetbrains.com/clion/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "DataGrip",
                Description = "JetBrains开发的数据库管理和SQL开发工具。",
                Category = "开发工具",
                Version = "2023.3.2",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/datagrip/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("DataGrip", "https://www.jetbrains.com/datagrip/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "GoLand",
                Description = "JetBrains开发的Go语言IDE。",
                Category = "开发工具",
                Version = "2023.3.2",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/go/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("GoLand", "https://www.jetbrains.com/go/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "PhpStorm",
                Description = "JetBrains开发的PHP和Web开发IDE。",
                Category = "开发工具",
                Version = "2023.3.2",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/phpstorm/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("PhpStorm", "https://www.jetbrains.com/phpstorm/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "RubyMine",
                Description = "JetBrains开发的Ruby和Rails IDE。",
                Category = "开发工具",
                Version = "2023.3.2",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/ruby/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("RubyMine", "https://www.jetbrains.com/ruby/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "AppCode",
                Description = "JetBrains开发的Objective-C/Swift IDE，用于iOS和macOS开发。",
                Category = "开发工具",
                Version = "2023.3",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/objc/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("AppCode", "https://www.jetbrains.com/objc/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Rider",
                Description = "JetBrains开发的.NET跨平台IDE。",
                Category = "开发工具",
                Version = "2023.3.2",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/rider/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Rider", "https://www.jetbrains.com/rider/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Arduino IDE",
                Description = "Arduino官方开发环境，用于编写和上传代码到Arduino板。",
                Category = "开发工具",
                Version = "2.2.1",
                Developer = "Arduino",
                WebsiteUrl = "https://www.arduino.cc/en/software",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Arduino IDE", "https://www.arduino.cc/en/software"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Processing",
                Description = "灵活的软件草图本，用于学习如何在视觉上下文中编码。",
                Category = "开发工具",
                Version = "4.2",
                Developer = "Processing Foundation",
                WebsiteUrl = "https://processing.org/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Processing", "https://processing.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "BlueJ",
                Description = "为初学者设计的Java IDE，提供可视化交互环境。",
                Category = "开发工具",
                Version = "5.2.1",
                Developer = "University of Kent",
                WebsiteUrl = "https://www.bluej.org/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("BlueJ", "https://www.bluej.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "JGrasp",
                Description = "轻量级Java IDE，提供软件可视化功能，帮助理解程序运行。",
                Category = "开发工具",
                Version = "2.0.6",
                Developer = "Auburn University",
                WebsiteUrl = "https://www.jgrasp.org/",
                Rating = 3.9,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("JGrasp", "https://www.jgrasp.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Greenfoot",
                Description = "为教育目的设计的交互式Java开发环境。",
                Category = "开发工具",
                Version = "3.8.0",
                Developer = "University of Kent",
                WebsiteUrl = "https://www.greenfoot.org/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Greenfoot", "https://www.greenfoot.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "DrJava",
                Description = "轻量级Java IDE，专为教学设计。",
                Category = "开发工具",
                Version = "20220818",
                Developer = "Rice University",
                WebsiteUrl = "https://www.drjava.org/",
                Rating = 3.8,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("DrJava", "https://www.drjava.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "JCreator",
                Description = "轻量级Java IDE，提供快速开发和调试功能。",
                Category = "开发工具",
                Version = "5.10",
                Developer = "Xinox Software",
                WebsiteUrl = "https://www.jcreator.com/",
                Rating = 3.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("JCreator", "https://www.jcreator.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Code::Blocks",
                Description = "免费开源的C/C++ IDE，支持多种编译器。",
                Category = "开发工具",
                Version = "20.03",
                Developer = "Code::Blocks Team",
                WebsiteUrl = "http://www.codeblocks.org/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Code::Blocks", "http://www.codeblocks.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Dev-C++",
                Description = "免费开源的C/C++ IDE，使用GCC编译器。",
                Category = "开发工具",
                Version = "5.11",
                Developer = "Bloodshed Software",
                WebsiteUrl = "https://sourceforge.net/projects/orwelldevcpp/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Dev-C++", "https://sourceforge.net/projects/orwelldevcpp/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "CodeLite",
                Description = "免费开源的跨平台C/C++/PHP/Node.js IDE。",
                Category = "开发工具",
                Version = "17.0.0",
                Developer = "Eran Ifrah",
                WebsiteUrl = "https://codelite.org/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("CodeLite", "https://codelite.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Geany",
                Description = "轻量级GTK+文本编辑器，具有IDE功能。",
                Category = "开发工具",
                Version = "2.0",
                Developer = "Geany contributors",
                WebsiteUrl = "https://www.geany.org/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Geany", "https://www.geany.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "KDevelop",
                Description = "免费开源的跨平台IDE，支持多种编程语言。",
                Category = "开发工具",
                Version = "5.6.2",
                Developer = "KDevelop Team",
                WebsiteUrl = "https://kdevelop.org/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("KDevelop", "https://kdevelop.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Qt Creator",
                Description = "跨平台Qt应用程序IDE，集成Qt设计器和UI开发工具。",
                Category = "开发工具",
                Version = "12.0.1",
                Developer = "The Qt Company",
                WebsiteUrl = "https://www.qt.io/product/development-tools",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Qt Creator", "https://www.qt.io/product/development-tools"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "MonoDevelop",
                Description = "跨平台IDE，主要用于C#和其他.NET语言开发。",
                Category = "开发工具",
                Version = "7.8.4",
                Developer = "Mono Community",
                WebsiteUrl = "http://www.monodevelop.com/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("MonoDevelop", "http://www.monodevelop.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "SharpDevelop",
                Description = "免费开源的.NET IDE，支持C#、VB.NET和F#。",
                Category = "开发工具",
                Version = "5.1",
                Developer = "ICSharpCode Team",
                WebsiteUrl = "http://www.icsharpcode.net/opensource/sd/",
                Rating = 3.9,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("SharpDevelop", "http://www.icsharpcode.net/opensource/sd/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Lazarus",
                Description = "免费开源的跨平台Delphi兼容IDE。",
                Category = "开发工具",
                Version = "3.0",
                Developer = "Lazarus Team",
                WebsiteUrl = "https://www.lazarus-ide.org/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Lazarus", "https://www.lazarus-ide.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Free Pascal",
                Description = "免费开源的Pascal编译器，支持多种平台。",
                Category = "开发工具",
                Version = "3.2.2",
                Developer = "Free Pascal Team",
                WebsiteUrl = "https://www.freepascal.org/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Free Pascal", "https://www.freepascal.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "RStudio",
                Description = "R语言的集成开发环境，提供数据分析和可视化工具。",
                Category = "开发工具",
                Version = "2023.12.0",
                Developer = "Posit Software",
                WebsiteUrl = "https://www.rstudio.com/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("RStudio", "https://www.rstudio.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Spyder",
                Description = "Python科学计算IDE，集成数据分析工具。",
                Category = "开发工具",
                Version = "5.5.0",
                Developer = "Spyder Project Contributors",
                WebsiteUrl = "https://www.spyder-ide.org/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Spyder", "https://www.spyder-ide.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Jupyter Notebook",
                Description = "交互式计算环境，支持多种编程语言。",
                Category = "开发工具",
                Version = "7.0.0",
                Developer = "Project Jupyter",
                WebsiteUrl = "https://jupyter.org/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Jupyter Notebook", "https://jupyter.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "JupyterLab",
                Description = "Jupyter的下一代Web界面，提供更灵活的用户体验。",
                Category = "开发工具",
                Version = "4.0.9",
                Developer = "Project Jupyter",
                WebsiteUrl = "https://jupyterlab.readthedocs.io/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("JupyterLab", "https://jupyterlab.readthedocs.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Anaconda",
                Description = "Python和R的数据科学平台，包含常用包和环境管理。",
                Category = "开发工具",
                Version = "2023.09-0",
                Developer = "Anaconda Inc.",
                WebsiteUrl = "https://www.anaconda.com/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Anaconda", "https://www.anaconda.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Miniconda",
                Description = "Anaconda的最小安装程序，只包含conda和Python。",
                Category = "开发工具",
                Version = "23.9.0-0",
                Developer = "Anaconda Inc.",
                WebsiteUrl = "https://docs.conda.io/en/latest/miniconda.html",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Miniconda", "https://docs.conda.io/en/latest/miniconda.html"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Visual Studio Community",
                Description = "功能齐全的免费IDE，支持.NET、C++、Python等开发。",
                Category = "开发工具",
                Version = "2022 17.8.0",
                Developer = "Microsoft",
                WebsiteUrl = "https://visualstudio.microsoft.com/vs/community/",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Visual Studio Community", "https://visualstudio.microsoft.com/vs/community/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Visual Studio Professional",
                Description = "专业级IDE，提供高级开发工具和服务。",
                Category = "开发工具",
                Version = "2022 17.8.0",
                Developer = "Microsoft",
                WebsiteUrl = "https://visualstudio.microsoft.com/vs/professional/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Visual Studio Professional", "https://visualstudio.microsoft.com/vs/professional/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Visual Studio Enterprise",
                Description = "企业级IDE，提供最全面的开发工具和服务。",
                Category = "开发工具",
                Version = "2022 17.8.0",
                Developer = "Microsoft",
                WebsiteUrl = "https://visualstudio.microsoft.com/vs/enterprise/",
                Rating = 4.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Visual Studio Enterprise", "https://visualstudio.microsoft.com/vs/enterprise/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "SQL Server Management Studio",
                Description = "管理SQL Server基础设施的集成环境。",
                Category = "开发工具",
                Version = "19.2",
                Developer = "Microsoft",
                WebsiteUrl = "https://docs.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("SQL Server Management Studio", "https://docs.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "MySQL Workbench",
                Description = "MySQL数据库的统一可视化工具。",
                Category = "开发工具",
                Version = "8.0.34",
                Developer = "Oracle",
                WebsiteUrl = "https://www.mysql.com/products/workbench/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("MySQL Workbench", "https://www.mysql.com/products/workbench/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "pgAdmin",
                Description = "PostgreSQL的开源管理和开发平台。",
                Category = "开发工具",
                Version = "7.6",
                Developer = "pgAdmin Development Team",
                WebsiteUrl = "https://www.pgadmin.org/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("pgAdmin", "https://www.pgadmin.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "DBeaver",
                Description = "免费开源的通用数据库工具，支持多种数据库。",
                Category = "开发工具",
                Version = "23.3.0",
                Developer = "DBeaver Corp",
                WebsiteUrl = "https://dbeaver.io/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("DBeaver", "https://dbeaver.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "HeidiSQL",
                Description = "免费开源的MySQL、MariaDB、PostgreSQL和SQL Server客户端。",
                Category = "开发工具",
                Version = "12.6",
                Developer = "Ansgar Becker",
                WebsiteUrl = "https://www.heidisql.com/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("HeidiSQL", "https://www.heidisql.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "TablePlus",
                Description = "现代、原生且友好的数据库管理工具。",
                Category = "开发工具",
                Version = "5.4.4",
                Developer = "TablePlus Inc.",
                WebsiteUrl = "https://tableplus.com/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("TablePlus", "https://tableplus.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "DataGrip",
                Description = "JetBrains开发的跨平台数据库IDE。",
                Category = "开发工具",
                Version = "2023.3.2",
                Developer = "JetBrains",
                WebsiteUrl = "https://www.jetbrains.com/datagrip/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("DataGrip", "https://www.jetbrains.com/datagrip/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Redis Desktop Manager",
                Description = "Redis的跨平台GUI客户端。",
                Category = "开发工具",
                Version = "2023.11.0",
                Developer = "Redis Inc.",
                WebsiteUrl = "https://redis.com/redis-enterprise/redis-desktop-manager/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Redis Desktop Manager", "https://redis.com/redis-enterprise/redis-desktop-manager/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "MongoDB Compass",
                Description = "MongoDB的官方GUI客户端。",
                Category = "开发工具",
                Version = "1.39.0",
                Developer = "MongoDB Inc.",
                WebsiteUrl = "https://www.mongodb.com/products/compass",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("MongoDB Compass", "https://www.mongodb.com/products/compass"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Postman",
                Description = "API开发和测试平台，支持RESTful API和GraphQL。",
                Category = "开发工具",
                Version = "10.19.0",
                Developer = "Postman Inc.",
                WebsiteUrl = "https://www.postman.com/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Postman", "https://www.postman.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Insomnia",
                Description = "REST客户端和GraphQL IDE，用于API开发。",
                Category = "开发工具",
                Version = "8.6.0",
                Developer = "Kong Inc.",
                WebsiteUrl = "https://insomnia.rest/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Insomnia", "https://insomnia.rest/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "SoapUI",
                Description = "功能强大的API测试工具，支持SOAP和REST。",
                Category = "开发工具",
                Version = "5.7.2",
                Developer = "SmartBear Software",
                WebsiteUrl = "https://www.soapui.org/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("SoapUI", "https://www.soapui.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Swagger Editor",
                Description = "用于设计OpenAPI规范的基于浏览器的编辑器。",
                Category = "开发工具",
                Version = "3.18.0",
                Developer = "SmartBear Software",
                WebsiteUrl = "https://swagger.io/tools/swagger-editor/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Swagger Editor", "https://swagger.io/tools/swagger-editor/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "GitKraken",
                Description = "强大而优雅的Git图形用户界面。",
                Category = "开发工具",
                Version = "9.10.0",
                Developer = "Axosoft",
                WebsiteUrl = "https://www.gitkraken.com/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("GitKraken", "https://www.gitkraken.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "SourceTree",
                Description = "Atlassian开发的免费Git和Mercurial客户端。",
                Category = "开发工具",
                Version = "3.4.15",
                Developer = "Atlassian",
                WebsiteUrl = "https://www.sourcetreeapp.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("SourceTree", "https://www.sourcetreeapp.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "TortoiseGit",
                Description = "Git的Windows Shell扩展，提供图形界面。",
                Category = "开发工具",
                Version = "2.14.0",
                Developer = "TortoiseGit Team",
                WebsiteUrl = "https://tortoisegit.org/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("TortoiseGit", "https://tortoisegit.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "GitHub Desktop",
                Description = "GitHub官方的Git客户端，提供简单的图形界面。",
                Category = "开发工具",
                Version = "3.4.0",
                Developer = "GitHub",
                WebsiteUrl = "https://desktop.github.com/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("GitHub Desktop", "https://desktop.github.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Git Extensions",
                Description = "功能丰富的Git用户界面，集成到Windows资源管理器。",
                Category = "开发工具",
                Version = "4.2.0",
                Developer = "Git Extensions Team",
                WebsiteUrl = "https://gitextensions.github.io/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Git Extensions", "https://gitextensions.github.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "SmartGit",
                Description = "功能强大的Git客户端，支持GitHub、Bitbucket等。",
                Category = "开发工具",
                Version = "22.1.4",
                Developer = "syntevo",
                WebsiteUrl = "https://www.syntevo.com/smartgit/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("SmartGit", "https://www.syntevo.com/smartgit/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Beyond Compare",
                Description = "强大的文件和目录比较工具。",
                Category = "开发工具",
                Version = "4.4.7",
                Developer = "Scooter Software",
                WebsiteUrl = "https://www.scootersoftware.com/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Beyond Compare", "https://www.scootersoftware.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "WinMerge",
                Description = "开源的Windows文件和目录比较工具。",
                Category = "开发工具",
                Version = "2.16.28",
                Developer = "WinMerge Team",
                WebsiteUrl = "https://winmerge.org/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("WinMerge", "https://winmerge.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "KDiff3",
                Description = "文件和目录比较及合并工具，支持三向比较。",
                Category = "开发工具",
                Version = "1.10.5",
                Developer = "Joachim Eibl",
                WebsiteUrl = "https://kdiff3.sourceforge.net/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("KDiff3", "https://kdiff3.sourceforge.net/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Meld",
                Description = "可视化文件和目录比较工具，支持三向合并。",
                Category = "开发工具",
                Version = "3.22.0",
                Developer = "Kai Willadsen",
                WebsiteUrl = "https://meldmerge.org/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Meld", "https://meldmerge.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "P4Merge",
                Description = "Perforce的可视化合并工具，支持三向比较。",
                Category = "开发工具",
                Version = "2023.3",
                Developer = "Perforce Software",
                WebsiteUrl = "https://www.perforce.com/products/helix-core-apps/helix-visual-merge-p4merge",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("P4Merge", "https://www.perforce.com/products/helix-core-apps/helix-visual-merge-p4merge"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "DiffMerge",
                Description = "可视化文件比较和合并工具。",
                Category = "开发工具",
                Version = "4.2.0",
                Developer = "SourceGear",
                WebsiteUrl = "https://sourcegear.com/diffmerge/",
                Rating = 3.9,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("DiffMerge", "https://sourcegear.com/diffmerge/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "ExamDiff",
                Description = "免费的可视化文件比较工具。",
                Category = "开发工具",
                Version = "1.9.0.2",
                Developer = "PrestoSoft",
                WebsiteUrl = "https://www.prestosoft.com/edp_examdiffpro.asp",
                Rating = 3.8,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("ExamDiff", "https://www.prestosoft.com/edp_examdiffpro.asp"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Code Compare",
                Description = "Visual Studio的文件比较工具，支持三向合并。",
                Category = "开发工具",
                Version = "4.2",
                Developer = "Devart",
                WebsiteUrl = "https://www.devart.com/codecompare/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Code Compare", "https://www.devart.com/codecompare/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Compare Suite",
                Description = "文件和文件夹比较工具，支持多种格式。",
                Category = "开发工具",
                Version = "9.0",
                Developer = "AKS-Labs",
                WebsiteUrl = "https://comparesuite.com/",
                Rating = 3.9,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Compare Suite", "https://comparesuite.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Guiffy",
                Description = "高级文件和目录比较工具，支持信任合并。",
                Category = "开发工具",
                Version = "11.2",
                Developer = "Guiffy Software",
                WebsiteUrl = "https://www.guiffy.com/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Guiffy", "https://www.guiffy.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Araxis Merge",
                Description = "专业级文件和文件夹比较及合并工具。",
                Category = "开发工具",
                Version = "2023.5956",
                Developer = "Araxis",
                WebsiteUrl = "https://araxis.com/merge",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Araxis Merge", "https://araxis.com/merge"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "DeltaWalker",
                Description = "跨平台文件和目录比较工具。",
                Category = "开发工具",
                Version = "2.5.6",
                Developer = "Deltopia Inc.",
                WebsiteUrl = "https://www.deltawalker.com/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("DeltaWalker", "https://www.deltawalker.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "UltraCompare",
                Description = "强大的文件/文件夹/文本比较工具。",
                Category = "开发工具",
                Version = "23.0.0.28",
                Developer = "IDM Computer Solutions",
                WebsiteUrl = "https://www.ultraedit.com/products/ultracompare.html",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("UltraCompare", "https://www.ultraedit.com/products/ultracompare.html"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "FileMerge",
                Description = "macOS自带的文件比较工具。",
                Category = "开发工具",
                Version = "7.3",
                Developer = "Apple",
                WebsiteUrl = "https://developer.apple.com/xcode/",
                Rating = 3.9,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("FileMerge", "https://developer.apple.com/xcode/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Diffuse",
                Description = "开源的图形化文件比较和合并工具。",
                Category = "开发工具",
                Version = "0.7.3",
                Developer = "Diffuse Developers",
                WebsiteUrl = "https://diffuse.sourceforge.net/",
                Rating = 3.7,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Diffuse", "https://diffuse.sourceforge.net/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "TkDiff",
                Description = "基于Tcl/Tk的文件比较工具。",
                Category = "开发工具",
                Version = "4.3",
                Developer = "John M. Kuhn",
                WebsiteUrl = "https://sourceforge.net/projects/tkdiff/",
                Rating = 3.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("TkDiff", "https://sourceforge.net/projects/tkdiff/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "xxdiff",
                Description = "基于Qt的图形化文件比较和合并工具。",
                Category = "开发工具",
                Version = "5.0",
                Developer = "Martin Blais",
                WebsiteUrl = "https://furius.ca/xxdiff/",
                Rating = 3.8,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("xxdiff", "https://furius.ca/xxdiff/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Vim",
                Description = "高度可配置的文本编辑器，支持多种编程语言。",
                Category = "开发工具",
                Version = "9.0",
                Developer = "Vim Community",
                WebsiteUrl = "https://www.vim.org/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Vim", "https://www.vim.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Emacs",
                Description = "可扩展的文本编辑器，支持多种编程语言。",
                Category = "开发工具",
                Version = "29.1",
                Developer = "GNU Project",
                WebsiteUrl = "https://www.gnu.org/software/emacs/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Emacs", "https://www.gnu.org/software/emacs/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Nano",
                Description = "简单易用的命令行文本编辑器。",
                Category = "开发工具",
                Version = "7.2",
                Developer = "GNU Project",
                WebsiteUrl = "https://www.nano-editor.org/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Nano", "https://www.nano-editor.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Micro",
                Description = "现代、直观的终端文本编辑器。",
                Category = "开发工具",
                Version = "2.0.11",
                Developer = "Zyed Idris",
                WebsiteUrl = "https://micro-editor.github.io/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Micro", "https://micro-editor.github.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Neovim",
                Description = "Vim的分支，专注于可扩展性和可用性。",
                Category = "开发工具",
                Version = "0.9.2",
                Developer = "Neovim Community",
                WebsiteUrl = "https://neovim.io/",
                Rating = 4.6,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Neovim", "https://neovim.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Kakoune",
                Description = "受Vim启发的代码编辑器，提供更直观的编辑模式。",
                Category = "开发工具",
                Version = "2023.08.01",
                Developer = "Kakoune Community",
                WebsiteUrl = "https://kakoune.org/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Kakoune", "https://kakoune.org/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Helix",
                Description = "受Kakoune和Neovim启发的现代终端编辑器。",
                Category = "开发工具",
                Version = "23.10",
                Developer = "Helix Editor",
                WebsiteUrl = "https://helix-editor.com/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Helix", "https://helix-editor.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Sublime Merge",
                Description = "Sublime Text团队开发的Git客户端。",
                Category = "开发工具",
                Version = "2.0.0",
                Developer = "Sublime HQ",
                WebsiteUrl = "https://www.sublimemerge.com/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Sublime Merge", "https://www.sublimemerge.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Lazygit",
                Description = "简单高效的Git终端UI。",
                Category = "开发工具",
                Version = "0.40.0",
                Developer = "Jesse Duffield",
                WebsiteUrl = "https://github.com/jesseduffield/lazygit",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Lazygit", "https://github.com/jesseduffield/lazygit"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Gitui",
                Description = "快速的Git终端UI。",
                Category = "开发工具",
                Version = "0.23.0",
                Developer = "extraymond",
                WebsiteUrl = "https://github.com/extraymond/gitui",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Gitui", "https://github.com/extraymond/gitui"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Fork",
                Description = "快速美观的Git客户端。",
                Category = "开发工具",
                Version = "2.22.0",
                Developer = "Danil Pristupov",
                WebsiteUrl = "https://git-fork.com/",
                Rating = 4.5,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Fork", "https://git-fork.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "GitAhead",
                Description = "功能丰富的Git图形界面。",
                Category = "开发工具",
                Version = "2.7.0",
                Developer = "G-Ahead Software",
                WebsiteUrl = "https://gitahead.github.io/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("GitAhead", "https://gitahead.github.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Cola",
                Description = "Git图形界面，支持多种Git操作。",
                Category = "开发工具",
                Version = "4.1.0",
                Developer = "David Aguilar",
                WebsiteUrl = "https://git-cola.github.io/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Cola", "https://git-cola.github.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "QGit",
                Description = "基于Qt的Git图形界面。",
                Category = "开发工具",
                Version = "2.9",
                Developer = "Marco Costalba",
                WebsiteUrl = "https://github.com/tibirna/qgit",
                Rating = 3.9,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("QGit", "https://github.com/tibirna/qgit"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Gitg",
                Description = "GNOME的Git图形界面。",
                Category = "开发工具",
                Version = "44",
                Developer = "Alberto Fanjul",
                WebsiteUrl = "https://wiki.gnome.org/Apps/Gitg",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Gitg", "https://wiki.gnome.org/Apps/Gitg"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Giggle",
                Description = "GTK+的Git图形界面。",
                Category = "开发工具",
                Version = "0.7",
                Developer = "Giggle Developers",
                WebsiteUrl = "https://wiki.gnome.org/Apps/Giggle",
                Rating = 3.8,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Giggle", "https://wiki.gnome.org/Apps/Giggle"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Git-Cola",
                Description = "功能强大的Git图形界面。",
                Category = "开发工具",
                Version = "4.1.0",
                Developer = "David Aguilar",
                WebsiteUrl = "https://git-cola.github.io/",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Git-Cola", "https://git-cola.github.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "GitEye",
                Description = "基于Eclipse的Git客户端。",
                Category = "开发工具",
                Version = "4.2.0",
                Developer = "Genuitec",
                WebsiteUrl = "https://www.genuitec.com/products/giteye/",
                Rating = 3.9,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("GitEye", "https://www.genuitec.com/products/giteye/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Git Extensions",
                Description = "功能丰富的Git用户界面，集成到Windows资源管理器。",
                Category = "开发工具",
                Version = "4.2.0",
                Developer = "Git Extensions Team",
                WebsiteUrl = "https://gitextensions.github.io/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Git Extensions", "https://gitextensions.github.io/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "GitKraken Glo",
                Description = "GitKraken团队的项目管理工具。",
                Category = "开发工具",
                Version = "1.7.1",
                Developer = "Axosoft",
                WebsiteUrl = "https://www.gitkraken.com/glo",
                Rating = 4.0,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("GitKraken Glo", "https://www.gitkraken.com/glo"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "GitUp",
                Description = "macOS的Git图形界面，提供直观的可视化。",
                Category = "开发工具",
                Version = "1.2.5",
                Developer = "GitUp",
                WebsiteUrl = "https://gitup.co/",
                Rating = 4.3,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("GitUp", "https://gitup.co/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "GitFinder",
                Description = "macOS的Git客户端，集成到Finder中。",
                Category = "开发工具",
                Version = "1.7.0",
                Developer = "GitFinder",
                WebsiteUrl = "https://gitfinder.com/",
                Rating = 4.1,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("GitFinder", "https://gitfinder.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Working Copy",
                Description = "iOS的Git客户端。",
                Category = "开发工具",
                Version = "5.6.1",
                Developer = "Anders Borum",
                WebsiteUrl = "https://workingcopyapp.com/",
                Rating = 4.4,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Working Copy", "https://workingcopyapp.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Git2Go",
                Description = "iOS的Git客户端，支持完整的Git功能。",
                Category = "开发工具",
                Version = "2.9.0",
                Developer = "Git2Go",
                WebsiteUrl = "https://git2go.com/",
                Rating = 4.2,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Git2Go", "https://git2go.com/"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "Pocket Git",
                Description = "Android的Git客户端。",
                Category = "开发工具",
                Version = "2.1.0",
                Developer = "Mugunth Kumar",
                WebsiteUrl = "https://play.google.com/store/apps/details?id=com.mugunthkl.pocketgit",
                Rating = 3.9,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("Pocket Git", "https://play.google.com/store/apps/details?id=com.mugunthkl.pocketgit"))
            });

            _allAppsList.Add(new AppStoreItem
            {
                Name = "AGit",
                Description = "Android的Git客户端。",
                Category = "开发工具",
                Version = "1.5.0",
                Developer = "AGit Team",
                WebsiteUrl = "https://github.com/phyzical/AGit",
                Rating = 3.8,
                IsInstalled = false,
                InstallCommand = new RelayCommand(async () => await OpenWebsite("AGit", "https://github.com/phyzical/AGit"))
            });

        }

        private async Task OpenWebsite(string appName, string websiteUrl)
        {
            try
            {
                await Launcher.LaunchUriAsync(new Uri(websiteUrl));
            }
            catch (Exception ex)
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "无法打开网站",
                    Content = $"无法打开 {appName} 的官方网站: {ex.Message}",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private async Task InstallApp(string appName)
        {
            var app = _apps.FirstOrDefault(a => a.Name == appName);
            if (app != null && !app.IsInstalled)
            {
                try
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "安装应用",
                        Content = $"正在安装 {appName}...",
                        XamlRoot = this.XamlRoot
                    };

                    var operation = dialog.ShowAsync();

                    await Task.Delay(2000);

                    app.IsInstalled = true;
                    dialog.Hide();

                    ContentDialog successDialog = new ContentDialog
                    {
                        Title = "安装成功",
                        Content = $"{appName} 已成功安装！",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await successDialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "安装失败",
                        Content = $"安装 {appName} 时出错: {ex.Message}",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.ToLowerInvariant();
            
            // 如果有搜索文本，从全部应用列表中搜索
            if (!string.IsNullOrEmpty(searchText))
            {
                var searchResults = _allAppsList.Where(app =>
                    app.Name.ToLowerInvariant().Contains(searchText) ||
                    app.Description.ToLowerInvariant().Contains(searchText) ||
                    app.Category.ToLowerInvariant().Contains(searchText));
                
                AppsGridView.ItemsSource = new ObservableCollection<AppStoreItem>(searchResults);
                
                // 异步加载搜索结果的图标
                foreach (var app in searchResults)
                {
                    if (app.IconImage == null && !app.IconLoadFailed)
                    {
                        _ = app.LoadIconAsync();
                    }
                }
            }
            else
            {
                // 如果搜索框为空，显示当前分类的应用
                AppsGridView.ItemsSource = _apps;
            }
        }

        private void CategoriesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoriesListView.SelectedItem is ListViewItem selectedItem && selectedItem.Tag is string category)
            {
                // 重置分页状态
                _currentPage = 0;
                _allAppsLoaded = false;
                _isLoading = false; // 确保重置加载状态
                _apps.Clear();
                _currentCategory = category; // 设置当前分类

                // 重置滚动位置到顶部
                if (AppsScrollViewer != null)
                {
                    _ = AppsScrollViewer.ChangeView(null, 0, null);
                }

                // 更新页码显示
                UpdatePageIndicator();

                if (category == "All")
                {
                    // 显示全部应用
                    if (AppsGridView != null)
                    {
                        AppsGridView.ItemsSource = _apps;
                    }
                    _ = LoadAppsAsync();
                }
                else
                {
                    // 过滤应用并存储到_filteredAppsList
                    _filteredAppsList = _allAppsList.Where(app => app.Category == category).ToList();

                    if (AppsGridView != null)
                    {
                        AppsGridView.ItemsSource = _apps;
                    }
                    _ = LoadAppsAsync();
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 开始异步加载第一页应用
            _ = LoadAppsAsync();
        }

        private void ShowLoadingProgress()
        {
            LoadingProgressBorder.Visibility = Visibility.Visible;
            LoadingProgressRing.IsActive = true;
            LoadingProgressText.Text = "加载应用中...";
        }

        private void UpdateLoadingProgress(int loaded, int total)
        {
            int totalPages = (int)Math.Ceiling((double)total / _pageSize);
            int currentPage = (int)Math.Ceiling((double)loaded / _pageSize);
            if (currentPage == 0) currentPage = 1; // 第一页显示为1

            LoadingProgressText.Text = $"加载应用中... {loaded}/{total} (第 {currentPage}/{totalPages} 页)";
        }

        // 滚动事件处理
        private void AppsScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (e.IsIntermediate)
                return;

            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null) return;
            
            var verticalOffset = scrollViewer.VerticalOffset;
            var maxVerticalOffset = scrollViewer.ScrollableHeight;

            // 确定当前应用列表的总数
            List<AppStoreItem> sourceList = _currentCategory == "All" ? _allAppsList : _filteredAppsList;

            // 当滚动到底部附近时，加载更多应用
            // 修改条件：确保sourceList不为null且有更多应用可以加载
            if (sourceList != null && verticalOffset >= maxVerticalOffset - 100 && !_allAppsLoaded && !_isLoading && _apps.Count < sourceList.Count)
            {
                _ = LoadAppsAsync();
            }
        }

        private void HideLoadingProgress()
        {
            LoadingProgressBorder.Visibility = Visibility.Collapsed;
            LoadingProgressRing.IsActive = false;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Func<Task>? _executeAsync;
        private readonly Action? _execute;
        private readonly Func<bool>? _canExecute;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object? parameter)
        {
            if (_executeAsync != null)
            {
                _executeAsync();
            }
            else
            {
                _execute?.Invoke();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}