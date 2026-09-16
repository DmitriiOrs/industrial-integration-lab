using Serilog;
using SwJsonExporter.Exporters;
using SwJsonExporter.Processing;
using SwJsonExporter.Readers;
using System;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(
    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(@"C:\Temp\ExportLogs\etl-export-.txt", 
    rollingInterval: RollingInterval.Day,
    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
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