namespace CachedInventoryClient;

using System.Diagnostics;
using System.Text.Json;

public class StockTester(int totalToRetrieve, bool isParallel, int operationCount, int productId)
{
  private const string LegacyFileStorageTemplate = "stock-{id}.json";
  private const string Url = "http://localhost:5250";

  private static readonly string LegacyFileStorage =
    Environment.GetEnvironmentVariable("LEGACY_FILE_STORAGE") ?? "../CachedInventory/";

  private readonly HttpClient client = new();
  private int requestCount;
  private Stopwatch stopwatch = new();

  private string SettingsSummary =>
    $"""

     - Total para retirar: {totalToRetrieve}.
     - En paralelo: {isParallel}.
     - Total de operaciones: {operationCount}.
     - ID del producto: {productId} 
     - Tiempo transcurrido: {stopwatch.ElapsedMilliseconds} ms.
     - Tiempo medio por solicitud: {TimeElapsedPerRequest} ms.
     """;

  private string TimeElapsedPerRequest => requestCount == 0
    ? "[no disponible]"
    : (stopwatch.ElapsedMilliseconds / requestCount).ToString();

  private void OutputResult(string message) =>
    Console.WriteLine($"Mensaje: {message}\nConfiguración: {SettingsSummary}");

  public async Task<RunResult> Run()
  {
    stopwatch = Stopwatch.StartNew();
    requestCount = 0;
    OutputResult("Inicio de la operación.");
    await Restock();
    var amountPerOperation = totalToRetrieve / operationCount;
    var amounts = Enumerable.Range(0, operationCount)
      .Select(_ => amountPerOperation).ToArray();
    var tasks = new List<Task<bool>>();
    foreach (var amount in amounts)
    {
      var task = Retrieve(amount);
      if (!isParallel)
      {
        await task;
      }

      tasks.Add(task);
    }

    var remaining = totalToRetrieve - amounts.Sum();
    if (remaining > 0)
    {
      tasks.Add(Retrieve(remaining));
    }

    var results = await Task.WhenAll(tasks);
    if (!results.All(r => r))
    {
      OutputResult("Error al retirar el stock.");
      return new(
        "Error al retirar el stock.",
        totalToRetrieve,
        isParallel,
        operationCount,
        productId,
        stopwatch.ElapsedMilliseconds,
        stopwatch.ElapsedMilliseconds / requestCount,
        false,
        await GetStock());
    }

    var finalStock = await GetStock();
    if (finalStock != 0)
    {
      OutputResult($"El stock final es {finalStock} en lugar de 0.");
      return new(
        $"El stock final es {finalStock} en lugar de 0.",
        totalToRetrieve,
        isParallel,
        operationCount,
        productId,
        stopwatch.ElapsedMilliseconds,
        stopwatch.ElapsedMilliseconds / requestCount,
        false,
        finalStock);
    }

    stopwatch.Stop();

    // Tiempo de espera para que se actualice el stock en el sistema antiguo.
    await Task.Delay(3_000);
    var stockFromFile = await GetStockFromFile();
    if (stockFromFile != 0)
    {
      OutputResult($"El fichero de stock no se actualizó correctamente. Stock en el fichero: {stockFromFile}.");
      return new(
        $"El fichero de stock no se actualizó correctamente. Stock en el fichero: {stockFromFile}.",
        totalToRetrieve,
        isParallel,
        operationCount,
        productId,
        stopwatch.ElapsedMilliseconds,
        stopwatch.ElapsedMilliseconds / requestCount,
        false,
        finalStock);
    }

    OutputResult("Completado con éxito.");
    return new(
      "Operación completada con éxito.",
      totalToRetrieve,
      isParallel,
      operationCount,
      productId,
      stopwatch.ElapsedMilliseconds,
      stopwatch.ElapsedMilliseconds / requestCount,
      true,
      await GetStock());
  }

  private async Task<bool> Retrieve(int amount)
  {
    var retrieveRequest = new { productId, amount };
    var retrieveRequestJson = JsonSerializer.Serialize(retrieveRequest);
    var retrieveRequestContent = new StringContent(retrieveRequestJson);
    retrieveRequestContent.Headers.ContentType = new("application/json");
    var response = await client.PostAsync($"{Url}/stock/retrieve", retrieveRequestContent);
    requestCount++;
    return response.IsSuccessStatusCode;
  }

  private async Task<int> GetStock()
  {
    var response = await client.GetAsync($"{Url}/stock/{productId}");
    var content = await response.Content.ReadAsStringAsync();
    requestCount++;
    return int.Parse(content);
  }

  private async Task<int> GetStockFromFile()
  {
    var response = await client.GetAsync($"{Url}/stock/verify-from-file/{productId}");
    var content = await response.Content.ReadAsStringAsync();
    return int.Parse(content);
  }

  private async Task Restock()
  {
    Console.WriteLine("Preparando stock inicial...");
    var currentStock = await GetStock();
    var missingStock = totalToRetrieve - currentStock;
    if (missingStock > 0)
    {
      Console.WriteLine($"Falta: {missingStock}. Reponiendo...");
      var restockRequest = new { productId, amount = missingStock };
      var restockRequestJson = JsonSerializer.Serialize(restockRequest);
      var restockRequestContent = new StringContent(restockRequestJson);
      restockRequestContent.Headers.ContentType = new("application/json");
      var response = await client.PostAsync($"{Url}/stock/restock", restockRequestContent);
      requestCount++;
      if (!response.IsSuccessStatusCode)
      {
        OutputResult("Error al reponer el stock.");
      }

      return;
    }

    if (missingStock < 0)
    {
      Console.WriteLine($"Exceso: {missingStock}. Eliminando...");
      if (!await Retrieve(-missingStock))
      {
        OutputResult("Error al reponer el stock inicial.");
      }

      requestCount++;

      return;
    }

    OutputResult("El stock ya está en el nivel deseado.");
  }

  private static string GetFileName(int productId) => Path.Combine(
    LegacyFileStorage,
    LegacyFileStorageTemplate.Replace("{id}", productId.ToString()));

  private record LegacyStock(int ProductId, int Amount);
}

public record RunResult(
  string Message,
  int TotalToRetrieve,
  bool IsParallel,
  int OperationCount,
  int ProductId,
  long TimeElapsed,
  long AverageTimeElapsedPerRequest,
  bool WasSuccessful,
  int FinalStock);
