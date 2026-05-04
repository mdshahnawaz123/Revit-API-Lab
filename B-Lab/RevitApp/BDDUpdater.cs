using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace B_Lab.RevitApp
{
    public static class BDDUpdater
    {
        private static readonly string GITHUB_API_URL = SecretService.GetUpdaterApiUrl();
        private static readonly string GITHUB_RELEASES_PAGE = SecretService.GetUpdaterPageUrl();

        public static void CheckForUpdates(UIControlledApplication app)
        {
            Task.Run(async () =>
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        await Task.Delay(3000);
                        
                        var token = SecretService.GetGithubToken();
                        if (!string.IsNullOrEmpty(token))
                        {
                            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        }

                        client.DefaultRequestHeaders.Add("User-Agent", "BDD-Revit-Updater");
                        client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                        
                        string jsonResponse = await client.GetStringAsync(GITHUB_API_URL);
                        JObject releaseInfo = JObject.Parse(jsonResponse);
                        
                        string tagName = releaseInfo["tag_name"]?.ToString();
                        if (string.IsNullOrEmpty(tagName)) return;

                        if (tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                        {
                            tagName = tagName.Substring(1);
                        }

                        if (Version.TryParse(tagName, out Version latestVersion))
                        {
                            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                            if (latestVersion > currentVersion)
                            {
                                string downloadUrl = null;
                                string assetName = null;
                                var assets = releaseInfo["assets"] as JArray;
                                if (assets != null)
                                {
                                    foreach (var asset in assets)
                                    {
                                        string name = asset["name"]?.ToString();
                                        if (name != null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                        {
                                            downloadUrl = asset["browser_download_url"]?.ToString();
                                            assetName = name;
                                            break;
                                        }
                                    }
                                }

                                ScheduleUpdatePrompt(app, currentVersion, latestVersion, downloadUrl, assetName);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Fail silently
                }
            });
        }

        private static void ScheduleUpdatePrompt(UIControlledApplication app, Version current, Version latest, string downloadUrl, string assetName)
        {
            EventHandler<Autodesk.Revit.UI.Events.IdlingEventArgs> handler = null;
            handler = (sender, e) =>
            {
                UIApplication uiApp = sender as UIApplication;
                uiApp.Idling -= handler;

                TaskDialog dialog = new TaskDialog("BIM Digital Design Update");
                dialog.MainInstruction = "A new version of BIM Digital Design Tools is available!";
                
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    dialog.MainContent = $"Current version: {current}\nLatest version: {latest}\n\nWould you like to automatically download and install the update now?\n\nNOTE: The download will happen in the background. Once the installer pops up, please completely close Revit before clicking 'Install'.";
                    dialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                    dialog.DefaultButton = TaskDialogResult.Yes;

                    if (dialog.Show() == TaskDialogResult.Yes)
                    {
                        DownloadAndRunInstaller(downloadUrl, assetName);
                    }
                }
                else
                {
                    dialog.MainContent = $"Current version: {current}\nLatest version: {latest}\n\nNo installer (.exe) was found attached to this release on GitHub. Would you like to go to the releases page to check for manual downloads?";
                    dialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                    dialog.DefaultButton = TaskDialogResult.Yes;

                    if (dialog.Show() == TaskDialogResult.Yes)
                    {
                        Process.Start(GITHUB_RELEASES_PAGE);
                    }
                }
            };

            app.Idling += handler;
        }

        private static void DownloadAndRunInstaller(string downloadUrl, string assetName)
        {
            Task.Run(async () =>
            {
                try
                {
                    string tempPath = Path.Combine(Path.GetTempPath(), "BDDUpdater");
                    if (!Directory.Exists(tempPath)) Directory.CreateDirectory(tempPath);
                    
                    string exePath = Path.Combine(tempPath, assetName);
                    
                    if (File.Exists(exePath)) File.Delete(exePath);

                    using (HttpClient client = new HttpClient())
                    {
                        var token = SecretService.GetGithubToken();
                        if (!string.IsNullOrEmpty(token))
                        {
                            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        }

                        client.DefaultRequestHeaders.Add("User-Agent", "BDD-Revit-Updater");
                        byte[] fileBytes = await client.GetByteArrayAsync(downloadUrl);
                        File.WriteAllBytes(exePath, fileBytes);
                    }

                    Process.Start(exePath);
                }
                catch (Exception)
                {
                    // If background download fails, at least open the release page
                    Process.Start(GITHUB_RELEASES_PAGE);
                }
            });
        }
    }
}
