using CooTee.Entities;

namespace CooTee.Services;




public interface IAuthService
{
    
    
    
    
    
    
    
    Task<AuthResult> RegisterAsync(string email, string password, string fullName);

    
    
    
    
    
    Task<VerificationResult> VerifyEmailAsync(string token);

    
    
    
    
    
    
    Task<LoginResult> LoginAsync(string email, string password);

    
    
    
    
    
    string GenerateJwtToken(User user);

    
    
    
    
    
    Task<PasswordResetResult> RequestPasswordResetAsync(string email);

    
    
    
    
    
    
    Task<PasswordResetResult> ResetPasswordAsync(string token, string newPassword);

    
    
    
    
    
    Task<bool> LogoutAsync(string token);
}




public class AuthResult
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public User? User { get; set; }
    public string? VerificationUrl { get; set; }
    public string? Token { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
}




public class VerificationResult
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public User? User { get; set; }
}




public class LoginResult
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public User? User { get; set; }
    public string? Token { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
}




public class PasswordResetResult
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public string? ResetUrl { get; set; }
}
