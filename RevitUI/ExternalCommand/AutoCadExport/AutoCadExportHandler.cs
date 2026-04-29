using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI.AutoCadExport;

namespace RevitUI.ExternalCommand.AutoCadExport
{
    public enum ExportMode
    {
        MultipleLayouts,   // Each sheet = Layout tab in one DWG
        ModelSpace,        // All sheets in Model Space with title blocks
        SingleLayout,      // All sheets on one Layout tab
        SeparateFiles      // Each sheet = separate DWG file
    }

    public class AutoCadExportHandler : IExternalEventHandler
    {
        public AutoCadExportUI Dashboard { get; set; }
        public List<ElementId> SelectedViewIds { get; set; } = new List<ElementId>();
        public string ExportPath { get; set; }
        public ExportMode Mode { get; set; }
        public ACADVersion Version { get; set; } = ACADVersion.R2018;
        public bool MergeLayers { get; set; } = true;

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            
            if (string.IsNullOrEmpty(ExportPath) || SelectedViewIds.Count == 0) return;

            try
            {
                using (Transaction tx = new Transaction(doc, "Export to AutoCAD"))
                {
                    tx.Start();

                    // ── 1. Setup Export Options ─────────────────────────────────
                    DWGExportOptions options = new DWGExportOptions();
                    options.FileVersion = Version;
                    // MergedViews = true flattens title block + drawing into Model Space
                    options.MergedViews = MergeLayers;

                    // ── 2. Execute Export ───────────────────────────────────────
                    bool success = doc.Export(ExportPath, "BDD_Export", SelectedViewIds, options);

                    tx.Commit();

                    if (success)
                    {
                        // ── 3. Generate merge script based on mode ─────────────
                        switch (Mode)
                        {
                            case ExportMode.MultipleLayouts:
                                GenerateMultipleLayoutsScript(doc, ExportPath, SelectedViewIds);
                                break;
                            case ExportMode.ModelSpace:
                                GenerateModelSpaceScript(doc, ExportPath, SelectedViewIds);
                                break;
                            case ExportMode.SingleLayout:
                                GenerateSingleLayoutScript(doc, ExportPath, SelectedViewIds);
                                break;
                            case ExportMode.SeparateFiles:
                                break;
                        }

                        // ── 4. Auto-run merge via AutoCAD Core Console ───────────
                        bool mergeSuccess = false;
                        if (Mode != ExportMode.SeparateFiles)
                        {
                            Dashboard?.Dispatcher.Invoke(() => Dashboard.UpdateStatus("Merging drawings..."));
                            mergeSuccess = RunAcCoreConsole(ExportPath);
                        }

                        Dashboard?.Dispatcher.Invoke(() => Dashboard.UpdateStatus($"Done — {SelectedViewIds.Count} sheets exported"));

                        string modeText = Mode switch
                        {
                            ExportMode.MultipleLayouts => "Multiple Layouts",
                            ExportMode.ModelSpace => "Model Space",
                            ExportMode.SingleLayout => "Single Layout",
                            ExportMode.SeparateFiles => "Separate Files",
                            _ => "Unknown"
                        };

                        string msg = $"Successfully exported {SelectedViewIds.Count} sheets.\nMode: {modeText}";
                        
                        if (Mode != ExportMode.SeparateFiles)
                        {
                            if (mergeSuccess)
                                msg += "\n\n✅ 'Combined CAD.dwg' has been created in your export folder!";
                            else
                                msg += "\n\n⚠ AutoCAD Core Console not found.\nA 'Master_Merge.scr' script has been saved.\n\nTo merge manually:\n1. Open a blank DWG in AutoCAD\n2. Type SCRIPT → Enter\n3. Select 'Master_Merge.scr'";
                        }

                        TaskDialog.Show("AutoCAD Export", msg);
                        System.Diagnostics.Process.Start("explorer.exe", ExportPath);
                    }
                    else
                    {
                        Dashboard?.Dispatcher.Invoke(() => Dashboard.UpdateStatus("Export failed"));
                        TaskDialog.Show("AutoCAD Export", "Export failed. Check your view selection.");
                    }
                }
            }
            catch (Exception ex)
            {
                Dashboard?.Dispatcher.Invoke(() => Dashboard.UpdateStatus("Error"));
                TaskDialog.Show("Export Error", ex.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Auto-run: Find AutoCAD and execute the merge script
        // ═══════════════════════════════════════════════════════════════════
        private bool RunAcCoreConsole(string folder)
        {
            try
            {
                string scriptPath = Path.Combine(folder, "Master_Merge.scr");
                if (!File.Exists(scriptPath)) return false;

                // Try 1: accoreconsole.exe (headless — fastest)
                string acCore = FindAutoCadExe("accoreconsole.exe");
                if (!string.IsNullOrEmpty(acCore))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = acCore,
                        Arguments = $"/s \"{scriptPath}\"",
                        WorkingDirectory = folder,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    var p = System.Diagnostics.Process.Start(psi);
                    p?.WaitForExit(180000); // Wait up to 3 minutes
                    
                    string combined = Path.Combine(folder, "Combined CAD.dwg");
                    if (File.Exists(combined)) return true;
                }

                // Try 2: acad.exe with /b flag (opens AutoCAD + runs script)
                string acad = FindAutoCadExe("acad.exe");
                if (!string.IsNullOrEmpty(acad))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = acad,
                        Arguments = $"/b \"{scriptPath}\"",
                        WorkingDirectory = folder,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private string FindAutoCadExe(string exeName)
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            
            // Check multiple AutoCAD versions (newest first)
            string[] versions = { "2026", "2025", "2024", "2023", "2022", "2021", "2020", "2019", "2018" };
            foreach (string ver in versions)
            {
                string path = Path.Combine(programFiles, "Autodesk", $"AutoCAD {ver}", exeName);
                if (File.Exists(path)) return path;
            }

            // Check all Autodesk subdirectories for non-standard installs
            string autodesk = Path.Combine(programFiles, "Autodesk");
            if (Directory.Exists(autodesk))
            {
                foreach (var dir in Directory.GetDirectories(autodesk, "AutoCAD*"))
                {
                    string path = Path.Combine(dir, exeName);
                    if (File.Exists(path)) return path;
                }
            }

            return null;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Mode 1: Multiple Layouts — each sheet becomes a Layout tab
        // ═══════════════════════════════════════════════════════════════════
        private void GenerateMultipleLayoutsScript(Document doc, string folder, List<ElementId> ids)
        {
            string scriptPath = Path.Combine(folder, "Master_Merge.scr");
            string savePath = Path.Combine(folder, "Combined CAD.dwg").Replace("\\", "/");
            double gap = 500000; // Increased gap to 500,000 units

            using (StreamWriter sw = new StreamWriter(scriptPath))
            {
                sw.WriteLine("(setvar \"FILEDIA\" 0)");
                sw.WriteLine("(setvar \"CMDECHO\" 0)");

                if (MergeLayers)
                {
                    // Bind all Xrefs if they exist
                    sw.WriteLine("(command \"-XREF\" \"B\" \"*\")");
                }

                sw.WriteLine("MODEL");

                // 1. Insert everything into Model Space first (Side-by-side)
                double xOffset = 0;
                foreach (var id in ids)
                {
                    Element el = doc.GetElement(id);
                    string name = GetExportedFileName(el);
                    string fullPath = Path.Combine(folder, name).Replace("\\", "/");

                    if (File.Exists(Path.Combine(folder, name)))
                    {
                        sw.WriteLine("-INSERT");
                        sw.WriteLine($"\"{fullPath}\"");
                        sw.WriteLine($"{xOffset},0");
                        sw.WriteLine("1");
                        sw.WriteLine("1");
                        sw.WriteLine("0");
                        xOffset += gap;
                    }
                }

                // 2. Import Layouts and fix them
                xOffset = 0;
                foreach (var id in ids)
                {
                    Element el = doc.GetElement(id);
                    string name = GetExportedFileName(el);
                    string fullPath = Path.Combine(folder, name).Replace("\\", "/");
                    
                    string tabName = el.Name;
                    if (el is ViewSheet vs) tabName = $"{vs.SheetNumber} - {vs.Name}";

                    if (File.Exists(Path.Combine(folder, name)))
                    {
                        // Store current layouts
                        sw.WriteLine("(setq old_layouts (layoutlist))");
                        
                        // Import all layouts from template (usually just one)
                        sw.WriteLine("-LAYOUT");
                        sw.WriteLine("T");
                        sw.WriteLine($"\"{fullPath}\"");
                        sw.WriteLine("*");

                        // Find the new layout and rename it
                        sw.WriteLine("(setq new_layout (car (vl-remove-if '(lambda (x) (member x old_layouts)) (layoutlist))))");
                        sw.WriteLine("(if new_layout (progn");
                        sw.WriteLine($"  (setvar \"CTAB\" new_layout)");
                        sw.WriteLine($"  (command \"-LAYOUT\" \"R\" new_layout \"{tabName}\")");
                        sw.WriteLine("  (command \"MSPACE\")");
                        sw.WriteLine("  (command \"-PAN\" \"0,0\" (list " + xOffset + " 0))");
                        sw.WriteLine("  (command \"PSPACE\")");
                        sw.WriteLine("))");

                        xOffset += gap;
                    }
                }

                // Cleanup default tabs
                sw.WriteLine("(if (member \"Layout1\" (layoutlist)) (command \"-LAYOUT\" \"D\" \"Layout1\"))");
                sw.WriteLine("(if (member \"Layout2\" (layoutlist)) (command \"-LAYOUT\" \"D\" \"Layout2\"))");

                sw.WriteLine("MODEL");
                sw.WriteLine("ZOOM E");
                // Simplified SAVEAS to avoid version/type prompt issues
                sw.WriteLine($"(command \"_.SAVEAS\" \"\" \"{savePath}\")");
                sw.WriteLine("(setvar \"FILEDIA\" 1)");
                sw.WriteLine("");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Mode 2: Model Space — all sheets inserted into Model Space
        // ═══════════════════════════════════════════════════════════════════
        private void GenerateModelSpaceScript(Document doc, string folder, List<ElementId> ids)
        {
            string scriptPath = Path.Combine(folder, "Master_Merge.scr");
            string savePath = Path.Combine(folder, "Combined CAD.dwg").Replace("\\", "/");
            double gap = 500000;

            using (StreamWriter sw = new StreamWriter(scriptPath))
            {
                sw.WriteLine("(setvar \"FILEDIA\" 0)");
                sw.WriteLine("(setvar \"CMDECHO\" 0)");

                if (MergeLayers)
                {
                    sw.WriteLine("(command \"-XREF\" \"B\" \"*\")");
                }

                sw.WriteLine("MODEL");
                
                double xOffset = 0;
                foreach (var id in ids)
                {
                    Element el = doc.GetElement(id);
                    string name = GetExportedFileName(el);
                    string fullPath = Path.Combine(folder, name).Replace("\\", "/");

                    if (File.Exists(Path.Combine(folder, name)))
                    {
                        sw.WriteLine("-INSERT");
                        sw.WriteLine($"\"{fullPath}\"");
                        sw.WriteLine($"{xOffset},0");
                        sw.WriteLine("1");
                        sw.WriteLine("1");
                        sw.WriteLine("0");
                        xOffset += gap;
                    }
                }

                sw.WriteLine("ZOOM E");
                sw.WriteLine($"(command \"_.SAVEAS\" \"\" \"{savePath}\")");
                sw.WriteLine("(setvar \"FILEDIA\" 1)");
                sw.WriteLine("");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Mode 3: Single Layout — all sheets on one Layout
        // ═══════════════════════════════════════════════════════════════════
        private void GenerateSingleLayoutScript(Document doc, string folder, List<ElementId> ids)
        {
            string scriptPath = Path.Combine(folder, "Master_Merge.scr");
            string savePath = Path.Combine(folder, "Combined CAD.dwg").Replace("\\", "/");
            double gap = 500000;

            using (StreamWriter sw = new StreamWriter(scriptPath))
            {
                sw.WriteLine("(setvar \"FILEDIA\" 0)");
                sw.WriteLine("(setvar \"CMDECHO\" 0)");

                if (MergeLayers)
                {
                    sw.WriteLine("(command \"-XREF\" \"B\" \"*\")");
                }

                sw.WriteLine("MODEL");
                
                double xOffset = 0;
                foreach (var id in ids)
                {
                    Element el = doc.GetElement(id);
                    string name = GetExportedFileName(el);
                    string fullPath = Path.Combine(folder, name).Replace("\\", "/");

                    if (File.Exists(Path.Combine(folder, name)))
                    {
                        sw.WriteLine("-INSERT");
                        sw.WriteLine($"\"{fullPath}\"");
                        sw.WriteLine($"{xOffset},0");
                        sw.WriteLine("1");
                        sw.WriteLine("1");
                        sw.WriteLine("0");
                        xOffset += gap;
                    }
                }

                sw.WriteLine("-LAYOUT");
                sw.WriteLine("N");
                sw.WriteLine("All Sheets");
                sw.WriteLine("ZOOM E");
                sw.WriteLine($"(command \"_.SAVEAS\" \"\" \"{savePath}\")");
                sw.WriteLine("(setvar \"FILEDIA\" 1)");
                sw.WriteLine("");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Helper: Get the filename Revit generates for each exported view
        // ═══════════════════════════════════════════════════════════════════
        private string GetExportedFileName(Element el)
        {
            if (el is ViewSheet s)
                return $"BDD_Export-Sheet - {s.SheetNumber} - {s.Name}.dwg";
            else
                return $"BDD_Export-{el.Name}.dwg";
        }

        public string GetName() => "AutoCAD Export Handler";
    }

    public class ViewSelectionItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
        public bool IsChecked { get; set; }
    }
}
