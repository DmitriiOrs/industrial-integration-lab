using System;
using System.Text.Json;
using SwJsonExporter.Domain;
using System.IO;

namespace SwJsonExporter.Exporters
{
    public class JsonExporter : IExporter
    {
        public void Export(CanonicalProduct product)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string jsonResult = JsonSerializer.Serialize(product, jsonOptions);

            Console.WriteLine("\n--- UPLOADED CANONICAL PRODUCT MODEL (JSON) ---");
            Console.WriteLine(jsonResult);

            string filePath = @"C:\Temp\ExportedAssembly.json";
            File.WriteAllText(filePath, jsonResult);

            Console.WriteLine($"\n[SUCCESS] File saved successfully to path: {filePath}");
        }
    }
    
}