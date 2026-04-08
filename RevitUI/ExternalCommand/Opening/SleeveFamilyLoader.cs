using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitUI.ExternalCommand.Opening
{
    /// <summary>
    /// Utility to manage loading of embedded sleeve families.
    /// </summary>
    public static class SleeveFamilyLoader
    {
        private const string CircularFamilyName = "CIRCULAR CUT";
        private const string RectangularFamilyName = "RECTANGULAR CUT";

        /// <summary>
        /// Ensures the circular and rectangular cut families are loaded into the project.
        /// </summary>
        public static void EnsureFamiliesLoaded(Document doc)
        {
            // 1. Check if families already exist
            bool hasCircular = HasFamily(doc, CircularFamilyName);
            bool hasRectangular = HasFamily(doc, RectangularFamilyName);

            if (hasCircular && hasRectangular) return;

            // 2. Load families from embedded resources
            using (Transaction t = new Transaction(doc, "Load Embedded Families"))
            {
                t.Start();
                try
                {
                    if (!hasCircular) LoadEmbeddedFamily(doc, "CIRCULAR CUT.rfa");
                    if (!hasRectangular) LoadEmbeddedFamily(doc, "RECTANGULAR CUT.rfa");
                    t.Commit();
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    System.Diagnostics.Debug.WriteLine($"[SleeveFamilyLoader] Failed to load families: {ex.Message}");
                }
            }
        }

        private static bool HasFamily(Document doc, string name)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private static void LoadEmbeddedFamily(Document doc, string filename)
        {
            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            // Embedded resource path: Namespace.Folder.Filename
            string resourcePath = $"{assemblyName}.Resources.{filename}";

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath))
            {
                if (stream == null)
                {
                    // Try fallback: list all resources to find the correct one
                    string[] names = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                    string actualName = names.FirstOrDefault(n => n.EndsWith(filename, StringComparison.OrdinalIgnoreCase));
                    if (actualName == null) throw new Exception($"Resource {resourcePath} not found in assembly.");
                    
                    using (Stream fallbackStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(actualName))
                    {
                        LoadFromStream(doc, fallbackStream, filename);
                    }
                }
                else
                {
                    LoadFromStream(doc, stream, filename);
                }
            }
        }

        private static void LoadFromStream(Document doc, Stream stream, string filename)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), filename);
            try
            {
                using (FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }

                doc.LoadFamily(tempPath, out Family family);
                if (family != null)
                {
                    // Activate all symbols
                    foreach (ElementId symbolId in family.GetFamilySymbolIds())
                    {
                        FamilySymbol symbol = (FamilySymbol)doc.GetElement(symbolId);
                        if (!symbol.IsActive) symbol.Activate();
                    }
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }
    }
}
