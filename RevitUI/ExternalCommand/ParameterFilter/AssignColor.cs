using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;

public class AssignColorHandler : IExternalEventHandler
{
    public ElementId CategoryId;
    public string ParameterName;
    public string RuleOperator;
    public string FilterValue;
    public Color RevitColor;

    public void Execute(UIApplication app)
    {
        var doc = app.ActiveUIDocument.Document;

        var elements = new FilteredElementCollector(doc)
            .OfCategoryId(CategoryId)
            .WhereElementIsNotElementType()
            .Where(e =>
            {
                var p = e.LookupParameter(ParameterName);
                if (p == null) return false;

                string val = p.AsValueString() ?? p.AsString();

                if (val == null) return false;

                switch (RuleOperator)
                {
                    case "Equals": return val == FilterValue;
                    case "Contains": return val.Contains(FilterValue);
                    case "Begins With": return val.StartsWith(FilterValue);
                    case "Ends With": return val.EndsWith(FilterValue);
                    default: return false;
                }
            })
            .ToList();

        if (!elements.Any()) return;

        var solidFill = new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .First(x => x.GetFillPattern().IsSolidFill);

        using (Transaction t = new Transaction(doc, "Assign Color"))
        {
            t.Start();

            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetProjectionLineColor(RevitColor);
            ogs.SetSurfaceForegroundPatternId(solidFill.Id);
            ogs.SetSurfaceForegroundPatternColor(RevitColor);

            foreach (var e in elements)
                doc.ActiveView.SetElementOverrides(e.Id, ogs);

            t.Commit();
        }
    }

    public string GetName() => "Assign Color";
}