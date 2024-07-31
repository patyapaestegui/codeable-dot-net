using CachedInventory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IWarehouseStockSystemClient, WarehouseStockSystemClient>();

// Configure Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
  options.Configuration = builder.Configuration.GetConnectionString("Redis");
  options.InstanceName = "CachedInventory:";
});

var app = CachedInventoryApiBuilder.Build(args);
app.Run();
