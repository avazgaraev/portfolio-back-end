namespace Vertex.DTOs.RequestDTOs
{
        public class CreatePostRequest
        {
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";

            // tək media üçün (image/video url)
            public string MediaURL { get; set; } = "";
            public string MediaType { get; set; } = ""; // "image" / "video"

            // carousel üçün (image urls)
            public List<string>? MediaUrls { get; set; }

            public bool PublishToSocial { get; set; } = true;
        }
}
