using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLab.LicFolder
{
    public static class TrialService
    {
        private const string RegKey = @"SOFTWARE\ABS_WIZZ\License";
        private const string RegValue = "TrialStart";
        public const int TrialDays = 30;

        /// <summary>
        /// Permanently records the trial start date for this user+machine.
        /// Safe to call multiple times — will NEVER overwrite an existing date.
        /// </summary>
        public static void RecordTrialStartIfNew(string username)
        {
            Logger.Log($"TrialService: Checking trial start for user '{username}'...");
            try
            {
                using (var key = GetOrCreateKey(username))
                {
                    if (key == null) return;
                    if (key.GetValue(RegValue) == null)
                    {
                        var start = DateTime.UtcNow;
                        key.SetValue(RegValue, start.ToString("o"), RegistryValueKind.String);
                        Logger.Log($"TrialService: NEW TRIAL RECORDED for '{username}' starting {start}.");
                    }
                }
            }
            catch (Exception ex) { Logger.LogError(ex, "TrialService: RecordTrialStartIfNew failed"); }
        }

        public static TrialStatus GetTrialStatus(string username)
        {
            try
            {
                using (var key = GetOrCreateKey(username))
                {
                    if (key == null)
                    {
                        Logger.Log("TrialService: Registry access failed.", "ERROR");
                        return TrialStatus.Expired("Unable to read trial data.");
                    }

                    // OFFLINE TIME-TRAVEL TRAP
                    bool isTampered = (key.GetValue("Tampered") as string) == "true";
                    if (isTampered)
                    {
                        Logger.Log($"TrialService: TAMPERING DETECTED for '{username}'. Permanent lockout.", "CRITICAL");
                        return TrialStatus.Expired("System clock tampering detected. License locked.");
                    }

                    string lastRunStr = key.GetValue("LastRunTime") as string;
                    if (lastRunStr != null && DateTime.TryParse(lastRunStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastRun))
                    {
                        if (DateTime.UtcNow < lastRun.AddMinutes(-5))
                        {
                            key.SetValue("Tampered", "true", RegistryValueKind.String);
                            Logger.Log($"TrialService: Clock rollback detected for '{username}'! Last: {lastRun}, Current: {DateTime.UtcNow}. LOCKING.", "CRITICAL");
                            return TrialStatus.Expired("System clock tampering detected. License permanently locked.");
                        }
                    }
                    
                    key.SetValue("LastRunTime", DateTime.UtcNow.ToString("o"), RegistryValueKind.String);

                    var raw = key.GetValue(RegValue) as string;
                    if (raw == null)
                    {
                        Logger.Log($"TrialService: No trial found for '{username}'.");
                        return TrialStatus.NotStarted();
                    }

                    if (!DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var startDate))
                        return TrialStatus.Expired("Trial data corrupted.");

                    var expiry = startDate.AddDays(TrialDays);
                    var remaining = (int)(expiry - DateTime.UtcNow).TotalDays;

                    if (DateTime.UtcNow > expiry)
                    {
                        Logger.Log($"TrialService: Trial for '{username}' expired on {expiry}.");
                        return TrialStatus.Expired($"Trial ended on {expiry.ToLocalTime():dd MMM yyyy}.");
                    }

                    Logger.Log($"TrialService: Trial for '{username}' is active. Days left: {remaining}");
                    return TrialStatus.Active(expiry, remaining);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "TrialService: GetTrialStatus failed");
                return TrialStatus.Expired("Trial validation error.");
            }
        }

        // Registry key path: HKCU\SOFTWARE\ABS_WIZZ\License\<MachineGuid>\<username>
        private static RegistryKey GetOrCreateKey(string username)
        {
            var machineId = MachineHelper.GetMachineId() ?? "UNKNOWN";
            var path = $@"{RegKey}\{machineId}\{username.ToLowerInvariant()}";
            return Registry.CurrentUser.CreateSubKey(path, writable: true);
        }
    }

    public class TrialStatus
    {
        public bool IsActive { get; private set; }
        public bool HasStarted { get; private set; }
        public DateTime? ExpiryDate { get; private set; }
        public int DaysRemaining { get; private set; }
        public string Message { get; private set; }

        public static TrialStatus Active(DateTime expiry, int days) =>
            new TrialStatus
            {
                IsActive = true,
                HasStarted = true,
                ExpiryDate = expiry,
                DaysRemaining = days
            };

        public static TrialStatus NotStarted() =>
            new TrialStatus { IsActive = false, HasStarted = false, Message = "No trial recorded." };

        public static TrialStatus Expired(string msg) =>
            new TrialStatus
            {
                IsActive = false,
                HasStarted = true,
                Message = msg,
                DaysRemaining = 0
            };
    }
}
