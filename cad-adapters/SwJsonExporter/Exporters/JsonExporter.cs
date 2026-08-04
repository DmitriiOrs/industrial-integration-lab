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

            Log.Information("UPLOADED CANONICAL PRODUCT MODEL (JSON)");

            string filePath = @"C:\Temp\ExportedAssembly.json";
            File.WriteAllText(filePath, jsonResult);

            Log.Information("[SUCCESS] File saved successfully");
        }
    }
    
}