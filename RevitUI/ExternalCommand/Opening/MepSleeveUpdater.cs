using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitUI.ExternalCommand.Opening
{
    public class MepSleeveUpdater : IUpdater
    {
        private readonly UpdaterId _updaterId;

        public MepSleeveUpdater(AddInId addInId)
        {
            // Use a unique GUID for this new generalized updater
            _updaterId = new UpdaterId(addInId, new Guid("DDEEAABB-1122-3344-5566-123456789ABC"));
        }

        public UpdaterId GetUpdaterId() => _updaterId;
        public string GetUpdaterName() => "MEP Sleeve Updater";
        public string GetAdditionalInformation() => "Moves master sleeves when pipes, ducts, or cable trays are moved.";
        public ChangePriority GetChangePriority() => ChangePriority.MEPFixtures;

        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();

            List<FamilyInstance> allSleeves = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                // We assume backward compatibility where the string param on the sleeve itself is literally called "PipeId"
                .Where(fi => fi.LookupParameter("PipeId") != null)
                .ToList();

            foreach (ElementId mepId in data.GetModifiedElementIds())
            {
                try
                {
                    Element mepElement = doc.GetElement(mepId);
                    if (mepElement == null) continue;

                    LocationCurve mepLoc = mepElement.Location as LocationCurve;
                    if (mepLoc == null) continue;

                    Curve mepCurve = mepLoc.Curve;
                    string mepIdStr = mepId.ToString();

                    List<FamilyInstance> sleeves = allSleeves
                        .Where(s => s.LookupParameter("PipeId")?.AsString() == mepIdStr)
                        .ToList();

                    if (sleeves.Count == 0) continue;

                    Line mepLine = mepCurve as Line;
                    if (mepLine == null) continue;

                    XYZ p0 = mepLine.GetEndPoint(0);
                    XYZ p1 = mepLine.GetEndPoint(1);
                    XYZ dir = (p1 - p0).Normalize();
                    if (dir.IsZeroLength()) continue;
                    
                    Line unboundLine = Line.CreateUnbound(p0, dir);

                    foreach (FamilyInstance sleeve in sleeves)
                    {
                        LocationPoint loc = sleeve.Location as LocationPoint;
                        if (loc == null) continue;

                        XYZ origin = loc.Point;
                        double height = 0;
                        
                        string[] sizeParams = { "Height", "H", "Opening Height", "Diameter", "Opening Diameter", "d", "D", "Size", "Width", "W", "B", "Opening Width" };
                        foreach (string paramName in sizeParams)
                        {
                            Parameter p = sleeve.LookupParameter(paramName);
                            if (p != null && p.HasValue)
                            {
                                height = p.AsDouble();
                                break;
                            }
                            
                            ElementType type = doc.GetElement(sleeve.GetTypeId()) as ElementType;
                            if (type != null)
                            {
                                Parameter pt = type.LookupParameter(paramName);
                                if (pt != null && pt.HasValue)
                                {
                                    height = pt.AsDouble();
                                    break;
                                }
                            }
                        }

                        // Up vector is global Z. Move center back down to offset.
                        XYZ visualCenter = origin + XYZ.BasisZ * (height / 2.0);
                        IntersectionResult result = unboundLine.Project(visualCenter);
                        if (result == null) continue;

                        XYZ closestPointOnPipe = result.XYZPoint;
                        XYZ translation = closestPointOnPipe - visualCenter;

                        if (translation.GetLength() > 0.001) 
                        {
                            ElementTransformUtils.MoveElement(doc, sleeve.Id, translation);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MepSleeveUpdater] {ex.Message}");
                }
            }
        }
    }
}
