using Microsoft.AspNetCore.Mvc;
using Slavyan.Services;
using Vertex.DTOs;
using Vertex.DTOs.RequestDTOs;
using Vertex.Services;

namespace Vertex.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly PostFileStore _postFileStore;

        public PostsController(PostFileStore postFileStore)
        {
            _postFileStore = postFileStore;
        }

        [HttpGet]
        public async Task<ActionResult<List<PostDTO>>> GetAll()
        {
            return Ok(await _postFileStore.GetAllPosts());
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<PostDTO>> GetById(int id)
        {
            var post = await _postFileStore.FindByIdAsync(id);
            if(post == null) 
                return NotFound();
            return Ok(post);
        }

        [HttpPost]
        public async Task<ActionResult<PostDTO>> Post(
    [FromBody] CreatePostRequest req,
    [FromServices] SocialPublisher socialPublisher)
        {
            if (string.IsNullOrWhiteSpace(req.Title))
                return BadRequest("Title is required.");

            var post = new PostDTO
            {
                Title = req.Title.Trim(),
                Description = req.Description?.Trim() ?? "",
                MediaURL = req.MediaURL?.Trim() ?? "",
                MediaType = req.MediaType?.Trim() ?? "",
                CreatedAt = DateTime.UtcNow
            };

            // 1) posts.json-a yaz, ID al
            post = await _postFileStore.AddAsync(post);

            // 2) Website link
            post.WebsiteLink = $"https://avazgaraev.github.io/Personal-portfolio/blog-detail.html?id={post.Id}";
            post.FacebookLink = "";
            post.InstagramLink = "";
            post.MediaType = post.MediaType ?? "";

            // 3) Social publish
            if (req.PublishToSocial)
            {
                var fbCaption = $"{post.Title}\n\n{post.Description}";
                var igCaption = $"{post.Title}\n\n{post.Description}";

                SocialPublishResult social = new();

                // a) Video
                if (string.Equals(post.MediaType, "video", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(post.MediaURL))
                {
                    social = await socialPublisher.PublishVideoAsync(post.MediaURL, fbCaption, igCaption);
                }
                // b) Carousel (MediaUrls varsa)
                else if (req.MediaUrls != null && req.MediaUrls.Count > 0)
                {
                    social = await socialPublisher.PublishCarouselAsync(req.MediaUrls, fbCaption, igCaption);

                    // istəsən ilk şəkli preview üçün saxla:
                    if (string.IsNullOrWhiteSpace(post.MediaURL))
                        post.MediaURL = req.MediaUrls.FirstOrDefault() ?? "";
                    post.MediaType = "image";

                    post.MediaUrls = req.MediaUrls
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();
                }
                // c) Single image
                else if (!string.IsNullOrWhiteSpace(post.MediaURL))
                {
                    post.MediaType = string.IsNullOrWhiteSpace(post.MediaType) ? "image" : post.MediaType;
                    social = await socialPublisher.PublishImageAsync(post.MediaURL, fbCaption, igCaption);
                }

                post.FacebookLink = social.FacebookLink ?? "";
                post.InstagramLink = social.InstagramLink ?? "";
            }

            // 4) posts.json update et
            var all = await _postFileStore.GetAllPosts();
            var idx = all.FindIndex(x => x.Id == post.Id);
            if (idx >= 0)
            {
                all[idx] = post;
                await _postFileStore.WriteAllAsync(all);
            }

            return Ok(post);
        }
    }
}
