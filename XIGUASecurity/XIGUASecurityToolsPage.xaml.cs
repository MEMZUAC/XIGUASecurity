using Microsoft.UI.Xaml.Controls;
using System.Linq;
using XIGUASecurity.Services;

namespace XIGUASecurity
{
    public sealed partial class XIGUASecurityToolsPage : Page
    {
        public XIGUASecurityToolsPage()
        {
            InitializeComponent();
            LoadPlugins();
        }

        private void LoadPlugins()
        {
            try
            {
                var loader = new PluginLoader();
                var plugins = loader.LoadPlugins(this).ToList();
                foreach (var plugin in plugins)
                {
                    try
                    {
                        var tab = new TabViewItem
                        {
                            Header = new TextBlock
                            {
                                Text = plugin.Name ?? plugin.Metadata?.Name ?? plugin.Id,
                                FontSize = 14
                            },
                            IconSource = plugin.Icon,
                            IsClosable = false,
                            Content = plugin.GetView() ?? new TextBlock { Text = "(No View)" }
                        };
                        TabView.TabItems.Add(tab);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}