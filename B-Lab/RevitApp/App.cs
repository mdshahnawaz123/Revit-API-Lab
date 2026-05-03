using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using RevitUI.UI;
using DataLab.Resources;

namespace B_Lab.RevitApp
{
    public class App : IExternalApplication
    {
        private const string? TAB_NAME = "BIM Digital Design";
        private const string? PANEL_NAME = "Opening";
        private const string? PANEL_NAME1 = "QC Panel";
        private const string? PANEL_NAME2 = "Model";
        private const string? PANEL_NAME3 = "Export";
        private const string? PANEL_NAME5 = "About Me";

        private static string? _assemblyFolder;

        public Result OnStartup(UIControlledApplication application)
        {
            _assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            try { application.CreateRibbonTab(TAB_NAME); } catch { }

            try
            {
                RibbonPanel panel = GetOrAddPanel(application, PANEL_NAME);
                RibbonPanel panel1 = GetOrAddPanel(application, PANEL_NAME1);
                RibbonPanel panel2 = GetOrAddPanel(application, PANEL_NAME2);
                RibbonPanel panel3 = GetOrAddPanel(application, PANEL_NAME3);
                RibbonPanel panel5 = GetOrAddPanel(application, PANEL_NAME5);

                string dll = Path.Combine(_assemblyFolder!, "B_Lab_RevitUI.dll");

                // ── Button Data ───────────────────────────────────────────────
                var mepBtn = new PushButtonData("MEP_Opening_BTN", "MEP\nOpening", dll, "RevitUI.Command.OpeningCommand");
                var dwBtn = new PushButtonData("DW_Opening_BTN", "Door/Window\nOpening", dll, "RevitUI.Command.LinkedDoorOpening");
                var paraBtn = new PushButtonData("BDD_ParameterFilter_BTN_V3", "Parameter\nFilter", dll, "RevitUI.Command.ParamCommand");
                var healthBtn = new PushButtonData("BDD_ModelHealth_BTN_V3", "Model\nHealth", dll, "RevitUI.Command.ModelHealthCommand");
                var worksetBtn = new PushButtonData("BDD_Workset_BTN", "Workset\nAuto", dll, "RevitUI.Command.WorksetCommand");
                var purgeBtn = new PushButtonData("BDD_PurgePlus_BTN", "Purge\nPlus", dll, "RevitUI.Command.PurgeCommand");
                var roomBtn = new PushButtonData("room_3D_Tag_BTN", "3D Room\nTag", dll, "RevitUI.Command.Room3DTag");
                var loadingBtn = new PushButtonData("BDD_StructuralLoading_BTN_V3", "Structural\nLoading", dll, "RevitUI.Command.LoadingDigram");
                var dimBtn = new PushButtonData("BDD_DimensionAuto_BTN", "Dimension\nAuto", dll, "RevitUI.Command.DimensionCommand");
                var slopeBtn = new PushButtonData("BDD_SlopeAnalysis_BTN", "Slope\nAnalysis", dll, "RevitUI.Command.SlopeAnalysisCommand");
                var interopBtn = new PushButtonData("BDD_Interop_BTN", "Interop\nDashboard", dll, "RevitUI.Command.InteropCommand");
                var exportBtn = new PushButtonData("BDD_AutoCadExport_BTN", "Master\nExport", dll, "RevitUI.Command.AutoCadExportCommand");
                var sharedBtn = new PushButtonData("BDD_SharedParam_BTN", "Shared\nParam", dll, "RevitUI.Command.SharedParamCommand");
                var roomSheetBtn = new PushButtonData("BDD_RoomSheet_BTN", "Room\nSheet", dll, "RevitUI.Command.RoomSheetCommand");
                var aboutBtn = new PushButtonData("BDD_AboutMe_BTN", "About\nDeveloper", dll, "RevitUI.Command.AboutMeCommand");

                // ── Add Buttons ───────────────────────────────────────────────
                // Stacked items: set icons on data objects before adding
                var uiAssembly = typeof(RevitUI.Command.ParamCommand).Assembly;
                mepBtn.Image = ImageUtils.GetEmbeddedImage("RevitUI.Resources.MEPOpening.png", uiAssembly);
                dwBtn.Image = ImageUtils.GetEmbeddedImage("RevitUI.Resources.DoorOpening.png", uiAssembly);
                var stackedItems = panel.AddStackedItems(mepBtn, dwBtn);
                var paraButton = GetOrAddButton(panel1, paraBtn);
                var healthButton = GetOrAddButton(panel1, healthBtn);
                var worksetButton = GetOrAddButton(panel1, worksetBtn);
                var purgeButton = GetOrAddButton(panel1, purgeBtn);
                var sharedButton = GetOrAddButton(panel1, sharedBtn);
                
                var roomButton = GetOrAddButton(panel2, roomBtn);
                var loadingButton = GetOrAddButton(panel2, loadingBtn);
                var dimensionButton = GetOrAddButton(panel2, dimBtn);
                var slopeButton = GetOrAddButton(panel2, slopeBtn);
                var interopButton = GetOrAddButton(panel2, interopBtn);
                
                var exportButton = GetOrAddButton(panel3, exportBtn);
                var roomSheetButton = GetOrAddButton(panel3, roomSheetBtn);
                var aboutButton = GetOrAddButton(panel5, aboutBtn);

                // ── Icons ─────────────────────────────────────────────────────
                var dataAssembly = typeof(DataLab.Resources.ImageUtils).Assembly;
                try {
                    // Original buttons (from DataLab assembly)
                    paraButton.LargeImage = ImageUtils.GetEmbeddedImage("DataLab.Resources.para.png", dataAssembly);

                    // Original buttons (from RevitUI assembly)
                    healthButton.LargeImage = ImageUtils.GetEmbeddedImage("RevitUI.Resources.ModelHealth.png", uiAssembly);
                    roomButton.LargeImage = ImageUtils.GetEmbeddedImage("RevitUI.Resources.3DTag.png", uiAssembly);
                    loadingButton.LargeImage = ImageUtils.GetEmbeddedImage("RevitUI.Resources.StructuralLoadingIcon.png", uiAssembly);

                    // New buttons (from RevitUI assembly)
                    dimensionButton.LargeImage = ImageUtils.GetEmbeddedImage("RevitUI.Resources.Dimension.png", uiAssembly);
                    slopeButton.LargeImage = ImageUtils.GetEmbeddedImage("RevitUI.Resources.Slope.png", uiAssembly);
                    interopButton.LargeImage = ImageUtils.GetEmbeddedImage("RevitUI.Resources.Interop.png", uiAssembly);
                    worksetButton.LargeImage = ImageUtils.GetEmbeddedImage("RevitUI.Resources.Workset.png", uiAssembly);
                    purgeButton.LargeImage = ImageUtils.GetEmbeddedImage("RevitUI.Resources.Purge.png", uiAssembly);
                    exportButton.LargeImage = ImageUtils.GetEmbeddedImage("RevitUI.Resources.Export.png", uiAssembly);
                    roomSheetButton.LargeImage = ImageUtils.GetEmbeddedImage("RevitUI.Resources.Export.png", uiAssembly); // Reusing export icon for now
                    aboutButton.LargeImage = ImageUtils.GetEmbeddedImage("RevitUI.Resources.BDD_Main.png", uiAssembly);
                    sharedButton.LargeImage = ImageUtils.GetEmbeddedImage("RevitUI.Resources.SharedParam.png", uiAssembly);
                } catch { }

                // ── Help ──────────────────────────────────────────────────────
                void SetHelp(PushButton btn, string file) => btn.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, Path.Combine(_assemblyFolder!, "Helper", file)));
                SetHelp(paraButton, "ParameterFilterHelp.html");
                SetHelp(healthButton, "ModelHealthHelp.html");
                SetHelp(worksetButton, "WorksetHelp.html");
                SetHelp(purgeButton, "PurgeHelp.html");
                SetHelp(roomButton, "Help.html");
                SetHelp(loadingButton, "StructuralLoadingHelp.html");
                SetHelp(dimensionButton, "DimensionHelp.html");
                SetHelp(slopeButton, "SlopeHelp.html");
                SetHelp(interopButton, "InteropHelp.html");
                SetHelp(exportButton, "AutoCadExportHelp.html");
                SetHelp(roomSheetButton, "AutoCadExportHelp.html");
                SetHelp(sharedButton, "SharedParamHelp.html");
                SetHelp(aboutButton, "AboutHelp.html");

                BDDUpdater.CheckForUpdates(application);
                return Result.Succeeded;
            }
            catch (Exception ex) { TaskDialog.Show("App Error", ex.Message); return Result.Failed; }
        }

        private RibbonPanel GetOrAddPanel(UIControlledApplication app, string name) => app.GetRibbonPanels(TAB_NAME).FirstOrDefault(p => p.Name == name) ?? app.CreateRibbonPanel(TAB_NAME, name);
        private PushButton GetOrAddButton(RibbonPanel p, PushButtonData d) => p.GetItems().FirstOrDefault(i => i.Name == d.Name) as PushButton ?? p.AddItem(d) as PushButton;
        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}