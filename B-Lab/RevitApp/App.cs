using Autodesk.Revit.UI;
using DataLab.Resources;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace B_Lab.RevitApp
{
    public class App : IExternalApplication
    {
        private const string TAB_NAME = "BIM Digital Design";
        private const string PANEL_NAME = "Opening";

        private static string? _assemblyFolder;

        public Result OnStartup(UIControlledApplication application)
        {
            _assemblyFolder = Path.GetDirectoryName(typeof(App).Assembly.Location);
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            try
            {
                try { application.CreateRibbonTab(TAB_NAME); } catch { }

                RibbonPanel panel = application
                    .GetRibbonPanels(TAB_NAME)
                    .FirstOrDefault(p => p.Name == PANEL_NAME)
                    ?? application.CreateRibbonPanel(TAB_NAME, PANEL_NAME);

                string dll = Path.Combine(_assemblyFolder!, "RevitUI.dll");

                // ── MEP Opening ──────────────────────────────────────────────
                var mepBtn = new PushButtonData(
                    "MEP_Opening_BTN",
                    "MEP",
                    dll,
                    "RevitUI.Command.OpeningCommand"
                );

                // ── Door/Window Opening ──────────────────────────────────────
                var dwBtn = new PushButtonData(
                    "DW_Opening_BTN",
                    "Door/Window",
                    dll,
                    "RevitUI.Command.LinkedDoorOpening"
                );

                // Add BOTH at once using stacked buttons — guaranteed to appear
                panel.AddStackedItems(mepBtn, dwBtn);

                // Set images, tooltips, and contextual help (F1)
                string helpPath = Path.Combine(_assemblyFolder!, "Helper", "master-opening-sleeves-help.html");
                
                // Fallback: Check if we are in AddinManager (temp folder) and look for original build folder
                if (!File.Exists(helpPath))
                {
                    // You might want to manually check a known location if this fails frequently in AddinManager
                }

                ContextualHelp? ch = null;
                if (File.Exists(helpPath))
                {
                    ch = new ContextualHelp(ContextualHelpType.Url, helpPath);
                }
                else
                {
                    // DEBUG: If you still don't see F1, uncomment the line below to see where it searches
                    // TaskDialog.Show("Help Missing", $"Searched at: {helpPath}");
                }

                foreach (RibbonItem item in panel.GetItems())
                {
                    if (item is PushButton btn)
                    {
                        // Common setup
                        if (ch != null) btn.SetContextualHelp(ch);

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

            // Get simple name
            string assemblyName = new AssemblyName(args.Name).Name;
            string fileName = assemblyName + ".dll";

            // List of directories to search
            var searchPaths = new[]
            {
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                @"C:\Users\Mohd Shahnawaz\source\repos\Revit-API-Lab\B-Lab\bin\x64\Debug\net8.0-windows"
            };

            foreach (var dir in searchPaths)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                string path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                {
                    try
                    {
                        return Assembly.LoadFrom(path);
                    }
                    catch { continue; }
                }
            }

            return null;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            return Result.Succeeded;
        }
    }
}