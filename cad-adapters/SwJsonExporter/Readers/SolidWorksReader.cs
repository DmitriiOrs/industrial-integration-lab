using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using SwJsonExporter.Domain;
using SldWorks;

namespace SwJsonExporter.Readers
{
    [SupportedOSPlatform("windows")]
    public class SolidWorksReader : ICadReader
    {
        private readonly SolidWorksConnector _connector = new();

        public bool IsAvailable() => true;

        public CanonicalProduct ReadActiveDocument()
        {
            // 1. Изолированное подключение через Коннектор
            var swApp = _connector.Connect();
            var swModel = _connector.GetActiveModel(swApp);

            // 2. Запуск полного сканирования сборки
            return ParseAssembly(swModel);
        }

        private CanonicalProduct ParseAssembly(IModelDoc2 swModel)
        {
            string assemblyName = swModel.GetTitle();

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

            // Получаем доступ к дереву компонентов активной конфигурации
            Configuration activeConfig = swModel.ConfigurationManager.ActiveConfiguration;
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

            // Если это подсборка — рекурсивно ныряем внутрь неё
            if (isSubAssembly)
            {
                object[] children = (object[])comp.GetChildren();
                foreach (object childObj in children)
                {
                    Component2 childComp = (Component2)childObj;
                    TraverseComponent(childComp, currentNode);
                }
            }
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
                CustomPropertyManager propMgr = swModel.Extension.CustomPropertyManager[configName];
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