using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DataLab.LicFolder
{
    public static class LicenseManager
    {
        private static readonly string URL = SecretService.GetLicenseUrl();

        // Hidden backup file anchor (secondary trial record)
        private static string GetBackupTrialPath(string username)
        {
            var machineId = MachineHelper.GetMachineId() ?? "UNKNOWN";
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                ".abswizz",
                machineId,
                username.ToLowerInvariant() + ".lic"
            );
        }

        private static void WriteBackupTrialAnchor(string username)
        {
            try
            {
                var path = GetBackupTrialPath(username);
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (!File.Exists(path))
                    File.WriteAllText(path, DateTime.UtcNow.ToString("o"));

                // Make it hidden so casual users don't notice it
                File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.System);
            }
            catch { /* silent fail — registry is primary */ }
        }

        private static DateTime? ReadBackupTrialAnchor(string username)
        {
            try
            {
                var path = GetBackupTrialPath(username);
                if (!File.Exists(path)) return null;
                var raw = File.ReadAllText(path).Trim();
                if (DateTime.TryParse(raw, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    return dt;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Returns the earliest trial start date across Registry + backup file.
        /// Whichever is oldest wins — prevents reset by deleting one anchor.
        /// </summary>
        private static DateTime? GetEarliestTrialStart(string username)
        {
            var regStatus = TrialService.GetTrialStatus(username);
            DateTime? regDate = (regStatus.HasStarted && regStatus.ExpiryDate.HasValue)
                ? regStatus.ExpiryDate.Value.AddDays(-TrialService.TrialDays)
                : (DateTime?)null;

            var backupDate = ReadBackupTrialAnchor(username);

            if (regDate == null && backupDate == null) return null;
            if (regDate == null) return backupDate;
            if (backupDate == null) return regDate;

            // Return the EARLIER of the two — most conservative
            return regDate < backupDate ? regDate : backupDate;
        }

        /// <summary>
        /// Core trial validity check using both anchors.
        /// Returns null if trial is valid, or an error message if expired/invalid.
        /// </summary>
        private static string CheckTrialValidity(string username)
        {
            var earliest = GetEarliestTrialStart(username);

            if (earliest == null)
                return null; // No trial recorded yet — first time user

            var expiry = earliest.Value.AddDays(TrialService.TrialDays);

            if (DateTime.UtcNow > expiry)
                return $"Your 30-day trial expired on {expiry.ToLocalTime():dd MMM yyyy}. Please purchase a license.";

            return null; // Still within trial window
        }

        /// <summary>
        /// Called on application startup. Attempts to auto-login from saved token.
        /// </summary>
        public static async Task<LoginResult> TryAutoLoginAsync()
        {
            var token = TokenService.LoadToken();
            if (token == null)
                return LoginResult.Fail("Login required.");

            bool isAdmin = string.Equals(token.Plan, "admin", StringComparison.OrdinalIgnoreCase);

            // 1. Machine binding check (Skip for admin)
            if (!isAdmin && token.MachineId != MachineHelper.GetMachineId())
                return LoginResult.Fail("This license is bound to a different machine.");

            // 2. Trial expiry check (uses both Registry + backup file) (Skip for admin)
            if (!isAdmin)
            {
                var trialError = CheckTrialValidity(token.Username);
                if (trialError != null)
                {
                    TokenService.DeleteToken();
                    return LoginResult.Fail(trialError);
                }
            }

            // 3. Token expiry check
            if (!isAdmin && token.ExpiresUtc < DateTime.UtcNow)
            {
                TokenService.DeleteToken();
                return LoginResult.Fail("License expired.");
            }

            // 4. Online validation (fails gracefully — allows offline use)
            try
            {
                var auth = new AuthService(URL);
                var loaded = await auth.LoadUsersAsync();
                if (loaded)
                {
                    var user = auth.GetUser(token.Username);
                    if (user == null || !user.Active)
                    {
                        TokenService.DeleteToken();
                        return LoginResult.Fail("License has been revoked. Please contact support.");
                    }

                    // Update admin status if changed on server
                    isAdmin = string.Equals(user.Plan, "admin", StringComparison.OrdinalIgnoreCase);

                    // Re-check trial against server expiry too (using Server True Time to prevent clock skewing)
                    DateTime trueTime = auth.ServerTimeUtc ?? DateTime.UtcNow;
                    if (!isAdmin && user.Expires.ToUniversalTime() < trueTime)
                    {
                        TokenService.DeleteToken();
                        return LoginResult.Fail("Server-side license expired.");
                    }
                }
            }
            catch
            {
                // Network unavailable — allow offline use with existing token
            }

            return LoginResult.Ok(token.Username, token.Plan, token.ExpiresUtc);
        }

        /// <summary>
        /// Called from the login form. Validates credentials and saves token.
        /// </summary>
        public static async Task<bool> LoginAsync(string username, string password, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                onError?.Invoke("Username and password are required.");
                return false;
            }

            // 1. Validate credentials against remote JSON first to check plan
            var auth = new AuthService(URL);
            var credentialsOk = await auth.ValidateCredentialsAsync(username, password, onError);
            if (!credentialsOk)
                return false;

            bool isAdmin = string.Equals(auth.CurrentUser.Plan, "admin", StringComparison.OrdinalIgnoreCase);

            // 2. Trial expiry check BEFORE hitting the server
            //    Only applies if a trial record already exists for this user+machine
            if (!isAdmin)
            {
                var trialError = CheckTrialValidity(username);
                if (trialError != null)
                {
                    onError?.Invoke(trialError);
                    return false;
                }
            }

            // 3. Record trial start permanently on this machine (first login only)
            //    Both Registry and hidden backup file are written here
            if (!isAdmin)
            {
                TrialService.RecordTrialStartIfNew(username);
                WriteBackupTrialAnchor(username);
            }

            // 4. Calculate effective expiry: trial cap vs server expiry — use the earlier one
            DateTime effectiveExpiry;
            if (isAdmin)
            {
                effectiveExpiry = DateTime.MaxValue; // Lifetime for admins
            }
            else
            {
                var trialStatus = TrialService.GetTrialStatus(username);
                var earliest = GetEarliestTrialStart(username);

                // Adjust the expiry check using the True Network Time if available
                DateTime trueTime = auth.ServerTimeUtc ?? DateTime.UtcNow;

                if (earliest.HasValue)
                {
                    var trialExpiry = earliest.Value.AddDays(TrialService.TrialDays);
                    var serverExpiry = auth.CurrentUser.Expires.ToUniversalTime();
                    effectiveExpiry = serverExpiry < trialExpiry ? serverExpiry : trialExpiry;
                    
                    if (effectiveExpiry < trueTime)
                    {
                        onError?.Invoke("Your trial or license has expired.");
                        return false;
                    }
                }
                else
                {
                    effectiveExpiry = auth.CurrentUser.Expires.ToUniversalTime();
                    if (effectiveExpiry < trueTime)
                    {
                        onError?.Invoke("Your server-side license has expired.");
                        return false;
                    }
                }
            }

            // 5. Save token locally (encrypted via DPAPI)
            var token = new LocalAuthToken
            {
                Username = auth.CurrentUser.Username,
                MachineId = MachineHelper.GetMachineId(),
                Plan = auth.CurrentUser.Plan,
                ExpiresUtc = effectiveExpiry
            };

            TokenService.SaveToken(token);

            return true;
        }

        /// <summary>
        /// Returns how many trial days remain for a given username on this machine.
        /// Returns -1 if trial has not started yet, 0 if expired.
        /// </summary>
        public static int GetTrialDaysRemaining(string username)
        {
            var earliest = GetEarliestTrialStart(username);
            if (earliest == null) return -1; // not started

            var expiry = earliest.Value.AddDays(TrialService.TrialDays);
            var remaining = (int)(expiry - DateTime.UtcNow).TotalDays;
            return remaining < 0 ? 0 : remaining;
        }
    }
}
