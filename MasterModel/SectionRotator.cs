using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Text;

namespace MasterModel
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SectionRotator : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;
            var sb = new StringBuilder();

            try
            {
                var sections = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSection))
                    .Cast<ViewSection>()
                    .Where(x => !x.IsTemplate && x.ViewType == ViewType.Section)
                    .ToList();

                if (!sections.Any())
                {
                    TaskDialog.Show("Info", "No section views found in project.");
                    return Result.Succeeded;
                }

                using (var tx = new Transaction(doc, "Rotate Sections 90°"))
                {
                    tx.Start(); // ✅ Don't forget this!

                    foreach (var sec in sections)
                    {
                        // ✅ Step 1: Get the view's crop box
                        //var bb = sec.GetCropBox();

                        // ✅ Step 2: Extract current orientation vectors
                        //var oldX = bb.Transform.BasisX;
                        //var oldY = bb.Transform.BasisY;

                        // ✅ Step 3: Rotate 90° — swap and negate
                        //bb.Transform.BasisX = -oldY;
                        //bb.Transform.BasisY = oldX;
                        // BasisZ stays the same (depth direction unchanged)

                        // ✅ Step 4: Ensure crop is visible
                        sec.CropBoxActive = true;
                        sec.CropBoxVisible = true;

                        // ✅ Step 5: Apply rotated crop box back
                        //sec.SetCropBox(bb);

                        sb.AppendLine($"✔ Rotated: {sec.Name}");
                    }

                    tx.Commit(); // ✅ Commit!
                }

                sb.AppendLine($"\nTotal rotated: {sections.Count}");
                TaskDialog.Show("Section Rotator", sb.ToString());
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}