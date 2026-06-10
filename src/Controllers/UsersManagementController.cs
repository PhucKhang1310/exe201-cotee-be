using CoTee.Entities;
using CoTee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoTee.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class UsersManagementController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersManagementController> _logger;

    public UsersManagementController(IUserService userService, ILogger<UsersManagementController> logger)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public async Task<ActionResult<PagedUserResponse>> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var users = (await _userService.GetAllUsersAsync()).ToList();
            var total = users.Count;
            var items = users
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(UserAdminDto.FromEntity)
                .ToList();

            return Ok(new PagedUserResponse
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                Items = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users for admin");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi lấy danh sách người dùng" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserAdminDto>> GetUserById(string id)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "Không tìm thấy tài khoản" });

            return Ok(UserAdminDto.FromEntity(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId} for admin", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi lấy chi tiết tài khoản" });
        }
    }

    [HttpGet("email/{email}")]
    public async Task<ActionResult<UserAdminDto>> GetUserByEmail(string email)
    {
        try
        {
            var user = await _userService.GetUserByEmailAsync(email);
            if (user == null)
                return NotFound(new { message = "Không tìm thấy tài khoản" });

            return Ok(UserAdminDto.FromEntity(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user by email {Email} for admin", email);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi lấy chi tiết tài khoản" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<UserAdminDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.FullName))
            {
                return BadRequest(new { message = "Email, password và fullName là bắt buộc" });
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var user = await _userService.CreateUserAsync(request.Email, passwordHash, request.FullName);

            return CreatedAtAction(nameof(GetUserById),
                new { id = user.Id }, UserAdminDto.FromEntity(user));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user for admin");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi tạo tài khoản" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserAdminDto>> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var existingUser = await _userService.GetUserByIdAsync(id);
            if (existingUser == null)
                return NotFound(new { message = "Không tìm thấy tài khoản" });

            if (!string.IsNullOrWhiteSpace(request.FullName))
                existingUser.FullName = request.FullName;

            if (!string.IsNullOrWhiteSpace(request.Role))
                existingUser.Role = request.Role;

            var success = await _userService.UpdateUserAsync(id, existingUser);
            if (!success)
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Không thể cập nhật tài khoản" });

            return Ok(UserAdminDto.FromEntity(existingUser));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId} for admin", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi cập nhật tài khoản" });
        }
    }

    [HttpPut("{id}/toggle-status")]
    public async Task<ActionResult<UserAdminDto>> ToggleStatus(string id)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "Không tìm thấy tài khoản" });

            user.IsActive = !user.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            var updated = await _userService.UpdateUserAsync(id, user);
            if (!updated)
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Không thể cập nhật trạng thái tài khoản" });

            return Ok(UserAdminDto.FromEntity(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling status for user {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi đổi trạng thái tài khoản" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteUser(string id)
    {
        try
        {
            var deleted = await _userService.DeleteUserAsync(id);
            if (!deleted)
                return NotFound(new { message = "Không tìm thấy tài khoản" });

            return Ok(new { message = "Tài khoản đã bị vô hiệu hóa" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId} for admin", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi xóa tài khoản" });
        }
    }
}

public class UserAdminDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static UserAdminDto FromEntity(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        Role = user.Role,
        IsActive = user.IsActive,
        IsEmailVerified = user.IsEmailVerified,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt
    };
}

public class PagedUserResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public List<UserAdminDto> Items { get; set; } = new();
}

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    public string? FullName { get; set; }
    public string? Role { get; set; }
}
