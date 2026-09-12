using Serilog;
using SwJsonExporter.Domain;
using SwJsonExporter.Readers;
using System;
using System.IO;
using System.Text.Json;

namespace SwJsonExporter.Exporters
{
    public class JsonExporter : IExporter
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<JsonExporter>();
        public void Export(CanonicalProduct product)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string jsonResult = JsonSerializer.Serialize(product, jsonOptions);

            string filePath = @"C:\Temp\ExportedAssembly.json";

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Log.Information("Serializing canonical product model to JSON...");
            File.WriteAllText(filePath, jsonResult);

            Log.Information("JSON exported successfully to {FilePath}", filePath);
        }
    }
    
}