using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Storage;

namespace XIGUASecurity.Services
{
    public class AnnouncementService
    {
        private static AnnouncementService _instance;
        public static AnnouncementService Instance => _instance ??= new AnnouncementService();

        private readonly HttpClient _httpClient;
        private const string SETTINGS_KEY = "LastAnnouncementId";
        private const string DEFAULT_SERVER_URL = "http://103.118.245.82:4000/api/announcements";

        public string ServerUrl { get; set; } = DEFAULT_SERVER_URL;

        private AnnouncementService()
        {
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// 获取最新公告
        /// </summary>
        /// <returns>公告数据，如果没有新公告则返回null</returns>
        public async Task<Announcement> GetLatestAnnouncementAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{ServerUrl}/latest");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"获取到的JSON数据: {json}");
                var announcement = JsonSerializer.Deserialize<Announcement>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (announcement != null)
                {
                    System.Diagnostics.Debug.WriteLine($"公告标题: {announcement.Title}");
                    System.Diagnostics.Debug.WriteLine($"公告发布日期: {announcement.PublishDate}");
                }

                return announcement;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取公告失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 标记公告为已读
        /// </summary>
        /// <param name="announcementId">公告ID</param>
        public void MarkAsRead(string announcementId)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[SETTINGS_KEY] = announcementId;
        }

        /// <summary>
        /// 获取上次已读公告的ID
        /// </summary>
        /// <returns>公告ID</returns>
        private string GetLastReadAnnouncementId()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            return localSettings.Values.TryGetValue(SETTINGS_KEY, out var value) ? value.ToString() : null;
        }
    }

    public class Announcement
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        
        [JsonPropertyName("title")]
        public string Title { get; set; }
        
        [JsonPropertyName("content")]
        public string Content { get; set; }
        
        [JsonPropertyName("publish_date")]
        public string PublishDate { get; set; }
        
        [JsonPropertyName("is_important")]
        public bool IsImportant { get; set; }
    }
}