using System;
using System.Collections.Generic;
using SldWorks;
using SwConst;
using SwJsonExporter.Models;

namespace SwJsonExporter.Services
{
    public class AssemblyTraversalService
    {
        // 1. ПУБЛИЧНЫЙ ДИСПЕТЧЕР: Точка входа для парсинга активной сборки
        public ManufacturingNodeDto? ParseAssembly(IModelDoc2 swModel)
        {
            // Защита от сбоев: проверяем, что документ открыт и это именно сборка (.SLDASM)
            if (swModel == null || swModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                Console.WriteLine("[Ошибка] Откройте сборку (.SLDASM) в SOLIDWORKS перед запуском сканера!");
                return null;
            }

            string rootTitle = swModel.GetTitle();
            Console.WriteLine($"[Система] Запуск сканирования сборки: {rootTitle}...");

            // Создаем нулевой, корневой узел нашего завода (Уровень 0)
            var rootNode = new ManufacturingNodeDto
            {
                Id = $"ROOT-{rootTitle}", // В Шаге 2 заменим на IdGeneratorService
                ParentId = null,         // У корня нет родителя
                Level = 0,
                Path = rootTitle,
                Name = rootTitle,
                Type = "Assembly"
            };

            // Получаем конфигурацию и корневой компонент по стандарту SOLIDWORKS 2025
            // параметр true гарантирует корректную работу с виртуальными деталями и легковесными компонентами
            Configuration swConf = swModel.ConfigurationManager.ActiveConfiguration;
            Component2 swRootComp = swConf.GetRootComponent3(true);

            // Получаем список компонентов первого уровня (жесткое приведение типов по правилам .NET 8)
            object[] children = (object[])swRootComp.GetChildren();

            if (children != null && children.Length > 0)
            {
                foreach (object child in children)
                {
                    Component2 swChildComp = (Component2)child;
                    // Запускаем рекурсивное погружение
                    TraverseComponent(swChildComp, rootNode);
                }
            }

            Console.WriteLine($"[Система] Сканирование завершено! Собран скелет изделия.");
            return rootNode;
        }

        // 2. РЕКУРСИВНЫЙ МОТОР: Проходит по всем уровням вложенности, зеркальным массивам и подсборкам
        private void TraverseComponent(Component2 comp, ManufacturingNodeDto parentNode)
        {
            // Игнорируем пустые ссылки и погашенные (Suppressed) компоненты — они не идут в производство
            if (comp == null || comp.IsSuppressed()) return;

            // Определяем тип: если имя пути файла заканчивается на .sldasm — это подсборка
            string pathName = comp.GetPathName();
            bool isSubAssembly = pathName.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase);

            // 1. Создаем DTO-паспорт для текущей детали
            var currentNode = new ManufacturingNodeDto
            {
                Name = comp.Name2,
                Level = parentNode.Level + 1,        // На 1 уровень глубже родителя
                ParentId = parentNode.Id,            // Ссылка на родителя для плоских баз данных ERP
                Path = $"{parentNode.Path}/{comp.Name2}", // Полный заводской путь
                Type = isSubAssembly ? "SubAssembly" : "Part"
            };

            // Временно генерируем ID на основе системного номера CAD (в Шаге 2 подключим наш MD5-хэшер)
            int cadId = comp.GetID();
            currentNode.Id = $"{comp.Name2}-CADID-{cadId}";

            // 2. Привязываем эту деталь к списку детей родительского узла
            parentNode.ChildNodes.Add(currentNode);

            // 3. РЕКУРСИЯ: Проверяем, есть ли дети у ТЕКУЩЕГО компонента
            object[] subChildren = (object[])comp.GetChildren();
            if (subChildren != null && subChildren.Length > 0)
            {
                // Если внутри есть детали — наш метод вызывает САМ СЕБЯ для каждого ребенка
                foreach (object subChild in subChildren)
                {
                    TraverseComponent((Component2)subChild, currentNode);
                }
            }
            else
            {
                // Это конечная деталь (Лист или Труба). 
                // Здесь в Шаге 3 мы будем вызывать сканер Списка вырезов (Cut List)
            }
        }
    }
}