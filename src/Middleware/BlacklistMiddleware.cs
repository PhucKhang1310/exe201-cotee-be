using System;
using System.Linq;
using System.Threading.Tasks;
using CooTee.Entities;
using CooTee.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CooTee.Middleware;

public class BlacklistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;

    public BlacklistMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var scope = _scopeFactory.CreateScope();
        var blacklistRepository = scope.ServiceProvider.GetRequiredService<IMongoRepository<BlacklistedToken>>();

        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                var entry = await blacklistRepository.FindOneAsync("token", token);
                if (entry != null)
                {
                    
                    if (entry.ExpiresAt > DateTime.UtcNow)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new { message = "Token is blacklisted" });
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}
