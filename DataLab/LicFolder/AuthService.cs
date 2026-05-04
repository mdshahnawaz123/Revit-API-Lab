using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DataLab.LicFolder
{
    public class AuthService
    {
        private readonly string _source;
        private List<UserRecord> _users = new List<UserRecord>();

        public UserRecord CurrentUser { get; private set; }
        public DateTime? ServerTimeUtc { get; private set; }

        public AuthService(string source)
        {
            _source = source;
        }

        public async Task<bool> LoadUsersAsync()
        {
            Logger.Log($"Fetching user database from {_source}...");
            try
            {
                using (var http = new HttpClient())
                {
                    var token = SecretService.GetGithubToken();
                    if (!string.IsNullOrEmpty(token))
                    {
                        http.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }
                    http.DefaultRequestHeaders.UserAgent.Add(
                        new System.Net.Http.Headers.ProductInfoHeaderValue("RevitAddin", "1.0"));

                    string urlWithCacheBuster = _source + "?t=" + DateTime.UtcNow.Ticks;
                    var response = await http.GetAsync(urlWithCacheBuster);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.Log($"GitHub request failed: {response.StatusCode}", "ERROR");
                        return false;
                    }

                    if (response.Headers.Date != null)
                    {
                        ServerTimeUtc = response.Headers.Date.Value.UtcDateTime;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    _users = JsonConvert.DeserializeObject<List<UserRecord>>(json);
                    Logger.Log($"Database loaded successfully. Found {_users?.Count ?? 0} users.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load users from GitHub");
                return false;
            }
        }

        public async Task<bool> ValidateCredentialsAsync(string user, string pass, Action<string> err)
        {
            Logger.Log($"Attempting login for user: {user}");
            var ok = await LoadUsersAsync();
            if (!ok)
            {
                err?.Invoke("Server unreachable.");
                return false;
            }

            var match = _users.FirstOrDefault(x =>
                x.Username.Equals(user, StringComparison.OrdinalIgnoreCase));

            if (match == null) 
            { 
                Logger.Log($"Login failed: User '{user}' not found.", "WARNING");
                err?.Invoke("User not found."); 
                return false; 
            }

            // Secure Hashing Logic with migration fallback
            string inputHash = HashUtils.ComputeHash(pass);
            bool isCorrect = false;

            if (HashUtils.IsHash(match.Password))
            {
                // DB has a hash, compare hashes
                isCorrect = match.Password.Equals(inputHash, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // DB has plain text (old user), compare plain text
                isCorrect = match.Password == pass;
                
                if (isCorrect)
                    Logger.Log($"MIGRATION: User '{user}' logged in with plain text password. Please update their record on GitHub to use hash: {inputHash}");
            }

            if (!isCorrect) 
            { 
                Logger.Log($"Login failed: Wrong password for user '{user}'.", "WARNING");
                err?.Invoke("Wrong password."); 
                return false; 
            }

            if (!match.Active) 
            { 
                Logger.Log($"Login failed: Account '{user}' is inactive.", "WARNING");
                err?.Invoke("License inactive."); 
                return false; 
            }

            DateTime trueTime = ServerTimeUtc ?? DateTime.UtcNow;
            if (match.Expires.ToUniversalTime() < trueTime) 
            { 
                Logger.Log($"Login failed: Account '{user}' has expired on {match.Expires}.", "WARNING");
                err?.Invoke("License expired."); 
                return false; 
            }

            Logger.Log($"Login successful for user: {user}");
            CurrentUser = match;
            return true;
        }

        public UserRecord GetUser(string username)
        {
            return _users.FirstOrDefault(x =>
                x.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }
    }
}
