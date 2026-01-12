using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.UI.Xaml.Media.Imaging;

namespace XIGUASecurity.Services
{
    public class FaviconService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const int MaxRetries = 2;
        private const int TimeoutSeconds = 5;

        public static async Task<BitmapImage?> GetFaviconAsync(string websiteUrl)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    if (string.IsNullOrEmpty(websiteUrl))
                        return null;

                    var uri = new Uri(websiteUrl);
                    var baseUrl = $"{uri.Scheme}://{uri.Host}";
                    
                    // 尝试获取favicon.ico
                    var faviconUrl = $"{baseUrl}/favicon.ico";
                    var faviconImage = await TryLoadImageFromUrlAsync(faviconUrl);
                    if (faviconImage != null)
                        return faviconImage!;

                    // 尝试从HTML中解析图标链接
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
                    var htmlContent = await _httpClient.GetStringAsync(baseUrl, cts.Token);
                    var doc = XDocument.Parse(htmlContent);
                    
                    // 查找各种可能的图标链接
                    var iconSelectors = new[]
                    {
                        "//link[@rel='icon']",
                        "//link[@rel='shortcut icon']",
                        "//link[@rel='apple-touch-icon']",
                        "//link[@rel='apple-touch-icon-precomposed']"
                    };

                    foreach (var selector in iconSelectors)
                    {
                        var iconElement = doc.XPathSelectElement(selector);
                        if (iconElement != null)
                        {
                            var iconHref = iconElement.Attribute("href")?.Value;
                            if (!string.IsNullOrEmpty(iconHref))
                            {
                                // 处理相对路径
                                if (iconHref.StartsWith("/"))
                                    iconHref = baseUrl + iconHref;
                                else if (!iconHref.StartsWith("http"))
                                    iconHref = baseUrl + "/" + iconHref;

                                var iconImage = await TryLoadImageFromUrlAsync(iconHref);
                                if (iconImage != null)
                                    return iconImage!;
                            }
                        }
                    }

                    // 如果都失败了，返回null而不是默认图标
                    return null;
                }
                catch (Exception)
                {
                    // 如果不是最后一次尝试，等待一段时间后重试
                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(500 * attempt); // 减少递增延迟
                    }
                }
            }

            // 所有尝试都失败了
            return null;
        }

        private static async Task<BitmapImage?> TryLoadImageFromUrlAsync(string imageUrl)
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
                var response = await _httpClient.GetAsync(imageUrl, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var stream = await response.Content.ReadAsStreamAsync();
                    var bitmapImage = new BitmapImage();
                    using (var randomAccessStream = stream.AsRandomAccessStream())
                    {
                        await bitmapImage.SetSourceAsync(randomAccessStream);
                    }
                    return bitmapImage!;
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}