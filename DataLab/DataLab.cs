using System;
using Autodesk.Revit.DB;

namespace DataLab
{
    /// <summary>
    /// Compatibility wrapper so callers that expect DataLab.GetElementByName(...) compile.
    /// This is a minimal implementation that returns the active view as an Element when
    /// no name is provided. It also forwards generic/name overloads to DocumentExtension.
    /// </summary>
    public static class DataLab
    {
        public static Element GetElementByName(Document doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            // Return the active view as a reasonable default for callers that didn't provide a name.
            return doc.ActiveView as Element;
        }

        public static Element GetElementByName(Document doc, string name)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("name", nameof(name));
            return DocumentExtension.GetElementByName<Element>(doc, name);
        }

        public static TElement GetElementByName<TElement>(Document doc, string name)
            where TElement : Element
        {
            return DocumentExtension.GetElementByName<TElement>(doc, name);
        }
    }
}
