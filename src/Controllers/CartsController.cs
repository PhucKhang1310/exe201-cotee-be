using CoTee.Entities;
using CoTee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CoTee.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Customer")]
public class CartsController : ControllerBase
{
    private readonly CartService _cartService;
    private readonly ILogger<CartsController> _logger;

    public CartsController(CartService cartService, ILogger<CartsController> logger)
    {
        _cartService = cartService ?? throw new ArgumentNullException(nameof(cartService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public async Task<ActionResult<List<CartItemDetailsResponse>>> GetCart()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Unauthorized" });

            var cartItems = await _cartService.GetCartByUserIdAsync(userId);
            return Ok(cartItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cart");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi lấy giỏ hàng" });
        }
    }

    [HttpPost("items")]
    public async Task<ActionResult<List<CartItemDetailsResponse>>> AddToCart([FromBody] AddToCartRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Unauthorized" });

            var cartItems = await _cartService.AddToCartAsync(userId, request.ProductId, request.Quantity, request.Size);
            return Ok(cartItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to cart");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi thêm sản phẩm vào giỏ hàng" });
        }
    }

    [HttpPut("items")]
    public async Task<ActionResult<List<CartItemDetailsResponse>>> UpdateCartItem([FromBody] UpdateCartItemRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Unauthorized" });

            var cartItems = await _cartService.UpdateCartItemAsync(userId, request.ProductId, request.Quantity, request.Size);
            return Ok(cartItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cart item");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi cập nhật giỏ hàng" });
        }
    }

    [HttpDelete("items/{productId}")]
    public async Task<ActionResult<List<CartItemDetailsResponse>>> RemoveCartItem(string productId, [FromQuery] string? size)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Unauthorized" });

            var cartItems = await _cartService.RemoveCartItemAsync(userId, productId, size);
            return Ok(cartItems);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cart item removal failed for product {ProductId}", productId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cart item {ProductId}", productId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi xoá sản phẩm khỏi giỏ hàng" });
        }
    }

    [HttpDelete]
    public async Task<ActionResult> ClearCart()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Unauthorized" });

            await _cartService.ClearCartAsync(userId);
            return Ok(new { message = "Giỏ hàng đã được xoá" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cart");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi xoá giỏ hàng" });
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}

public class AddToCartRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string Size { get; set; } = string.Empty;
}

public class UpdateCartItemRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Size { get; set; } = string.Empty;
}
