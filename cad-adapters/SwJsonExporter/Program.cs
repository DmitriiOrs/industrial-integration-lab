using System;
using SwJsonExporter.Readers;
using SwJsonExporter.Exporters;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=== INDUSTRIAL INTEGRATION PLATFORM (CORE CORE) ===");

try
{
    // 1. Этап Извлечения (Reader) -> Вызываем нужный плагин чтения CAD
    ICadReader reader = new SolidWorksReader();
    var canonicalProduct = reader.ReadActiveDocument();

    // 2. Этап Обработки (Processing) -> Место для будущей очистки данных
    // var cleanedProduct = new Normalizer().Process(canonicalProduct);

    // 3. Этап Загрузки (Exporter) -> Выгружаем каноническую модель в нужный формат
    IExporter exporter = new JsonExporter();
    exporter.Export(canonicalProduct);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n[Критическая ошибка шины данных]: {ex.Message}");
    Console.ResetColor();
}

Console.WriteLine("\nНажмите любую клавишу для завершения работы конвейера...");
Console.ReadKey();