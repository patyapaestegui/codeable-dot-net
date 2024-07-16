using CachedInventoryClient;

var productId = 1;
var resultTasks = new List<Task<RunResult>>();
for (var operationCount = 1; operationCount < 5; operationCount++)
{
  foreach (var isParallel in new[] { true, false })
  {
    resultTasks.Add(new StockTester(50, isParallel, operationCount, productId).Run());
    Interlocked.Increment(ref productId);
  }
}

var results = await Task.WhenAll(resultTasks);

if (results.All(r => r.WasSuccessful))
{
  Console.WriteLine("¡Todas las operaciones se completaron con éxito!");
  PrintSlowRequests();
  return;
}

Console.WriteLine($"Operaciones con errores: {results.Count(r => !r.WasSuccessful)}");
Console.WriteLine($"Operaciones completadas con éxito: {results.Count(r => r.WasSuccessful)}");
Console.WriteLine("Resultados:");

foreach (var result in results.OrderBy(r => r.WasSuccessful).ThenBy(r => r.ProductId))
{
  Console.WriteLine(result);
}

PrintSlowRequests();
return;

void PrintSlowRequests()
{
  const int slowRequestThreshold = 350;
  var slowRequests = results
    .Where(r => r is { WasSuccessful: true, AverageTimeElapsedPerRequest: > slowRequestThreshold })
    .ToList();
  if (slowRequests.Count == 0)
  {
    Console.WriteLine("No hubo solicitudes lentas.");
    return;
  }

  foreach (var slowRequest in slowRequests)
  {
    Console.WriteLine($"Solicitud con más de {slowRequestThreshold} ms de media:\n {slowRequest}");
  }
}
