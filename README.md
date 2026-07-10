# CoTee API

Backend ASP.NET Core 8 + MongoDB cho hệ thống thương mại điện tử gồm xác thực, sản phẩm, giỏ hàng, đơn hàng và thanh toán MoMo.

## Triển khai trực tuyến

- Backend API: https://api.yourdomain.com/
- Swagger UI: https://api.yourdomain.com/swagger/index.html
- Frontend: https://exe101-cotee-fe.vercel.app/

## Tổng quan dự án
CoTee API là một REST API được xây dựng bằng ASP.NET Core 8, dùng MongoDB làm cơ sở dữ liệu và hỗ trợ các luồng chính sau:
- Đăng ký / đăng nhập / xác thực email / đặt lại mật khẩu
- Quản lý sản phẩm (xem công khai, tạo/sửa/xóa cho Admin)
- Quản lý giỏ hàng cho khách hàng
- Tạo đơn hàng và thanh toán qua cổng MoMo
- Quản lý người dùng và trạng thái tài khoản cho Admin
- Tài liệu API qua Swagger

## Tính năng chính
- JWT Authentication + Authorization
- Phân quyền theo vai trò: Customer, Admin
- MongoDB repository pattern
- BCrypt password hashing
- Resend API email verification và password reset
- MoMo payment gateway integration + IPN callback
- Swagger UI cho thử API

## Công nghệ sử dụng
- ASP.NET Core 8 Web API
- MongoDB.Driver
- JWT Bearer Authentication
- BCrypt.Net-Next
- Resend Email API over HTTPS
- Swashbuckle.AspNetCore
- Serilog.AspNetCore

## Cấu trúc thư mục chính
- Program.cs: khởi tạo app, DI, JWT, CORS, Swagger
- src/Controllers/: AuthController, ProductsController, CartsController, OrdersController, UsersManagementController
- src/Services/: AuthService, CartService, OrderService, UserService, EmailService
- src/Infrastructure/Repositories/: Mongo repository abstraction
- src/Entities/: User, Product, Order, Cart, BlacklistedToken

## Yêu cầu hệ thống
- .NET 8 SDK
- MongoDB chạy local tại mongodb://localhost:27017 hoặc MongoDB Atlas
- Resend API key và sender đã xác minh để gửi email xác thực / reset mật khẩu
- Tài khoản MoMo test nếu muốn chạy luồng thanh toán thật

## Hướng dẫn setup MongoDB

### Cách 1: Dùng MongoDB local đã cài sẵn trên máy (khuyên dùng)
Nếu bạn đã cài MongoDB Community Server rồi, hãy làm theo 3 bước sau:

1. Khởi động dịch vụ MongoDB
   - Windows: mở Services → tìm "MongoDB Server" → Start
   - Hoặc mở MongoDB Compass và kiểm tra server đang chạy
   - Nếu dùng terminal: chạy `mongod`

2. Kiểm tra MongoDB đã chạy đúng chưa
   ```bash
   mongosh "mongodb://localhost:27017"
   ```
   Nếu thấy shell `mongosh>` thì MongoDB đang hoạt động bình thường.

3. Dùng connection string mặc định trong project
   ```text
   mongodb://localhost:27017
   ```
   Project hiện tại đã dùng giá trị này trong `appsettings.json`, nên bạn không cần đổi gì thêm nếu chạy local.

> Nếu bạn đã cài MongoDB rồi, không cần chạy Docker. Chỉ cần đảm bảo service MongoDB đang chạy.

### Cách 1b: Chạy MongoDB bằng Docker (nếu bạn muốn dùng container)
Nếu máy bạn có Docker và muốn chạy MongoDB qua container, dùng:

```bash
docker-compose up -d
```

Lệnh này sẽ khởi động MongoDB với:
- Host: localhost
- Port: 27017
- Database: CoTeeDB

Nếu lệnh này báo lỗi, có thể Docker chưa chạy hoặc cổng 27017 đang bị chiếm. Khi đó hãy dùng cách 1 (MongoDB local) thay vì Docker.

Kiểm tra bằng:

```bash
docker ps
```

### Cách 2: Dùng MongoDB Atlas
1. Tạo cluster trên MongoDB Atlas.
2. Copy connection string theo mẫu:

```text
mongodb+srv://<username>:<password>@<cluster-url>/<database>?retryWrites=true&w=majority
```

3. Dán vào mục `MongoDbSettings.ConnectionString` trong `appsettings.json` hoặc `appsettings.Development.json`.

### Cách 3: Kiểm tra kết nối
Sau khi MongoDB đang chạy, bạn có thể thử kết nối bằng:

```bash
mongosh "mongodb://localhost:27017"
```

Nếu dùng Atlas:

```bash
mongosh "mongodb+srv://<username>:<password>@<cluster-url>/"
```

### Cấu hình database mặc định
Project đang dùng database tên:

```json
"DatabaseName": "CoTeeDB"
```

Các collection sẽ được tạo tự động khi API lưu dữ liệu lần đầu.

## Cấu hình nhanh
Sửa nội dung trong `appsettings.json` hoặc `appsettings.Development.json`:

```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "CoTeeDB"
  },
  "Jwt": {
    "SecretKey": "your-super-secret-key-here-min-32-characters-long",
    "Issuer": "CoTeeApi",
    "Audience": "CoTeeClient",
    "ExpirationMinutes": 60
  },
  "ResendSettings": {
    "ApiKey": "re_your_api_key",
    "ApiBaseUrl": "https://api.resend.com",
    "FromEmail": "onboarding@resend.dev",
    "FromName": "CoTee Account"
  },
  "MomoSettings": {
    "PartnerCode": "MOMO_PARTNER_CODE",
    "AccessKey": "MOMO_ACCESS_KEY",
    "SecretKey": "MOMO_SECRET_KEY",
    "Endpoint": "https://test-payment.momo.vn/v2/gateway/api/create",
    "RedirectUrl": "http://localhost:5173/payment-result",
    "IpnUrl": "http://localhost:5001/api/orders/momo-ipn"
  },
  "Google": {
    "Enabled": false,
    "ClientId": "your-google-oauth-client-id.apps.googleusercontent.com"
  }
}
```

> Khuyến nghị: trong môi trường production, hãy chuyển các secret sang biến môi trường hoặc secret manager.
> 
> Google login có thể cấu hình bằng biến môi trường:
> - `Google__Enabled=false`
> - `Google__ClientId=your-google-oauth-client-id.apps.googleusercontent.com`

> Lưu ý MoMo:
> - `RedirectUrl` trỏ tới trang frontend hiển thị kết quả sau khi khách hàng thanh toán xong.
> - `IpnUrl` là endpoint để MoMo gửi thông báo thanh toán (IPN) và xác nhận trạng thái đơn hàng.
> - Khi chạy local, bạn có thể cần tạo tunnel công khai (ví dụ ngrok) để MoMo có thể truy cập `IpnUrl` nếu dùng endpoint trên localhost.

## Hướng dẫn chạy dự án

### 1. Restore package
```bash
dotnet restore
```

### 2. Build project
```bash
dotnet build
```

### 3. Chạy local
```bash
cp .env.example .env
# Điền Resend API key và sender trong .env
dotnet run
```

Sau khi chạy, API sẽ sẵn sàng tại:
- HTTP: http://localhost:5001
- HTTPS: http://localhost:7001 (nếu bật launch profile)
- Swagger: http://localhost:5001/swagger/index.html

## Các API chính

### Xác thực
- POST /api/auth/register
- GET /api/auth/verify-email?token=...
- POST /api/auth/resend-verification
- POST /api/auth/login
- POST /api/auth/google-login
- POST /api/auth/forgot-password
- POST /api/auth/reset-password
- POST /api/auth/logout

### Sản phẩm
- GET /api/products
- GET /api/products/{id}
- POST /api/products (Admin)
- PUT /api/products/{id} (Admin)
- DELETE /api/products/{id} (Admin)

### Giỏ hàng
- GET /api/carts
- POST /api/carts/items
- PUT /api/carts/items
- DELETE /api/carts/items/{productId}
- DELETE /api/carts

### Đơn hàng
- POST /api/orders/checkout
- GET /api/orders/history
- GET /api/orders/{orderCode}
- POST /api/orders/{orderCode}/cancel
- GET /api/orders/admin (Admin)
- PATCH /api/orders/{orderCode}/status (Admin)
- GET /api/orders/momo-return
- POST /api/orders/momo-ipn

### Quản trị người dùng
- GET /api/admin/users
- GET /api/admin/users/{id}
- GET /api/admin/users/email/{email}
- POST /api/admin/users
- PUT /api/admin/users/{id}
- PUT /api/admin/users/{id}/toggle-status
- DELETE /api/admin/users/{id}

## Lưu ý quan trọng
- Để sử dụng luồng thanh toán MoMo, cần cung cấp đúng `PartnerCode`, `AccessKey`, `SecretKey` và endpoint test của MoMo.
- Nếu Resend chưa được cấu hình, chức năng gửi email xác thực / reset mật khẩu sẽ bị ảnh hưởng.
- Khi deploy production, nên bật HTTPS và dùng biến môi trường cho `Jwt:SecretKey`, `MomoSettings`, `ResendSettings`.

## Kết luận
Dự án này cung cấp nền tảng backend hoàn chỉnh cho một hệ thống bán hàng nhỏ đến vừa, phù hợp để phát triển tiếp các tính năng như thanh toán thật, phân tích đơn hàng, quản lý kho, voucher và dashboard admin.
