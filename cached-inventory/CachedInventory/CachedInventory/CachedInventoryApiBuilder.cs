namespace CachedInventory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public static class CachedInventoryApiBuilder
{
  private static readonly ConcurrentDictionary<int, object> LockObjects = new();

  public static WebApplication Build(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddScoped<IWarehouseStockSystemClient, WarehouseStockSystemClient>();
    builder.Services.AddStackExchangeRedisCache(options =>
    {
      options.Configuration = builder.Configuration.GetConnectionString("Redis");
      options.InstanceName = "CachedInventory:";
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
      app.UseSwagger();
      app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.MapGet(
            "/stock/{productId:int}",
            async ([FromServices] IWarehouseStockSystemClient client, [FromServices] IDistributedCache cache, [FromServices] ILogger<Program> logger, int productId) =>
            {
              var cacheKey = $"product_stock_{productId}";
              var cachedStock = await cache.GetStringAsync(cacheKey);
              if (cachedStock == null)
              {
                logger.LogInformation($"Cache miss for product {productId}");
                int stock = await client.GetStock(productId);
                await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(stock), new DistributedCacheEntryOptions
                {
                  AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
                });
                return stock;
              }
              else
              {
                logger.LogInformation($"Cache hit for product {productId}");
                int stock = JsonSerializer.Deserialize<int>(cachedStock);
                return stock;
              }
            })
          .WithName("GetStock")
          .WithOpenApi();

    app.MapPost(
        "/stock/retrieve",
        async ([FromServices] IWarehouseStockSystemClient client, [FromServices] IDistributedCache cache, [FromServices] ILogger<Program> logger, [FromBody] RetrieveStockRequest req) =>
        {
          var lockObject = LockObjects.GetOrAdd(req.ProductId, _ => new object());

          int stock;
          lock (lockObject)
          {
            var cacheKey = $"product_stock_{req.ProductId}";
            var cachedStock = cache.GetStringAsync(cacheKey).Result;
            if (cachedStock == null)
            {
              stock = client.GetStock(req.ProductId).Result;
              cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(stock), new DistributedCacheEntryOptions
              {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
              }).Wait();
              logger.LogInformation($"Cache miss for product {req.ProductId}. Stock: {stock}");
            }
            else
            {
              stock = JsonSerializer.Deserialize<int>(cachedStock);
              logger.LogInformation($"Cache hit for product {req.ProductId}. Stock: {stock}");
            }

            if (stock < req.Amount)
            {
              return Results.BadRequest("Not enough stock.");
            }

            stock -= req.Amount;
            cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(stock), new DistributedCacheEntryOptions
            {
              AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
            }).Wait();
          }

          await client.UpdateStock(req.ProductId, stock);
          return Results.Ok();
        })
      .WithName("RetrieveStock")
      .WithOpenApi();

    app.MapPost(
        "/stock/restock",
        async ([FromServices] IWarehouseStockSystemClient client, [FromServices] IDistributedCache cache, [FromServices] ILogger<Program> logger, [FromBody] RestockRequest req) =>
        {
          var lockObject = LockObjects.GetOrAdd(req.ProductId, _ => new object());

          int stock;
          lock (lockObject)
          {
            var cacheKey = $"product_stock_{req.ProductId}";
            var cachedStock = cache.GetStringAsync(cacheKey).Result;
            if (cachedStock == null)
            {
              stock = client.GetStock(req.ProductId).Result;
              cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(stock), new DistributedCacheEntryOptions
              {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
              }).Wait();
              logger.LogInformation($"Cache miss for product {req.ProductId}. Stock: {stock}");
            }
            else
            {
              stock = JsonSerializer.Deserialize<int>(cachedStock);
              logger.LogInformation($"Cache hit for product {req.ProductId}. Stock: {stock}");
            }

            stock += req.Amount;
            cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(stock), new DistributedCacheEntryOptions
            {
              AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
            }).Wait();
          }

          await client.UpdateStock(req.ProductId, stock);
          return Results.Ok();
        })
      .WithName("Restock")
      .WithOpenApi();

    return app;
  }
}

public record RetrieveStockRequest(int ProductId, int Amount);

public record RestockRequest(int ProductId, int Amount);