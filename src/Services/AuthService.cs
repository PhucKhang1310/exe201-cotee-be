using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using CoTee.Configuration;
using CoTee.Entities;
using CoTee.Infrastructure.Repositories;
using Google.Apis.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;

namespace CoTee.Services;




public class AuthService : IAuthService
{
    private readonly IMongoRepository<User> _userRepository;
    private readonly IMongoRepository<BlacklistedToken> _blacklistRepository;
    private readonly IEmailService _emailService;
    private readonly JwtSettings _jwtSettings;
    private readonly AppSettings _appSettings;
    private readonly GoogleSettings _googleSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IMongoRepository<User> userRepository,
        IMongoRepository<BlacklistedToken> blacklistRepository,
        IEmailService emailService,
        JwtSettings jwtSettings,
        AppSettings appSettings,
        GoogleSettings googleSettings,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _blacklistRepository = blacklistRepository ?? throw new ArgumentNullException(nameof(blacklistRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _jwtSettings = jwtSettings ?? throw new ArgumentNullException(nameof(jwtSettings));
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _googleSettings = googleSettings ?? throw new ArgumentNullException(nameof(googleSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jwtSettings.Validate();
        _appSettings.Validate();
        _googleSettings.Validate();
    }

    
    
    
    public async Task<AuthResult> RegisterAsync(string email, string password, string fullName)
    {
        try
        {
            
            if (string.IsNullOrWhiteSpace(email) || 
                string.IsNullOrWhiteSpace(password) || 
                string.IsNullOrWhiteSpace(fullName))
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Email, mật khẩu và tên đầy đủ không được để trống"
                };
            }

            email = NormalizeEmail(email);
            fullName = fullName.Trim();

            
            if (!IsValidEmail(email))
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Địa chỉ email không hợp lệ"
                };
            }

            
            if (!IsStrongPassword(password))
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Mật khẩu phải chứa ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt"
                };
            }

            
            var existingUser = await _userRepository.FindOneAsync("email", email);
            if (existingUser != null)
            {
                _logger.LogWarning("Attempt to register with existing email: {Email}", email);
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Email này đã được đăng ký"
                };
            }

            
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            
            var autoVerifyEmail = _appSettings.AutoVerifyEmailOnRegistration;
            string? verificationToken = autoVerifyEmail
                ? null
                : GenerateRandomToken(_appSettings.VerificationTokenLength);
            DateTime? tokenExpiresAt = autoVerifyEmail
                ? null
                : DateTime.UtcNow.AddMinutes(_appSettings.VerificationTokenExpirationMinutes);

            
            var newUser = new User
            {
                Email = email,
                PasswordHash = passwordHash,
                FullName = fullName,
                Role = "Customer",
                IsEmailVerified = autoVerifyEmail,
                VerificationToken = verificationToken,
                TokenExpiresAt = tokenExpiresAt,
                VerificationEmailLastSentAt = autoVerifyEmail ? null : DateTime.UtcNow,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            
            var createdUser = await _userRepository.CreateAsync(newUser);
            _logger.LogInformation("User registered successfully: {Email}", email);

            
            if (!autoVerifyEmail)
            {
                string verificationUrl = BuildFrontendUrl("/verify-email", "token", verificationToken!);
                bool emailSent = await _emailService.SendVerificationEmailAsync(
                    email,
                    fullName,
                    verificationToken!,
                    verificationUrl);

                if (!emailSent)
                {
                    _logger.LogWarning("Failed to send verification email to: {Email}", email);
                }
            }

            return new AuthResult
            {
                IsSuccess = true,
                Message = autoVerifyEmail
                    ? "Đăng ký thành công! Bạn có thể đăng nhập ngay"
                    : "Đăng ký thành công! Vui lòng kiểm tra email để xác nhận tài khoản",
                User = createdUser
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user: {Email}", email);
            return new AuthResult
            {
                IsSuccess = false,
                Message = "Lỗi khi đăng ký. Vui lòng thử lại sau"
            };
        }
    }

    
    
    
    public async Task<VerificationResult> VerifyEmailAsync(string token)
    {
        try
        {
            
            if (string.IsNullOrWhiteSpace(token))
            {
                return new VerificationResult
                {
                    IsSuccess = false,
                    Message = "Token xác minh không hợp lệ"
                };
            }

            
            var user = await _userRepository.FindOneAsync("verificationToken", token);

            if (user == null)
            {
                _logger.LogWarning("Verification attempt with invalid token");
                return new VerificationResult
                {
                    IsSuccess = false,
                    Message = "Token xác minh không tìm thấy"
                };
            }

            
            if (!user.TokenExpiresAt.HasValue || user.TokenExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("Verification token expired for user: {Email}", user.Email);
                return new VerificationResult
                {
                    IsSuccess = false,
                    Message = "Token xác minh đã hết hạn. Vui lòng đăng ký lại"
                };
            }

            
            if (user.IsEmailVerified)
            {
                _logger.LogInformation("Email already verified for user: {Email}", user.Email);
                return new VerificationResult
                {
                    IsSuccess = true,
                    Message = "Email đã được xác nhận trước đó",
                    User = user
                };
            }

            
            user.IsEmailVerified = true;
            user.VerificationToken = null;
            user.TokenExpiresAt = null;
            user.UpdatedAt = DateTime.UtcNow;

            
            var updateResult = await _userRepository.UpdateAsync(user.Id, user);
            
            if (!updateResult.IsSuccess)
            {
                _logger.LogError("Failed to update user verification status: {Email}", user.Email);
                return new VerificationResult
                {
                    IsSuccess = false,
                    Message = "Lỗi khi cập nhật xác minh email"
                };
            }

            _logger.LogInformation("Email verified successfully for user: {Email}", user.Email);

            
            _ = _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);

            return new VerificationResult
            {
                IsSuccess = true,
                Message = "Email đã được xác nhận thành công!",
                User = user
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying email with token");
            return new VerificationResult
            {
                IsSuccess = false,
                Message = "Lỗi khi xác minh email"
            };
        }
    }

    public async Task<VerificationResult> ResendVerificationEmailAsync(string email)
    {
        const string genericMessage = "Nếu tài khoản tồn tại và chưa được xác nhận, email xác minh mới sẽ được gửi";

        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return new VerificationResult { IsSuccess = false, Message = "Địa chỉ email không hợp lệ" };
            }

            email = NormalizeEmail(email);
            if (!IsValidEmail(email))
            {
                return new VerificationResult { IsSuccess = false, Message = "Địa chỉ email không hợp lệ" };
            }

            var user = await _userRepository.FindOneAsync("email", email);
            if (user == null || user.IsEmailVerified || !user.IsActive)
            {
                return new VerificationResult { IsSuccess = true, Message = genericMessage };
            }

            var resendAvailableAt = user.VerificationEmailLastSentAt?.AddSeconds(
                _appSettings.VerificationEmailResendCooldownSeconds);
            if (resendAvailableAt > DateTime.UtcNow)
            {
                return new VerificationResult { IsSuccess = true, Message = genericMessage };
            }

            var verificationToken = GenerateRandomToken(_appSettings.VerificationTokenLength);
            user.VerificationToken = verificationToken;
            user.TokenExpiresAt = DateTime.UtcNow.AddMinutes(_appSettings.VerificationTokenExpirationMinutes);
            user.VerificationEmailLastSentAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            var updateResult = await _userRepository.UpdateAsync(user.Id, user);
            if (!updateResult.IsSuccess)
            {
                _logger.LogError("Failed to refresh verification token for user: {Email}", email);
                return new VerificationResult { IsSuccess = false, Message = "Không thể gửi lại email xác minh" };
            }

            var verificationUrl = BuildFrontendUrl("/verify-email", "token", verificationToken);
            var emailSent = await _emailService.SendVerificationEmailAsync(
                user.Email, user.FullName, verificationToken, verificationUrl);

            if (!emailSent)
            {
                _logger.LogWarning("Failed to resend verification email to: {Email}", email);
            }

            return new VerificationResult { IsSuccess = true, Message = genericMessage };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending verification email for: {Email}", email);
            return new VerificationResult { IsSuccess = false, Message = "Không thể gửi lại email xác minh" };
        }
    }

    
    
    
    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return new LoginResult
                {
                    IsSuccess = false,
                    Message = "Email và mật khẩu không được để trống"
                };
            }

            email = NormalizeEmail(email);

            
            var user = await _userRepository.FindOneAsync("email", email);

            if (user == null)
            {
                _logger.LogWarning("Login attempt with non-existent email: {Email}", email);
                return new LoginResult
                {
                    IsSuccess = false,
                    Message = "Email hoặc mật khẩu không đúng"
                };
            }

            
            if (!user.IsActive)
            {
                _logger.LogWarning("Login attempt with inactive account: {Email}", email);
                return new LoginResult
                {
                    IsSuccess = false,
                    Message = "Tài khoản này đã bị vô hiệu hóa"
                };
            }

            
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                _logger.LogWarning("Failed password verification for user: {Email}", email);
                return new LoginResult
                {
                    IsSuccess = false,
                    Message = "Email hoặc mật khẩu không đúng"
                };
            }

            if (_appSettings.AutoVerifyEmailOnRegistration && !user.IsEmailVerified)
            {
                user.IsEmailVerified = true;
                user.VerificationToken = null;
                user.TokenExpiresAt = null;
                user.UpdatedAt = DateTime.UtcNow;

                var updateResult = await _userRepository.UpdateAsync(user.Id, user);
                if (!updateResult.IsSuccess)
                {
                    _logger.LogError("Failed to auto-verify user during login: {Email}", email);
                    return new LoginResult
                    {
                        IsSuccess = false,
                        Message = "Lỗi khi cập nhật xác minh email"
                    };
                }
            }

            
            if (!user.IsEmailVerified)
            {
                _logger.LogWarning("Login attempt with unverified email: {Email}", email);
                return new LoginResult
                {
                    IsSuccess = false,
                    Message = "Vui lòng xác nhận email trước khi đăng nhập"
                };
            }

            
            string token = GenerateJwtToken(user);
            DateTime tokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);

            _logger.LogInformation("User logged in successfully: {Email}", email);

            return new LoginResult
            {
                IsSuccess = true,
                Message = "Đăng nhập thành công",
                User = user,
                Token = token,
                TokenExpiresAt = tokenExpiresAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", email);
            return new LoginResult
            {
                IsSuccess = false,
                Message = "Lỗi khi đăng nhập"
            };
        }
    }

    public async Task<LoginResult> LoginWithGoogleAsync(string idToken)
    {
        try
        {
            if (!_googleSettings.Enabled)
            {
                return new LoginResult
                {
                    IsSuccess = false,
                    Message = "Google login chưa được cấu hình"
                };
            }

            if (string.IsNullOrWhiteSpace(idToken))
            {
                return new LoginResult
                {
                    IsSuccess = false,
                    Message = "IdToken Google không được để trống"
                };
            }

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _googleSettings.ClientId }
            });

            if (string.IsNullOrWhiteSpace(payload.Email) || !payload.EmailVerified)
            {
                return new LoginResult
                {
                    IsSuccess = false,
                    Message = "Xác thực Google thất bại hoặc email chưa được xác minh"
                };
            }

            var email = NormalizeEmail(payload.Email);
            var user = await _userRepository.FindOneAsync("email", email);

            if (user == null)
            {
                var generatedPassword = Guid.NewGuid().ToString();
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(generatedPassword);

                user = new User
                {
                    Email = email,
                    PasswordHash = passwordHash,
                    FullName = string.IsNullOrWhiteSpace(payload.Name) ? email : payload.Name.Trim(),
                    Role = "Customer",
                    IsEmailVerified = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                user = await _userRepository.CreateAsync(user);
                _logger.LogInformation("New user created through Google login: {Email}", email);
            }
            else if (!user.IsActive)
            {
                return new LoginResult
                {
                    IsSuccess = false,
                    Message = "Tài khoản đã bị vô hiệu hóa"
                };
            }
            else if (!user.IsEmailVerified)
            {
                user.IsEmailVerified = true;
                user.VerificationToken = null;
                user.TokenExpiresAt = null;
                user.UpdatedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user.Id, user);
            }

            if (!string.IsNullOrWhiteSpace(payload.Name) && payload.Name.Trim() != user.FullName)
            {
                user.FullName = payload.Name.Trim();
                user.UpdatedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user.Id, user);
            }

            string token = GenerateJwtToken(user);
            DateTime tokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);

            return new LoginResult
            {
                IsSuccess = true,
                Message = "Đăng nhập bằng Google thành công",
                User = user,
                Token = token,
                TokenExpiresAt = tokenExpiresAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google token validation failed");
            return new LoginResult
            {
                IsSuccess = false,
                Message = "Google login không hợp lệ"
            };
        }
    }

    
    
    public string GenerateJwtToken(User user)
    {
        try
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("role", user.Role),
                new Claim("isEmailVerified", user.IsEmailVerified.ToString())
            };

            
            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: credentials);

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.WriteToken(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating JWT token for user: {UserId}", user.Id);
            throw;
        }
    }

    
    
    
    public async Task<PasswordResetResult> RequestPasswordResetAsync(string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return new PasswordResetResult
                {
                    IsSuccess = false,
                    Message = "Email không được để trống"
                };
            }

            email = NormalizeEmail(email);

            
            var user = await _userRepository.FindOneAsync("email", email);

            if (user == null)
            {
                
                _logger.LogWarning("Password reset request for non-existent email: {Email}", email);
                return new PasswordResetResult
                {
                    IsSuccess = true,
                    Message = "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được link đặt lại mật khẩu"
                };
            }

            
            string resetToken = GenerateRandomToken(_appSettings.VerificationTokenLength);
            DateTime tokenExpiresAt = DateTime.UtcNow.AddMinutes(_appSettings.VerificationTokenExpirationMinutes);

            
            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpiresAt = tokenExpiresAt;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user.Id, user);

            
            string resetUrl = BuildUrl("/reset-password", "token", resetToken);

            
            await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, resetToken, resetUrl);

            _logger.LogInformation("Password reset email sent to: {Email}", email);

            return new PasswordResetResult
            {
                IsSuccess = true,
                Message = "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được link đặt lại mật khẩu",
                ResetUrl = resetUrl
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting password reset for email: {Email}", email);
            return new PasswordResetResult
            {
                IsSuccess = false,
                Message = "Lỗi khi yêu cầu đặt lại mật khẩu"
            };
        }
    }

    
    
    
    public async Task<PasswordResetResult> ResetPasswordAsync(string token, string newPassword)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
            {
                return new PasswordResetResult
                {
                    IsSuccess = false,
                    Message = "Token và mật khẩu mới không được để trống"
                };
            }

            
            if (!IsStrongPassword(newPassword))
            {
                return new PasswordResetResult
                {
                    IsSuccess = false,
                    Message = "Mật khẩu phải chứa ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt"
                };
            }

            
            var user = await _userRepository.FindOneAsync("passwordResetToken", token);

            if (user == null)
            {
                _logger.LogWarning("Password reset attempt with invalid token");
                return new PasswordResetResult
                {
                    IsSuccess = false,
                    Message = "Token không hợp lệ"
                };
            }

            
            if (!user.PasswordResetTokenExpiresAt.HasValue || user.PasswordResetTokenExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("Password reset token expired for user: {Email}", user.Email);
                return new PasswordResetResult
                {
                    IsSuccess = false,
                    Message = "Token đã hết hạn. Vui lòng yêu cầu đặt lại mật khẩu mới"
                };
            }

            
            string newPasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

            
            user.PasswordHash = newPasswordHash;
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiresAt = null;
            user.UpdatedAt = DateTime.UtcNow;

            var updateResult = await _userRepository.UpdateAsync(user.Id, user);

            if (!updateResult.IsSuccess)
            {
                _logger.LogError("Failed to update password for user: {Email}", user.Email);
                return new PasswordResetResult
                {
                    IsSuccess = false,
                    Message = "Lỗi khi cập nhật mật khẩu"
                };
            }

            _logger.LogInformation("Password reset successfully for user: {Email}", user.Email);

            return new PasswordResetResult
            {
                IsSuccess = true,
                Message = "Mật khẩu đã được đặt lại thành công. Vui lòng đăng nhập với mật khẩu mới"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password with token");
            return new PasswordResetResult
            {
                IsSuccess = false,
                Message = "Lỗi khi đặt lại mật khẩu"
            };
        }
    }

    
    
    
    public async Task<bool> LogoutAsync(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var handler = new JwtSecurityTokenHandler();
            JwtSecurityToken? jwt = null;
            try
            {
                jwt = handler.ReadJwtToken(token);
            }
            catch
            {
                
            }

            DateTime expiresAt = DateTime.UtcNow.AddDays(7);
            if (jwt != null)
            {
                var exp = jwt.Payload.Exp;
                if (exp.HasValue)
                {
                    expiresAt = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(exp.Value)).UtcDateTime;
                }
            }

            
            var entry = new BlacklistedToken
            {
                Token = token,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow
            };

            await _blacklistRepository.CreateAsync(entry);
            _logger.LogInformation("Token blacklisted until {ExpiresAt}", expiresAt);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blacklisting token");
            return false;
        }
    }

    
    
    
    private string GenerateRandomToken(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private string BuildUrl(string path, string queryName, string queryValue)
    {
        var baseUrl = _appSettings.BaseUrl.TrimEnd('/');
        return QueryHelpers.AddQueryString($"{baseUrl}{path}", queryName, queryValue);
    }

    private string BuildFrontendUrl(string path, string queryName, string queryValue)
    {
        var baseUrl = _appSettings.FrontendBaseUrl.TrimEnd('/');
        return QueryHelpers.AddQueryString($"{baseUrl}{path}", queryName, queryValue);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    
    
    
    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    
    
    
    
    private bool IsStrongPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return false;

        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

        return hasUpper && hasLower && hasDigit && hasSpecial;
    }
}
