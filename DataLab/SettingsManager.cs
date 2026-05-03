using System;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace DataLab
{
    public class PluginSettings
    {
        public string CompanyName { get; set; } = "BIM Digital Design";
        public List<string> SlopeColors { get; set; } = new List<string> { "#10B981", "#F59E0B", "#EF4444" };
        public List<double> SlopeThresholds { get; set; } = new List<double> { 5.0, 10.0, 15.0 };
        public bool IsFirstRun { get; set; } = true;
        public string LastExportPath { get; set; } = "";
    }

    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "B-Lab",
            "settings.json"
        );

        public static PluginSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonConvert.DeserializeObject<PluginSettings>(json) ?? new PluginSettings();
                }
            }
            catch { }
            return new PluginSettings();
        }

        public static void Save(PluginSettings settings)
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}
