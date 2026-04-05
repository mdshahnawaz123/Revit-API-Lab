using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitUI.ExternalCommand.Opening
{
    public static class RevitUtils
    {
        /// <summary>
        /// Simple helper to run an action inside a Transaction on the given document.
        /// </summary>
        public static void DoAction(this Document doc, Action action, string txName = "Action")
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (action == null) throw new ArgumentNullException(nameof(action));

            using (var tx = new Transaction(doc, txName))
            {
                tx.Start();
                try
                {
                    action();
                    tx.Commit();
                }
                catch (Exception)
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                    {
                        try { tx.RollBack(); } catch { }
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// Collects family instances of a specific category from all linked documents and returns them with their instance transform.
        /// </summary>
        public static IList<(FamilyInstance Instance, Transform Transform)>
            GetLinkedFamilyWithTransform(this Document doc, BuiltInCategory bic)
        {
            var result = new List<(FamilyInstance, Transform)>();

            var links = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .WhereElementIsNotElementType()
                .Cast<RevitLinkInstance>()
                .ToList();

            foreach (var link in links)
            {
                var linkDoc = link.GetLinkDocument();
                if (linkDoc == null) continue;

                var transform = link.GetTransform();

                var instances = new FilteredElementCollector(linkDoc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>();

                foreach (var inst in instances)
                {
                    result.Add((inst, transform));
                }
            }

            return result;
        }
    }
}
