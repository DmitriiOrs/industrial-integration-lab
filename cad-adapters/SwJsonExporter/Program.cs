using System;
using SwJsonExporter.Readers;
using SwJsonExporter.Exporters;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=== INDUSTRIAL INTEGRATION PLATFORM (CORE CORE) ===");

try
{
    ICadReader reader = new SolidWorksReader();
    var canonicalProduct = reader.ReadActiveDocument();

    IExporter exporter = new JsonExporter();
    exporter.Export(canonicalProduct);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n[Critical data bus error]: {ex.Message}");
    Console.ResetColor();
}

Console.WriteLine("\nPress any key to stop the conveyor...");
Console.ReadKey();