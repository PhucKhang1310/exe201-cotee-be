using CoTee.Entities;
using CoTee.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;

namespace CoTee.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMongoRepository<Product> _productRepository;
    private readonly IMongoCollection<Product> _productCollection;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IMongoRepository<Product> productRepository,
        IMongoDatabase database,
        ILogger<ProductsController> logger)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _productCollection = (database ?? throw new ArgumentNullException(nameof(database))).GetCollection<Product>("products");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Authorize]
    [HttpGet("admin-summary")]
    public async Task<ActionResult<IEnumerable<ProductSummaryResponse>>> GetAdminProductSummaries()
    {
        try
        {
            if (!IsAdmin())
                return Forbid();

            var inlineImageRegex = new BsonRegularExpression("^data:image/", "i");
            var hasInlineImageExpression = new BsonDocument("$regexMatch", new BsonDocument
            {
                { "input", new BsonDocument("$ifNull", new BsonArray { "$imageUrl", "" }) },
                { "regex", inlineImageRegex }
            });
            var documents = await _productCollection.Aggregate()
                .Project(new BsonDocument
                {
                    { "_id", 0 },
                    { "id", new BsonDocument("$toString", "$_id") },
                    { "name", "$name" },
                    { "imageUrl", new BsonDocument("$cond", new BsonArray
                        {
                            hasInlineImageExpression,
                            BsonNull.Value,
                            "$imageUrl"
                        })
                    },
                    { "imageThumbnailUrl", "$imageThumbnailUrl" },
                    { "hasInlineImage", hasInlineImageExpression },
                    { "price", "$price" },
                    { "stock", "$stock" }
                })
                .ToListAsync();

            var products = documents.Select(document => new ProductSummaryResponse
            {
                Id = GetString(document, "id"),
                Name = GetString(document, "name"),
                ImageUrl = GetNullableString(document, "imageUrl"),
                ImageThumbnailUrl = GetNullableString(document, "imageThumbnailUrl"),
                HasInlineImage = GetBoolean(document, "hasInlineImage"),
                Price = GetInt64(document, "price"),
                Stock = GetInt32(document, "stock")
            });

            return Ok(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving admin product summaries");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi lấy danh sách sản phẩm" });
        }
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
                ImageThumbnailUrl = request.ImageThumbnailUrl,
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

            if (request.ImageThumbnailUrl != null)
                product.ImageThumbnailUrl = request.ImageThumbnailUrl;

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

    private static string GetString(BsonDocument document, string key)
    {
        var value = document.GetValue(key, BsonNull.Value);
        return value.IsString ? value.AsString : string.Empty;
    }

    private static string? GetNullableString(BsonDocument document, string key)
    {
        var value = document.GetValue(key, BsonNull.Value);
        return value.IsString ? value.AsString : null;
    }

    private static long GetInt64(BsonDocument document, string key)
    {
        var value = document.GetValue(key, BsonNull.Value);
        return value.IsBsonNull ? 0 : Convert.ToInt64(BsonTypeMapper.MapToDotNetValue(value));
    }

    private static int GetInt32(BsonDocument document, string key)
    {
        var value = document.GetValue(key, BsonNull.Value);
        return value.IsBsonNull ? 0 : Convert.ToInt32(BsonTypeMapper.MapToDotNetValue(value));
    }

    private static bool GetBoolean(BsonDocument document, string key)
    {
        var value = document.GetValue(key, BsonBoolean.False);
        return value.IsBoolean && value.AsBoolean;
    }
}

public class ProductSummaryResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ImageThumbnailUrl { get; set; }
    public bool HasInlineImage { get; set; }
    public long Price { get; set; }
    public int Stock { get; set; }
}

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ImageThumbnailUrl { get; set; }
    public long Price { get; set; }
    public int Stock { get; set; }
}

public class UpdateProductRequest
{
    public string? Name { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageThumbnailUrl { get; set; }
    public long? Price { get; set; }
    public int? Stock { get; set; }
}
