using CoTee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoTee.Controllers;

[ApiController]
[AllowAnonymous]
[Route("gen_image")]
public class GenImageController : ControllerBase
{
    private readonly IOpenAiImageService _imageService;
    private readonly ILogger<GenImageController> _logger;

    public GenImageController(IOpenAiImageService imageService, ILogger<GenImageController> logger)
    {
        _imageService = imageService ?? throw new ArgumentNullException(nameof(imageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    [ProducesResponseType(typeof(OpenAiImageGenerationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OpenAiImageGenerationResponse>> GenerateImage(
        [FromBody] OpenAiImageGenerationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
                return BadRequest(new { message = "Request body không được để trống" });

            if (string.IsNullOrWhiteSpace(request.Prompt))
                return BadRequest(new { message = "prompt không được để trống" });

            var response = await _imageService.GenerateAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (OpenAiApiException ex)
        {
            _logger.LogError(ex, "OpenAI image generation request failed");
            return StatusCode((int)ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating image");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi tạo ảnh" });
        }
    }
}
