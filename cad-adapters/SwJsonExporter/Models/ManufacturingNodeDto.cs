using System;
using System.Collections.Generic;
using System.Text;

namespace SwJsonExporter.Models
{
    // Универсальный узел нашей промышленной сети данных (Единый источник истины)
    public class ManufacturingNodeDto
    {
        // Обязательные поля гибридной модели для ERP / PLM
        public string Id { get; set; } = string.Empty;
        public string? ParentId { get; set; }
        public int Level { get; set; }
        public string Path { get; set; } = string.Empty;

        // Общепроизводственные параметры
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Assembly, SubAssembly, Part, WeldmentProfile, SheetMetal

        public Dictionary<string, string> CustomProperties { get; set; } = new();

        // Специфические блоки (заполняются только при совпадении типа детали)
        public WeldmentData? WeldmentProperties { get; set; }
        public SheetMetalData? SheetMetalProperties { get; set; }

        // Дочерние элементы (иерархическое дерево)
        public List<ManufacturingNodeDto> ChildNodes { get; set; } = new();
    }

    public class WeldmentData
    {
        public double Length { get; set; }
        public string Profile { get; set; } = string.Empty;
    }

    public class SheetMetalData
    {
        public double Thickness { get; set; }
        public double BendingRadius { get; set; }
    }
}