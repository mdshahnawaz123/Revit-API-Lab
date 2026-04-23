using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using DataLab.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RevitUI.ExternalCommand.Opening;

namespace B_Lab.RevitApp
{
    public class App : IExternalApplication
    {
        private const string? TAB_NAME = "BIM Digital Design";
        private const string? PANEL_NAME = "Opening";
        private const string? PANEL_NAME1 = "QC Panel";
        private const string? PANEL_NAME2 = "Model";

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

                string dll = Path.Combine(_assemblyFolder!, "RevitUI.dll");

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

                var roomBtn = new PushButtonData(
                    "room_3D_Tag_BTN", "3D Room\nTag", dll,
                    "RevitUI.Command.Room3DTag");

                // ── Add to panels ─────────────────────────────────────────────
                panel.AddStackedItems(mepBtn, dwBtn);
                var paraButton = panel1.AddItem(paraBtn) as PushButton; 
                var roomButton = panel2.AddItem(roomBtn) as PushButton; 

                // ── Help file paths ───────────────────────────────────────────
                string mepHelpPath = Path.Combine(_assemblyFolder!, "Helper", "master-opening-sleeves-help.html");
                string paraHelpPath = Path.Combine(_assemblyFolder!, "Helper", "ParameterFilterHelp.html"); // ✅ F1 for ParamFilter

                ContextualHelp? mepHelp = File.Exists(mepHelpPath)
                    ? new ContextualHelp(ContextualHelpType.Url, mepHelpPath)
                    : null;

                ContextualHelp? paraHelp = File.Exists(paraHelpPath)
                    ? new ContextualHelp(ContextualHelpType.Url, paraHelpPath)
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

                // ── Setup Room 3D Tag button (Model panel) ────────────────────
                if (roomButton != null)
                {
                    roomButton.ToolTip = "3D Room Tag - Create and sync 3D tags for rooms in Host and Linked models.";
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