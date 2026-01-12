using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using XIGUASecurity.Services;

namespace XIGUASecurity.Models
{
    public class AppStoreItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private string _category = string.Empty;
        private string _iconPath = string.Empty;
        private BitmapImage? _iconImage;
        private string _version = string.Empty;
        private string _developer = string.Empty;
        private string _websiteUrl = string.Empty;
        private double _rating;
        private bool _isInstalled;
        private bool _iconLoadFailed;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        public string IconPath
        {
            get => _iconPath;
            set => SetProperty(ref _iconPath, value);
        }

        public BitmapImage? IconImage
        {
            get => _iconImage;
            set => SetProperty(ref _iconImage, value);
        }

        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        public string Developer
        {
            get => _developer;
            set => SetProperty(ref _developer, value);
        }

        public string WebsiteUrl
        {
            get => _websiteUrl;
            set => SetProperty(ref _websiteUrl, value);
        }

        public double Rating
        {
            get => _rating;
            set => SetProperty(ref _rating, value);
        }

        public bool IsInstalled
        {
            get => _isInstalled;
            set => SetProperty(ref _isInstalled, value);
        }

        public ICommand InstallCommand { get; set; } = new RelayCommand(async () => await Task.CompletedTask);

        public bool IconLoadFailed
        {
            get => _iconLoadFailed;
            set => SetProperty(ref _iconLoadFailed, value);
        }

        public async Task LoadIconAsync()
        {
            if (!string.IsNullOrEmpty(WebsiteUrl))
            {
                try
                {
                    // 添加超时控制，最多等待3秒
                    var iconTask = FaviconService.GetFaviconAsync(WebsiteUrl);
                    var completedTask = await Task.WhenAny(iconTask, Task.Delay(3000));
                    
                    if (completedTask == iconTask)
                    {
                        IconImage = iconTask.Result;
                        IconLoadFailed = IconImage == null;
                    }
                    else
                    {
                        // 超时处理
                        IconLoadFailed = true;
                    }
                }
                catch
                {
                    IconLoadFailed = true;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}