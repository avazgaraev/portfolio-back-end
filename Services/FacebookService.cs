using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using Vertex.Settings;

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

        // =========================
        // Public API (Timeline Posts)
        // =========================
        private async Task<bool?> GetIsHiddenAsync(string postId)
        {
            var endpoint =
                $"https://graph.facebook.com/v24.0/{postId}" +
                $"?fields=is_hidden&access_token={_settings.PageAccessToken}";

            var res = await _httpClient.GetAsync(endpoint);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine("FB GetIsHidden error: " + json);
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("is_hidden", out var h))
                    return h.GetBoolean();
            }
            catch (Exception ex)
            {
                Console.WriteLine("FB GetIsHidden parse error: " + ex.Message);
            }

            return null;
        }

        private async Task<bool> SetHiddenAsync(string postId, bool hidden)
        {
            var endpoint = $"https://graph.facebook.com/v24.0/{postId}";

            var content = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string,string>("is_hidden", hidden ? "true" : "false"),
        new KeyValuePair<string,string>("access_token", _settings.PageAccessToken),
    });

            var res = await _httpClient.PostAsync(endpoint, content);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine("FB SetHidden error: " + json);
                return false;
            }

            return true;
        }
        public async Task<string?> PostLinkToTimelineAsync(string linkUrl, string message)
        {
            // Link post /feed ilə timeline-a düşür
            var kvps = new List<KeyValuePair<string, string>>
            {
                new("link", linkUrl),
                new("message", message),
                new("access_token", _settings.PageAccessToken),
            };

            return await CreateFeedPostAsync(kvps);
        }

        public async Task<string?> PostSinglePhotoToTimelineAsync(string imageUrl, string caption)
        {
            // 1) Upload unpublished (media_fbid)
            var mediaFbid = await UploadPhotoUnpublishedAsync(imageUrl);
            if (string.IsNullOrWhiteSpace(mediaFbid))
                return null;

            // 2) /feed post (attached_media[0]) => real timeline post
            return await CreateFeedPostWithMediaAsync(new[] { mediaFbid }, caption);
        }

        public async Task<string?> PostMultiPhotoToTimelineAsync(IReadOnlyList<string> imageUrls, string caption)
        {
            if (imageUrls == null || imageUrls.Count == 0)
                return null;

            var mediaFbids = new List<string>();

            foreach (var url in imageUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
            {
                var mediaFbid = await UploadPhotoUnpublishedAsync(url);
                if (string.IsNullOrWhiteSpace(mediaFbid))
                    return null;

                mediaFbids.Add(mediaFbid);
            }

            if (mediaFbids.Count == 0)
                return null;

            return await CreateFeedPostWithMediaAsync(mediaFbids, caption);
        }

        public async Task<string?> PostVideoToTimelineAsync(string videoUrl, string caption)
        {
            // 1) Upload video (file_url) => returns video id
            var videoId = await UploadVideoAsync(videoUrl, caption);
            if (string.IsNullOrWhiteSpace(videoId))
                return null;

            // 2) Share to timeline via /feed (link to the video)
            // Bu yol timeline-da mütləq post yaratmağa kömək edir.
            // Video permalink bəzən dərhal hazır olmur; ona görə video URL-ini paylaşırıq.
            // İstəsən, aşağıda video permalink almağı da əlavə edə bilərik.
            var kvps = new List<KeyValuePair<string, string>>
            {
                new("message", caption),
                // Video linkini story kimi paylaşmaq üçün:
                new("link", $"https://www.facebook.com/{videoId}"),
                new("access_token", _settings.PageAccessToken),
            };

            return await CreateFeedPostAsync(kvps);
        }

        // =========================
        // Core Helpers
        // =========================

        /// <summary>
        /// Uploads a photo as unpublished media and returns media_fbid (photo id usable in attached_media)
        /// </summary>
        private async Task<string?> UploadPhotoUnpublishedAsync(string imageUrl)
        {
            var endpoint = $"https://graph.facebook.com/v24.0/{_settings.PageId}/photos";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("url", imageUrl),
                new KeyValuePair<string, string>("published", "false"),
                new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
            });

            var response = await _httpClient.PostAsync(endpoint, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("FB photo upload (unpublished) error: " + json);
                return null;
            }

            // Response: { "id": "MEDIA_FBID" }
            return GetJsonProp(json, "id");
        }

        /// <summary>
        /// Creates a /feed post with attached media and returns the created post id (pageid_postid)
        /// </summary>
        private async Task<string?> CreateFeedPostWithMediaAsync(IReadOnlyList<string> mediaFbids, string caption)
        {
            var kvps = new List<KeyValuePair<string, string>>
            {
                 new("message", caption ?? string.Empty),
                new("published", "true"), // ✅ əlavə et
                new("access_token", _settings.PageAccessToken),
            };

            for (int i = 0; i < mediaFbids.Count; i++)
            {
                // attached_media[i]={"media_fbid":"..."}
                kvps.Add(new KeyValuePair<string, string>(
                    $"attached_media[{i}]",
                    JsonSerializer.Serialize(new { media_fbid = mediaFbids[i] })
                ));
            }

            return await CreateFeedPostAsync(kvps);
        }

        /// <summary>
        /// Creates a /feed post with provided kvps and returns created post id
        /// </summary>
        /// 
        public async Task<(bool ok, bool? isHidden, string raw)> InspectHiddenAsync(string postId)
        {
            var endpoint =
                $"https://graph.facebook.com/v24.0/{postId}" +
                $"?fields=is_hidden,permalink_url&access_token={_settings.PageAccessToken}";

            var res = await _httpClient.GetAsync(endpoint);
            var raw = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode) return (false, null, raw);

            using var doc = JsonDocument.Parse(raw);
            bool? hidden = doc.RootElement.TryGetProperty("is_hidden", out var h) ? h.GetBoolean() : null;
            return (true, hidden, raw);
        }

        private async Task<string?> CreateFeedPostAsync(List<KeyValuePair<string, string>> kvps)
        {
            // ✅ published=true zorla (bəzən default olsa da, burada explicit edirik)
            if (!kvps.Any(x => x.Key == "published"))
                kvps.Add(new KeyValuePair<string, string>("published", "true"));

            var endpoint = $"https://graph.facebook.com/v24.0/{_settings.PageId}/feed";
            var response = await _httpClient.PostAsync(endpoint, new FormUrlEncodedContent(kvps));
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("FB feed post error: " + json);
                return null;
            }

            var postId = GetJsonProp(json, "id");
            if (string.IsNullOrWhiteSpace(postId))
                return null;

            Console.WriteLine("FB created postId: " + postId);

            // ✅ Postun hidden olub-olmadığını yoxla
            var isHidden = await GetIsHiddenAsync(postId);
            Console.WriteLine("FB is_hidden: " + (isHidden?.ToString() ?? "null"));

            // ✅ Hidden-dirsə, timeline-a çıxart (unhide)
            if (isHidden == true)
            {
                var ok = await SetHiddenAsync(postId, hidden: false);
                Console.WriteLine("FB unhide result: " + ok);
            }

            return postId;
        }


        /// <summary>
        /// Upload video and returns video id
        /// </summary>
        private async Task<string?> UploadVideoAsync(string videoUrl, string caption)
        {
            var endpoint = $"https://graph.facebook.com/v24.0/{_settings.PageId}/videos";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("file_url", videoUrl),
                new KeyValuePair<string, string>("description", caption ?? string.Empty),
                new KeyValuePair<string, string>("access_token", _settings.PageAccessToken),
            });

            var response = await _httpClient.PostAsync(endpoint, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("FB video upload error: " + json);
                return null;
            }

            return GetJsonProp(json, "id");
        }

        private static string? GetJsonProp(string json, string propName)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(propName, out var prop))
                    return prop.GetString();
            }
            catch { }
            return null;
        }

        public async Task<string?> GetPermalinkAsync(string postId)
        {
            if (string.IsNullOrWhiteSpace(postId))
                return null;

            // postId adətən "PAGEID_POSTID" formatında gəlir (/{page-id}/feed response)
            var endpoint =
                $"https://graph.facebook.com/v24.0/{postId}" +
                $"?fields=permalink_url&access_token={_settings.PageAccessToken}";

            var response = await _httpClient.GetAsync(endpoint);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("FB GetPermalink error: " + json);
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("permalink_url", out var p))
                    return p.GetString();
            }
            catch (Exception ex)
            {
                Console.WriteLine("FB GetPermalink parse error: " + ex.Message);
            }

            return null;
        }

        public async Task<bool> UnhidePostAsync(string postId)
        {
            var endpoint = $"https://graph.facebook.com/v24.0/{postId}";
            var content = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string,string>("is_hidden","false"),
        new KeyValuePair<string,string>("access_token", _settings.PageAccessToken),
    });

            var res = await _httpClient.PostAsync(endpoint, content);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine("FB UnhidePost error: " + json);
                return false;
            }

            return true;
        }

    }
}
