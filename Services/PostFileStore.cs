using System.Text.Json;
using Vertex.DTOs;

namespace Vertex.Services
{
    public class PostFileStore
    {
        private readonly string _filePath;
        public PostFileStore(IWebHostEnvironment _hostEnvironment)
        {
                _filePath = Path.Combine(_hostEnvironment.ContentRootPath, "posts.json");
        }

        

        public async Task<List<PostDTO>> GetAllPosts()
        {
            if(!File.Exists(_filePath)) 
                return new List<PostDTO>();

            var json = await File.ReadAllTextAsync(_filePath);
            if(string.IsNullOrEmpty(json))
                return new List<PostDTO>();

            return JsonSerializer.Deserialize<List<PostDTO>>(json) ?? new List<PostDTO>();
        }
        

        public async Task WriteAllAsync(List<PostDTO> posts)
        {
            var json = JsonSerializer.Serialize(posts, new JsonSerializerOptions
            {
                WriteIndented = true,
            });

            await File.WriteAllTextAsync(_filePath, json);
        }

        public async Task<PostDTO> AddAsync(PostDTO post)
        {
            var posts = await GetAllPosts();

            var id = posts.Count == 0 ? 1 : posts.Max(p => p.Id) + 1;
            post.Id = id;

            posts.Insert(0, post);
            await WriteAllAsync(posts);

            return post;
        }

        public async Task<PostDTO> FindByIdAsync(int id)
        {
            var posts = await GetAllPosts();
            var post = posts.FirstOrDefault(x=>x.Id==id);
            return post;
        }


    }
}
