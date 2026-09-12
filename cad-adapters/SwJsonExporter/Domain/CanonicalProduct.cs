
namespace SwJsonExporter.Domain
{
    public class CanonicalProduct
    {
        public string Id { get; set; } = string.Empty;
        public string? ParentId { get; set; }
        public int Level { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = "Part";

        public bool IsVirtual { get; set; }

        public string? SourceDocument { get; set; }
        public string? Configuration { get; set; }
        public Dictionary<string, string> CustomProperties { get; set; } = new();

        public List<CanonicalProduct> ChildNodes { get; set; } = new();
    }
}