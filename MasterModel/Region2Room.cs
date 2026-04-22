using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MasterModel
{
    [Transaction(TransactionMode.Manual)]
    public class Region2Room : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            var log = new StringBuilder();
            log.AppendLine("=== Region2Room Debug Log ===");
            log.AppendLine($"Revit Version : {commandData.Application.Application.VersionNumber}");
            log.AppendLine($"Document      : {doc?.Title ?? "NULL"}");
            log.AppendLine();

            try
            {
                // ── STEP 1: Document ──────────────────────────────────
                log.AppendLine("STEP 1: Checking document...");
                if (doc == null)
                {
                    TaskDialog.Show("Debug", log + "\nFAIL: doc is null.");
                    return Result.Failed;
                }
                log.AppendLine("  OK: document found.");

                // ── STEP 2: Level ─────────────────────────────────────
                log.AppendLine("\nSTEP 2: Collecting levels...");
                var allLevels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                log.AppendLine($"  Found {allLevels.Count} level(s):");
                foreach (var lv in allLevels)
                    log.AppendLine($"    - '{lv.Name}'  elevation={lv.Elevation:F4}");

                var level = allLevels.FirstOrDefault();
                if (level == null)
                {
                    TaskDialog.Show("Debug", log + "\nFAIL: No levels found.");
                    return Result.Failed;
                }
                log.AppendLine($"  Using level: '{level.Name}'");

                // ── STEP 3: Plan view ─────────────────────────────────
                log.AppendLine("\nSTEP 3: Collecting plan views...");
                var allViews = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(v => !v.IsTemplate)
                    .ToList();

                log.AppendLine($"  Found {allViews.Count} non-template plan view(s):");
                foreach (var vw in allViews)
                    log.AppendLine($"    - '{vw.Name}'  GenLevel='{vw.GenLevel?.Name ?? "NULL"}'");

                var planView = allViews.FirstOrDefault(v =>
                    v.GenLevel != null && v.GenLevel.Id == level.Id);

                if (planView == null)
                {
                    // Fallback: just take any non-template plan view
                    planView = allViews.FirstOrDefault();
                    log.AppendLine($"  WARN: No view matched level — using fallback: '{planView?.Name ?? "NONE"}'");
                }
                else
                {
                    log.AppendLine($"  Using view: '{planView.Name}'");
                }

                if (planView == null)
                {
                    TaskDialog.Show("Debug", log + "\nFAIL: No plan view available.");
                    return Result.Failed;
                }

                // ── STEP 4: FilledRegions ─────────────────────────────
                log.AppendLine("\nSTEP 4: Collecting FilledRegions...");
                var regions = new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegion))
                    .OfCategory(BuiltInCategory.OST_DetailComponents)
                    .WhereElementIsNotElementType()
                    .Cast<FilledRegion>()
                    .ToList();

                log.AppendLine($"  Found {regions.Count} filled region(s):");
                foreach (var r in regions)
                {
                    var loops = r.GetBoundaries();
                    log.AppendLine($"    - Id={r.Id}  " +
                                   $"Name='{r.Name}'  " +
                                   $"Loops={loops.Count}  " +
                                   $"OwnerViewId={r.OwnerViewId}");
                }

                if (!regions.Any())
                {
                    TaskDialog.Show("Debug", log.ToString() + "\nFAIL: No filled regions found.");
                    return Result.Succeeded;
                }

                // ── STEP 5: Transaction ───────────────────────────────
                log.AppendLine("\nSTEP 5: Starting transaction...");
                var errors = new List<string>();
                int created = 0;

                using (Transaction t = new Transaction(doc, "FilledRegion → Room"))
                {
                    t.Start();
                    log.AppendLine("  Transaction started.");

                    // ── STEP 6: SketchPlane ───────────────────────────
                    log.AppendLine("\nSTEP 6: Creating SketchPlane...");
                    SketchPlane sketchPlane;
                    try
                    {
                        sketchPlane = SketchPlane.Create(
                            doc,
                            Plane.CreateByNormalAndOrigin(
                                XYZ.BasisZ,
                                new XYZ(0, 0, level.Elevation)
                            )
                        );
                        log.AppendLine($"  OK: SketchPlane id={sketchPlane.Id}");
                    }
                    catch (Exception ex)
                    {
                        t.RollBack();
                        TaskDialog.Show("Debug", log + $"\nFAIL at SketchPlane.Create:\n{ex.Message}");
                        return Result.Failed;
                    }

                    // ── STEP 7: Per-region loop ───────────────────────
                    log.AppendLine("\nSTEP 7: Processing regions...");

                    foreach (var region in regions)
                    {
                        string regionTag = $"Region {region.Id}";
                        log.AppendLine($"\n  [{regionTag}]");

                        try
                        {
                            // 7a — boundaries
                            var loops = region.GetBoundaries();
                            log.AppendLine($"    Loops count : {loops.Count}");

                            var outerLoop = loops.FirstOrDefault();
                            if (outerLoop == null)
                            {
                                log.AppendLine("    SKIP: outerLoop is null.");
                                continue;
                            }

                            var curveList = outerLoop.ToList();
                            log.AppendLine($"    Curves in loop : {curveList.Count}");

                            // 7b — centroid
                            var pts = curveList
                                .Select(c => c.GetEndPoint(0))
                                .ToList();

                            var centroid = new XYZ(
                                pts.Average(p => p.X),
                                pts.Average(p => p.Y),
                                level.Elevation
                            );
                            log.AppendLine($"    Centroid : ({centroid.X:F3}, {centroid.Y:F3}, {centroid.Z:F3})");

                            // 7c — duplicate check
                            var existingRoom = doc.GetRoomAtPoint(centroid);
                            if (existingRoom != null)
                            {
                                log.AppendLine($"    SKIP: room already exists (id={existingRoom.Id}).");
                                continue;
                            }
                            log.AppendLine("    No existing room at centroid.");

                            // 7d — CurveArray
                            var curveArray = new CurveArray();
                            foreach (var curve in curveList)
                                curveArray.Append(curve);
                            log.AppendLine($"    CurveArray built ({curveArray.Size} curves).");

                            // 7e — boundary lines
                            log.AppendLine("    Calling NewRoomBoundaryLines...");
                            doc.Create.NewRoomBoundaryLines(sketchPlane, curveArray, planView);
                            log.AppendLine("    OK: boundary lines created.");

                            // 7f — regenerate
                            log.AppendLine("    Calling Regenerate...");
                            doc.Regenerate();
                            log.AppendLine("    OK: regenerated.");

                            // 7g — create room
                            log.AppendLine("    Calling NewRoom...");
                            var room = doc.Create.NewRoom(level, new UV(centroid.X, centroid.Y));

                            if (room == null)
                            {
                                log.AppendLine("    FAIL: NewRoom returned null.");
                                errors.Add($"{regionTag}: NewRoom returned null.");
                                continue;
                            }

                            log.AppendLine($"    OK: room created id={room.Id}  " +
                                           $"Name='{room.Name}'  Number='{room.Number}'");
                            created++;
                        }
                        catch (Exception ex)
                        {
                            log.AppendLine($"    EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                            log.AppendLine($"    StackTrace: {ex.StackTrace}");
                            errors.Add($"{regionTag}: {ex.Message}");
                        }
                    }

                    t.Commit();
                    log.AppendLine("\nTransaction committed.");
                }

                // ── Final report ──────────────────────────────────────
                log.AppendLine($"\n=== DONE: {created} room(s) created, {errors.Count} error(s) ===");

                // Show full log — copy from dialog for analysis
                TaskDialog td = new TaskDialog("Region2Room Debug");
                td.MainInstruction = $"Created {created} / {regions.Count}   Errors: {errors.Count}";
                td.MainContent = log.ToString();
                td.Show();
            }
            catch (Exception ex)
            {
                // Outer catch — something crashed before/outside the transaction
                log.AppendLine($"\nOUTER EXCEPTION: {ex.GetType().Name}");
                log.AppendLine($"Message   : {ex.Message}");
                log.AppendLine($"StackTrace: {ex.StackTrace}");
                TaskDialog.Show("Debug – Outer Crash", log.ToString());
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}