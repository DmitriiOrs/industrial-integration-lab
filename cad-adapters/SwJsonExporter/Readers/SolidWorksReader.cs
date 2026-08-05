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
        private static readonly ILogger Log = Serilog.Log.ForContext<SolidWorksReader>();
        private readonly SolidWorksConnector _connector = new();

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

                return ParseAssembly(swModel);
            }
            finally
            {
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

        private CanonicalProduct ParseAssembly(IModelDoc2 swModel)
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
                        TraverseComponent(childComp, rootNode);
                    }
                }
            }

            return rootNode;
        }

        private void TraverseComponent(Component2 comp, CanonicalProduct parentNode)
        {
            if (comp.IsSuppressed()) return;

            string[] childManagers = new string[] { comp.ReferencedConfiguration, "" };
            bool isSubAssembly = (comp.GetChildren() != null && ((object[])comp.GetChildren()).Length > 0);

            IModelDoc2 childModel = (IModelDoc2)comp.GetModelDoc2();

            var properties = childModel != null
            ? ExtractFromManagers(childModel, childManagers)
            : new Dictionary<string, string>();

            var currentNode = new CanonicalProduct
            {
                Id = $"{comp.Name2}-CADID-{comp.GetID()}",
                Name = comp.Name2,
                Level = parentNode.Level + 1,
                ParentId = parentNode.Id,
                Path = $"{parentNode.Path}/{comp.Name2}",
                Type = isSubAssembly ? "SubAssembly" : "Part",
                CustomProperties = properties
            };

            parentNode.ChildNodes.Add(currentNode);

            if (isSubAssembly)
            {
                object[] children = (object[])comp.GetChildren();
                foreach (object childObj in children)
                {
                    Component2 childComp = (Component2)childObj;
                    TraverseComponent(childComp, currentNode);
                }
            }
            else
            {
                ExtractCutListsFromPart(comp, currentNode);
            }
        }

        private void ExtractCutListsFromPart(Component2 comp, CanonicalProduct currentNode)
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
                        var cutListNode = new CanonicalProduct
                        {
                            Id = $"{currentNode.Id}-CUT-{swFeat.Name}",
                            Name = swFeat.Name,
                            Level = currentNode.Level + 1,
                            ParentId = currentNode.Id,
                            Path = $"{currentNode.Path}/{swFeat.Name}",
                            Type = "SheetMetalCut",
                            CustomProperties = ExtractCutListProperties(swFeat)
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
    }
}