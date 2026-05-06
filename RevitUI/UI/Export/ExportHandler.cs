using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RevitUI.UI.Export
{
    public class ExportHandler : IExternalEventHandler
    {
        public string ExportPath { get; set; }
        public bool ExportPdf { get; set; }
        public bool ExportCad { get; set; }
        public bool ExportNwc { get; set; }
        public bool CombinePdf { get; set; }
        public string NamingFormat { get; set; } = "{ProjectNumber}_{SheetNumber}_{SheetName}";
        public List<ElementId> SelectedViewIds { get; set; } = new List<ElementId>();
        public bool ExportLinksIndividually { get; set; } = false;
        public bool ExportNwcByLevel { get; set; } = false;

        public class ViewSelectionItem
        {
            public string Name { get; set; }
            public ElementId Id { get; set; }
            public bool IsChecked { get; set; }
        }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            
            try
            {
                if (!Directory.Exists(ExportPath)) Directory.CreateDirectory(ExportPath);

                var viewsToExport = (SelectedViewIds != null && SelectedViewIds.Count > 0) 
                    ? SelectedViewIds 
                    : new List<ElementId> { doc.ActiveView.Id };

                if (ExportPdf) RunPdfExport(doc, viewsToExport);
                if (ExportCad) RunCadExport(doc, viewsToExport);
                if (ExportNwc) RunNwcExport(doc);

                TaskDialog.Show("B-Lab", "Master Export Complete!\nFiles are sorted into sub-folders.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Export Error", ex.Message);
            }
        }

        private void RunPdfExport(Document doc, List<ElementId> viewIds)
        {
            string pdfDir = Path.Combine(ExportPath, "PDF");
            if (!Directory.Exists(pdfDir)) Directory.CreateDirectory(pdfDir);

            PDFExportOptions options = new PDFExportOptions
            {
                Combine = CombinePdf
            };

            using (Transaction t = new Transaction(doc, "Export PDF"))
            {
                t.Start();
                if (CombinePdf)
                {
                    options.FileName = GetCustomName(doc, null, NamingFormat);
                    doc.Export(pdfDir, viewIds, options);
                }
                else
                {
                    foreach (var vid in viewIds)
                    {
                        View v = doc.GetElement(vid) as View;
                        options.FileName = GetCustomName(doc, v, NamingFormat);
                        doc.Export(pdfDir, new List<ElementId> { vid }, options);
                    }
                }
                t.Commit();
            }
        }

        private string GetCustomName(Document doc, View view, string format)
        {
            string name = format;
            string projectNum = doc.ProjectInformation.Number ?? "000";
            string date = DateTime.Now.ToString("yyyyMMdd");

            name = name.Replace("{ProjectNumber}", projectNum);
            name = name.Replace("{Date}", date);

            if (view != null)
            {
                if (view is ViewSheet sheet)
                {
                    name = name.Replace("{SheetNumber}", sheet.SheetNumber);
                    name = name.Replace("{SheetName}", sheet.Name);
                }
                else
                {
                    name = name.Replace("{SheetNumber}", "");
                    name = name.Replace("{SheetName}", view.Name);
                }
            }
            else
            {
                name = name.Replace("{SheetNumber}", "Combined");
                name = name.Replace("{SheetName}", "Set");
            }

            // Remove invalid characters
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            
            return name;
        }

        private void RunCadExport(Document doc, List<ElementId> viewIds)
        {
            string cadDir = Path.Combine(ExportPath, "CAD");
            if (!Directory.Exists(cadDir)) Directory.CreateDirectory(cadDir);

            DWGExportOptions options = new DWGExportOptions();
            
            using (Transaction t = new Transaction(doc, "Export DWG"))
            {
                t.Start();
                foreach (var vid in viewIds)
                {
                    View v = doc.GetElement(vid) as View;
                    string fileName = GetCustomName(doc, v, NamingFormat);
                    doc.Export(cadDir, fileName, new List<ElementId> { vid }, options);
                }
                t.Commit();
            }
        }

        private void RunNwcExport(Document doc)
        {
            string nwcDir = Path.Combine(ExportPath, "NWC");
            if (!Directory.Exists(nwcDir)) Directory.CreateDirectory(nwcDir);

            NavisworksExportOptions options = new NavisworksExportOptions();
            options.ExportScope = NavisworksExportScope.View;
            
            int exportCount = 0;
            List<string> errors = new List<string>();

            if (ExportNwcByLevel)
            {
                exportCount = ExportNwcSlicesWithDebug(doc, nwcDir, options, out errors);
            }
            else
            {
                using (Transaction t = new Transaction(doc, "Export NWC"))
                {
                    t.Start();
                    if (ExportLinksIndividually)
                    {
                        var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
                        foreach (var link in links)
                        {
                            Document linkDoc = link.GetLinkDocument();
                            if (linkDoc != null)
                            {
                                try 
                                { 
                                    // Note: Exporting a link directly with 'View' scope requires a view in that link.
                                    // If we don't have one, we might need to change scope or skip.
                                    options.ExportScope = NavisworksExportScope.Model; 
                                    linkDoc.Export(nwcDir, link.Name.Replace(".rvt", "") + ".nwc", options); 
                                    exportCount++;
                                } 
                                catch (Exception ex) { errors.Add($"Link {link.Name}: {ex.Message}"); }
                            }
                        }
                    }
                    else
                    {
                        try 
                        { 
                            options.ViewId = doc.ActiveView.Id;
                            options.ExportScope = NavisworksExportScope.View;
                            doc.Export(nwcDir, doc.Title + ".nwc", options); 
                            exportCount++;
                        } 
                        catch (Exception ex) { errors.Add($"Host Model: {ex.Message}"); }
                    }
                    t.Commit();
                }
            }

            string msg = $"NWC Export Complete.\nFiles Exported: {exportCount}";
            if (errors.Any()) msg += "\n\nErrors:\n" + string.Join("\n", errors.Take(5));
            TaskDialog.Show("B-Lab Debug", msg);
        }

        private int ExportNwcSlicesWithDebug(Document doc, string dir, NavisworksExportOptions options, out List<string> errors)
        {
            errors = new List<string>();
            int count = 0;
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            if (levels.Count == 0) { errors.Add("No levels found in project."); return 0; }

            ViewFamilyType vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>().FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

            if (vft == null) { errors.Add("No 3D View Type found."); return 0; }

            var targets = new List<(Document doc, string prefix, bool isLink)>();
            if (ExportLinksIndividually)
            {
                var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>();
                foreach (var l in links)
                {
                    var ld = l.GetLinkDocument();
                    if (ld != null) targets.Add((ld, l.Name.Replace(".rvt", ""), true));
                }
            }
            else
            {
                targets.Add((doc, doc.Title, false));
            }

            foreach (var target in targets)
            {
                if (target.isLink)
                {
                    errors.Add($"Skipping Link {target.prefix}: Cannot create temporary 3D views in read-only linked documents. Please export from the linked file itself or export host with links visible.");
                    continue;
                }

                using (Transaction t = new Transaction(target.doc, "NWC Slice Export"))
                {
                    t.Start();
                    try 
                    {
                        View3D view = View3D.CreateIsometric(target.doc, vft.Id);
                        view.IsSectionBoxActive = true;

                        for (int i = 0; i < levels.Count; i++)
                        {
                            Level lvl = levels[i];
                            double minZ = lvl.Elevation;
                            double maxZ = (i < levels.Count - 1) ? levels[i + 1].Elevation : (minZ + 15.0);

                            BoundingBoxXYZ sbox = new BoundingBoxXYZ();
                            sbox.Min = new XYZ(-1000, -1000, minZ); // Using large model extents
                            sbox.Max = new XYZ(1000, 1000, maxZ);
                            view.SetSectionBox(sbox);

                            options.ViewId = view.Id;
                            options.ExportScope = NavisworksExportScope.View;
                            string fileName = $"{target.prefix}_{lvl.Name}.nwc";
                            target.doc.Export(dir, fileName, options);
                            count++;
                        }
                        target.doc.Delete(view.Id);
                    }
                    catch (Exception ex) { errors.Add($"{target.prefix} Error: {ex.Message}"); }
                    t.Commit();
                }
            }
            return count;
        }

        public string GetName() => "Master Export Handler";
    }
}
