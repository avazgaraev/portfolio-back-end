using Microsoft.Extensions.Options;
using Vertex.Settings;
using System.Text.Json;

namespace Slavyan.Services
{
    public class InstagramService
    {
        private readonly HttpClient _httpClient;
        private readonly MetaSettings _settings;

        public InstagramService(HttpClient httpClient, IOptions<MetaSettings> options)
        {
            _httpClient = httpClient;
            _settings = options.Value;
        }

        public async Task<string?> PublishToInstagramAsync(string imageUrl, string caption)
        {
            // 1) Container yarat
            var containerEndpoint =
                $"https://graph.facebook.com/v24.0/{_settings.InstagramUserId}/media";

            var createContent = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("image_url", imageUrl),
            new KeyValuePair<string, string>("caption", caption),
            new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
        });

            var createResponse = await _httpClient.PostAsync(containerEndpoint, createContent);
            var createJson = await createResponse.Content.ReadAsStringAsync();

            if (!createResponse.IsSuccessStatusCode)
            {
                // burda logger də qoşa bilərsən
                Console.WriteLine("IG container error: " + createJson);
                return null;
            }

            var mediaCreationId = JsonDocument.Parse(createJson)
                .RootElement
                .GetProperty("id")
                .GetString();

            if (string.IsNullOrEmpty(mediaCreationId))
                return null;

            // 2) Container-in hazır olmağını gözlə
            var ready = await WaitForMediaReadyAsync(mediaCreationId);
            if (!ready)
            {
                Console.WriteLine("IG media is not ready.");
                return null;
            }

            // 3) Publish
            var publishEndpoint =
                $"https://graph.facebook.com/v24.0/{_settings.InstagramUserId}/media_publish";

            var publishContent = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("creation_id", mediaCreationId),
            new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
        });

            var publishResponse = await _httpClient.PostAsync(publishEndpoint, publishContent);
            var publishJson = await publishResponse.Content.ReadAsStringAsync();

            if (!publishResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("IG publish error: " + publishJson);
                return null;
            }

            var publishedId = JsonDocument.Parse(publishJson)
                .RootElement
                .GetProperty("id")
                .GetString();

            return publishedId;
        }

        public async Task<string?> PublishCarouselAsync(IReadOnlyList<string> imageUrls, string caption)
        {
            if (imageUrls == null || imageUrls.Count == 0)
                return null;

            var childIds = new List<string>();

            // 1) Hər şəkil üçün child container yarat (is_carousel_item = true)
            foreach (var imageUrl in imageUrls)
            {
                var createChildEndpoint =
                    $"https://graph.facebook.com/v24.0/{_settings.InstagramUserId}/media";

                var childContent = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("image_url", imageUrl),
                new KeyValuePair<string, string>("is_carousel_item", "true"),
                new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
            });

                var childResponse = await _httpClient.PostAsync(createChildEndpoint, childContent);
                var childJson = await childResponse.Content.ReadAsStringAsync();

                if (!childResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("IG child container error: " + childJson);
                    return null;
                }

                var childId = JsonDocument.Parse(childJson)
                    .RootElement
                    .GetProperty("id")
                    .GetString();

                if (!string.IsNullOrEmpty(childId))
                    childIds.Add(childId);
            }

            if (childIds.Count == 0)
                return null;

            // 2) Parent carousel container yarat (media_type=CAROUSEL + children)
            var parentEndpoint =
                $"https://graph.facebook.com/v24.0/{_settings.InstagramUserId}/media";

            var kvps = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("media_type", "CAROUSEL"),
            new KeyValuePair<string, string>("caption", caption),
            new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
        };

            for (int i = 0; i < childIds.Count; i++)
            {
                kvps.Add(new KeyValuePair<string, string>($"children[{i}]", childIds[i]));
            }

            var parentContent = new FormUrlEncodedContent(kvps);

            var parentResponse = await _httpClient.PostAsync(parentEndpoint, parentContent);
            var parentJson = await parentResponse.Content.ReadAsStringAsync();

            if (!parentResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("IG carousel parent error: " + parentJson);
                return null;
            }

            var parentCreationId = JsonDocument.Parse(parentJson)
                .RootElement
                .GetProperty("id")
                .GetString();

            if (string.IsNullOrEmpty(parentCreationId))
                return null;

            // 3) Carousel container-in hazır olmasını gözlə
            var ready = await WaitForMediaReadyAsync(parentCreationId);
            if (!ready)
            {
                Console.WriteLine("IG carousel media is not ready.");
                return null;
            }

            // 4) Parent container-i publish et
            var publishEndpoint =
                $"https://graph.facebook.com/v24.0/{_settings.InstagramUserId}/media_publish";

            var publishContent = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("creation_id", parentCreationId),
            new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
        });

            var publishResponse = await _httpClient.PostAsync(publishEndpoint, publishContent);
            var publishJson = await publishResponse.Content.ReadAsStringAsync();

            if (!publishResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("IG carousel publish error: " + publishJson);
                return null;
            }

            var publishedId = JsonDocument.Parse(publishJson)
                .RootElement
                .GetProperty("id")
                .GetString();

            return publishedId;
        }

        public async Task<string?> PublishVideoAsync(string videoUrl, string caption, string? coverUrl = null)
        {
            var containerEndpoint =
                $"https://graph.facebook.com/v24.0/{_settings.InstagramUserId}/media";

            var kvps = new List<KeyValuePair<string, string>>
    {
        new KeyValuePair<string, string>("media_type", "REELS"),
        new KeyValuePair<string, string>("video_url", videoUrl),
        new KeyValuePair<string, string>("caption", caption),
        new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
    };

            // 🔹 Əgər PageImgPath varsa, Reel cover kimi istifadə edirik
            if (!string.IsNullOrWhiteSpace(coverUrl))
            {
                kvps.Add(new KeyValuePair<string, string>("cover_url", coverUrl));
            }

            var createContent = new FormUrlEncodedContent(kvps);

            var createResponse = await _httpClient.PostAsync(containerEndpoint, createContent);
            var createJson = await createResponse.Content.ReadAsStringAsync();

            if (!createResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("IG video container error: " + createJson);
                return null;
            }

            var creationId = JsonDocument.Parse(createJson)
                .RootElement.GetProperty("id").GetString();

            if (string.IsNullOrEmpty(creationId))
                return null;

            // Burada səndə olan WaitForMediaReadyAsync (status_code = FINISHED
            var ready = await WaitForMediaReadyAsync(creationId);
            if (!ready)
            {
                Console.WriteLine("IG reel not ready.");
                return null;
            }

            var publishEndpoint =
                $"https://graph.facebook.com/v24.0/{_settings.InstagramUserId}/media_publish";

            var publishContent = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("creation_id", creationId),
        new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
    });

            var publishResponse = await _httpClient.PostAsync(publishEndpoint, publishContent);
            var publishJson = await publishResponse.Content.ReadAsStringAsync();

            if (!publishResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("IG reel publish error: " + publishJson);
                return null;
            }

            var publishedId = JsonDocument.Parse(publishJson)
                .RootElement.GetProperty("id").GetString();

            return publishedId;
        }

        // Səndə artıq bu helper var idi – eynisini istifadə edirik:
        private async Task<bool> WaitForMediaReadyAsync(string creationId)
        {
            const int maxChecks = 20;      // 20 dəfə (20 dəqiqə yox, 20 sorğu)
            const int delayMs = 80000;     // 80 saniyə gözləmə

            for (int i = 0; i < maxChecks; i++)
            {
                var url =
                    $"https://graph.facebook.com/v24.0/{creationId}" +
                    "?fields=status_code" +
                    $"&access_token={_settings.PageAccessToken}";

                var resp = await _httpClient.GetAsync(url);
                var json = await resp.Content.ReadAsStringAsync();

                Console.WriteLine("IG container status response: " + json);

                if (!resp.IsSuccessStatusCode)
                    return false;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var statusCode = root.GetProperty("status_code").GetString();

                if (statusCode == "FINISHED")
                    return true;

                if (statusCode == "ERROR")
                {
                    Console.WriteLine("IG container ERROR: " + json);
                    return false;
                }

                // IN_PROGRESS → 30 saniyə gözlə
                await Task.Delay(delayMs);
            }

            return false;
        }

        public async Task<string?> GetPermalinkAsync(string igMediaId)
        {
            var url =
                $"https://graph.facebook.com/v24.0/{igMediaId}" +
                "?fields=permalink" +
                $"&access_token={_settings.PageAccessToken}";

            var resp = await _httpClient.GetAsync(url);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine("IG permalink error: " + json);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("permalink", out var p))
                return p.GetString();

            return null;
        }
    }
}
