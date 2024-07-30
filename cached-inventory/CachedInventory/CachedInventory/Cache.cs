using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CachedInventory;

public class Cache
{
  private List<Producto> productosCache;
  private DateTime ultimaActualizacion;
  private readonly object cacheLock = new object();
  private readonly Timer syncTimer;
  private readonly IWarehouseStockSystemClient warehouseClient;

  public Cache(IWarehouseStockSystemClient client)
  {
    warehouseClient = client;
    productosCache = RefrescarCache().Result; // Inicializa la caché con datos del sistema antiguo
    syncTimer = new Timer(async _ => await SincronizarConSistemaAntiguoAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10)); // Configura el Timer para sincronizar cada 10 segundos
  }

  public List<Producto> ObtenerProductos()
  {
    lock (cacheLock)
    {
      if (productosCache == null || DateTime.Now > ultimaActualizacion.AddMinutes(30))
      {
        productosCache = RefrescarCache().Result;
      }
      return productosCache;
    }
  }
  public async Task<bool> ActualizarStock(int productId, int cantidad)
  {
    bool actualizado;
    lock (cacheLock)
    {
      var item = productosCache.FirstOrDefault(p => p.ProductId == productId);
      if (item != null && item.Cantidad >= cantidad)
      {
        item.Cantidad -= cantidad;
        actualizado = true;
      }
      else
      {
        actualizado = false;
      }
    }
    return await Task.FromResult(actualizado);
  }
  public async Task<List<Producto>> RefrescarCache()
  {
    var productos = new List<Producto>();
    for (int i = 1; i <= 5; i++) // Supongamos que tienes 5 productos
    {
      var stock = await warehouseClient.GetStock(i); // Llamada asincrónica
      productos.Add(new Producto { ProductId = i, Nombre = $"Producto {i}", Cantidad = stock });
    }
    lock (cacheLock)
    {
      productosCache = productos;
      ultimaActualizacion = DateTime.Now;
    }
    return productosCache;
  }
  public async Task SincronizarConSistemaAntiguoAsync()
  {
    List<Producto> productos;
    lock (cacheLock)
    {
      productos = new List<Producto>(productosCache); // Crea una copia de la caché actual
    }
    foreach (var producto in productos)
    {
      await warehouseClient.UpdateStock(producto.ProductId, producto.Cantidad); // Llamada asincrónica fuera del bloque lock
    }
  }
}

public class Producto
{
  public int ProductId { get; set; }
  public string Nombre { get; set; } = string.Empty;
  public int Cantidad { get; set; }
}
