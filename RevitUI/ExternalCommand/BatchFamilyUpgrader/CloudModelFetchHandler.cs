using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitUI.ExternalCommand.BatchFamilyUpgrader
{
    /// <summary>
    /// Lightweight data class holding cloud model info discovered from the Revit session.
    /// </summary>
    public class CloudModelInfo
    {
        public string ModelName { get; set; } = "";
        public string ProjectGuid { get; set; } = "";
        public string ModelGuid { get; set; } = "";
        public string Region { get; set; } = "US";
        public bool IsCurrentlyOpen { get; set; }
        public bool IsSelected { get; set; }

        public string DisplayName => string.IsNullOrEmpty(ModelName) ? ModelGuid : ModelName;
    }

    /// <summary>
    /// External event handler that fetches cloud models from the current Revit session.
    /// Scans all open documents for cloud-based models and returns their info.
    /// </summary>
    public class CloudModelFetchHandler : IExternalEventHandler
    {
        /// <summary>
        /// Callback invoked on the UI thread with the discovered cloud models and login info.
        /// </summary>
        public Action<string, List<CloudModelInfo>> OnCloudModelsFetched { get; set; }

        public void Execute(UIApplication app)
        {
            var cloudModels = new List<CloudModelInfo>();
            string loginUser = "";

            try
            {
                // Check login status — LoginUserId is available in Revit 2019+
                try
                {
                    loginUser = app.Application.LoginUserId;
                }
                catch
                {
                    loginUser = "";
                }

                // Scan all open documents for cloud models
                foreach (Document doc in app.Application.Documents)
                {
                    try
                    {
                        if (doc.IsModelInCloud)
                        {
                            ModelPath cloudPath = doc.GetCloudModelPath();
                            if (cloudPath != null)
                            {
                                // Extract GUIDs from the cloud path
                                string projectGuid = "";
                                string modelGuid = "";
                                string region = "US";

                                try
                                {
                                    // Revit 2023+ exposes GetProjectGUID / GetModelGUID on cloud paths
                                    var pGuid = cloudPath.GetType().GetMethod("GetProjectGUID")?.Invoke(cloudPath, null);
                                    var mGuid = cloudPath.GetType().GetMethod("GetModelGUID")?.Invoke(cloudPath, null);
                                    var regionProp = cloudPath.GetType().GetProperty("Region")?.GetValue(cloudPath);
                                    if (pGuid is Guid pg) projectGuid = pg.ToString();
                                    if (mGuid is Guid mg) modelGuid = mg.ToString();
                                    if (regionProp is string reg && !string.IsNullOrEmpty(reg)) region = reg;
                                }
                                catch { /* Reflection fallback failed, try CentralServerPath */ }

                                // Fallback: parse from CentralServerPath if reflection didn't work
                                if (string.IsNullOrEmpty(projectGuid))
                                {
                                    try
                                    {
                                        string serverPath = cloudPath.CentralServerPath;
                                        if (!string.IsNullOrEmpty(serverPath))
                                        {
                                            // Cloud paths often contain GUIDs in segments
                                            var parts = serverPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                                            foreach (var part in parts)
                                            {
                                                if (Guid.TryParse(part, out Guid parsed))
                                                {
                                                    if (string.IsNullOrEmpty(projectGuid))
                                                        projectGuid = parsed.ToString();
                                                    else if (string.IsNullOrEmpty(modelGuid))
                                                        modelGuid = parsed.ToString();
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }

                                // Skip if we couldn't get both GUIDs
                                if (string.IsNullOrEmpty(projectGuid) || string.IsNullOrEmpty(modelGuid))
                                    continue;

                                // Avoid duplicates
                                if (cloudModels.Any(c => c.ModelGuid == modelGuid))
                                    continue;

                                cloudModels.Add(new CloudModelInfo
                                {
                                    ModelName = doc.Title ?? "Untitled",
                                    ProjectGuid = projectGuid,
                                    ModelGuid = modelGuid,
                                    Region = region,
                                    IsCurrentlyOpen = true,
                                    IsSelected = false
                                });
                            }
                        }
                    }
                    catch { /* Skip documents that throw */ }
                }
            }
            catch { /* Swallow top-level errors */ }

            // Invoke callback on UI thread
            OnCloudModelsFetched?.Invoke(loginUser, cloudModels);
        }

        public string GetName() => "Cloud Model Fetch Handler";
    }
}
