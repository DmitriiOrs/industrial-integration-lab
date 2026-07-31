
namespace SwJsonExporter.Domain
{
    // Единая каноническая модель изделия для завода (не зависит от CAD или ERP)
    public class CanonicalProduct
    {
        /*
        private int _level; // Скрытая переменная

        public int Level 
        { 
            get { return _level; } 
            set 
            { 
                if (value < 0) 
                    throw new Exception("Уровень сборки не может быть отрицательным!");
                _level = value; 
            } 
        }
        */
        public string Id { get; set; } = string.Empty;
        public string? ParentId { get; set; }
        public int Level { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        // Тип сущности: Assembly, Part, SheetMetal, Weldment, Purchased
        public string Type { get; set; } = "Part";

        // Заводские атрибуты (Артикул, Материал, Масса)
        public Dictionary<string, string> CustomProperties { get; set; } = new();

        // Дочерние элементы (детали сборки или листы многотелки)
        public List<CanonicalProduct> ChildNodes { get; set; } = new();
    }
}