using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using SwJsonExporter.Domain;
using SldWorks;
using System.Runtime.InteropServices;

namespace SwJsonExporter.Readers
{
    [SupportedOSPlatform("windows")]
    public class SolidWorksReader : ICadReader
    {
        private readonly SolidWorksConnector _connector = new();

        public bool IsAvailable() => true;

        public CanonicalProduct ReadActiveDocument()
        {
            ISldWorks? swApp = null;
            IModelDoc2? swModel = null;

            try
            {
                // 1. ВТЫКАЕМ КАБЕЛЬ
                swApp = _connector.Connect();
                swModel = _connector.GetActiveModel(swApp);

                // 2. Запуск полного сканирования сборки
                return ParseAssembly(swModel);
            }
            finally
            {
                // 3. БЛОК FINALLY (Выполняется ВСЕГДА, даже если внутри произошла авария и вылетел throw!)

                // Вручную выдергиваем штекер из модели
                if (swModel != null)
                {
                    Marshal.ReleaseComObject(swModel);
                }

                // Вручную выдергиваем штекер из самого Солида
                if (swApp != null)
                {
                    Marshal.ReleaseComObject(swApp);
                }
            }
                                
        }

        private CanonicalProduct ParseAssembly(IModelDoc2 swModel)
        {
            string assemblyName = swModel.GetTitle();

            Configuration activeConfig = swModel.ConfigurationManager.ActiveConfiguration;

            string[] rootManager = new string[] { activeConfig.Name, "" };

            // Создаем корневой паспорт изделия
            var rootNode = new CanonicalProduct
            {
                Id = $"ROOT-{assemblyName}",
                Name = assemblyName,
                Path = assemblyName,
                Level = 0,
                Type = "Assembly",
                // Читаем общие свойства самого документа сборки:
                CustomProperties = ExtractCustomProperties(swModel, "")
            };
            
            Component2 rootComponent = activeConfig.GetRootComponent3(true);

            if (rootComponent != null)
            {
                // Получаем массив всех деталей первого уровня
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
            // Пропускаем подавленные (выключенные конструктором) детали
            if (comp.IsSuppressed()) return;

            bool isSubAssembly = (comp.GetChildren() != null && ((object[])comp.GetChildren()).Length > 0);

            // Формируем канонический узел для текущей детали
            var currentNode = new CanonicalProduct
            {
                Id = $"{comp.Name2}-CADID-{comp.GetID()}",
                Name = comp.Name2,
                Level = parentNode.Level + 1,
                ParentId = parentNode.Id,
                Path = $"{parentNode.Path}/{comp.Name2}",
                Type = isSubAssembly ? "SubAssembly" : "Part",
                CustomProperties = ExtractCustomProperties(comp)
            };

            // Добавляем деталь в список детей родителя
            parentNode.ChildNodes.Add(currentNode);

            // === ИЗМЕНЕНИЕ 1: Разделяем логику для Сборок и Деталей ===
            if (isSubAssembly)
            {
                // Если это подсборка — рекурсивно ныряем внутрь её компонентов
                object[] children = (object[])comp.GetChildren();
                foreach (object childObj in children)
                {
                    Component2 childComp = (Component2)childObj;
                    TraverseComponent(childComp, currentNode);
                }
            }
            else
            {
                // Если это деталь — запускаем сканер многотелок (списков вырезов)
                ExtractCutListsFromPart(comp, currentNode);
            }
        }

        // === ИЗМЕНЕНИЕ 2: Новый метод сканирования папок Cut-List внутри детали ===
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

                    // Берем только непустые папки, где есть реальные листовые тела
                    if (swBodyFolder != null && swBodyFolder.GetBodyCount() > 0)
                    {
                        var cutListNode = new CanonicalProduct
                        {
                            Id = $"{currentNode.Id}-CUT-{swFeat.Name}",
                            Name = swFeat.Name,                    // Например: "Sheet<2>"
                            Level = currentNode.Level + 1,         // Лист становится дочерним узлом детали
                            ParentId = currentNode.Id,
                            Path = $"{currentNode.Path}/{swFeat.Name}",
                            Type = "SheetMetalCut",
                            // Читаем свойства именно этого выреза:
                            CustomProperties = ExtractCutListProperties(swFeat)
                        };

                        currentNode.ChildNodes.Add(cutListNode);
                    }
                }
                swFeat = (Feature)swFeat.GetNextFeature();
            }
        }

        // === ИЗМЕНЕНИЕ 3: Новый метод извлечения свойств (длина, ширина, материал) из выреза ===
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

        // Экстрактор свойств для компонентов сборки
        private Dictionary<string, string> ExtractCustomProperties(Component2 comp)
        {
            var properties = new Dictionary<string, string>();
            if (comp == null) return properties;

            IModelDoc2 swModel = (IModelDoc2)comp.GetModelDoc2();
            if (swModel == null) return properties;

            string[] managersToInspect = new string[] { comp.ReferencedConfiguration, "" };
            return ExtractFromManagers(swModel, managersToInspect);
        }

        // Экстрактор свойств для главного документа
        private Dictionary<string, string> ExtractCustomProperties(IModelDoc2 swModel, string configName)
        {
            return ExtractFromManagers(swModel, new string[] { configName });
        }

        // Универсальный парсер карточек SOLIDWORKS (Get6)
        private Dictionary<string, string> ExtractFromManagers(IModelDoc2 swModel, string[] configNames)
        {
            var properties = new Dictionary<string, string>();

            foreach (string configName in configNames)
            {
                ICustomPropertyManager propMgr = swModel.Extension.CustomPropertyManager[configName];
                if (propMgr == null) continue;

                /*object[] propNames = (object[])propMgr.GetNames();

                if (propNames != null && propNames.Length > 0)
                {
                   В чистом C# этот цикл можно заменить на LINQ в одну строку:
                    // string[] names = propNames.Cast<string>().ToArray();

                    // Но для безопасной отладки СОЛИДА используем явный цикл:
                    foreach (object nameObj in propNames)
                    {
                        string propName = (string)nameObj;
                        // ... дальнейшая логика ...
                    }
                }*/

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