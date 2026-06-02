using CooTee.Entities;
using CooTee.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CooTee.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMongoRepository<Product> _productRepository;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IMongoRepository<Product> productRepository, ILogger<ProductsController> logger)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
    {
        try
        {
            var products = await _productRepository.GetAllAsync();
            return Ok(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi lấy danh sách sản phẩm" });
        }
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProductById(string id)
    {
        try
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
                return NotFound(new { message = "Không tìm thấy sản phẩm" });

            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product {ProductId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi lấy chi tiết sản phẩm" });
        }
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct([FromBody] CreateProductRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Unauthorized" });

            if (request == null)
                return BadRequest(new { message = "Request body không được để trống" });

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "Tên sản phẩm không được để trống" });

            if (request.Price < 0)
                return BadRequest(new { message = "Giá không được âm" });

            if (request.Stock < 0)
                return BadRequest(new { message = "Tồn kho không được âm" });

            var product = new Product
            {
                Name = request.Name.Trim(),
                ImageUrl = request.ImageUrl,
                Price = request.Price,
                Stock = request.Stock,
                OwnerId = userId
            };

            var createdProduct = await _productRepository.CreateAsync(product);
            return CreatedAtAction(nameof(GetProductById), new { id = createdProduct.Id }, createdProduct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi tạo sản phẩm" });
        }
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult<Product>> UpdateProduct(string id, [FromBody] UpdateProductRequest request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { message = "Request body không được để trống" });

            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
                return NotFound(new { message = "Không tìm thấy sản phẩm" });

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Unauthorized" });

            if (!IsAdmin() && !string.Equals(product.OwnerId, userId, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            if (!string.IsNullOrWhiteSpace(request.Name))
                product.Name = request.Name.Trim();

            if (request.ImageUrl != null)
                product.ImageUrl = request.ImageUrl;

            if (request.Price.HasValue)
            {
                if (request.Price.Value < 0)
                    return BadRequest(new { message = "Giá không được âm" });

                product.Price = request.Price.Value;
            }

            if (request.Stock.HasValue)
            {
                if (request.Stock.Value < 0)
                    return BadRequest(new { message = "Tồn kho không được âm" });

                product.Stock = request.Stock.Value;
            }

            var updateResult = await _productRepository.UpdateAsync(id, product);
            if (!updateResult.IsSuccess)
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi cập nhật sản phẩm" });

            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {ProductId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi cập nhật sản phẩm" });
        }
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteProduct(string id)
    {
        try
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
                return NotFound(new { message = "Không tìm thấy sản phẩm" });

            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Unauthorized" });

            if (!IsAdmin() && !string.Equals(product.OwnerId, userId, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var deleted = await _productRepository.DeleteAsync(id);
            if (!deleted)
                return NotFound(new { message = "Không tìm thấy sản phẩm" });

            return Ok(new { message = "Sản phẩm đã được xoá" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {ProductId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi xoá sản phẩm" });
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

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public long Price { get; set; }
    public int Stock { get; set; }
}

public class UpdateProductRequest
{
    public string? Name { get; set; }
    public string? ImageUrl { get; set; }
    public long? Price { get; set; }
    public int? Stock { get; set; }
}