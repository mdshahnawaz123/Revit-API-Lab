using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RevitUI.UI.SharedParam
{
    public enum SharedParamMode { LoadFile, Apply }

    public class SharedParamHandler : IExternalEventHandler
    {
        public SharedParamMode Mode { get; set; }
        public string SharedParamFilePath { get; set; }
        public List<SharedParamInfo> SelectedParams { get; set; }
        public List<string> SelectedCategoryNames { get; set; }
        public bool IsInstance { get; set; }
        public string ParameterGroupName { get; set; }

        private SharedParamDashboard _dashboard;

        public SharedParamHandler(SharedParamDashboard dashboard)
        {
            _dashboard = dashboard;
        }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;

            try
            {
                switch (Mode)
                {
                    case SharedParamMode.LoadFile:
                        LoadSharedParameterFile(app, doc);
                        break;
                    case SharedParamMode.Apply:
                        ApplyParameters(app, doc);
                        break;
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("B-Lab Error", ex.Message);
            }
        }

        private void LoadSharedParameterFile(UIApplication app, Document doc)
        {
            // Set the shared parameter file
            app.Application.SharedParametersFilename = SharedParamFilePath;
            DefinitionFile defFile = app.Application.OpenSharedParameterFile();

            if (defFile == null)
            {
                TaskDialog.Show("B-Lab", "Could not open the shared parameter file.");
                return;
            }

            // Extract all parameters
            var parameters = new List<SharedParamInfo>();
            foreach (DefinitionGroup group in defFile.Groups)
            {
                foreach (ExternalDefinition def in group.Definitions)
                {
                    parameters.Add(new SharedParamInfo
                    {
                        Name = def.Name,
                        Group = group.Name,
                        DataType = GetParamTypeName(def),
                        Guid = def.GUID.ToString()
                    });
                }
            }

            // Extract model categories
            var categories = new List<string>();
            foreach (Category cat in doc.Settings.Categories)
            {
                if (cat.AllowsBoundParameters && cat.CategoryType == CategoryType.Model)
                {
                    categories.Add(cat.Name);
                }
            }

            // Update UI on dispatcher thread
            _dashboard.Dispatcher.Invoke(() =>
            {
                _dashboard.PopulateParameters(parameters);
                _dashboard.LoadCategories(categories);
            });
        }

        private void ApplyParameters(UIApplication app, Document doc)
        {
            app.Application.SharedParametersFilename = SharedParamFilePath;
            DefinitionFile defFile = app.Application.OpenSharedParameterFile();
            if (defFile == null) return;

            // Build category set
            CategorySet catSet = app.Application.Create.NewCategorySet();
            foreach (string catName in SelectedCategoryNames)
            {
                Category cat = doc.Settings.Categories.Cast<Category>().FirstOrDefault(c => c.Name == catName);
                if (cat != null) catSet.Insert(cat);
            }

            if (catSet.Size == 0)
            {
                TaskDialog.Show("B-Lab", "No valid categories selected.");
                return;
            }

            int applied = 0;
            int skipped = 0;

            using (Transaction trans = new Transaction(doc, "Add Shared Parameters"))
            {
                trans.Start();

                foreach (var paramInfo in SelectedParams)
                {
                    // Find the definition in the file
                    ExternalDefinition extDef = null;
                    foreach (DefinitionGroup group in defFile.Groups)
                    {
                        foreach (ExternalDefinition def in group.Definitions)
                        {
                            if (def.GUID.ToString() == paramInfo.Guid)
                            {
                                extDef = def;
                                break;
                            }
                        }
                        if (extDef != null) break;
                    }

                    if (extDef == null) { skipped++; continue; }

                    // Check if already bound
                    BindingMap bindingMap = doc.ParameterBindings;
                    bool alreadyBound = false;
                    var iter = bindingMap.ForwardIterator();
                    while (iter.MoveNext())
                    {
                        if (iter.Key.Name == extDef.Name)
                        {
                            alreadyBound = true;
                            break;
                        }
                    }

                    if (alreadyBound) { skipped++; continue; }

                    // Create binding
                    Binding binding;
                    if (IsInstance)
                        binding = app.Application.Create.NewInstanceBinding(catSet);
                    else
                        binding = app.Application.Create.NewTypeBinding(catSet);

                    // Get the parameter group
#if NET8_0_OR_GREATER
                    var groupId = GetGroupTypeId(ParameterGroupName);
                    bool success = doc.ParameterBindings.Insert(extDef, binding, groupId);
#else
                    var paramGroup = GetBuiltInParamGroup(ParameterGroupName);
                    bool success = doc.ParameterBindings.Insert(extDef, binding, paramGroup);
#endif

                    if (success) applied++;
                    else skipped++;
                }

                trans.Commit();
            }

            TaskDialog.Show("B-Lab Shared Parameter Manager",
                $"Operation Complete!\n\n" +
                $"  ✅ Applied: {applied} parameters\n" +
                $"  ⏭ Skipped: {skipped} (already exist or failed)\n" +
                $"  📁 Categories: {SelectedCategoryNames.Count}\n" +
                $"  📌 Binding: {(IsInstance ? "Instance" : "Type")}");
        }

        private string GetParamTypeName(ExternalDefinition def)
        {
            try
            {
                var specId = def.GetDataType();
                if (specId != null && !string.IsNullOrEmpty(specId.TypeId))
                    return specId.TypeId.Split('/').LastOrDefault() ?? "Unknown";
            }
            catch { }
            return "Unknown";
        }


#if NET8_0_OR_GREATER
        private ForgeTypeId GetGroupTypeId(string groupName)
        {
            switch (groupName)
            {
                case "Identity Data": return GroupTypeId.IdentityData;
                case "Dimensions": return GroupTypeId.Geometry;
                case "Construction": return GroupTypeId.Construction;
                case "Structural": return GroupTypeId.Structural;
                case "Mechanical": return GroupTypeId.Mechanical;
                case "Electrical": return GroupTypeId.Electrical;
                case "Plumbing": return GroupTypeId.Plumbing;
                case "Energy Analysis": return GroupTypeId.EnergyAnalysis;
                default: return GroupTypeId.General;
            }
        }
#else
        private BuiltInParameterGroup GetBuiltInParamGroup(string groupName)
        {
            switch (groupName)
            {
                case "Identity Data": return BuiltInParameterGroup.PG_IDENTITY_DATA;
                case "Dimensions": return BuiltInParameterGroup.PG_GEOMETRY;
                case "Construction": return BuiltInParameterGroup.PG_CONSTRUCTION;
                case "Structural": return BuiltInParameterGroup.PG_STRUCTURAL;
                case "Mechanical": return BuiltInParameterGroup.PG_MECHANICAL;
                case "Electrical": return BuiltInParameterGroup.PG_ELECTRICAL;
                case "Plumbing": return BuiltInParameterGroup.PG_PLUMBING;
                case "Energy Analysis": return BuiltInParameterGroup.PG_ENERGY_ANALYSIS;
                default: return BuiltInParameterGroup.PG_GENERAL;
            }
        }
#endif

        public string GetName() => "Shared Parameter Manager Handler";
    }
}
