using CooTee.Entities;
using CooTee.Infrastructure.Repositories;

namespace CooTee.Services;




public interface IUserService
{
    Task<User?> GetUserByIdAsync(string id);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User> CreateUserAsync(string email, string passwordHash, string fullName);
    Task<bool> UpdateUserAsync(string id, User user);
    Task<bool> VerifyEmailAsync(string email, string token);
    Task<bool> DeleteUserAsync(string id);
    Task<IEnumerable<User>> GetAllUsersAsync();
}




public class UserService : IUserService
{
    private readonly IMongoRepository<User> _userRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(IMongoRepository<User> userRepository, ILogger<UserService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    
    
    
    public async Task<User?> GetUserByIdAsync(string id)
    {
        try
        {
            return await _userRepository.GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by id: {UserId}", id);
            return null;
        }
    }

    
    
    
    public async Task<User?> GetUserByEmailAsync(string email)
    {
        try
        {
            return await _userRepository.FindOneAsync("email", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by email: {Email}", email);
            return null;
        }
    }

    
    
    
    public async Task<User> CreateUserAsync(string email, string passwordHash, string fullName)
    {
        try
        {
            
            var existingUser = await GetUserByEmailAsync(email);
            if (existingUser != null)
                throw new InvalidOperationException($"User with email {email} already exists");

            var newUser = new User
            {
                Email = email,
                PasswordHash = passwordHash,
                FullName = fullName,
                Role = "Customer",
                IsEmailVerified = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdUser = await _userRepository.CreateAsync(newUser);
            _logger.LogInformation("User created successfully: {Email}", email);
            return createdUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {Email}", email);
            throw;
        }
    }

    
    
    
    public async Task<bool> UpdateUserAsync(string id, User user)
    {
        try
        {
            user.UpdatedAt = DateTime.UtcNow;
            var result = await _userRepository.UpdateAsync(id, user);
            
            if (result.IsSuccess)
                _logger.LogInformation("User updated successfully: {UserId}", id);
            
            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user: {UserId}", id);
            throw;
        }
    }

    
    
    
    public async Task<bool> VerifyEmailAsync(string email, string token)
    {
        try
        {
            var user = await GetUserByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("User not found for email verification: {Email}", email);
                return false;
            }

            if (user.VerificationToken != token)
            {
                _logger.LogWarning("Invalid verification token for: {Email}", email);
                return false;
            }

            if (user.TokenExpiresAt.HasValue && user.TokenExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Verification token expired for: {Email}", email);
                return false;
            }

            user.IsEmailVerified = true;
            user.VerificationToken = null;
            user.TokenExpiresAt = null;

            return await UpdateUserAsync(user.Id, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying email: {Email}", email);
            throw;
        }
    }

    
    
    
    public async Task<bool> DeleteUserAsync(string id)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return false;

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;

            var updated = await _userRepository.UpdateAsync(id, user);
            if (updated.IsSuccess)
                _logger.LogInformation("User soft-deleted successfully: {UserId}", id);

            return updated.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft-deleting user: {UserId}", id);
            throw;
        }
    }

    
    
    
    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        try
        {
            return await _userRepository.GetAllAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all users");
            throw;
        }
    }
}
