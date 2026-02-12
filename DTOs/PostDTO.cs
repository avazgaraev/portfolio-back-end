namespace Vertex.DTOs
{
    public class PostDTO
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MediaURL {  get; set; } = string.Empty;
        public List<string> MediaUrls { get; set; } = new();
        public string MediaType {  get; set; } = string.Empty;
        public string InstagramLink {  get; set; } = string.Empty;
        public string FacebookLink {  get; set; } = string.Empty;
        public string WebsiteLink {  get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
