using Autodesk.Revit.UI;
using System;
using System.Reflection;
using DataLab.Resources;

namespace B_Lab.RevitApp
{
    public class App : IExternalApplication
    {
        private const string TAB_NAME = "BIM Digital Design";
        private const string PANEL_NAME = "MEP Wizz";
        private const string BUTTON_NAME = "Opening";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Create Tab (ignore if already exists)
                try { application.CreateRibbonTab(TAB_NAME); } catch { }

                // Get or Create Panel
                RibbonPanel panel = null;

                foreach (RibbonPanel p in application.GetRibbonPanels(TAB_NAME))
                {
                    if (p.Name == PANEL_NAME)
                    {
                        panel = p;
                        break;
                    }
                }

                if (panel == null)
                    panel = application.CreateRibbonPanel(TAB_NAME, PANEL_NAME);

                // Avoid duplicate button
                foreach (var item in panel.GetItems())
                {
                    if (item.Name == "Opening_Master_BTN")
                        return Result.Succeeded;
                }

                // ✅ CORRECT command path (IMPORTANT)
                PushButtonData buttonData = new PushButtonData(
                    "Opening_Master_BTN",
                    BUTTON_NAME,
                    Assembly.GetExecutingAssembly().Location,
                    "B_Lab.Command.OpeningCommand"
                );

                PushButton button = panel.AddItem(buttonData) as PushButton;

                // Optional images
                button.LargeImage = ImageUtils.GetEmbeddedImage("");
                button.Image = ImageUtils.GetEmbeddedImage("");

                // Tooltip
                button.ToolTip = "Opening";
                button.LongDescription = "Generates and validates Opening Based on MEP Element.";

                // Help file (F1)
                string helpPath = @"C:\Autodesk-APS\DataLab\Resources\Helper\master-opening-sleeves-help.html";
                ContextualHelp help = new ContextualHelp(ContextualHelpType.ChmFile, helpPath);
                button.SetContextualHelp(help);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Opening Wizz", ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}