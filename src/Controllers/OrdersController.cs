using CoTee.Entities;
using CoTee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace CoTee.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(OrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Authorize]
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> Checkout([FromBody] CheckoutRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Unauthorized" });

            var shippingDetails = new ShippingDetails
            {
                FullName = request.FullName,
                Phone = request.Phone,
                Address = request.Address
            };

            var checkoutResponse = await _orderService.CreateOrderAndGetMomoUrlAsync(userId, shippingDetails);
            return Ok(checkoutResponse);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Checkout failed due to invalid business state");
            return BadRequest(new { message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Checkout failed because payment gateway request failed");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checkout order");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi tạo đơn hàng thanh toán" });
        }
    }

    [Authorize]
    [HttpGet("history")]
    [HttpGet("my-orders")]
    public async Task<ActionResult<IEnumerable<Order>>> GetMyOrders()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Unauthorized" });

            var orders = await _orderService.GetUserOrdersAsync(userId);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user orders");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi lấy danh sách đơn hàng" });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin")]
    public async Task<ActionResult<IEnumerable<Order>>> GetAllOrdersForAdmin()
    {
        try
        {
            if (!IsAdmin())
                return Forbid();

            var orders = await _orderService.GetAllOrdersAsync();
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all orders for admin");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi lấy danh sách đơn hàng" });
        }
    }

    [Authorize]
    [HttpGet("{orderCode}")]
    public async Task<ActionResult<Order>> GetOrderByCode(string orderCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Unauthorized" });

            var order = await _orderService.GetOrderByCodeAsync(userId, orderCode, IsAdmin());
            if (order == null)
                return NotFound(new { message = "Không tìm thấy đơn hàng" });

            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order {OrderCode}", orderCode);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi lấy chi tiết đơn hàng" });
        }
    }

    [Authorize]
    [HttpPost("{orderCode}/cancel")]
    public async Task<ActionResult<Order>> CancelOrder(string orderCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Unauthorized" });

            var order = await _orderService.CancelOrderAsync(userId, orderCode, IsAdmin());
            if (order == null)
                return NotFound(new { message = "Không tìm thấy đơn hàng" });

            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cancel order request rejected for order {OrderCode}", orderCode);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderCode}", orderCode);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi hủy đơn hàng" });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{orderCode}/status")]
    public async Task<ActionResult<Order>> UpdateOrderStatus(string orderCode, [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            if (!IsAdmin())
                return Forbid();

            if (request == null)
                return BadRequest(new { message = "Request body không được để trống" });

            var order = await _orderService.UpdateOrderStatusAsync(orderCode, request.OrderStatus, request.PaymentStatus);
            if (order == null)
                return NotFound(new { message = "Không tìm thấy đơn hàng" });

            return Ok(order);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid order status update request for order {OrderCode}", orderCode);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status for order {OrderCode}", orderCode);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi cập nhật trạng thái đơn hàng" });
        }
    }

    [AllowAnonymous]
    [HttpGet("momo-return")]
    public ActionResult<MomoReturnResponse> MomoReturn([FromQuery] MomoReturnQuery query)
    {
        if (query == null)
            return BadRequest(new { message = "Missing MoMo return parameters" });

        _logger.LogInformation("MoMo return received for order {OrderId} with resultCode {ResultCode}", query.OrderId, query.ResultCode);

        var resultMessage = query.ResultCode == 0
            ? "Thanh toán MoMo đã hoàn tất. Vui lòng chờ xác nhận từ cổng thanh toán." 
            : "Thanh toán MoMo không thành công. Vui lòng kiểm tra lại thông tin hoặc thử lại.";

        return Ok(new MomoReturnResponse
        {
            OrderId = query.OrderId,
            ResultCode = query.ResultCode,
            Message = resultMessage,
            ExtraData = query.ExtraData
        });
    }

    [AllowAnonymous]
    [HttpPost("momo-ipn")]
    public async Task<ActionResult<MomoIpnResult>> MomoIpn([FromBody] MomoIpnRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _orderService.ProcessMomoIpnAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MoMo IPN");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi xử lý IPN" });
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private bool IsAdmin()
    {
        return string.Equals(User.FindFirst(ClaimTypes.Role)?.Value, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(User.FindFirst("role")?.Value, "Admin", StringComparison.OrdinalIgnoreCase);
    }
}

public class CheckoutRequest
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string Phone { get; set; } = string.Empty;

    [Required]
    public string Address { get; set; } = string.Empty;
}

public class UpdateOrderStatusRequest
{
    public string? OrderStatus { get; set; }
    public string? PaymentStatus { get; set; }
}

public class MomoReturnQuery
{
    public string OrderId { get; set; } = string.Empty;
    public int ResultCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ExtraData { get; set; } = string.Empty;
}

public class MomoReturnResponse
{
    public string OrderId { get; set; } = string.Empty;
    public int ResultCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ExtraData { get; set; } = string.Empty;
}
