using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DataLab.LicFolder
{
    public class LocalAuthToken
    {
        public string Username { get; set; }
        public string MachineId { get; set; }
        public string Plan { get; set; }
        public DateTime ExpiresUtc { get; set; }
    }

    public static class TokenService
    {
        private static readonly string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ABS_WIZZ");

        private static readonly string PathFile = System.IO.Path.Combine(Folder, "auth.dat");

        public static void SaveToken(LocalAuthToken token)
        {
            Directory.CreateDirectory(Folder);

            var json = JsonConvert.SerializeObject(token);
            var data = Encoding.UTF8.GetBytes(json);
            var enc = ProtectedData.Protect(data, Encoding.UTF8.GetBytes("ABS_SALT"), DataProtectionScope.CurrentUser);

            File.WriteAllBytes(PathFile, enc);
        }

        public static LocalAuthToken LoadToken()
        {
            try
            {
                if (!File.Exists(PathFile)) return null;

                var enc = File.ReadAllBytes(PathFile);
                var data = ProtectedData.Unprotect(enc, Encoding.UTF8.GetBytes("ABS_SALT"), DataProtectionScope.CurrentUser);
                return JsonConvert.DeserializeObject<LocalAuthToken>(Encoding.UTF8.GetString(data));
            }
            catch { return null; }
        }

        public static void DeleteToken()
        {
            if (File.Exists(PathFile))
                File.Delete(PathFile);
        }
    }
}
