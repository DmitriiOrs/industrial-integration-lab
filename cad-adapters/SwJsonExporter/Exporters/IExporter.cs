
using SwJsonExporter.Domain;

namespace SwJsonExporter.Exporters
{
    public interface IExporter
    {
        void Export(CanonicalProduct product);
    }
}