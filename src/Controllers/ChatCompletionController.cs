using CoTee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoTee.Controllers;

[ApiController]
[AllowAnonymous]
public class ChatCompletionController : ControllerBase
{
    private readonly IOpenAiChatService _chatService;
    private readonly ILogger<ChatCompletionController> _logger;

    public ChatCompletionController(IOpenAiChatService chatService, ILogger<ChatCompletionController> logger)
    {
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("chat_completion")]
    [HttpPost("v1/chat/completions")]
    [ProducesResponseType(typeof(OpenAiChatCompletionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OpenAiChatCompletionResponse>> CreateChatCompletion(
        [FromBody] OpenAiChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
                return BadRequest(new { message = "Request body không được để trống" });

            if (string.IsNullOrWhiteSpace(request.Model))
                return BadRequest(new { message = "model không được để trống" });

            if (request.Messages == null || request.Messages.Count == 0)
                return BadRequest(new { message = "messages không được để trống" });

            if (request.Stream == true)
                return BadRequest(new { message = "stream=true chưa được hỗ trợ trong endpoint này" });

            var response = await _chatService.CreateAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (OpenAiApiException ex)
        {
            _logger.LogError(ex, "OpenAI chat completion request failed");
            return StatusCode((int)ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating chat completion");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi tạo chat completion" });
        }
    }
}
