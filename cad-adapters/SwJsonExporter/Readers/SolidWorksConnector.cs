using System;
using System.Runtime.Versioning;
using SldWorks;

namespace SwJsonExporter.Readers
{
    [SupportedOSPlatform("windows")] // Заявляем, что этот мост работает только под Windows
    public class SolidWorksConnector
    {
        public SldWorks.SldWorks Connect()
        {
            var swApp = (SldWorks.SldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application")!)!;
            if (swApp == null)
                throw new InvalidOperationException("Не удалось подключиться к процессу SOLIDWORKS.");
            return swApp;
        }

        public IModelDoc2 GetActiveModel(SldWorks.SldWorks swApp)
        {
            var swModel = (IModelDoc2)swApp.ActiveDoc;
            if (swModel == null)
                throw new InvalidOperationException("В SOLIDWORKS нет открытого документа для сканирования!");
            return swModel;
        }
    }
}