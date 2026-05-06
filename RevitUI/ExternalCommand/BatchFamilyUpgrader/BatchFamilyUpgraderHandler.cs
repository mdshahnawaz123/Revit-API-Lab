using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitUI.UI.BatchFamilyUpgrader;

namespace RevitUI.ExternalCommand.BatchFamilyUpgrader
{
    /// <summary>
    /// Implements IFamilyLoadOptions to control behavior when reloading families.
    /// </summary>
    public class FamilyLoadOptionsHandler : IFamilyLoadOptions
    {
        private readonly bool _overwriteParameterValues;

        public FamilyLoadOptionsHandler(bool overwriteParameterValues)
        {
            _overwriteParameterValues = overwriteParameterValues;
        }

        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = _overwriteParameterValues;
            return true; // Always load/overwrite the family
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse,
            out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = _overwriteParameterValues;
            return true; // Always load/overwrite shared families
        }
    }

    public class BatchFamilyUpgraderHandler : IExternalEventHandler
    {
        public BatchFamilyUpgraderWindow Dashboard { get; set; }

        /// <summary>
        /// Legacy single-folder path (kept for backward compatibility).
        /// </summary>
        public string FolderPath { get; set; }

        /// <summary>
        /// Multi-model source list provided by the upgraded UI.
        /// </summary>
        public List<ModelSourceItem> ModelSources { get; set; }

        /// <summary>
        /// Family .rfa files to reload into target models (matched by name).
        /// </summary>
        public List<FamilyFileItem> FamilyFilesToReload { get; set; }

        /// <summary>
        /// Whether to overwrite parameter values when reloading families.
        /// </summary>
        public bool OverwriteParameterValues { get; set; } = true;

        public void Execute(UIApplication app)
        {
            // If multi-source list is available, use the new path
            if (ModelSources != null && ModelSources.Count > 0)
            {
                ExecuteMultiSource(app);
                return;
            }

            // Legacy single-folder fallback
            if (string.IsNullOrEmpty(FolderPath) || !Directory.Exists(FolderPath)) return;

            string[] familyFiles = Directory.GetFiles(FolderPath, "*.rfa", SearchOption.AllDirectories);
            int successCount = 0;
            int totalCount = familyFiles.Length;

            for (int i = 0; i < totalCount; i++)
            {
                string filePath = familyFiles[i];
                try
                {
                    Dashboard?.Dispatcher.Invoke(() => Dashboard.UpdateProgress(i + 1, totalCount, Path.GetFileName(filePath)));

                    // Open in background
                    Document familyDoc = app.Application.OpenDocumentFile(filePath);

                    SaveAsOptions saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
                    familyDoc.SaveAs(filePath, saveOptions);
                    familyDoc.Close(false);

                    successCount++;
                }
                catch (Exception ex)
                {
                    // Handle file errors quietly to not break the batch process
                    Dashboard?.Dispatcher.Invoke(() => Dashboard.LogMessage($"  ✕ Failed: {Path.GetFileName(filePath)} - {ex.Message}"));
                }
            }

            Dashboard?.Dispatcher.Invoke(() => Dashboard.UpgradeComplete(successCount, totalCount));
        }

        /// <summary>
        /// Processes multiple model sources (both local folders and cloud models),
        /// and optionally reloads family files into each model.
        /// </summary>
        private void ExecuteMultiSource(UIApplication app)
        {
            int totalSuccess = 0;
            int globalCurrent = 0;

            // ── Gather all work items ──
            var allLocalFiles = new List<(int sourceIndex, string filePath)>();
            var cloudSources = new List<(int sourceIndex, ModelSourceItem source)>();
            bool hasFamilyReload = FamilyFilesToReload != null && FamilyFilesToReload.Count > 0;

            for (int s = 0; s < ModelSources.Count; s++)
            {
                var source = ModelSources[s];
                if (source.SourceType.Contains("Local"))
                {
                    if (!string.IsNullOrEmpty(source.Path) && Directory.Exists(source.Path))
                    {
                        string[] files = Directory.GetFiles(source.Path, "*.rfa", SearchOption.AllDirectories);
                        foreach (var f in files)
                            allLocalFiles.Add((s, f));

                        Dashboard?.Dispatcher.Invoke(() =>
                        {
                            source.FamilyCount = files.Length;
                            source.Status = "Queued";
                        });
                    }
                    else
                    {
                        Dashboard?.Dispatcher.Invoke(() =>
                        {
                            source.Status = "Invalid Path";
                            Dashboard.LogMessage($"  ⚠ Skipped invalid path: {source.Path}");
                        });
                    }
                }
                else if (source.SourceType.Contains("Cloud"))
                {
                    cloudSources.Add((s, source));
                    Dashboard?.Dispatcher.Invoke(() => source.Status = "Queued");
                }
            }

            int totalFamilies = allLocalFiles.Count + cloudSources.Count;
            if (totalFamilies == 0 && !hasFamilyReload)
            {
                Dashboard?.Dispatcher.Invoke(() =>
                {
                    Dashboard.LogMessage("No families found to process.");
                    Dashboard.UpgradeComplete(0, 0);
                });
                return;
            }

            Dashboard?.Dispatcher.Invoke(() =>
                Dashboard.LogMessage($"Total items to process: {totalFamilies}" +
                    (hasFamilyReload ? $" + {FamilyFilesToReload.Count} families to reload per model" : "")));

            // ══════════════════════════════════════════════════════
            //  PHASE 1: UPGRADE LOCAL FAMILIES (open → save → close)
            // ══════════════════════════════════════════════════════

            int currentSourceIndex = -1;
            foreach (var (sourceIndex, filePath) in allLocalFiles)
            {
                globalCurrent++;
                if (sourceIndex != currentSourceIndex)
                {
                    currentSourceIndex = sourceIndex;
                    Dashboard?.Dispatcher.Invoke(() =>
                    {
                        Dashboard.UpdateSourceStatus(sourceIndex, "Processing...");
                        Dashboard.LogMessage($"── Processing local folder: {ModelSources[sourceIndex].Path} ──");
                    });
                }

                try
                {
                    string fileName = Path.GetFileName(filePath);
                    Dashboard?.Dispatcher.Invoke(() => Dashboard.UpdateProgress(globalCurrent, totalFamilies, $"Upgrading: {fileName}"));

                    Document familyDoc = app.Application.OpenDocumentFile(filePath);
                    SaveAsOptions saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
                    familyDoc.SaveAs(filePath, saveOptions);
                    familyDoc.Close(false);

                    totalSuccess++;
                }
                catch (Exception ex)
                {
                    Dashboard?.Dispatcher.Invoke(() =>
                        Dashboard.LogMessage($"  ✕ Failed: {Path.GetFileName(filePath)} - {ex.Message}"));
                }
            }

            // Mark completed local sources
            var processedLocalSources = new HashSet<int>();
            foreach (var (sourceIndex, _) in allLocalFiles)
            {
                if (processedLocalSources.Add(sourceIndex))
                {
                    int si = sourceIndex;
                    Dashboard?.Dispatcher.Invoke(() => Dashboard.UpdateSourceStatus(si, "✓ Complete"));
                }
            }

            // ══════════════════════════════════════════════════════
            //  PHASE 2: PROCESS CLOUD MODELS (open → sync → close)
            //           + Family Reload if families are provided
            // ══════════════════════════════════════════════════════

            foreach (var (sourceIndex, source) in cloudSources)
            {
                globalCurrent++;
                try
                {
                    Dashboard?.Dispatcher.Invoke(() =>
                    {
                        Dashboard.UpdateSourceStatus(sourceIndex, "Processing...");
                        Dashboard.UpdateProgress(globalCurrent, totalFamilies, $"Cloud: {source.ModelGuid}");
                        Dashboard.LogMessage($"── Processing cloud model: {source.ModelGuid} ──");
                    });

                    // Open cloud model using Revit API
                    Guid projectGuid = Guid.Parse(source.ProjectGuid);
                    Guid modelGuid = Guid.Parse(source.ModelGuid);

                    ModelPath cloudPath = ModelPathUtils.ConvertCloudGUIDsToCloudPath(
                        "US" /* default region */, projectGuid, modelGuid);

                    OpenOptions openOpts = new OpenOptions();
                    Document cloudDoc = app.Application.OpenDocumentFile(cloudPath, openOpts);

                    if (cloudDoc != null)
                    {
                        // ── Reload families into this cloud model ──
                        if (hasFamilyReload)
                        {
                            ReloadFamiliesIntoDocument(cloudDoc, source.ModelGuid);
                        }

                        // Synchronize with central to save changes
                        TransactWithCentralOptions transOpts = new TransactWithCentralOptions();
                        SynchronizeWithCentralOptions syncOpts = new SynchronizeWithCentralOptions();
                        syncOpts.SetRelinquishOptions(new RelinquishOptions(true));
                        syncOpts.Comment = "Batch Family Upgrader - Version Upgrade";

                        cloudDoc.SynchronizeWithCentral(transOpts, syncOpts);
                        cloudDoc.Close(false);
                        totalSuccess++;

                        Dashboard?.Dispatcher.Invoke(() =>
                        {
                            source.FamilyCount = 1;
                            Dashboard.UpdateSourceStatus(sourceIndex, "✓ Complete");
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dashboard?.Dispatcher.Invoke(() =>
                    {
                        Dashboard.UpdateSourceStatus(sourceIndex, "✕ Failed");
                        Dashboard.LogMessage($"  ✕ Cloud model failed: {ex.Message}");
                    });
                }
            }

            // ══════════════════════════════════════════════════════════════
            //  PHASE 3: RELOAD FAMILIES INTO LOCAL MODELS (.rvt in folders)
            //  If user added family files and local folders contain .rvt files
            // ══════════════════════════════════════════════════════════════

            if (hasFamilyReload)
            {
                // Scan local folders for .rvt model files to reload families into
                var rvtModels = new List<(int sourceIndex, string rvtPath)>();
                for (int s = 0; s < ModelSources.Count; s++)
                {
                    var source = ModelSources[s];
                    if (source.SourceType.Contains("Local") && !string.IsNullOrEmpty(source.Path) && Directory.Exists(source.Path))
                    {
                        string[] rvtFiles = Directory.GetFiles(source.Path, "*.rvt", SearchOption.AllDirectories);
                        foreach (var rvt in rvtFiles)
                            rvtModels.Add((s, rvt));
                    }
                }

                if (rvtModels.Count > 0)
                {
                    Dashboard?.Dispatcher.Invoke(() =>
                        Dashboard.LogMessage($"\n── Family Reload Phase: {rvtModels.Count} .rvt model(s) found ──"));

                    int reloadTotal = rvtModels.Count;
                    int reloadCurrent = 0;

                    foreach (var (sourceIndex, rvtPath) in rvtModels)
                    {
                        reloadCurrent++;
                        string rvtName = Path.GetFileName(rvtPath);

                        try
                        {
                            Dashboard?.Dispatcher.Invoke(() =>
                            {
                                Dashboard.UpdateProgress(reloadCurrent, reloadTotal, $"Reloading into: {rvtName}");
                                Dashboard.LogMessage($"  Opening model: {rvtName}");
                            });

                            Document rvtDoc = app.Application.OpenDocumentFile(rvtPath);

                            if (rvtDoc != null)
                            {
                                ReloadFamiliesIntoDocument(rvtDoc, rvtName);

                                // Save the model
                                rvtDoc.Save();
                                rvtDoc.Close(false);
                                totalSuccess++;

                                Dashboard?.Dispatcher.Invoke(() =>
                                    Dashboard.LogMessage($"  ✓ Saved and closed: {rvtName}"));
                            }
                        }
                        catch (Exception ex)
                        {
                            Dashboard?.Dispatcher.Invoke(() =>
                                Dashboard.LogMessage($"  ✕ Failed to process {rvtName}: {ex.Message}"));
                        }
                    }
                }
            }

            Dashboard?.Dispatcher.Invoke(() => Dashboard.UpgradeComplete(totalSuccess, totalFamilies));
        }

        /// <summary>
        /// Reloads all matching family files into the given document.
        /// Matches are found by comparing the family name (without .rfa extension)
        /// to the loaded families in the document.
        /// </summary>
        private void ReloadFamiliesIntoDocument(Document doc, string modelIdentifier)
        {
            if (FamilyFilesToReload == null || FamilyFilesToReload.Count == 0) return;

            Dashboard?.Dispatcher.Invoke(() =>
                Dashboard.LogMessage($"    🔄 Reloading families into: {modelIdentifier}"));

            // Get all loaded families in the document
            var loadedFamilies = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToList();

            // Build a lookup by family name (case-insensitive)
            var familyLookup = new Dictionary<string, Family>(StringComparer.OrdinalIgnoreCase);
            foreach (var fam in loadedFamilies)
            {
                if (!string.IsNullOrEmpty(fam.Name) && !familyLookup.ContainsKey(fam.Name))
                {
                    familyLookup[fam.Name] = fam;
                }
            }

            var loadOptions = new FamilyLoadOptionsHandler(OverwriteParameterValues);
            int reloadedCount = 0;
            int skippedCount = 0;

            using (Transaction tx = new Transaction(doc, "Batch Family Reload"))
            {
                tx.Start();

                foreach (var familyFile in FamilyFilesToReload)
                {
                    string familyName = familyFile.FamilyName;

                    if (familyLookup.ContainsKey(familyName))
                    {
                        try
                        {
                            // Found a matching family in the model — reload it
                            bool loaded = doc.LoadFamily(familyFile.FilePath, loadOptions, out Family loadedFamily);

                            if (loaded)
                            {
                                reloadedCount++;
                                Dashboard?.Dispatcher.Invoke(() =>
                                    Dashboard.LogMessage($"      ✓ Reloaded: {familyName}"));
                            }
                            else
                            {
                                Dashboard?.Dispatcher.Invoke(() =>
                                    Dashboard.LogMessage($"      ⚠ Already up-to-date: {familyName}"));
                            }
                        }
                        catch (Exception ex)
                        {
                            Dashboard?.Dispatcher.Invoke(() =>
                                Dashboard.LogMessage($"      ✕ Failed to reload {familyName}: {ex.Message}"));
                        }
                    }
                    else
                    {
                        skippedCount++;
                        Dashboard?.Dispatcher.Invoke(() =>
                            Dashboard.LogMessage($"      ⊘ Not found in model: {familyName}"));
                    }
                }

                tx.Commit();
            }

            Dashboard?.Dispatcher.Invoke(() =>
                Dashboard.LogMessage($"    Summary: {reloadedCount} reloaded, {skippedCount} not found in {modelIdentifier}"));
        }

        public string GetName() => "Batch Family Upgrader Handler";
    }
}
