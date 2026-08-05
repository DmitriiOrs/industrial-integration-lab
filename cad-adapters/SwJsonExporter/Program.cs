using Serilog;
using SwJsonExporter.Exporters;
using SwJsonExporter.Processing;
using SwJsonExporter.Readers;
using System;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(@"C:\Temp\ExportLogs\etl-export-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("=== INDUSTRIAL INTEGRATION PLATFORM (CORE) ===");
    Log.Information("Starting extraction pipeline...");

    ICadReader reader = new SolidWorksReader();
    var canonicalProduct = reader.ReadActiveDocument();

    var normalizer = new DataNormalizer();
    normalizer.Normalize(canonicalProduct);

    IExporter exporter = new JsonExporter();
    exporter.Export(canonicalProduct);

    Log.Information("Extraction pipeline finished successfully.");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Critical data bus error during extraction!");
}
finally
{
    Console.WriteLine("\nPress any key to stop the conveyor...");
    Console.ReadKey();

    Log.CloseAndFlush();
}