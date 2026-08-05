using Serilog;
using SwJsonExporter.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace SwJsonExporter.Processing
{
    public class DataNormalizer
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<DataNormalizer>();

        public void Normalize(CanonicalProduct rootProduct)
        {
            Log.Information("Starting data normalization...");

            NormalizeNode(rootProduct);

            Log.Information("Data normalization completed successfully.");
        }

        private void NormalizeNode(CanonicalProduct node)
        {
            node.Name = node.Name?.Trim() ?? string.Empty;
            node.Type = node.Type?.Trim() ?? string.Empty;

            var cleanedProperties = new Dictionary<string, string>();

            foreach (var kvp in node.CustomProperties)
            {
                string key = kvp.Key?.Trim() ?? string.Empty;
                string value = kvp.Value?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(value))
                {
                    Log.Debug("Dropped empty property [{PropKey}] in component [{CompName}]", key, node.Name);
                    continue;
                }

                if (value.Contains("<not specified>", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Debug("Dropped unspecified property [{PropKey}] in component [{CompName}]", key, node.Name);
                    continue;
                }

                if (key.Equals("Material", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Материал", StringComparison.OrdinalIgnoreCase))
                {
                    value = value.ToUpper();
                }

                cleanedProperties[key] = value;
            }

            node.CustomProperties = cleanedProperties;

            foreach (var child in node.ChildNodes)
            {
                NormalizeNode(child);
            }
        }
    }
}
