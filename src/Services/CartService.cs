using CooTee.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CooTee.Services;

public class CartService
{
    private readonly IMongoCollection<Cart> _cartCollection;
    private readonly IMongoCollection<Product> _productCollection;
    private readonly ILogger<CartService> _logger;

    public CartService(IMongoDatabase database, ILogger<CartService> logger)
    {
        if (database == null)
            throw new ArgumentNullException(nameof(database));

        _cartCollection = database.GetCollection<Cart>("carts");
        _productCollection = database.GetCollection<Product>("products");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<CartItemDetailsResponse>> AddToCartAsync(string userId, string productId, int quantity, string size)
    {
        try
        {
            ValidateUserId(userId);
            ValidateCartInput(productId, quantity);

            var product = await GetProductByIdAsync(productId);
            if (product == null)
                throw new InvalidOperationException("Sản phẩm không tồn tại");

            if (product.Stock < quantity)
                throw new InvalidOperationException("Số lượng vượt quá tồn kho");

            var userObjectId = ObjectId.Parse(userId);
            var cart = await _cartCollection.Find(Builders<Cart>.Filter.Eq("userId", userObjectId)).FirstOrDefaultAsync()
                       ?? new Cart
                       {
                           UserId = userId,
                           Items = new List<CartItem>()
                       };

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId && string.Equals(i.Size, size, StringComparison.OrdinalIgnoreCase));
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    Size = size
                });
            }

            await SaveCartAsync(cart);
            return await GetCartByUserIdAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding product {ProductId} to cart for user {UserId}", productId, userId);
            throw;
        }
    }

    public async Task<List<CartItemDetailsResponse>> UpdateCartItemAsync(string userId, string productId, int quantity, string size)
    {
        try
        {
            ValidateUserId(userId);
            ValidateCartInput(productId, quantity);

            var userObjectId = ObjectId.Parse(userId);
            var cart = await _cartCollection.Find(Builders<Cart>.Filter.Eq("userId", userObjectId)).FirstOrDefaultAsync();
            if (cart == null)
                throw new InvalidOperationException("Giỏ hàng không tồn tại");

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId && string.Equals(i.Size, size, StringComparison.OrdinalIgnoreCase));
            if (existingItem == null)
                throw new InvalidOperationException("Sản phẩm không tồn tại trong giỏ hàng");

            var product = await GetProductByIdAsync(productId);
            if (product == null)
                throw new InvalidOperationException("Sản phẩm không tồn tại");

            if (product.Stock < quantity)
                throw new InvalidOperationException("Số lượng vượt quá tồn kho");

            existingItem.Quantity = quantity;

            await SaveCartAsync(cart);
            return await GetCartByUserIdAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cart item {ProductId} for user {UserId}", productId, userId);
            throw;
        }
    }

    public async Task<List<CartItemDetailsResponse>> RemoveCartItemAsync(string userId, string productId, string? size)
    {
        try
        {
            ValidateUserId(userId);
            ValidateCartInput(productId, 1);

            var userObjectId = ObjectId.Parse(userId);
            var cart = await _cartCollection.Find(Builders<Cart>.Filter.Eq("userId", userObjectId)).FirstOrDefaultAsync();
            if (cart == null)
                throw new InvalidOperationException("Giỏ hàng không tồn tại");

            var itemsToRemove = cart.Items
                .Where(i => i.ProductId == productId && (string.IsNullOrWhiteSpace(size) || string.Equals(i.Size, size, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (itemsToRemove.Count == 0)
                throw new InvalidOperationException("Sản phẩm không tồn tại trong giỏ hàng");

            foreach (var item in itemsToRemove)
            {
                cart.Items.Remove(item);
            }

            await SaveCartAsync(cart);
            return await GetCartByUserIdAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cart item {ProductId} for user {UserId}", productId, userId);
            throw;
        }
    }

    public async Task ClearCartAsync(string userId)
    {
        try
        {
            ValidateUserId(userId);

            var userObjectId = ObjectId.Parse(userId);
            await _cartCollection.DeleteManyAsync(Builders<Cart>.Filter.Eq("userId", userObjectId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cart for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<CartItemDetailsResponse>> GetCartByUserIdAsync(string userId)
    {
        try
        {
            ValidateUserId(userId);

            var userObjectId = ObjectId.Parse(userId);
            var cart = await _cartCollection.Find(Builders<Cart>.Filter.Eq("userId", userObjectId)).FirstOrDefaultAsync();
            if (cart == null || cart.Items.Count == 0)
                return new List<CartItemDetailsResponse>();

            var productObjectIds = cart.Items
                .Select(i => ObjectId.Parse(i.ProductId))
                .Distinct()
                .ToList();
            var products = await _productCollection
                .Find(Builders<Product>.Filter.In("_id", productObjectIds))
                .ToListAsync();
            var productMap = products.ToDictionary(p => p.Id, p => p);

            var response = new List<CartItemDetailsResponse>();
            foreach (var item in cart.Items)
            {
                var product = productMap.TryGetValue(item.ProductId, out var foundProduct) ? foundProduct : null;
                response.Add(new CartItemDetailsResponse
                {
                    ProductId = item.ProductId,
                    ProductName = product?.Name ?? "Sản phẩm không tồn tại",
                    ImageUrl = product?.ImageUrl ?? string.Empty,
                    Price = product?.Price ?? 0,
                    Quantity = item.Quantity,
                    Size = item.Size
                });
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cart for user {UserId}", userId);
            throw;
        }
    }

    private async Task<Product?> GetProductByIdAsync(string productId)
    {
        if (!ObjectId.TryParse(productId, out var objectId))
            return null;

        return await _productCollection
            .Find(Builders<Product>.Filter.Eq("_id", objectId))
            .FirstOrDefaultAsync();
    }

    private async Task SaveCartAsync(Cart cart)
    {
        if (string.IsNullOrWhiteSpace(cart.Id))
            cart.Id = ObjectId.GenerateNewId().ToString();

        var objectId = ObjectId.Parse(cart.Id);
        await _cartCollection.ReplaceOneAsync(
            Builders<Cart>.Filter.Eq("_id", objectId),
            cart,
            new ReplaceOptions { IsUpsert = true });
    }

    private static void ValidateUserId(string userId)
    {
        if (!ObjectId.TryParse(userId, out _))
            throw new ArgumentException("UserId không hợp lệ", nameof(userId));
    }

    private static void ValidateCartInput(string productId, int quantity)
    {
        if (!ObjectId.TryParse(productId, out _))
            throw new ArgumentException("ProductId không hợp lệ", nameof(productId));

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Số lượng phải lớn hơn 0");
    }
}

public class CartItemDetailsResponse
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public long Price { get; set; }
    public int Quantity { get; set; }
    public string Size { get; set; } = string.Empty;
}
