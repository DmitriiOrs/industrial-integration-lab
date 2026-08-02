using System;
using System.Runtime.Versioning;
using SldWorks;

namespace SwJsonExporter.Readers
{
    [SupportedOSPlatform("windows")]
    public class SolidWorksConnector
    {
        public ISldWorks Connect()
        {
            var swApp = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application")!)!;
            if (swApp == null)
                throw new InvalidOperationException("Failed to connect to SOLIDWORKS.");
            return swApp;
        }

        public IModelDoc2 GetActiveModel(ISldWorks swApp)
        {
            var swModel = (IModelDoc2)swApp.ActiveDoc;
            if (swModel == null)
                throw new InvalidOperationException("There is no open document to scan in SOLIDWORKS.");
            return swModel;
        }
    }
}