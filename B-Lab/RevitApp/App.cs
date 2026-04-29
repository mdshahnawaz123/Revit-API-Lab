using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using DataLab.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RevitUI.ExternalCommand.Opening;
using System.Windows.Media;

namespace B_Lab.RevitApp
{
    public class App : IExternalApplication
    {
        private const string? TAB_NAME = "BIM Digital Design";
        private const string? PANEL_NAME = "Opening";
        private const string? PANEL_NAME1 = "QC Panel";
        private const string? PANEL_NAME2 = "Model";
        private const string? PANEL_NAME3 = "Export";

        private static string? _assemblyFolder;

        public Result OnStartup(UIControlledApplication application)
        {
            _assemblyFolder = Path.GetDirectoryName(typeof(App).Assembly.Location);
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            try
            {
                RegisterMepSleeveUpdater(application.ActiveAddInId);

                try { application.CreateRibbonTab(TAB_NAME); } catch { }

                // ── Panels ────────────────────────────────────────────────────
                RibbonPanel panel = application
                    .GetRibbonPanels(TAB_NAME)
                    .FirstOrDefault(p => p.Name == PANEL_NAME)
                    ?? application.CreateRibbonPanel(TAB_NAME, PANEL_NAME);

                RibbonPanel panel1 = application
                    .GetRibbonPanels(TAB_NAME)
                    .FirstOrDefault(p => p.Name == PANEL_NAME1)
                    ?? application.CreateRibbonPanel(TAB_NAME, PANEL_NAME1);

                RibbonPanel panel2 = application
                    .GetRibbonPanels(TAB_NAME)
                    .FirstOrDefault(p => p.Name == PANEL_NAME2)
                    ?? application.CreateRibbonPanel(TAB_NAME, PANEL_NAME2);

                RibbonPanel panel3 = application
                    .GetRibbonPanels(TAB_NAME)
                    .FirstOrDefault(p => p.Name == PANEL_NAME3)
                    ?? application.CreateRibbonPanel(TAB_NAME, PANEL_NAME3);

                string dll = Path.Combine(_assemblyFolder!, "B_Lab_RevitUI.dll");

                // ── Button Data ───────────────────────────────────────────────
                var mepBtn = new PushButtonData(
                    "MEP_Opening_BTN", "MEP", dll,
                    "RevitUI.Command.OpeningCommand");

                var dwBtn = new PushButtonData(
                    "DW_Opening_BTN", "Door/Window", dll,
                    "RevitUI.Command.LinkedDoorOpening");

                var paraBtn = new PushButtonData(
                    "para_Filter_BTN", "Parameter\nFilter", dll,
                    "RevitUI.Command.ParamCommand");

                var healthBtn = new PushButtonData(
                    "BDD_ModelHealth_BTN", "Model\nHealth", dll,
                    "RevitUI.Command.ModelHealthCommand");

                var roomBtn = new PushButtonData(
                    "room_3D_Tag_BTN", "3D Room\nTag", dll,
                    "RevitUI.Command.Room3DTag");

                var loadingBtn = new PushButtonData(
                    "BDD_StructuralLoading_BTN_V3", "Structural\nLoading", dll,
                    "RevitUI.Command.LoadingDigram");

                var exportBtn = new PushButtonData(
                    "BDD_AutoCadExport_BTN", "AutoCAD\nExport", dll,
                    "RevitUI.Command.AutoCadExportCommand");

                // ── Add to panels ─────────────────────────────────────────────
                var existingPanelItems = panel.GetItems();
                var existingPanel1Items = panel1.GetItems();
                var existingPanel2Items = panel2.GetItems();

                if (!existingPanelItems.Any(i => i.Name == "MEP_Opening_BTN"))
                {
                    panel.AddStackedItems(mepBtn, dwBtn);
                }

                var paraButton = existingPanel1Items.FirstOrDefault(i => i.Name == "para_Filter_BTN") as PushButton 
                    ?? panel1.AddItem(paraBtn) as PushButton; 

                var healthButton = existingPanel1Items.FirstOrDefault(i => i.Name == "BDD_ModelHealth_BTN") as PushButton 
                    ?? panel1.AddItem(healthBtn) as PushButton; 

                var roomButton = existingPanel2Items.FirstOrDefault(i => i.Name == "room_3D_Tag_BTN") as PushButton 
                    ?? panel2.AddItem(roomBtn) as PushButton; 

                var loadingButton = existingPanel2Items.FirstOrDefault(i => i.Name == "BDD_StructuralLoading_BTN_V3") as PushButton 
                    ?? panel2.AddItem(loadingBtn) as PushButton; 

                var exportButton = panel3.GetItems().FirstOrDefault(i => i.Name == "BDD_AutoCadExport_BTN") as PushButton 
                    ?? panel3.AddItem(exportBtn) as PushButton; 

                // ── Help file paths ───────────────────────────────────────────
                string mepHelpPath = Path.Combine(_assemblyFolder!, "Helper", "master-opening-sleeves-help.html");
                string paraHelpPath = Path.Combine(_assemblyFolder!, "Helper", "ParameterFilterHelp.html"); // ✅ F1 for ParamFilter
                string roomHelpPath = Path.Combine(_assemblyFolder!, "Helper", "Help.html"); // ✅ F1 for Room 3D Tag
                string loadingHelpPath = Path.Combine(_assemblyFolder!, "Helper", "StructuralLoadingHelp.html"); // ✅ F1 for Loading
                string cadHelpPath = Path.Combine(_assemblyFolder!, "Helper", "AutoCadExportHelp.html"); // ✅ F1 for AutoCAD Export

                ContextualHelp? mepHelp = File.Exists(mepHelpPath)
                    ? new ContextualHelp(ContextualHelpType.Url, mepHelpPath)
                    : null;

                ContextualHelp? paraHelp = File.Exists(paraHelpPath)
                    ? new ContextualHelp(ContextualHelpType.Url, paraHelpPath)
                    : null;

                ContextualHelp? roomHelp = File.Exists(roomHelpPath)
                    ? new ContextualHelp(ContextualHelpType.Url, roomHelpPath)
                    : null;

                ContextualHelp? loadingHelp = File.Exists(loadingHelpPath)
                    ? new ContextualHelp(ContextualHelpType.Url, loadingHelpPath)
                    : null;

                ContextualHelp? cadHelp = File.Exists(cadHelpPath)
                    ? new ContextualHelp(ContextualHelpType.Url, cadHelpPath)
                    : null;

                // ── Setup Opening panel buttons ───────────────────────────────
                foreach (RibbonItem item in panel.GetItems())
                {
                    if (item is not PushButton btn) continue;
                    if (mepHelp != null) btn.SetContextualHelp(mepHelp);

                    if (item.Name == "MEP_Opening_BTN")
                    {
                        var img = ImageUtils.GetEmbeddedImage("DataLab.Resources.Wall.png");
                        btn.Image = img;
                        btn.LargeImage = img;
                        btn.ToolTip = "MEP Opening Tool - Create openings for Pipes, Ducts, and Cable Trays.";
                    }

                    if (item.Name == "DW_Opening_BTN")
                    {
                        var img = ImageUtils.GetEmbeddedImage("DataLab.Resources.Door.png");
                        btn.Image = img;
                        btn.LargeImage = img;
                        btn.ToolTip = "Door/Window Opening Tool - Create openings from linked models.";
                    }
                }

                // ── Setup Parameter Filter button (panel1) ────────────────────
                if (paraButton != null)
                {
                    var img = ImageUtils.GetEmbeddedImage("DataLab.Resources.para.png");
                    paraButton.Image = img;
                    paraButton.LargeImage = img;
                    paraButton.ToolTip = "Parameter Filter - Filter and isolate elements by parameter value.";

                    // ✅ F1 contextual help pointing to ParameterFilterHelp.html
                    if (paraHelp != null)
                        paraButton.SetContextualHelp(paraHelp);
                }

                // ── Setup Model Health button (panel1) ────────────────────────
                if (healthButton != null)
                {
                    var uiAssembly = typeof(RevitUI.Command.ModelHealthCommand).Assembly;
                    var img = ImageUtils.GetEmbeddedImage("RevitUI.Resources.ModelHealth.png", uiAssembly);
                    healthButton.Image = img;
                    healthButton.LargeImage = img;
                    healthButton.ToolTip = "Model Health Dashboard - Monitor model performance, warnings, and CAD imports.";
                    healthButton.LongDescription = "This dashboard provides a real-time 'Health Score' for your model by scanning for performance bottlenecks such as excessive warnings, in-place families, and unpurged CAD imports.";
                }

                // ── Setup Room 3D Tag button (Model panel) ────────────────────
                if (roomButton != null)
                {
                    try 
                    {
                        Assembly revitUIAssembly = Assembly.LoadFrom(dll);
                        var img = ImageUtils.GetEmbeddedImage("RevitUI.Resources.3DTag.png", revitUIAssembly);
                        roomButton.Image = img;
                        roomButton.LargeImage = img;
                    }
                    catch { /* Fallback if image fails to load */ }

                    roomButton.ToolTip = "3D Room Tag - Create and sync 3D tags for rooms in Host and Linked models.";

                    if (roomHelp != null)
                        roomButton.SetContextualHelp(roomHelp);
                }

                // Check GitHub for updates asynchronously
                BDDUpdater.CheckForUpdates(application);

                // ── Setup Loading Diagram button (Model panel) ────────────────────
                if (loadingButton != null)
                {
                    try 
                    {
                        var uiAssembly = typeof(RevitUI.Command.LoadingDigram).Assembly;
                        var img = ImageUtils.GetEmbeddedImage("RevitUI.Resources.StructuralLoadingIcon.png", uiAssembly);
                        loadingButton.Image = img;
                        loadingButton.LargeImage = img;
                        loadingButton.ToolTipImage = ImageUtils.GetEmbeddedImage("RevitUI.Resources.StructuralLoadingPreview.png", uiAssembly);
                    }
                    catch { /* Fallback if image fails to load */ }

                    loadingButton.ToolTip = "Structural Loading Diagram - Automate and sync structural load visualizations.";
                    loadingButton.LongDescription = "This tool automates the generation of structural loading diagrams by mapping wall types to distinct detail line styles. " +
                        "It features a smart sync system that updates geometry as walls move and a cleanup utility to remove outdated or orphaned lines, ensuring your analysis diagrams stay accurate and professional.";

                    if (loadingHelp != null)
                        loadingButton.SetContextualHelp(loadingHelp);
                }

                // ── Setup AutoCAD Export button (panel3) ──────────────────────
                if (exportButton != null)
                {
                    try 
                    {
                        var uiAssembly = typeof(RevitUI.Command.AutoCadExportCommand).Assembly;
                        ImageSource? img = null;
                        try { img = ImageUtils.GetEmbeddedImage("RevitUI.Resources.cad.png", uiAssembly); }
                        catch { img = ImageUtils.GetEmbeddedImage("B_Lab_RevitUI.Resources.cad.png", uiAssembly); }

                        exportButton.Image = img;
                        exportButton.LargeImage = img;
                    }
                    catch { /* Fallback if image fails */ }

                    exportButton.ToolTip = "AutoCAD Batch Export - Export and merge multiple views to DWG.";
                    exportButton.LongDescription = "This professional tool automates the delivery of CAD packages by consolidating Revit sheets into a single, coordinated master file. " +
                        "It features a high-performance merge engine that supports Multiple Layouts (with automated viewport centering), Model Space grids, and Single Layout modes. " +
                        "By eliminating manual file manipulation, it ensures your AutoCAD deliverables are consistent, professional, and ready for submission.";

                    if (cadHelp != null)
                        exportButton.SetContextualHelp(cadHelp);
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("B-Lab Startup", ex.Message);
                return Result.Failed;
            }
        }

        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            if (string.IsNullOrEmpty(args.Name)) return null;

            string assemblyName = new AssemblyName(args.Name).Name;
            string fileName = assemblyName + ".dll";

            var searchPaths = new[]
            {
                _assemblyFolder
            };

            foreach (var dir in searchPaths)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                string path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                {
                    try { return Assembly.LoadFrom(path); }
                    catch { continue; }
                }
            }

            return null;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            // Unregister using the known GUID to be safe
            var updaterId = new UpdaterId(application.ActiveAddInId, new Guid("E5D8F0A2-C14B-4E31-9F38-725CD19A8B73"));
            if (UpdaterRegistry.IsUpdaterRegistered(updaterId))
            {
                try { UpdaterRegistry.UnregisterUpdater(updaterId); } catch { }
            }

            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            return Result.Succeeded;
        }

        private static MepSleeveUpdater? _mepSleeveUpdater;

        private void RegisterMepSleeveUpdater(AddInId addInId)
        {
            // Call the static Register method to ensure consistent filters and logic
            MepSleeveUpdater.Register(addInId);
            
            // Still keep a reference if needed for Unregister in OnShutdown, 
            // though MepSleeveUpdater.Register manages its own singleton.
            // We can retrieve the registered updater's ID for OnShutdown.
        }
    }
}