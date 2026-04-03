using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Reflection;
using DataLab.Resources;

namespace B_Lab.RevitApp
{
    public class App : IExternalApplication
    {
        private const string TAB_NAME = "BIM Digital Design";
        private const string PANEL_NAME = "MEP Wizz";
        private const string BUTTON_NAME = "Opening";

        private static string? _assemblyFolder;

        public Result OnStartup(UIControlledApplication application)
        {
            _assemblyFolder = Path.GetDirectoryName(
                typeof(App).Assembly.Location);

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            try
            {
                try { application.CreateRibbonTab(TAB_NAME); } catch { }

                RibbonPanel? panel = null;

                foreach (RibbonPanel p in application.GetRibbonPanels(TAB_NAME))
                {
                    if (p.Name == PANEL_NAME)
                    {
                        panel = p;
                        break;
                    }
                }

                panel ??= application.CreateRibbonPanel(TAB_NAME, PANEL_NAME);

                foreach (var item in panel.GetItems())
                {
                    if (item.Name == "Opening_Master_BTN")
                        return Result.Succeeded;
                }

                // Points to B-Lab.dll — OpeningCommand lives here
                string revitUiPath = Path.Combine(_assemblyFolder!, "RevitUI.dll");

                PushButtonData buttonData = new PushButtonData(
                    "Opening_Master_BTN",
                    BUTTON_NAME,
                    revitUiPath,
                    "RevitUI.ExternalCommand.Opening.OpeningCommand"  // new full class name
                );

                PushButton button = (PushButton)panel.AddItem(buttonData);

                button.LargeImage = ImageUtils.GetEmbeddedImage("");
                button.Image = ImageUtils.GetEmbeddedImage("");

                button.ToolTip = "Opening";
                button.LongDescription = "Generates and validates Opening Based on MEP Element.";

                string helpPath = @"C:\Autodesk-APS\DataLab\Resources\Helper\master-opening-sleeves-help.html";
                ContextualHelp help = new ContextualHelp(ContextualHelpType.ChmFile, helpPath);
                button.SetContextualHelp(help);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("B-Lab", ex.Message);
                return Result.Failed;
            }
        }

        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            if (_assemblyFolder is null) return null;

            string assemblyName = new AssemblyName(args.Name).Name + ".dll";
            string fullPath = Path.Combine(_assemblyFolder, assemblyName);

            if (File.Exists(fullPath))
                return Assembly.LoadFrom(fullPath);

            return null;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            return Result.Succeeded;
        }
    }
}