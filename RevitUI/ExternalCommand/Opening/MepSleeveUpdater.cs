using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitUI.ExternalCommand.Opening
{
    /// <summary>
    /// Keeps MEP sleeves snapped to the MEP curve whenever an MEP curve is moved.
    /// Tracks MEP movement delta and applies the same delta to associated sleeves.
    /// </summary>
    public class MepSleeveUpdater : IUpdater
    {
        private readonly UpdaterId _updaterId;

        public MepSleeveUpdater(AddInId addInId)
        {
            _updaterId = new UpdaterId(addInId, new Guid("E5D8F0A2-C14B-4E31-9F38-725CD19A8B73"));
        }

        public UpdaterId GetUpdaterId() => _updaterId;
        public string GetUpdaterName() => "MEP Sleeve Updater";
        public string GetAdditionalInformation() => "Moves sleeves when their host MEP curve (Pipe, Duct, Cable Tray) is repositioned.";
        public ChangePriority GetChangePriority() => ChangePriority.MEPFixtures;

        private static MepSleeveUpdater _instance;
        public static void Register(AddInId addInId)
        {
            if (_instance != null) return;
            if (UpdaterRegistry.IsUpdaterRegistered(new UpdaterId(addInId, new Guid("E5D8F0A2-C14B-4E31-9F38-725CD19A8B73")))) return;

            _instance = new MepSleeveUpdater(addInId);
            UpdaterRegistry.RegisterUpdater(_instance);

            var filterList = new List<ElementFilter>
            {
                new ElementCategoryFilter(BuiltInCategory.OST_PipeCurves),
                new ElementCategoryFilter(BuiltInCategory.OST_DuctCurves),
                new ElementCategoryFilter(BuiltInCategory.OST_CableTray),
                new ElementCategoryFilter(BuiltInCategory.OST_Conduit),
                new ElementCategoryFilter(BuiltInCategory.OST_RvtLinks) // Add trigger for linked models
            };
            var mepFilter = new LogicalOrFilter(filterList);

            UpdaterRegistry.AddTrigger(_instance.GetUpdaterId(), mepFilter, Element.GetChangeTypeGeometry());
            
            // For links, use a more sensitive trigger
            var linkFilter = new ElementCategoryFilter(BuiltInCategory.OST_RvtLinks);
            UpdaterRegistry.AddTrigger(_instance.GetUpdaterId(), linkFilter, Element.GetChangeTypeAny());
        }

        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            var modifiedIds = data.GetModifiedElementIds();

            // Collect all sleeve instances once (they have the PipeId parameter)
            List<FamilyInstance> allSleeves = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.LookupParameter("PipeId") != null && !string.IsNullOrEmpty(fi.LookupParameter("PipeId").AsString()))
                .ToList();

            foreach (ElementId modId in modifiedIds)
            {
                Element e = doc.GetElement(modId);
                if (e is RevitLinkInstance link)
                {
                    ProcessLinkSleeves(doc, link, allSleeves);
                }
                else if (e is MEPCurve curve)
                {
                    ProcessHostSleeves(doc, curve, allSleeves);
                }
            }
        }

        public static int ProcessAllSleeves(Document doc)
        {
            int movedCount = 0;
            List<FamilyInstance> allSleeves = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.LookupParameter("PipeId") != null && !string.IsNullOrEmpty(fi.LookupParameter("PipeId").AsString()))
                .ToList();

            var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
            foreach (var link in links)
            {
                movedCount += ProcessLinkSleeves(doc, link, allSleeves);
            }

            var curves = new FilteredElementCollector(doc).OfClass(typeof(MEPCurve)).Cast<MEPCurve>();
            foreach (var curve in curves)
            {
                movedCount += ProcessHostSleeves(doc, curve, allSleeves);
            }
            
            return movedCount;
        }

        private static int ProcessLinkSleeves(Document doc, RevitLinkInstance linkInstance, List<FamilyInstance> allSleeves)
        {
            int count = 0;
            Document linkDoc = linkInstance.GetLinkDocument();
            if (linkDoc == null) return 0;

            Transform linkTransform = linkInstance.GetTransform();
            // net48 compatibility: Use IndexOf or Regex for case-insensitive replace/contains
            string linkName = linkDoc.Title;
            if (linkName.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
                linkName = linkName.Substring(0, linkName.Length - 4);

            List<FamilyInstance> linkSleeves = allSleeves
                .Where(s => {
                    string? pid = s.LookupParameter("PipeId")?.AsString();
                    if (string.IsNullOrEmpty(pid)) return false;
                    
                    // .Contains(s, comparison) is netcore only
                    return pid.IndexOf($":{linkName}:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           pid.StartsWith($"LINK:{linkName}:", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            foreach (FamilyInstance sleeve in linkSleeves)
            {
                string pid = sleeve.LookupParameter("PipeId").AsString();
                int lastColon = pid.LastIndexOf(':');
                if (lastColon == -1) continue;

                string rawIdString = pid.Substring(lastColon + 1);
                if (Int64.TryParse(rawIdString, out long mepIdLong))
                {
                    Element linkedMep = linkDoc.GetElement(new ElementId(mepIdLong));
                    if (linkedMep is MEPCurve linkedMepCurve)
                    {
                        LocationCurve linkedLoc = linkedMepCurve.Location as LocationCurve;
                        if (linkedLoc != null && linkedLoc.Curve is Line linkedLine)
                        {
                            XYZ p0 = linkTransform.OfPoint(linkedLine.GetEndPoint(0));
                            XYZ p1 = linkTransform.OfPoint(linkedLine.GetEndPoint(1));
                            XYZ dir = (p1 - p0).Normalize();

                            if (!dir.IsZeroLength())
                            {
                                if (AlignSleeveToLine(doc, sleeve, Line.CreateUnbound(p0, dir)))
                                    count++;
                            }
                        }
                    }
                }
            }
            return count;
        }

        private static int ProcessHostSleeves(Document doc, MEPCurve mepCurve, List<FamilyInstance> allSleeves)
        {
            int count = 0;
            LocationCurve mepLoc = mepCurve.Location as LocationCurve;
            if (mepLoc == null) return 0;

            Line mepLine = mepLoc.Curve as Line;
            if (mepLine == null) return 0;

            string mepIdStrHost = mepCurve.Id.ToString();
            List<FamilyInstance> sleeves = allSleeves
                .Where(s => s.LookupParameter("PipeId")?.AsString() == mepIdStrHost)
                .ToList();

            XYZ p0 = mepLine.GetEndPoint(0);
            XYZ p1 = mepLine.GetEndPoint(1);
            XYZ dir = (p1 - p0).Normalize();
            if (dir.IsZeroLength()) return 0;

            Line unboundLine = Line.CreateUnbound(p0, dir);
            foreach (FamilyInstance sleeve in sleeves)
            {
                if (AlignSleeveToLine(doc, sleeve, unboundLine))
                    count++;
            }
            return count;
        }
        
        private static bool AlignSleeveToLine(Document doc, FamilyInstance sleeve, Line unboundLine)
        {
            LocationPoint loc = sleeve.Location as LocationPoint;
            if (loc == null) return false;

            XYZ origin = loc.Point;

            BoundingBoxXYZ bb = sleeve.get_BoundingBox(null);
            XYZ visualCenter;
            if (bb != null)
            {
                visualCenter = (bb.Min + bb.Max) / 2.0;
            }
            else
            {
                // Fallback: use origin and try to offset by height parameter if known
                visualCenter = origin;
                string[] heightParams = { "Height", "H", "Opening Height", "L" };
                foreach (string hName in heightParams)
                {
                    Parameter p = sleeve.LookupParameter(hName) ?? sleeve.Symbol.LookupParameter(hName);
                    if (p != null && p.HasValue)
                    {
                        visualCenter += XYZ.BasisZ * (p.AsDouble() / 2.0);
                        break;
                    }
                }
            }

            // Project the true center onto the MEP line
            IntersectionResult ir = unboundLine.Project(visualCenter);
            if (ir == null) return false;

            XYZ targetPoint = ir.XYZPoint;

            // Compute move vector (correcting the visual center offset)
            XYZ translation = targetPoint - visualCenter;
            
            // Only move if significantly misaligned
            if (translation.GetLength() > 0.001) 
            {
                ElementTransformUtils.MoveElement(doc, sleeve.Id, translation);
                return true;
            }
            return false;
        }
    }
}
