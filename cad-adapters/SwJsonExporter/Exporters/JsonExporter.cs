using System;
using System.Text.Json;
using SwJsonExporter.Domain;

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
                // Исправляет кракозябры \u041F, разрешая русский язык:
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string jsonResult = JsonSerializer.Serialize(product, jsonOptions);

            Console.WriteLine("\n--- ВЫГРУЖЕННАЯ КАНОНИЧЕСКАЯ МОДЕЛЬ ИЗДЕЛИЯ (JSON) ---");
            Console.WriteLine(jsonResult);
        }
    }
}