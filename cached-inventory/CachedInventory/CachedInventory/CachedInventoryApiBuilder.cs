namespace CachedInventory;

using Microsoft.AspNetCore.Mvc;

public static class CachedInventoryApiBuilder
{
  public static WebApplication Build(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddScoped<IWarehouseStockSystemClient, WarehouseStockSystemClient>();
    builder.Services.AddSingleton<Cache>();

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
        async ([FromServices] Cache cache, int productId) =>
                {
                  var productos = cache.ObtenerProductos();
                  var producto = productos.FirstOrDefault(p => p.ProductId == productId);
                  return producto != null ? Results.Ok(producto.Cantidad) : Results.NotFound();
                })
      .WithName("GetStock")
      .WithOpenApi();

    app.MapPost(
        "/stock/retrieve",
        async ([FromServices] Cache cache, [FromBody] RetrieveStockRequest req) =>
                {
                  var actualizado = await cache.ActualizarStock(req.ProductId, req.Amount);
                  if (!actualizado)
                  {
                    await cache.RefrescarCache();
                    actualizado = await cache.ActualizarStock(req.ProductId, req.Amount);
                    if (!actualizado)
                    {
                      return Results.BadRequest("Not enough stock.");
                    }
                  }

                  await cache.SincronizarConSistemaAntiguoAsync();
                  return Results.Ok();
                })
      .WithName("RetrieveStock")
      .WithOpenApi();


    app.MapPost(
        "/stock/restock",
         async ([FromServices] Cache cache, [FromBody] RestockRequest req) =>
                {
                  var productos = cache.ObtenerProductos();
                  var producto = productos.FirstOrDefault(p => p.ProductId == req.ProductId);

                  if (producto != null)
                  {
                    producto.Cantidad += req.Amount;
                  }
                  else
                  {
                    productos.Add(new Producto { ProductId = req.ProductId, Nombre = "", Cantidad = req.Amount });
                  }

                  await cache.SincronizarConSistemaAntiguoAsync();
                  return Results.Ok();
                })
      .WithName("Restock")
      .WithOpenApi();

    return app;
  }
}

public record RetrieveStockRequest(int ProductId, int Amount);

public record RestockRequest(int ProductId, int Amount);
