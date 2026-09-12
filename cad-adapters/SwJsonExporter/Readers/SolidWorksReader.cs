using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using SwJsonExporter.Domain;
using SldWorks;
using System.Runtime.InteropServices;
using Serilog;

namespace SwJsonExporter.Readers
{
    public class SolidWorksReader : ICadReader
    {
        public record BodySignature(string LocalName, double Volume, double SurfaceArea, double X, double Y, double Z);
        private static readonly ILogger Log = Serilog.Log.ForContext<SolidWorksReader>();
        private readonly SolidWorksConnector _connector = new();

        private readonly Dictionary<string, IModelDoc2> _openMasterModels = new(StringComparer.OrdinalIgnoreCase);

        public bool IsAvailable() => true;

        public CanonicalProduct ReadActiveDocument()
        {
            ISldWorks? swApp = null;
            IModelDoc2? swModel = null;

            try
            {
                Log.Information("Connecting to active SOLIDWORKS");
                swApp = _connector.Connect();
                swModel = _connector.GetActiveModel(swApp);

                return ParseAssembly(swModel, swApp);
            }
            finally
            {
                if (swApp != null)
                {
                    foreach (var kvp in _openMasterModels)
                    {
                        Log.Information("Closing cached Master Model: {Path}", kvp.Key);
                        swApp.CloseDoc(kvp.Key);
                        if (kvp.Value != null)
                        {
                            Marshal.ReleaseComObject(kvp.Value);
                        }
                    }
                    _openMasterModels.Clear();
                }

                if (swModel != null)
                {
                    Marshal.ReleaseComObject(swModel);
                }

                if (swApp != null)
                {
                    Marshal.ReleaseComObject(swApp);
                }
            }
        }

        private CanonicalProduct ParseAssembly(IModelDoc2 swModel, ISldWorks swApp)
        {
            Log.Information("Parse SW model");
            string assemblyName = swModel.GetTitle();
            Configuration activeConfig = swModel.ConfigurationManager.ActiveConfiguration;

            string configName = activeConfig.Name;
            string[] rootManagers = new string[] { configName, "" };

            var rootNode = new CanonicalProduct
            {
                Id = $"ROOT-{assemblyName}-{configName}",
                Name = $"{assemblyName} ({configName})",
                Path = assemblyName,
                Level = 0,
                Type = "Assembly",
                IsVirtual = false,
                CustomProperties = ExtractFromManagers(swModel, rootManagers)
            };

            Component2 rootComponent = activeConfig.GetRootComponent3(true);

            if (rootComponent != null)
            {
                object[] children = (object[])rootComponent.GetChildren();
                if (children != null)
                {
                    foreach (object childObj in children)
                    {
                        Component2 childComp = (Component2)childObj;
                        TraverseComponent(childComp, rootNode, swApp);
                    }
                }
            }

            return rootNode;
        }

        private void TraverseComponent(Component2 comp, CanonicalProduct parentNode, ISldWorks swApp)
        {
            if (comp.IsSuppressed()) return;

            string[] childManagers = new string[] { comp.ReferencedConfiguration, "" };
            bool isSubAssembly = (comp.GetChildren() != null && ((object[])comp.GetChildren()).Length > 0);

            bool isVirtual = comp.IsVirtual;

            string rawName = comp.Name2;
            string cleanName = isVirtual && rawName.Contains('^') ? rawName.Split('^')[0] : rawName;

            IModelDoc2 childModel = (IModelDoc2)comp.GetModelDoc2();

            var properties = childModel != null
            ? ExtractFromManagers(childModel, childManagers)
            : new Dictionary<string, string>();

            var currentNode = new CanonicalProduct
            {
                Id = $"{rawName}-CADID-{comp.GetID()}",
                Name = cleanName,
                Level = parentNode.Level + 1,
                ParentId = parentNode.Id,
                Path = $"{parentNode.Path}/{cleanName}",
                Type = isSubAssembly ? "SubAssembly" : "Part",
                IsVirtual = isVirtual,
                CustomProperties = properties
            };

            parentNode.ChildNodes.Add(currentNode);

            if (isSubAssembly)
            {
                object[] children = (object[])comp.GetChildren();
                foreach (object childObj in children)
                {
                    Component2 childComp = (Component2)childObj;
                    TraverseComponent(childComp, currentNode, swApp);
                }
            }
            else
            {
                var masterPropertiesMap = InvestigateInsertedParts(comp, swApp);
                ExtractCutListsFromPart(comp, currentNode, masterPropertiesMap);
            }
        }

        private void ExtractCutListsFromPart(Component2 comp, CanonicalProduct currentNode, Dictionary<string, Dictionary<string, string>> masterPropertiesMap)
        {
            IModelDoc2 swModel = (IModelDoc2)comp.GetModelDoc2();
            if (swModel == null) return;

            Feature swFeat = (Feature)swModel.FirstFeature();
            while (swFeat != null)
            {
                if (swFeat.GetTypeName2() == "CutListFolder")
                {
                    BodyFolder swBodyFolder = (BodyFolder)swFeat.GetSpecificFeature2();

                    if (swBodyFolder != null && swBodyFolder.GetBodyCount() > 0)
                    {
                        object[] bodies = (object[])swBodyFolder.GetBodies();
                        string? localBodyName = null;

                        if (bodies != null && bodies.Length > 0)
                        {
                            Body2 swBody = (Body2)bodies[0];
                            localBodyName = swBody.Name;

                            double[] massProps = (double[])swBody.GetMassProperties(1.0);

                            if (massProps != null && massProps.Length >= 6)
                            {
                                double mass = massProps[3];
                                double volume = massProps[4];
                                double surfaceArea = massProps[5];

                                Log.Information("  -> ТЕЛО: {BodyName} | CutList: {CutName}", localBodyName, swFeat.Name);
                                Log.Information("     Масса: {Mass} | Объем: {Vol}", mass, volume);
                            }
                        }

                        var cutListProperties = ExtractCutListProperties(swFeat);

                        if (localBodyName != null && masterPropertiesMap.TryGetValue(localBodyName, out var masterProps))
                        {
                            foreach (var prop in masterProps)
                            {
                                cutListProperties[$"Master_{prop.Key}"] = prop.Value;
                            }
                        }

                        var cutListNode = new CanonicalProduct
                        {
                            Id = $"{currentNode.Id}-CUT-{swFeat.Name}",
                            Name = swFeat.Name,
                            Level = currentNode.Level + 1,
                            ParentId = currentNode.Id,
                            Path = $"{currentNode.Path}/{swFeat.Name}",
                            Type = "SheetMetalCut",
                            IsVirtual = false,
                            CustomProperties = cutListProperties
                        };

                        currentNode.ChildNodes.Add(cutListNode);
                    }
                }
                swFeat = (Feature)swFeat.GetNextFeature();
            }
        }

        private Dictionary<string, string> ExtractCutListProperties(Feature swFeat)
        {
            var properties = new Dictionary<string, string>();
            CustomPropertyManager propMgr = swFeat.CustomPropertyManager;
            if (propMgr == null) return properties;

            object[] propNames = (object[])propMgr.GetNames();
            if (propNames != null && propNames.Length > 0)
            {
                foreach (object nameObj in propNames)
                {
                    string propName = (string)nameObj;
                    propMgr.Get6(propName, false, out _, out string resolvedValOut, out _, out _);

                    if (!string.IsNullOrEmpty(resolvedValOut))
                    {
                        properties[propName] = resolvedValOut;
                    }
                }
            }
            return properties;
        }

        private Dictionary<string, string> ExtractFromManagers(IModelDoc2 swModel, string[] configNames)
        {
            var properties = new Dictionary<string, string>();

            foreach (string configName in configNames)
            {
                ICustomPropertyManager propMgr = swModel.Extension.CustomPropertyManager[configName];
                if (propMgr == null) continue;

                object[] propNames = (object[])propMgr.GetNames();
                if (propNames != null && propNames.Length > 0)
                {
                    foreach (object nameObj in propNames)
                    {
                        string propName = (string)nameObj;
                        if (properties.ContainsKey(propName)) continue;

                        propMgr.Get6(
                            propName,
                            false,
                            out string valOut,
                            out string resolvedValOut,
                            out bool wasResolved,
                            out bool linkToProp
                        );

                        if (!string.IsNullOrEmpty(resolvedValOut))
                        {
                            properties[propName] = resolvedValOut;
                        }
                    }
                }
            }

            return properties;
        }

        private Dictionary<string, Dictionary<string, string>> InvestigateInsertedParts(Component2 swComponent, ISldWorks swApp)
        {
            var resultMap = new Dictionary<string, Dictionary<string, string>>();

            IModelDoc2 swModel = (IModelDoc2)swComponent.GetModelDoc2();
            if (swModel == null) return resultMap;

            Log.Information("--- Investigation of the part: {CompName} ---", swComponent.Name2);

            Feature swFeature = (Feature)swModel.FirstFeature();
            string masterModelPath = string.Empty;

            while (swFeature != null)
            {
                string typeName = swFeature.GetTypeName2();
                if (typeName == "Stock" || typeName == "MirrorStock" || typeName == "DerivedPart")
                {
                    try
                    {
                        object featData = swFeature.GetDefinition();
                        if (featData != null)
                        {
                            IDerivedPartFeatureData derivedData = (IDerivedPartFeatureData)featData;
                            masterModelPath = derivedData.PathName;
                            Log.Information(">>> Master Model found: {Path} <<<", masterModelPath);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error retrieving master path: {Msg}", ex.Message);
                    }
                }
                swFeature = (Feature)swFeature.GetNextFeature();
            }

            if (string.IsNullOrEmpty(masterModelPath))
            {
                Log.Information("End of the investigation\n");
                return resultMap;
            }

            var localSignatures = new List<BodySignature>();
            object[] bodies = (object[])swComponent.GetBodies3(0, out _);

            if (bodies != null && bodies.Length > 0)
            {
                foreach (object bodyObj in bodies)
                {
                    Body2 swBody = (Body2)bodyObj;
                    double[] massProps = (double[])swBody.GetMassProperties(1.0);

                    if (massProps != null && massProps.Length >= 6)
                    {
                        localSignatures.Add(new BodySignature(
                            swBody.Name,
                            Math.Round(massProps[4], 5), // Volume
                            Math.Round(massProps[5], 5), // SurfaceArea
                            Math.Round(massProps[0], 5), // X
                            Math.Round(massProps[1], 5), // Y
                            Math.Round(massProps[2], 5)  // Z
                        ));
                    }
                }
            }

            IModelDoc2? masterModel = null;

            if (_openMasterModels.TryGetValue(masterModelPath, out var cachedModel))
            {
                Log.Information("Using cached Master Model: {Path}", masterModelPath);
                masterModel = cachedModel;
            }
            else
            {
                int errors = 0;
                int warnings = 0;
                Log.Information("Opening Master Model silently to extract Cut List properties: {Path}", masterModelPath);
                masterModel = swApp.OpenDoc6(masterModelPath, 1, 2, "", ref errors, ref warnings);

                if (masterModel != null)
                {
                    _openMasterModels[masterModelPath] = masterModel;
                }
            }

            if (masterModel != null)
            {
                Feature masterFeat = (Feature)masterModel.FirstFeature();

                while (masterFeat != null)
                {
                    if (masterFeat.GetTypeName2() == "CutListFolder")
                    {
                        BodyFolder swBodyFolder = (BodyFolder)masterFeat.GetSpecificFeature2();

                        if (swBodyFolder != null && swBodyFolder.GetBodyCount() > 0)
                        {
                            object[] masterBodies = (object[])swBodyFolder.GetBodies();
                            if (masterBodies != null && masterBodies.Length > 0)
                            {
                                Body2 masterBody = (Body2)masterBodies[0];
                                double[] mProps = (double[])masterBody.GetMassProperties(1.0);

                                if (mProps != null && mProps.Length >= 6)
                                {
                                    double mVol = Math.Round(mProps[4], 5);
                                    double mArea = Math.Round(mProps[5], 5);
                                    double mX = Math.Round(mProps[0], 5);
                                    double mY = Math.Round(mProps[1], 5);
                                    double mZ = Math.Round(mProps[2], 5);

                                    foreach (var sig in localSignatures)
                                    {
                                        bool isVolumeMatch = Math.Abs(sig.Volume - mVol) < 0.0001;
                                        bool isAreaMatch = Math.Abs(sig.SurfaceArea - mArea) < 0.0001;

                                        bool isXMatch = Math.Abs(Math.Abs(sig.X) - Math.Abs(mX)) < 0.0001;
                                        bool isYMatch = Math.Abs(Math.Abs(sig.Y) - Math.Abs(mY)) < 0.0001;
                                        bool isZMatch = Math.Abs(Math.Abs(sig.Z) - Math.Abs(mZ)) < 0.0001;

                                        if (isVolumeMatch && isAreaMatch && isXMatch && isYMatch && isZMatch)
                                        {
                                            Log.Information("MATCH: '{Local}' == '{MasterCut}'", sig.LocalName, masterFeat.Name);

                                            var properties = ExtractCutListProperties(masterFeat);
                                            resultMap[sig.LocalName] = properties;

                                            foreach (var prop in properties)
                                            {
                                                Log.Information("    + {Key}: {Value}", prop.Key, prop.Value);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    masterFeat = (Feature)masterFeat.GetNextFeature();
                }

                Log.Information("Finished reading Master Model (kept in cache).");
            }
            else
            {
                Log.Error("Failed to open Master Model silently.");
            }

            Log.Information("End of the investigation\n");
            return resultMap;
        }
    }
}