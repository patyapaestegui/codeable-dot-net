namespace CachedInventory;

using System.Text.Json;

public interface IWarehouseStockSystemClient
{
  /// <summary>
  ///   Obtiene el stock de un producto.
  /// </summary>
  /// <param name="productId">El identificador del producto.</param>
  /// <returns>El stock de dicho producto.</returns>
  Task<int> GetStock(int productId);

  /// <summary>
  ///   Actualiza el stock de un producto.
  /// </summary>
  /// <param name="productId">El identificador del producto a actualizar.</param>
  /// <param name="newAmount">La cantidad de producto a asignar.</param>
  Task UpdateStock(int productId, int newAmount);

  /// <summary>
  ///   NO USAR, Simula comprobar el fichero directamente, se usa para testear.
  /// </summary>
  /// <param name="productId">El identificador del producto.</param>
  /// <returns>El stock de dicho producto.</returns>
  Task<int> GetStockDirectlyFromFile(int productId);
}

/// <summary>
///   Cliente que accede al sistema antiguo de almacén.
///   Desafortunadamente, el sistema no fue diseñado con integraciones en mente, y solamente expone dos puntos de acceso
///   para obtener el stock de un producto y actualizarlo. Es significativamente lento y se programó para un procesador
///   antiguo que tenía un error en una instrucción de coma flotante, por lo que no se puede trasladar a un servidor
///   más moderno que no tenga ese error sin modificar el código. Tampoco tenemos el código, así que el retraso es
///   inevitable.
///   Por fortuna, hemos podido prohibir al cliente de escritorio acceso a los ficheros de lectura, así que solamente el
///   servicio al que conecta este cliente puede leer y escribir en ellos. Esto nos permite mantener la integridad del
///   stock.
/// </summary>
public class WarehouseStockSystemClient : IWarehouseStockSystemClient
{
  private const string LegacyFileStorageTemplate = "stock-{id}.json";
  private static readonly string LegacyFileStorage = Environment.GetEnvironmentVariable("LEGACY_FILE_STORAGE") ?? "./";

  /// <summary>
  ///   Obtiene el stock de un producto.
  /// </summary>
  /// <param name="productId">El identificador del producto.</param>
  /// <returns>El stock de dicho producto.</returns>
  public async Task<int> GetStock(int productId)
  {
    await WaitForDatabase();
    try
    {
      return JsonSerializer.Deserialize<LegacyStock>(await File.ReadAllTextAsync(GetFileName(productId)))?.Amount ?? 0;
    }
    catch
    {
      return 0;
    }
  }

  public async Task<int> GetStockDirectlyFromFile(int productId) =>
    JsonSerializer.Deserialize<LegacyStock>(await File.ReadAllTextAsync(GetFileName(productId)))!.Amount;

  /// <summary>
  ///   Actualiza el stock de un producto.
  /// </summary>
  /// <param name="productId">El identificador del producto a actualizar.</param>
  /// <param name="newAmount">La cantidad de producto a asignar.</param>
  public async Task UpdateStock(int productId, int newAmount)
  {
    await WaitForDatabase();
    var stock = new LegacyStock(productId, newAmount);
    await File.WriteAllTextAsync(GetFileName(productId), JsonSerializer.Serialize(stock));
  }

  private static string GetFileName(int productId) => Path.Combine(
    LegacyFileStorage,
    LegacyFileStorageTemplate.Replace("{id}", productId.ToString()));

  // Este retraso simula la latencia del sistema antiguo..
  private static async Task WaitForDatabase() => await Task.Delay(2_500);

  private record LegacyStock(int ProductId, int Amount);
}
