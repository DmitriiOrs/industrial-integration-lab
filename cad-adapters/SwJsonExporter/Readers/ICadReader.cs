
using SwJsonExporter.Domain;

namespace SwJsonExporter.Readers
{
    public interface ICadReader
    {
        bool IsAvailable();
        CanonicalProduct ReadActiveDocument();
    }
}
