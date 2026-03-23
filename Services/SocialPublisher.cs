using Vertex.Services;

namespace Slavyan.Services
{
    public class SocialPublisher
    {
        private readonly InstagramService _instagramService;
        private readonly FacebookService _facebookService;

        public SocialPublisher(InstagramService instagramService, FacebookService facebookService)
        {
            _instagramService = instagramService;
            _facebookService = facebookService;
        }

        public async Task<SocialPublishResult> PublishImageAsync(string imageUrl, string fbCaption, string igCaption)
        {
            var result = new SocialPublishResult();

            // ========= INSTAGRAM =========
            var igId = await _instagramService.PublishToInstagramAsync(imageUrl, igCaption);
            result.InstagramId = igId;

            if (!string.IsNullOrWhiteSpace(igId))
                result.InstagramLink = await _instagramService.GetPermalinkAsync(igId);

            // ========= FACEBOOK (TIMELINE) =========
            var fbId = await _facebookService.PostSinglePhotoToTimelineAsync(imageUrl, fbCaption);
            result.FacebookId = fbId;

            if (!string.IsNullOrWhiteSpace(fbId))
                result.FacebookLink = await _facebookService.GetPermalinkAsync(fbId);

            return result;
        }

        public async Task<SocialPublishResult> PublishCarouselAsync(IReadOnlyList<string> imageUrls, string fbCaption, string igCaption)
        {
            var result = new SocialPublishResult();

            var clean = imageUrls
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (clean.Count == 0)
                return result;

            // ========= INSTAGRAM =========
            var igImages = clean.Take(10).ToList();

            string? igId;
            if (igImages.Count == 1)
                igId = await _instagramService.PublishToInstagramAsync(igImages[0], igCaption);
            else
                igId = await _instagramService.PublishCarouselAsync(igImages, igCaption);

            result.InstagramId = igId;

            if (!string.IsNullOrWhiteSpace(igId))
                result.InstagramLink = await _instagramService.GetPermalinkAsync(igId);

            // ========= FACEBOOK (TIMELINE) =========
            string? fbId;
            if (clean.Count == 1)
                fbId = await _facebookService.PostSinglePhotoToTimelineAsync(clean[0], fbCaption);
            else
                fbId = await _facebookService.PostMultiPhotoToTimelineAsync(clean, fbCaption);

            result.FacebookId = fbId;

            if (!string.IsNullOrWhiteSpace(fbId))
                result.FacebookLink = await _facebookService.GetPermalinkAsync(fbId);

            return result;
        }

        public async Task<SocialPublishResult> PublishVideoAsync(string videoUrl, string fbCaption, string igCaption, string? coverUrl = null)
        {
            var result = new SocialPublishResult();

            // ========= INSTAGRAM =========
            var igId = await _instagramService.PublishVideoAsync(videoUrl, igCaption, coverUrl);
            result.InstagramId = igId;

            if (!string.IsNullOrWhiteSpace(igId))
                result.InstagramLink = await _instagramService.GetPermalinkAsync(igId);

            // ========= FACEBOOK (TIMELINE) =========
            var fbId = await _facebookService.PostVideoToTimelineAsync(videoUrl, fbCaption);
            result.FacebookId = fbId;

            if (!string.IsNullOrWhiteSpace(fbId))
                result.FacebookLink = await _facebookService.GetPermalinkAsync(fbId);

            return result;
        }
    }
}
