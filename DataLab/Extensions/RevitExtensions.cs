using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataLab.Extensions
{
    public static class RevitExtensions
    {
        /// <summary>
        /// Return all levels in the document.
        /// </summary>
        public static IList<Level> GetLevel(this Document doc)
        {
            if (doc == null) return new List<Level>();
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .WhereElementIsNotElementType()
                .Cast<Level>()
                .ToList();
        }

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
                catch
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
        /// Returns a Transform mapping from the source view coordinate system to model coordinates.
        /// This is a minimal stub (identity) suitable for compilation. Replace with proper
        /// view-to-model transform calculation if needed.
        /// </summary>
        public static Transform GetViewTransform(this ViewSection view)
        {
            // TODO: implement correct view->model transform if required by the feature.
            return Transform.Identity;
        }
    }
}
