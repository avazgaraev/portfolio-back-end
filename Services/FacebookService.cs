using Microsoft.Extensions.Options;
using Vertex.Settings;
using System.Text.Json;

namespace Slavyan.Services
{
    public class FacebookService
    {
        private readonly HttpClient _httpClient;
        private readonly MetaSettings _settings;

        public FacebookService(HttpClient httpClient, IOptions<MetaSettings> options)
        {
            _httpClient = httpClient;
            _settings = options.Value;
        }
        public async Task<string?> PostLinkAsync(string linkUrl, string message)
        {
            var endpoint = $"https://graph.facebook.com/v24.0/{_settings.PageId}/feed";

            var content = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("link", linkUrl),
        new KeyValuePair<string, string>("message", message),
        new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
    });

            var response = await _httpClient.PostAsync(endpoint, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("FB link post error: " + json);
                return null;
            }

            return JsonDocument.Parse(json).RootElement.GetProperty("id").GetString();
        }
        public async Task<string?> PostPhotoAsync(string imageUrl, string caption)
        {
            var endpoint = $"https://graph.facebook.com/v24.0/{_settings.PageId}/photos";

            var content = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("url", imageUrl),
        new KeyValuePair<string, string>("caption", caption),
        new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
    });

            var response = await _httpClient.PostAsync(endpoint, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("FB photo error: " + json);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // post_id varsa onu götür (permalink üçün daha uyğundur)
            if (root.TryGetProperty("post_id", out var postIdEl))
                return postIdEl.GetString();

            // yoxdursa id
            if (root.TryGetProperty("id", out var idEl))
                return idEl.GetString();

            return null;
        }

        public async Task<string?> PostVideoAsync(string videoUrl, string caption)
        {
            // Sadə variant: kiçik videolar üçün file_url ilə birbaşa upload
            var endpoint =
                $"https://graph.facebook.com/v24.0/{_settings.PageId}/videos";

            var content = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("file_url", videoUrl),
        new KeyValuePair<string, string>("description", caption),
        new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
    });

            var response = await _httpClient.PostAsync(endpoint, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("FB video error: " + json);
                return null;
            }

            var id = JsonDocument.Parse(json)
                .RootElement.GetProperty("id").GetString();

            return id;
        }

        public async Task<string?> PostMultiPhotoAsync(List<string> imageUrls, string caption)
        {
            if (imageUrls == null || imageUrls.Count == 0)
                return null;

            var uploadedPhotoIds = new List<string>();

            // 1) Hər şəkli "unpublished" media kimi yüklə
            foreach (var url in imageUrls)
            {
                var uploadEndpoint =
                    $"https://graph.facebook.com/v24.0/{_settings.PageId}/photos";

                var uploadContent = new FormUrlEncodedContent(new[]
                {
            new KeyValuePair<string, string>("url", url),
            new KeyValuePair<string, string>("published", "false"),
            new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
        });

                var uploadResponse = await _httpClient.PostAsync(uploadEndpoint, uploadContent);
                var uploadJson = await uploadResponse.Content.ReadAsStringAsync();

                if (!uploadResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("FB upload error: " + uploadJson);
                    return null;
                }

                var id = JsonDocument.Parse(uploadJson)
                    .RootElement.GetProperty("id").GetString();

                uploadedPhotoIds.Add(id);
            }

            // 2) Bütün şəkilləri bir postda paylaş
            var feedEndpoint =
                $"https://graph.facebook.com/v24.0/{_settings.PageId}/feed";

            var kvps = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("message", caption),
                new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
            };

            for (int i = 0; i < uploadedPhotoIds.Count; i++)
            {
                kvps.Add(new KeyValuePair<string, string>($"attached_media[{i}]",
                    "{\"media_fbid\":\"" + uploadedPhotoIds[i] + "\"}"));
            }

            var feedContent = new FormUrlEncodedContent(kvps);

            var feedResponse = await _httpClient.PostAsync(feedEndpoint, feedContent);
            var feedJson = await feedResponse.Content.ReadAsStringAsync();

            if (!feedResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("FB feed error: " + feedJson);
                return null;
            }

            var postId = JsonDocument.Parse(feedJson)
                .RootElement.GetProperty("id").GetString();

            return postId;
        }

        public async Task<string?> GetPermalinkAsync(string postOrMediaId)
        {
            var url =
                $"https://graph.facebook.com/v24.0/{postOrMediaId}" +
                "?fields=permalink_url" +
                $"&access_token={_settings.PageAccessToken}";

            var resp = await _httpClient.GetAsync(url);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine("FB permalink error: " + json);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("permalink_url", out var p))
                return null;

            var link = p.GetString();
            if (string.IsNullOrWhiteSpace(link))
                return null;

            // ✅ normalize: relative link -> full link
            if (!link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                link = link.TrimStart('/');
                link = "https://www.facebook.com/" + link;
            }

            return link;
        }

    }
}
