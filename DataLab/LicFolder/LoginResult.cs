using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataLab.LicFolder
{
    public class LoginResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }

        // Populated on successful auto-login
        public string Username { get; set; }
        public string Plan { get; set; }
        public DateTime? ExpiresUtc { get; set; }

        public int DaysRemaining =>
            ExpiresUtc.HasValue
                ? Math.Max(0, (int)(ExpiresUtc.Value - DateTime.UtcNow).TotalDays)
                : 0;

        public static LoginResult Ok(string username = null, string plan = null, DateTime? expires = null)
            => new LoginResult
            {
                Success = true,
                Username = username,
                Plan = plan,
                ExpiresUtc = expires
            };

        public static LoginResult Fail(string message)
            => new LoginResult { Success = false, Error = message };
    }
}
