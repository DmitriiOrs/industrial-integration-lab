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
                // Исправляет кракозябры \u041F, разрешая русский язык:
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string jsonResult = JsonSerializer.Serialize(product, jsonOptions);

            Console.WriteLine("\n--- ВЫГРУЖЕННАЯ КАНОНИЧЕСКАЯ МОДЕЛЬ ИЗДЕЛИЯ (JSON) ---");
            Console.WriteLine(jsonResult);

            // 3. Сохраняем в файл на жесткий диск
            // Например, положим его прямо на диск C (убедись, что есть права на запись)
            // Или в папку с проектом
            string filePath = @"C:\Temp\ExportedAssembly.json";
            File.WriteAllText(filePath, jsonResult);

            Console.WriteLine($"\n[УСПЕХ] Файл успешно сохранен по пути: {filePath}");
        }
    }
    
}