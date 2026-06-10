using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoTee.Configuration;
using CoTee.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CoTee.Services;

public class OrderService
{
    private readonly IMongoCollection<Order> _orderCollection;
    private readonly IMongoCollection<Cart> _cartCollection;
    private readonly IMongoCollection<Product> _productCollection;
    private readonly HttpClient _httpClient;
    private readonly MomoSettings _momoSettings;
    private readonly ILogger<OrderService> _logger;
    private static readonly HashSet<string> AllowedOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pending",
        "Processing",
        "Shipping",
        "Completed",
        "Cancelled"
    };

    private static readonly HashSet<string> AllowedPaymentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pending",
        "Paid",
        "Failed"
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedStatusTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pending"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Pending", "Processing", "Cancelled" },
        ["Processing"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Processing", "Shipping", "Cancelled" },
        ["Shipping"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Shipping", "Completed" },
        ["Completed"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Completed" },
        ["Cancelled"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cancelled" }
    };

    public OrderService(IMongoDatabase database, HttpClient httpClient, MomoSettings momoSettings, ILogger<OrderService> logger)
    {
        if (database == null)
            throw new ArgumentNullException(nameof(database));

        _orderCollection = database.GetCollection<Order>("orders");
        _cartCollection = database.GetCollection<Cart>("carts");
        _productCollection = database.GetCollection<Product>("products");
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _momoSettings = momoSettings ?? throw new ArgumentNullException(nameof(momoSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _momoSettings.Validate();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<CheckoutResponse> CreateOrderAndGetMomoUrlAsync(string userId, ShippingDetails shippingDetails)
    {
        try
        {
            ValidateUserId(userId);
            if (shippingDetails == null)
                throw new ArgumentNullException(nameof(shippingDetails));

            var userObjectId = ObjectId.Parse(userId);
            var cart = await _cartCollection.Find(Builders<Cart>.Filter.Eq("userId", userObjectId)).FirstOrDefaultAsync();
            if (cart == null || cart.Items == null || cart.Items.Count == 0)
                throw new InvalidOperationException("Giỏ hàng trống");

            var productObjectIds = cart.Items
                .Select(i => ObjectId.Parse(i.ProductId))
                .Distinct()
                .ToList();
            var products = await _productCollection
                .Find(Builders<Product>.Filter.In("_id", productObjectIds))
                .ToListAsync();
            var productMap = products.ToDictionary(p => p.Id, p => p);

            var orderItems = new List<OrderItem>();
            long totalAmount = 0;

            foreach (var item in cart.Items)
            {
                if (!productMap.TryGetValue(item.ProductId, out var product))
                    throw new InvalidOperationException($"Sản phẩm {item.ProductId} không tồn tại");

                if (product.Stock < item.Quantity)
                    throw new InvalidOperationException($"Sản phẩm {product.Name} không đủ tồn kho");

                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    PriceAtPurchase = product.Price,
                    Quantity = item.Quantity,
                    Size = item.Size
                });

                totalAmount += product.Price * item.Quantity;
            }

            var orderCode = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
            var order = new Order
            {
                UserId = userId,
                OrderCode = orderCode,
                ShippingDetails = shippingDetails,
                Items = orderItems,
                TotalAmount = totalAmount,
                PaymentStatus = "Pending",
                OrderStatus = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            var requestId = Guid.NewGuid().ToString("N");
            var extraData = string.Empty;
            var orderInfo = $"Thanh toán đơn hàng {orderCode}";
            var rawData = $"partnerCode={_momoSettings.PartnerCode}&accessKey={_momoSettings.AccessKey}&requestId={requestId}&amount={totalAmount}&orderId={orderCode}&orderInfo={orderInfo}&returnUrl={_momoSettings.ReturnUrl}&notifyUrl={_momoSettings.IpnUrl}&extraData={extraData}&requestType=captureWallet";
            var signature = ComputeHmacSha256(rawData, _momoSettings.SecretKey);

            var momoRequest = new
            {
                partnerCode = _momoSettings.PartnerCode,
                accessKey = _momoSettings.AccessKey,
                requestId,
                amount = totalAmount,
                orderId = orderCode,
                orderInfo,
                returnUrl = _momoSettings.ReturnUrl,
                notifyUrl = _momoSettings.IpnUrl,
                extraData,
                requestType = "captureWallet",
                signature
            };

            var json = JsonSerializer.Serialize(momoRequest);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(_momoSettings.Endpoint, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"MoMo API call failed with status {response.StatusCode}: {responseBody}");

            var paymentResponse = JsonSerializer.Deserialize<MomoPaymentResponse>(responseBody);
            if (paymentResponse == null || string.IsNullOrWhiteSpace(paymentResponse.PayUrl))
                throw new InvalidOperationException("MoMo không trả về payUrl hợp lệ");

            await _orderCollection.InsertOneAsync(order);

            return new CheckoutResponse
            {
                OrderCode = orderCode,
                PayUrl = paymentResponse.PayUrl,
                TotalAmount = totalAmount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checkout for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<Order>> GetAllOrdersAsync()
    {
        return await _orderCollection
            .Find(_ => true)
            .SortByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Order>> GetUserOrdersAsync(string userId)
    {
        ValidateUserId(userId);

        var userObjectId = ObjectId.Parse(userId);
        return await _orderCollection
            .Find(Builders<Order>.Filter.Eq("userId", userObjectId))
            .SortByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<Order?> GetOrderByCodeAsync(string userId, string orderCode, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
            throw new ArgumentException("orderCode không được để trống", nameof(orderCode));

        if (isAdmin)
        {
            return await _orderCollection
                .Find(Builders<Order>.Filter.Eq("orderCode", orderCode))
                .FirstOrDefaultAsync();
        }

        ValidateUserId(userId);
        var userObjectId = ObjectId.Parse(userId);
        return await _orderCollection
            .Find(Builders<Order>.Filter.And(
                Builders<Order>.Filter.Eq("userId", userObjectId),
                Builders<Order>.Filter.Eq("orderCode", orderCode)))
            .FirstOrDefaultAsync();
    }

    public async Task<Order?> CancelOrderAsync(string userId, string orderCode, bool isAdmin)
    {
        ValidateUserId(userId);
        if (string.IsNullOrWhiteSpace(orderCode))
            throw new ArgumentException("orderCode không được để trống", nameof(orderCode));

        var filter = Builders<Order>.Filter.Eq("orderCode", orderCode);
        if (!isAdmin)
        {
            var userObjectId = ObjectId.Parse(userId);
            filter = Builders<Order>.Filter.And(
                filter,
                Builders<Order>.Filter.Eq("userId", userObjectId));
        }

        var order = await _orderCollection.Find(filter).FirstOrDefaultAsync();
        if (order == null)
            return null;

        if (order.PaymentStatus == "Paid")
            throw new InvalidOperationException("Không thể hủy đơn đã thanh toán");

        order.PaymentStatus = "Failed";
        order.OrderStatus = "Cancelled";

        var objectId = ObjectId.Parse(order.Id);
        await _orderCollection.ReplaceOneAsync(
            Builders<Order>.Filter.Eq("_id", objectId),
            order,
            new ReplaceOptions { IsUpsert = false });

        return order;
    }

    public async Task<Order?> UpdateOrderStatusAsync(string orderCode, string? orderStatus, string? paymentStatus)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
            throw new ArgumentException("orderCode không được để trống", nameof(orderCode));

        if (string.IsNullOrWhiteSpace(orderStatus) && string.IsNullOrWhiteSpace(paymentStatus))
            throw new ArgumentException("Cần cung cấp trạng thái đơn hàng hoặc trạng thái thanh toán", nameof(orderStatus));

        var order = await _orderCollection.Find(o => o.OrderCode == orderCode).FirstOrDefaultAsync();
        if (order == null)
            return null;

        var normalizedOrderStatus = NormalizeStatus(orderStatus);
        var normalizedPaymentStatus = NormalizeStatus(paymentStatus);

        ValidateStatusValue(normalizedOrderStatus, AllowedOrderStatuses, nameof(orderStatus));
        ValidateStatusValue(normalizedPaymentStatus, AllowedPaymentStatuses, nameof(paymentStatus));

        var nextOrderStatus = string.IsNullOrWhiteSpace(normalizedOrderStatus)
            ? order.OrderStatus
            : normalizedOrderStatus;
        var nextPaymentStatus = string.IsNullOrWhiteSpace(normalizedPaymentStatus)
            ? order.PaymentStatus
            : normalizedPaymentStatus;

        ValidateStatusTransition(order.OrderStatus, nextOrderStatus, nextPaymentStatus);

        order.OrderStatus = nextOrderStatus;
        order.PaymentStatus = nextPaymentStatus;

        var objectId = ObjectId.Parse(order.Id);
        await _orderCollection.ReplaceOneAsync(
            Builders<Order>.Filter.Eq("_id", objectId),
            order,
            new ReplaceOptions { IsUpsert = false });

        return order;
    }

    public async Task<MomoIpnResult> ProcessMomoIpnAsync(MomoIpnRequest request)
    {
        try
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            using var session = await _orderCollection.Database.Client.StartSessionAsync();
            var transactionStarted = false;

            try
            {
                session.StartTransaction();
                transactionStarted = true;

                var order = await _orderCollection.Find(session, Builders<Order>.Filter.Eq(o => o.OrderCode, request.OrderId)).FirstOrDefaultAsync();
                if (order == null)
                {
                    _logger.LogWarning("MoMo IPN received for unknown order {OrderId}", request.OrderId);
                    await session.AbortTransactionAsync();
                    return new MomoIpnResult
                    {
                        ResultCode = 1,
                        Message = "Order not found"
                    };
                }

                var validationResult = ValidateMomoWebhookAsync(request, order);
                if (validationResult != null)
                {
                    _logger.LogWarning("MoMo IPN validation failed for order {OrderId}: {Message}", request.OrderId, validationResult.Message);
                    await session.AbortTransactionAsync();
                    return validationResult;
                }

                if (order.PaymentStatus == "Paid")
                {
                    await session.CommitTransactionAsync();
                    return new MomoIpnResult
                    {
                        ResultCode = 0,
                        Message = "Success"
                    };
                }

                if (request.ResultCode == 0)
                {
                    var stockUpdateSucceeded = await DeductStockAsync(session, order.Items);
                    if (!stockUpdateSucceeded)
                    {
                        order.PaymentStatus = "Failed";
                        order.OrderStatus = "Pending";
                    }
                    else
                    {
                        order.PaymentStatus = "Paid";
                        order.OrderStatus = "Processing";
                        await ClearCartForUserAsync(session, order.UserId);
                    }
                }
                else
                {
                    order.PaymentStatus = "Failed";
                    order.OrderStatus = "Pending";
                }

                var objectId = ObjectId.Parse(order.Id);
                await _orderCollection.ReplaceOneAsync(
                    session,
                    Builders<Order>.Filter.Eq("_id", objectId),
                    order,
                    new ReplaceOptions { IsUpsert = false });

                await session.CommitTransactionAsync();

                return new MomoIpnResult
                {
                    ResultCode = 0,
                    Message = "Success"
                };
            }
            catch
            {
                if (transactionStarted)
                    await session.AbortTransactionAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MoMo IPN for order {OrderId}", request?.OrderId);
            throw;
        }
    }

    private async Task<bool> DeductStockAsync(IClientSessionHandle session, List<OrderItem> items)
    {
        var productIds = items
            .Select(item => ObjectId.Parse(item.ProductId))
            .Distinct()
            .ToList();

        var products = await _productCollection
            .Find(session, Builders<Product>.Filter.In("_id", productIds))
            .ToListAsync();

        var productMap = products.ToDictionary(product => product.Id, product => product);

        foreach (var item in items)
        {
            if (!productMap.TryGetValue(item.ProductId, out var product) || product.Stock < item.Quantity)
                return false;

            product.Stock -= item.Quantity;
            var result = await _productCollection.ReplaceOneAsync(
                session,
                Builders<Product>.Filter.Eq("_id", ObjectId.Parse(item.ProductId)),
                product,
                new ReplaceOptions { IsUpsert = false });

            if (result.ModifiedCount == 0)
                return false;
        }

        return true;
    }

    private async Task ClearCartForUserAsync(IClientSessionHandle session, string userId)
    {
        if (!ObjectId.TryParse(userId, out var userObjectId))
            return;

        await _cartCollection.DeleteManyAsync(session, Builders<Cart>.Filter.Eq("userId", userObjectId));
    }

    private static string NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim();
    }

    private static void ValidateStatusValue(string? status, HashSet<string> allowedStatuses, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(status))
            return;

        if (!allowedStatuses.Contains(status))
            throw new ArgumentException($"{parameterName} không hợp lệ", parameterName);
    }

    private static void ValidateStatusTransition(string currentOrderStatus, string nextOrderStatus, string nextPaymentStatus)
    {
        if (!AllowedStatusTransitions.TryGetValue(currentOrderStatus, out var allowedTransitions))
            throw new InvalidOperationException($"Không thể xác định luồng trạng thái cho {currentOrderStatus}");

        if (!allowedTransitions.Contains(nextOrderStatus))
            throw new InvalidOperationException($"Không thể chuyển từ {currentOrderStatus} sang {nextOrderStatus}");

        if (string.Equals(nextPaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(nextOrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Không thể thanh toán thành công cho đơn đã hủy");
        }

        if (string.Equals(nextPaymentStatus, "Failed", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(nextOrderStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Không thể hoàn thành đơn khi thanh toán thất bại");
        }

        if (string.Equals(nextPaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(nextOrderStatus, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Không thể đánh dấu đơn đã thanh toán khi trạng thái đơn hàng ở Pending");
        }
    }

    private string BuildIpnSignature(MomoIpnRequest request)
    {
        var rawData = string.Join("&", new[]
        {
            ($"partnerCode={request.PartnerCode}"),
            ($"accessKey={_momoSettings.AccessKey}"),
            ($"requestId={request.RequestId}"),
            ($"amount={request.Amount}"),
            ($"orderId={request.OrderId}"),
            ($"orderInfo={request.OrderInfo}"),
            ($"orderType={request.OrderType}"),
            ($"transId={request.TransId}"),
            ($"resultCode={request.ResultCode}"),
            ($"message={request.Message}"),
            ($"payType={request.PayType}"),
            ($"responseTime={request.ResponseTime}"),
            ($"extraData={request.ExtraData}")
        });

        return ComputeHmacSha256(rawData, _momoSettings.SecretKey);
    }

    private MomoIpnResult? ValidateMomoWebhookAsync(MomoIpnRequest request, Order order)
    {
        if (!string.Equals(request.PartnerCode, _momoSettings.PartnerCode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.AccessKey, _momoSettings.AccessKey, StringComparison.OrdinalIgnoreCase))
        {
            return new MomoIpnResult
            {
                ResultCode = 1,
                Message = "Invalid partner or access key"
            };
        }

        var expectedSignature = BuildIpnSignature(request);
        if (!string.Equals(expectedSignature, request.Signature, StringComparison.OrdinalIgnoreCase))
        {
            return new MomoIpnResult
            {
                ResultCode = 1,
                Message = "Invalid signature"
            };
        }

        if (request.Amount != order.TotalAmount)
        {
            return new MomoIpnResult
            {
                ResultCode = 1,
                Message = "Amount mismatch"
            };
        }

        return null;
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ValidateUserId(string userId)
    {
        if (!ObjectId.TryParse(userId, out _))
            throw new ArgumentException("UserId không hợp lệ", nameof(userId));
    }
}

public class CheckoutResponse
{
    public string OrderCode { get; set; } = string.Empty;
    public string PayUrl { get; set; } = string.Empty;
    public long TotalAmount { get; set; }
}

public class MomoIpnResult
{
    public int ResultCode { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class MomoPaymentResponse
{
    [JsonPropertyName("payUrl")]
    public string PayUrl { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; set; }
}

public class MomoIpnRequest
{
    [JsonPropertyName("partnerCode")]
    public string PartnerCode { get; set; } = string.Empty;

    [JsonPropertyName("accessKey")]
    public string AccessKey { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("orderInfo")]
    public string OrderInfo { get; set; } = string.Empty;

    [JsonPropertyName("orderType")]
    public string OrderType { get; set; } = string.Empty;

    [JsonPropertyName("transId")]
    public string TransId { get; set; } = string.Empty;

    [JsonPropertyName("resultCode")]
    public int ResultCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("payType")]
    public string PayType { get; set; } = string.Empty;

    [JsonPropertyName("responseTime")]
    public long ResponseTime { get; set; }

    [JsonPropertyName("extraData")]
    public string ExtraData { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}
