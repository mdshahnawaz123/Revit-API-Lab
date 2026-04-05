using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DataLab.Extensions;
using System.Text;

namespace MasterModel
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FloorPlanGenerator : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;
            var sb = new StringBuilder();

            try
            {
                var lvls = doc.GetLevel();

                var floorTypeId = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.FloorPlan)?.Id;

                // ✅ Null guard with early return
                if (floorTypeId == null)
                {
                    message = "FloorPlan ViewFamilyType was not found in the project.";
                    return Result.Failed;
                }

                using (Transaction tx = new Transaction(doc, "Generate Floor Plans"))
                {
                    tx.Start();

                    foreach (var lvl in lvls)
                    {
                        var floorName = $"Auto_FloorPlan_{lvl.Name}";

                        // ✅ Check duplicate
                        bool alreadyExists = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewPlan))
                            .Any(x => x.Name == floorName);

                        if (!alreadyExists)
                        {
                            var newFloor = ViewPlan.Create(doc, floorTypeId, lvl.Id);
                            newFloor.Name = floorName;
                            sb.AppendLine($"✔ Created: '{floorName}'");
                        }
                        else
                        {
                            // ✅ Log the actual name, not the bool
                            sb.AppendLine($"⚠ Skipped: '{floorName}' already exists.");
                        }
                    }

                    tx.Commit();
                }

                // ✅ Show dialog ONCE after everything is done
                TaskDialog.Show("Floor Plan Report", sb.ToString());
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed; // ✅ Correct failure return
            }

            return Result.Succeeded;
        }
    }
}