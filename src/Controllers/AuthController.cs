using CoTee.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Linq;

namespace CoTee.Controllers;




[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    
    
    
    
    
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.RegisterAsync(
                request.Email,
                request.Password,
                request.FullName);

            if (!result.IsSuccess)
                return BadRequest(new { message = result.Message });

            return CreatedAtAction(nameof(Register), new RegisterResponse
            {
                Message = result.Message,
                Email = result.User?.Email,
                IsEmailVerified = result.User?.IsEmailVerified ?? false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Lỗi khi đăng ký" });
        }
    }

    
    
    
    
    
    [HttpGet("verify-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<VerifyEmailResponse>> VerifyEmail([FromQuery] string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { message = "Token không được để trống" });

            var result = await _authService.VerifyEmailAsync(token);

            if (!result.IsSuccess)
                return BadRequest(new { message = result.Message });

            return Ok(new VerifyEmailResponse
            {
                Message = result.Message,
                Email = result.User?.Email,
                FullName = result.User?.FullName,
                IsEmailVerified = result.User?.IsEmailVerified ?? false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying email");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Lỗi khi xác minh email" });
        }
    }

    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ResendVerificationResponse>> ResendVerification(
        [FromBody] ResendVerificationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.ResendVerificationEmailAsync(request.Email);
        if (!result.IsSuccess)
            return BadRequest(new { message = result.Message });

        return Ok(new ResendVerificationResponse { Message = result.Message });
    }

    
    
    
    
    
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.LoginAsync(request.Email, request.Password);

            if (!result.IsSuccess)
            {
                if (result.Message?.Contains("email") ?? false)
                    return BadRequest(new { message = result.Message });
                return Unauthorized(new { message = result.Message });
            }

            return Ok(new LoginResponse
            {
                Message = result.Message,
                Token = result.Token,
                TokenExpiresAt = result.TokenExpiresAt,
                User = UserDto.FromEntity(result.User!)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Lỗi khi đăng nhập" });
        }
    }

    
    
    
    
    
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.RequestPasswordResetAsync(request.Email);

            return Ok(new ForgotPasswordResponse
            {
                Message = result.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting password reset");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Lỗi khi yêu cầu đặt lại mật khẩu" });
        }
    }

    
    
    
    
    
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.ResetPasswordAsync(request.Token, request.NewPassword);

            if (!result.IsSuccess)
                return BadRequest(new { message = result.Message });

            return Ok(new ResetPasswordResponse
            {
                Message = result.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Lỗi khi đặt lại mật khẩu" });
        }
    }

    
    
    
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
            return BadRequest(new { message = "Authorization header missing" });

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var success = await _authService.LogoutAsync(token);
        if (!success)
            return BadRequest(new { message = "Logout failed" });

        return Ok(new { message = "Logged out successfully" });
    }
}




public class RegisterRequest
{
    
    
    
    public string Email { get; set; } = string.Empty;

    
    
    
    public string Password { get; set; } = string.Empty;

    
    
    
    public string FullName { get; set; } = string.Empty;
}




public class RegisterResponse
{
    public string? Message { get; set; }
    public string? Email { get; set; }
    public bool IsEmailVerified { get; set; }
}




public class VerifyEmailResponse
{
    public string? Message { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public bool IsEmailVerified { get; set; }
}

public class ResendVerificationRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResendVerificationResponse
{
    public string? Message { get; set; }
}




public class LoginRequest
{
    
    
    
    public string Email { get; set; } = string.Empty;

    
    
    
    public string Password { get; set; } = string.Empty;
}




public class LoginResponse
{
    public string? Message { get; set; }
    public string? Token { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public UserDto? User { get; set; }
}




public class ForgotPasswordRequest
{
    
    
    
    public string Email { get; set; } = string.Empty;
}




public class ForgotPasswordResponse
{
    public string? Message { get; set; }
}




public class ResetPasswordRequest
{
    
    
    
    public string Token { get; set; } = string.Empty;

    
    
    
    public string NewPassword { get; set; } = string.Empty;

    
    
    
    public string ConfirmPassword { get; set; } = string.Empty;
}




public class ResetPasswordResponse
{
    public string? Message { get; set; }
}
