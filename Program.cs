using CooTee.Configuration;
using CooTee.Entities;
using CooTee.Infrastructure.Repositories;
using CooTee.Services;
using MongoDB.Driver;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using CooTee.Middleware;

var builder = WebApplication.CreateBuilder(args);




var mongoDbSettings = new MongoDbSettings();
builder.Configuration.GetSection("MongoDbSettings").Bind(mongoDbSettings);

var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("Jwt").Bind(jwtSettings);

var smtpSettings = new SmtpSettings();
builder.Configuration.GetSection("SmtpSettings").Bind(smtpSettings);

var appSettings = new AppSettings();
builder.Configuration.GetSection("AppSettings").Bind(appSettings);

var momoSettings = new MomoSettings();
builder.Configuration.GetSection("MomoSettings").Bind(momoSettings);


try
{
    mongoDbSettings.Validate();
    jwtSettings.Validate();
    smtpSettings.Validate();
    appSettings.Validate();
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

builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("SmtpSettings"));

builder.Services.Configure<AppSettings>(
    builder.Configuration.GetSection("AppSettings"));

builder.Services.Configure<MomoSettings>(
    builder.Configuration.GetSection("MomoSettings"));


builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(smtpSettings);
builder.Services.AddSingleton(appSettings);
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




builder.Services.AddScoped<IEmailService, EmailService>();




builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CooTee API", Version = "v1" });

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




var app = builder.Build();




if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");


app.UseAuthentication();
app.UseMiddleware<BlacklistMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
