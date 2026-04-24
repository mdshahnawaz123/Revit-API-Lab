using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace B_Lab.RevitApp
{
    public static class BDDUpdater
    {
        // Reference to your new public GitHub repository for releases
        // Replace 'BDD-Releases' with whatever you name your new repository!
        private const string GITHUB_VERSION_URL = "https://raw.githubusercontent.com/mdshahnawaz123/BDD-Releases/main/version.txt";
        
        // Where the user should be taken to download the new setup file
        private const string DOWNLOAD_URL = "https://github.com/mdshahnawaz123/BDD-Releases/releases/latest";

        /// <summary>
        /// Checks GitHub for a newer version asynchronously and prompts the user if one is found.
        /// </summary>
        public static void CheckForUpdates(UIControlledApplication app)
        {
            Task.Run(async () =>
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        // Add a small delay so we don't bog down Revit during its initial startup
                        await Task.Delay(3000);

                        // Disable caching to ensure we get the fresh version file
                        client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                        
                        string latestVersionStr = await client.GetStringAsync(GITHUB_VERSION_URL);
                        latestVersionStr = latestVersionStr.Trim();

                        if (Version.TryParse(latestVersionStr, out Version latestVersion))
                        {
                            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                            if (latestVersion > currentVersion)
                            {
                                // Update found! Schedule a UI notification on the main thread when Revit is idle.
                                EventHandler<Autodesk.Revit.UI.Events.IdlingEventArgs> handler = null;
                                handler = (sender, e) =>
                                {
                                    UIApplication uiApp = sender as UIApplication;
                                    uiApp.Idling -= handler; // Unsubscribe so it only fires once

                                    TaskDialog dialog = new TaskDialog("BIM Digital Design Update");
                                    dialog.MainInstruction = "A new version of BIM Digital Design Tools is available!";
                                    dialog.MainContent = $"Current version: {currentVersion}\nLatest version: {latestVersion}\n\nWould you like to download the update now?";
                                    dialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                                    dialog.DefaultButton = TaskDialogResult.Yes;
                                    
                                    if (dialog.Show() == TaskDialogResult.Yes)
                                    {
                                        System.Diagnostics.Process.Start(DOWNLOAD_URL);
                                    }
                                };

                                app.Idling += handler;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Fail silently if offline or repo is unavailable
                }
            });
        }
    }
}
