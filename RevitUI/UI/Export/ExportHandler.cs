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

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            
            try
            {
                if (!Directory.Exists(ExportPath)) Directory.CreateDirectory(ExportPath);

                var viewIds = new List<ElementId> { doc.ActiveView.Id };

                if (ExportPdf) RunPdfExport(doc, viewIds);
                if (ExportCad) RunCadExport(doc, viewIds);
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
            
            using (Transaction t = new Transaction(doc, "Export NWC"))
            {
                t.Start();
                doc.Export(nwcDir, doc.Title + ".nwc", options);
                t.Commit();
            }
        }

        public string GetName() => "Master Export Handler";
    }
}
