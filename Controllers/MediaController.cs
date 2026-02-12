using Microsoft.AspNetCore.Mvc;
using Vertex.Services;

[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly AzureBlobStorageService _azure;

    public MediaController(AzureBlobStorageService azure)
    {
        _azure = azure;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(200_000_000)] // 200MB (istəyə görə)
    public async Task<IActionResult> Upload([FromForm] List<IFormFile> files, CancellationToken ct)
    {
        try
        {
            if (files == null || files.Count == 0)
                return BadRequest(new { message = "No files provided." });

            var urls = await _azure.UploadAsync(files, ct);
            return Ok(new { urls });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
