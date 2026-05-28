namespace CooTee.Services;




public interface IEmailService
{
    
    
    
    
    
    
    
    
    Task<bool> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody);

    
    
    
    
    
    
    
    
    Task<bool> SendVerificationEmailAsync(string toEmail, string toName, 
        string verificationToken, string verificationUrl);

    
    
    
    
    
    
    
    
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string toName, 
        string resetToken, string resetUrl);

    
    
    
    
    
    
    Task<bool> SendWelcomeEmailAsync(string toEmail, string toName);
}
