using System;
using System.Text.Json;
using SldWorks;
using SwJsonExporter.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8; // Чтобы консоль Windows красиво писала по-русски
Console.WriteLine("=== INDUSTRIAL INTEGRATION LAB: SW-JSON-EXPORTER ===");
Console.WriteLine("[Старт] Подключение к процессу SOLIDWORKS...");

try
{
    // 1. Подключаемся к открытому SOLIDWORKS через COM Interop
    SldWorks.SldWorks swApp = (SldWorks.SldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application")!)!;
    IModelDoc2 swModel = swApp.ActiveDoc;

    // 2. Запускаем наш сканер сборки
    var traversalService = new AssemblyTraversalService();
    var factoryData = traversalService.ParseAssembly(swModel);

    if (factoryData != null)
    {
        // 3. Упаковываем результат в красивый JSON по стандарту REST API
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true, // Красивые отступы
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase // стиль id, parentId, level
        };

        string jsonResult = JsonSerializer.Serialize(factoryData, jsonOptions);

        Console.WriteLine("\n--- ВЫГРУЖЕННЫЙ ПРОМЫШЛЕННЫЙ КОНТРАКТ ДАННЫХ (JSON) ---");
        Console.WriteLine(jsonResult);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\n[Критическая ошибка] Не удалось связаться с SOLIDWORKS: {ex.Message}");
    Console.WriteLine("Убедитесь, что SOLIDWORKS запущен и в нем открыта сборка изделия!");
}

Console.WriteLine("\nНажмите любую клавишу для завершения...");
Console.ReadKey();