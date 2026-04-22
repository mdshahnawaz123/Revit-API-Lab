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

        public AuthService(string source)
        {
            _source = source;
        }

        public async Task<bool> LoadUsersAsync()
        {
            try
            {
                using (var http = new HttpClient())
                {
                    var json = await http.GetStringAsync(_source);
                    _users = JsonConvert.DeserializeObject<List<UserRecord>>(json);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ValidateCredentialsAsync(string user, string pass, Action<string> err)
        {
            var ok = await LoadUsersAsync();
            if (!ok)
            {
                err?.Invoke("Server unreachable.");
                return false;
            }

            var match = _users.FirstOrDefault(x =>
                x.Username.Equals(user, StringComparison.OrdinalIgnoreCase));

            if (match == null) { err?.Invoke("User not found."); return false; }
            if (match.Password != pass) { err?.Invoke("Wrong password."); return false; }
            if (!match.Active) { err?.Invoke("License inactive."); return false; }
            if (match.Expires < DateTime.UtcNow) { err?.Invoke("License expired."); return false; }

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
