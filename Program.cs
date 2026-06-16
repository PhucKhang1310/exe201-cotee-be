using CoTee.Configuration;
using CoTee.Entities;
using CoTee.Infrastructure.Repositories;
using CoTee.Services;
using MongoDB.Driver;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using CoTee.Middleware;

LoadDotEnv(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var builder = WebApplication.CreateBuilder(args);




var mongoDbSettings = new MongoDbSettings();
builder.Configuration.GetSection("MongoDbSettings").Bind(mongoDbSettings);

var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("Jwt").Bind(jwtSettings);

var resendSettings = new ResendSettings();
builder.Configuration.GetSection("ResendSettings").Bind(resendSettings);

var appSettings = new AppSettings();
builder.Configuration.GetSection("AppSettings").Bind(appSettings);

var googleSettings = new CoTee.Configuration.GoogleSettings();
builder.Configuration.GetSection("Google").Bind(googleSettings);

var momoSettings = new MomoSettings();
builder.Configuration.GetSection("MomoSettings").Bind(momoSettings);

var swaggerEnabled = builder.Configuration.GetValue<bool>("Swagger:Enabled");


try
{
    mongoDbSettings.Validate();
    jwtSettings.Validate();
    if (!appSettings.AutoVerifyEmailOnRegistration)
        resendSettings.Validate();
    appSettings.Validate();
    googleSettings.Validate();
    momoSettings.Validate();
}
catch (InvalidOperationException ex)
{
    throw new InvalidOperationException("Configuration validation failed", ex);
}




builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

builder.Services.Configure<ResendSettings>(
    builder.Configuration.GetSection("ResendSettings"));

builder.Services.Configure<AppSettings>(
    builder.Configuration.GetSection("AppSettings"));

builder.Services.Configure<GoogleSettings>(
    builder.Configuration.GetSection("Google"));

builder.Services.Configure<MomoSettings>(
    builder.Configuration.GetSection("MomoSettings"));

builder.Services.Configure<OpenAiSettings>(
    builder.Configuration.GetSection("OpenAi"));


builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(resendSettings);
builder.Services.AddSingleton(appSettings);
builder.Services.AddSingleton(googleSettings);
builder.Services.AddSingleton(momoSettings);


builder.Services.AddSingleton<IMongoClient>(
    new MongoClient(mongoDbSettings.ConnectionString));


builder.Services.AddSingleton(serviceProvider =>
{
    var client = serviceProvider.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoDbSettings.DatabaseName);
});




builder.Services.AddScoped<IMongoRepository<User>>(serviceProvider =>
{
    var database = serviceProvider.GetRequiredService<IMongoDatabase>();
    return new MongoRepository<User>(database, "users");
});

builder.Services.AddScoped<IMongoRepository<Product>>(serviceProvider =>
{
    var database = serviceProvider.GetRequiredService<IMongoDatabase>();
    return new MongoRepository<Product>(database, "products");
});


builder.Services.AddScoped<IMongoRepository<BlacklistedToken>>(serviceProvider =>
{
    var database = serviceProvider.GetRequiredService<IMongoDatabase>();
    return new MongoRepository<BlacklistedToken>(database, "blacklisted_tokens");
});




builder.Services.AddHttpClient<IEmailService, EmailService>(client =>
{
    client.BaseAddress = new Uri(resendSettings.ApiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});




builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
if (builder.Configuration.GetValue<bool>("OpenAi:UseMock"))
{
    builder.Services.AddScoped<IOpenAiImageService, OpenAiImageMockService>();
    builder.Services.AddScoped<IOpenAiChatService, OpenAiChatMockService>();
}
else
{
    builder.Services.AddHttpClient<OpenAiImageRealService>();
    builder.Services.AddHttpClient<OpenAiChatRealService>();
    builder.Services.AddScoped<IOpenAiImageService, OpenAiImageRealService>();
    builder.Services.AddScoped<IOpenAiChatService, OpenAiChatRealService>();
}
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddHttpClient<OrderService>();




builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });





builder.Services.AddControllers();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CoTee API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT token theo định dạng: Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policyBuilder =>
    {
        policyBuilder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});


builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});



var app = builder.Build();


app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTimeOffset.UtcNow
}));


app.UseForwardedHeaders();



if (app.Environment.IsDevelopment() || swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CoTee API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");


app.UseAuthentication();
app.UseMiddleware<BlacklistMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

static void LoadDotEnv(string path)
{
    if (!File.Exists(path))
        return;

    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            continue;

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
            continue;

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(key) || Environment.GetEnvironmentVariable(key) != null)
            continue;

        Environment.SetEnvironmentVariable(key, value);
    }
}
