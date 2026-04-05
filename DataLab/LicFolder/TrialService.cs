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
            try
            {
                using (var key = GetOrCreateKey(username))
                {
                    if (key == null) return;
                    // Only write if not already recorded — this is the critical guard
                    if (key.GetValue(RegValue) == null)
                        key.SetValue(RegValue, DateTime.UtcNow.ToString("o"), RegistryValueKind.String);
                }
            }
            catch { }
        }

        /// <summary>
        /// Returns the current trial status for this user on this machine.
        /// </summary>
        public static TrialStatus GetTrialStatus(string username)
        {
            try
            {
                using (var key = GetOrCreateKey(username))
                {
                    if (key == null)
                        return TrialStatus.Expired("Unable to read trial data.");

                    var raw = key.GetValue(RegValue) as string;
                    if (raw == null)
                        return TrialStatus.NotStarted();

                    if (!DateTime.TryParse(raw, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var startDate))
                        return TrialStatus.Expired("Trial data corrupted.");

                    var expiry = startDate.AddDays(TrialDays);
                    var remaining = (int)(expiry - DateTime.UtcNow).TotalDays;

                    if (DateTime.UtcNow > expiry)
                        return TrialStatus.Expired($"Trial ended on {expiry.ToLocalTime():dd MMM yyyy}.");

                    return TrialStatus.Active(expiry, remaining);
                }
            }
            catch
            {
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
