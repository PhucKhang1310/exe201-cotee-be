using CooTee.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace CooTee.Services;




public class EmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ResendSettings _resendSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        HttpClient httpClient,
        ResendSettings resendSettings,
        ILogger<EmailService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _resendSettings = resendSettings ?? throw new ArgumentNullException(nameof(resendSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resendSettings.Validate();
    }

    
    
    
    public async Task<bool> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        try
        {
            
            if (string.IsNullOrWhiteSpace(toEmail) || 
                string.IsNullOrWhiteSpace(subject) || 
                string.IsNullOrWhiteSpace(htmlBody))
            {
                _logger.LogWarning("Invalid email parameters - toEmail: {ToEmail}, subject: {Subject}", 
                    toEmail, subject);
                return false;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _resendSettings.ApiKey);
            request.Content = JsonContent.Create(new
            {
                from = $"{_resendSettings.FromName} <{_resendSettings.FromEmail}>",
                to = new[] { toEmail },
                subject,
                html = htmlBody
            });

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Resend rejected email to {ToEmail}. Status: {StatusCode}. Response: {ResponseBody}",
                    toEmail,
                    (int)response.StatusCode,
                    responseBody);
                return false;
            }

            _logger.LogInformation("Email sent successfully to {ToEmail} with subject: {Subject}", 
                toEmail, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {ToEmail} with subject: {Subject}", 
                toEmail, subject);
            return false;
        }
    }

    
    
    
    public async Task<bool> SendVerificationEmailAsync(string toEmail, string toName, 
        string verificationToken, string verificationUrl)
    {
        try
        {
            
            var htmlBody = GenerateVerificationEmailHtml(toName, verificationToken, verificationUrl);

            var subject = "CooTee - Xác Nhận Tài Khoản";
            return await SendEmailAsync(toEmail, toName, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending verification email to {ToEmail}", toEmail);
            return false;
        }
    }

    
    
    
    public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string toName, 
        string resetToken, string resetUrl)
    {
        try
        {
            var htmlBody = GeneratePasswordResetEmailHtml(toName, resetToken, resetUrl);

            var subject = "CooTee - Đặt Lại Mật Khẩu";
            return await SendEmailAsync(toEmail, toName, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending password reset email to {ToEmail}", toEmail);
            return false;
        }
    }

    
    
    
    public async Task<bool> SendWelcomeEmailAsync(string toEmail, string toName)
    {
        try
        {
            var htmlBody = GenerateWelcomeEmailHtml(toName);
            var subject = "Chào Mừng Đến Với CooTee";
            return await SendEmailAsync(toEmail, toName, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending welcome email to {ToEmail}", toEmail);
            return false;
        }
    }

    
    
    
    private string GenerateVerificationEmailHtml(string userName, string verificationToken, string verificationUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9; }}
        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .header h1 {{ margin: 0; }}
        .content {{ background-color: white; padding: 30px; }}
        .content p {{ margin: 0 0 15px 0; }}
        .button-container {{ text-align: center; margin: 30px 0; }}
        .button {{ background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; }}
        .button:hover {{ background-color: #218838; }}
        .footer {{ background-color: #f0f0f0; padding: 15px; text-align: center; font-size: 12px; color: #666; border-radius: 0 0 5px 5px; }}
        .token-note {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }}
        .token-note strong {{ color: #856404; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>CooTee - Xác Nhận Tài Khoản</h1>
        </div>
        <div class='content'>
            <p>Xin chào <strong>{userName}</strong>,</p>
            
            <p>Cảm ơn bạn đã đăng ký tài khoản CooTee! Để hoàn tất việc đăng ký, vui lòng xác nhận email của bạn bằng cách nhấp vào nút bên dưới:</p>

            <div class='button-container'>
                <a href='{verificationUrl}' class='button'>Xác Nhận Email</a>
            </div>

            <p>Hoặc sao chép và dán liên kết này vào trình duyệt:</p>
            <p><a href='{verificationUrl}'>{verificationUrl}</a></p>

            <div class='token-note'>
                <strong>Token Xác Minh:</strong> {verificationToken}
                <p style='margin: 10px 0 0 0; font-size: 11px;'>Token này sẽ hết hạn sau 15 phút</p>
            </div>

            <p>Nếu bạn không tạo tài khoản này, vui lòng bỏ qua email này.</p>

            <p>Trân trọng,<br><strong>CooTee Team</strong></p>
        </div>
        <div class='footer'>
            <p>© 2026 CooTee. Tất cả quyền được bảo lưu.</p>
            <p>Đây là email tự động, vui lòng không trả lời.</p>
        </div>
    </div>
</body>
</html>";
    }

    
    
    
    private string GeneratePasswordResetEmailHtml(string userName, string resetToken, string resetUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9; }}
        .header {{ background-color: #dc3545; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .header h1 {{ margin: 0; }}
        .content {{ background-color: white; padding: 30px; }}
        .content p {{ margin: 0 0 15px 0; }}
        .button-container {{ text-align: center; margin: 30px 0; }}
        .button {{ background-color: #dc3545; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; }}
        .button:hover {{ background-color: #c82333; }}
        .footer {{ background-color: #f0f0f0; padding: 15px; text-align: center; font-size: 12px; color: #666; border-radius: 0 0 5px 5px; }}
        .warning {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Đặt Lại Mật Khẩu</h1>
        </div>
        <div class='content'>
            <p>Xin chào <strong>{userName}</strong>,</p>
            
            <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Nhấp vào nút bên dưới để tạo mật khẩu mới:</p>

            <div class='button-container'>
                <a href='{resetUrl}' class='button'>Đặt Lại Mật Khẩu</a>
            </div>

            <p>Hoặc sao chép và dán liên kết này vào trình duyệt:</p>
            <p><a href='{resetUrl}'>{resetUrl}</a></p>

            <div class='warning'>
                <strong>Lưu ý:</strong> Liên kết này sẽ hết hạn sau 15 phút. Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.
            </div>

            <p>Nếu bạn gặp vấn đề, vui lòng liên hệ với chúng tôi.</p>

            <p>Trân trọng,<br><strong>CooTee Team</strong></p>
        </div>
        <div class='footer'>
            <p>© 2026 CooTee. Tất cả quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";
    }

    
    
    
    private string GenerateWelcomeEmailHtml(string userName)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f9f9f9; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .header h1 {{ margin: 0; }}
        .content {{ background-color: white; padding: 30px; }}
        .content p {{ margin: 0 0 15px 0; }}
        .features {{ background-color: #f0f0f0; padding: 20px; border-radius: 5px; margin: 20px 0; }}
        .features ul {{ margin: 0; padding-left: 20px; }}
        .features li {{ margin: 10px 0; }}
        .footer {{ background-color: #f0f0f0; padding: 15px; text-align: center; font-size: 12px; color: #666; border-radius: 0 0 5px 5px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎉 Chào Mừng Đến Với CooTee!</h1>
        </div>
        <div class='content'>
            <p>Xin chào <strong>{userName}</strong>,</p>
            
            <p>Tài khoản của bạn đã được xác nhận thành công! Chúng tôi rất vui được chào đón bạn trở thành một phần của cộng đồng CooTee.</p>

            <div class='features'>
                <p><strong>Bạn có thể bắt đầu với:</strong></p>
                <ul>
                    <li>📋 Quản lý hồ sơ cá nhân</li>
                    <li>🔐 Bảo mật tài khoản</li>
                    <li>⚙️ Tùy chỉnh cài đặt</li>
                    <li>📞 Liên hệ hỗ trợ</li>
                </ul>
            </div>

            <p>Nếu bạn có bất kỳ câu hỏi hoặc cần hỗ trợ, đừng ngần ngại liên hệ với chúng tôi.</p>

            <p>Cảm ơn bạn đã chọn CooTee!</p>

            <p>Trân trọng,<br><strong>CooTee Team</strong></p>
        </div>
        <div class='footer'>
            <p>© 2026 CooTee. Tất cả quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";
    }
}
