using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RevitUI.UI.SharedParam
{
    public enum SharedParamMode { LoadFile, Apply, FetchExisting, CreateNew }

    public class SharedParamHandler : IExternalEventHandler
    {
        public SharedParamMode Mode { get; set; }
        public string SharedParamFilePath { get; set; }
        public List<SharedParamInfo> SelectedParams { get; set; }
        public List<string> SelectedCategoryNames { get; set; }
        public bool IsInstance { get; set; }
        public string ParameterGroupName { get; set; }

        public List<string> NewParamNames { get; set; } = new List<string>();
        public string NewParamTypeStr { get; set; }
        public bool IsNewParamShared { get; set; }

        private SharedParamDashboard _dashboard;

        public SharedParamHandler(SharedParamDashboard dashboard)
        {
            _dashboard = dashboard;
        }

        public void SetDashboard(SharedParamDashboard dashboard)
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
                    case SharedParamMode.FetchExisting:
                        FetchExistingParameters(app, doc);
                        break;
                    case SharedParamMode.LoadFile:
                        LoadSharedParameterFile(app, doc);
                        break;
                    case SharedParamMode.CreateNew:
                        CreateNewParameter(app, doc);
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
            finally
            {
                Mode = SharedParamMode.FetchExisting; // Reset mode
            }
        }

        private void CreateNewParameter(UIApplication app, Document doc)
        {
            if (string.IsNullOrEmpty(SharedParamFilePath))
            {
                TaskDialog.Show("Error", "Please load or select a shared parameter file first.");
                return;
            }

            app.Application.SharedParametersFilename = SharedParamFilePath;
            DefinitionFile defFile = app.Application.OpenSharedParameterFile();

            if (defFile == null)
            {
                TaskDialog.Show("Error", "Shared parameter file not found.");
                return;
            }

            // Get or create the group
            string groupName = "B-Lab Created";
            DefinitionGroup group = defFile.Groups.get_Item(groupName) ?? defFile.Groups.Create(groupName);

            int createdCount = 0;
            int skippedCount = 0;
            var specId = GetSpecTypeId(NewParamTypeStr);

            foreach (string name in NewParamNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (group.Definitions.get_Item(name) != null)
                {
                    skippedCount++;
                    continue;
                }

                ExternalDefinitionCreationOptions options = new ExternalDefinitionCreationOptions(name, specId);
                Definition def = group.Definitions.Create(options);

                if (def != null) createdCount++;
                else skippedCount++;
            }

            // Refresh the list
            LoadSharedParameterFile(app, doc);
            TaskDialog.Show("Batch Creation Complete", 
                $"✅ Successfully created: {createdCount}\n" +
                $"⏭ Skipped (duplicate or error): {skippedCount}");
        }

        private ForgeTypeId GetSpecTypeId(string typeStr)
        {
            switch (typeStr)
            {
                case "Text": return SpecTypeId.String.Text;
                case "Multiline Text": return SpecTypeId.String.MultilineText;
                case "Integer": return SpecTypeId.Int.Integer;
                case "Number": return SpecTypeId.Number;
                case "Length": return SpecTypeId.Length;
                case "Area": return SpecTypeId.Area;
                case "Volume": return SpecTypeId.Volume;
                case "Angle": return SpecTypeId.Angle;
                case "Slope": return SpecTypeId.Slope;
                case "Currency": return SpecTypeId.Currency;
                case "URL": return SpecTypeId.String.Url;
                case "Material": return SpecTypeId.Reference.Material;
                case "Fill Pattern": return SpecTypeId.Reference.FillPattern;
                case "Image": return SpecTypeId.Reference.Image;
                case "YesNo": return SpecTypeId.Boolean.YesNo;
                default: return SpecTypeId.String.Text;
            }
        }

        private void FetchExistingParameters(UIApplication app, Document doc)
        {
            var parameters = new List<SharedParamInfo>();
            
            // Get all project parameters that are shared
            BindingMap bindingMap = doc.ParameterBindings;
            DefinitionBindingMapIterator iter = bindingMap.ForwardIterator();
            while (iter.MoveNext())
            {
                if (iter.Key is ExternalDefinition extDef)
                {
                    parameters.Add(new SharedParamInfo
                    {
                        Name = extDef.Name,
                        Group = "Project Shared",
                        DataType = GetParamTypeName(extDef),
                        Guid = extDef.GUID.ToString()
                    });
                }
                else if (iter.Key is InternalDefinition intDef)
                {
                    parameters.Add(new SharedParamInfo
                    {
                        Name = intDef.Name,
                        Group = "Project Non-Shared",
                        DataType = intDef.GetDataType().TypeId.Split('/').LastOrDefault() ?? "Unknown",
                        Guid = "N/A"
                    });
                }
            }

            // Also get model categories
            var categories = new List<string>();
            foreach (Category cat in doc.Settings.Categories)
            {
                if (cat.AllowsBoundParameters && cat.CategoryType == CategoryType.Model)
                {
                    categories.Add(cat.Name);
                }
            }

            // Auto-load the active shared parameter file path if it exists
            string activeFilePath = app.Application.SharedParametersFilename;

            _dashboard.Dispatcher.Invoke(() =>
            {
                _dashboard.PopulateParameters(parameters);
                _dashboard.LoadCategories(categories);

                if (!string.IsNullOrEmpty(activeFilePath) && System.IO.File.Exists(activeFilePath))
                {
                    SharedParamFilePath = activeFilePath;
                    _dashboard.CmbFileHistory.Text = activeFilePath;
                    // Pre-fill the group and data type combo boxes or just refresh the list
                    LoadSharedParameterFile(app, doc);
                }

                _dashboard.TxtStatus.Text = "Parameters loaded from current project and active shared file.";
            });
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
                _dashboard.PopulateParameters(parameters, false);
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

                    // Get the parameter group using ForgeTypeId (Revit 2022+)
                    var groupId = GetGroupTypeId(ParameterGroupName);
                    bool success = doc.ParameterBindings.Insert(extDef, binding, groupId);

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
                if (specId != null)
                {
                    try { return LabelUtils.GetLabelForSpec(specId); }
                    catch { return specId.TypeId.Split(':').LastOrDefault() ?? "Unknown"; }
                }
            }
            catch { }
            return "Unknown";
        }

        private ForgeTypeId GetGroupTypeId(string groupName)
        {
            switch (groupName)
            {
                case "General": return GroupTypeId.General;
                case "Identity Data": return GroupTypeId.IdentityData;
                case "Dimensions": return GroupTypeId.Geometry;
                case "Construction": return GroupTypeId.Construction;
                case "Graphics": return GroupTypeId.Graphics;
                case "Phasing": return GroupTypeId.Phasing;
                case "Mechanical": return GroupTypeId.Mechanical;
                case "Mechanical - Flow": return GroupTypeId.Mechanical; // Fallback for compatibility
                case "Mechanical - Loads": return GroupTypeId.Mechanical; // Fallback for compatibility
                case "Electrical": return GroupTypeId.Electrical;
                case "Electrical - Lighting": return GroupTypeId.Electrical; // Fallback for compatibility
                case "Electrical - Loads": return GroupTypeId.Electrical; // Fallback for compatibility
                case "Plumbing": return GroupTypeId.Plumbing;
                case "Structural": return GroupTypeId.Structural;
                case "Structural Analysis": return GroupTypeId.StructuralAnalysis;
                case "Energy Analysis": return GroupTypeId.EnergyAnalysis;
                case "Fire Protection": return GroupTypeId.FireProtection;
                case "Materials and Finishes": return GroupTypeId.Materials;
                case "IFC Parameters": return GroupTypeId.Ifc;
                case "Other": return GroupTypeId.General;
                default: return GroupTypeId.General;
            }
        }

        public string GetName() => "Shared Parameter Manager Handler";
    }
}
