using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitUI.UI;

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

        public static int ProcessAllSleeves(Document doc, double? clearanceFeet = null)
        {
            int movedCount = 0;

            // 1. Collect all potential sleeves (must have PipeId parameter populated)
            List<FamilyInstance> allSleeves = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.LookupParameter("PipeId") != null && !string.IsNullOrEmpty(fi.LookupParameter("PipeId").AsString()))
                .ToList();

            if (allSleeves.Count == 0) return 0;

            // 2. Identify all possibly affected hosts
            // Process Links
            var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
            foreach (var link in links)
            {
                movedCount += ProcessLinkSleeves(doc, link, allSleeves, clearanceFeet);
            }

            // Process Host MEP Curves (explicitly including Pipes, Ducts, Cable Trays, Conduits)
            var catFilter = new LogicalOrFilter(new List<ElementFilter>
            {
                new ElementCategoryFilter(BuiltInCategory.OST_PipeCurves),
                new ElementCategoryFilter(BuiltInCategory.OST_DuctCurves),
                new ElementCategoryFilter(BuiltInCategory.OST_CableTray),
                new ElementCategoryFilter(BuiltInCategory.OST_Conduit)
            });

            var curves = new FilteredElementCollector(doc)
                .WherePasses(catFilter)
                .WhereElementIsNotElementType()
                .Cast<MEPCurve>();

            foreach (var curve in curves)
            {
                movedCount += ProcessHostSleeves(doc, curve, allSleeves, clearanceFeet);
            }

            return movedCount;
        }

        private static int ProcessLinkSleeves(Document doc, RevitLinkInstance linkInstance, List<FamilyInstance> allSleeves, double? clearanceFeet = null)
        {
            int count = 0;
            Document linkDoc = linkInstance.GetLinkDocument();
            if (linkDoc == null) return 0;

            Transform linkTransform = linkInstance.GetTransform();
            string linkTitle = linkDoc.Title.ToUpper();
            string linkTitleNoExt = linkTitle.EndsWith(".RVT") ? linkTitle.Substring(0, linkTitle.Length - 4) : linkTitle;

            // Filter sleeves belonging to this link using robust case-insensitive matching
            List<FamilyInstance> linkSleeves = allSleeves
                .Where(s => {
                    string? pid = s.LookupParameter("PipeId")?.AsString()?.ToUpper();
                    if (string.IsNullOrEmpty(pid)) return false;
                    
                    // Match link title with or without extension, or simple inclusion
                    return pid.Contains($":{linkTitle}:") || 
                           pid.Contains($":{linkTitleNoExt}:") ||
                           pid.Contains($"LINK:{linkTitle}:") ||
                           pid.Contains($"LINK:{linkTitleNoExt}:");
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
                        if (SyncSleeve(doc, sleeve, linkedMepCurve, linkTransform, clearanceFeet))
                            count++;
                    }
                }
            }
            return count;
        }

        private static int ProcessHostSleeves(Document doc, MEPCurve mepCurve, List<FamilyInstance> allSleeves, double? clearanceFeet = null)
        {
            int count = 0;
            string mepIdStrHost = mepCurve.Id.ToString();
            
            // Match sleeves pointing to this specific host ID
            List<FamilyInstance> sleeves = allSleeves
                .Where(s => s.LookupParameter("PipeId")?.AsString() == mepIdStrHost)
                .ToList();

            foreach (FamilyInstance sleeve in sleeves)
            {
                if (SyncSleeve(doc, sleeve, mepCurve, null, clearanceFeet))
                    count++;
            }
            return count;
        }

        private static bool SyncSleeve(Document doc, FamilyInstance sleeve, MEPCurve mep, Transform? xform, double? clearanceFeet)
        {
            bool changed = false;

            // 1. Position Sync
            LocationCurve? mepLoc = mep.Location as LocationCurve;
            if (mepLoc != null && mepLoc.Curve is Line mepLine)
            {
                XYZ p0 = xform != null ? xform.OfPoint(mepLine.GetEndPoint(0)) : mepLine.GetEndPoint(0);
                XYZ p1 = xform != null ? xform.OfPoint(mepLine.GetEndPoint(1)) : mepLine.GetEndPoint(1);
                XYZ dir = (p1 - p0).Normalize();
                if (!dir.IsZeroLength())
                {
                    if (AlignSleeveToLine(doc, sleeve, Line.CreateUnbound(p0, dir)))
                        changed = true;
                }
            }

            // 2. Dimension Sync (only if clearance specified)
            if (clearanceFeet.HasValue)
            {
                if (UpdateSleeveDimensions(sleeve, mep, clearanceFeet.Value))
                    changed = true;
            }

            return changed;
        }

        private static bool UpdateSleeveDimensions(FamilyInstance sleeve, MEPCurve mep, double clearance)
        {
            bool changed = false;
            (double halfW, double halfH) = GeometryHelper.GetMepHalfSize(mep);

            // Circular: Pipe / Conduit (Supports "Dia", "Diameter", "d")
            string[] diaNames = { "Dia", "Diameter", "d" };
            foreach (string name in diaNames)
            {
                Parameter? p = sleeve.LookupParameter(name);
                if (p != null && !p.IsReadOnly)
                {
                    double targetDia = (halfW + clearance) * 2.0;
                    if (Math.Abs(p.AsDouble() - targetDia) > 0.001)
                    {
                        p.Set(targetDia);
                        changed = true;
                    }
                    break;
                }
            }

            // Rectangular: Duct / Cable Tray (Supports "L", "B", "Width", "Height", "W", "H", "Opening Width", etc.)
            string[] wNames = { "L", "Width", "W", "Sleeve Width", "Opening Width" };
            string[] hNames = { "B", "Height", "H", "Sleeve Height", "Opening Height" };

            // Sync Width
            foreach (string name in wNames)
            {
                Parameter? p = sleeve.LookupParameter(name);
                if (p != null && !p.IsReadOnly)
                {
                    double targetW = (halfW + clearance) * 2.0;
                    if (Math.Abs(p.AsDouble() - targetW) > 0.001)
                    {
                        p.Set(targetW);
                        changed = true;
                    }
                    break;
                }
            }

            // Sync Height
            foreach (string name in hNames)
            {
                Parameter? p = sleeve.LookupParameter(name);
                if (p != null && !p.IsReadOnly)
                {
                    double targetH = (halfH + clearance) * 2.0;
                    if (Math.Abs(p.AsDouble() - targetH) > 0.001)
                    {
                        p.Set(targetH);
                        changed = true;
                    }
                    break;
                }
            }

            // Sync Depth (H) based on Host Thickness (if hosted)
            if (sleeve.Host != null)
            {
                Parameter? pH = sleeve.LookupParameter("H");
                if (pH != null && !pH.IsReadOnly)
                {
                    double targetH = GeometryHelper.GetHostThickness(sleeve.Host);
                    if (Math.Abs(pH.AsDouble() - targetH) > 0.001)
                    {
                        pH.Set(targetH);
                        changed = true;
                    }
                }
            }

            return changed;
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
