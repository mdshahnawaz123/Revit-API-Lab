using System;
using System.IO;
using System.Text;

namespace DataLab.LicFolder
{
    public static class SecretService
    {
        // This is a simple XOR obfuscation to hide the token from plain text search/decompilation.
        private static readonly byte[] ObfuscatedToken = new byte[] 
        { 
            37, 55, 60, 30, 118, 12, 59, 46, 116, 6, 124, 98, 13, 50, 36, 16, 
            14, 84, 1, 116, 0, 114, 110, 10, 16, 43, 41, 50, 41, 17, 31, 46, 
            0, 44, 123, 46, 33, 109, 93, 6
        };

        private static readonly string Key = "B_LAB_SECRET_KEY_2024";
        private static readonly string OverrideFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "github_token.txt");

        public static string GetGithubToken()
        {
            if (File.Exists(OverrideFilePath))
            {
                string overrideToken = File.ReadAllText(OverrideFilePath).Trim();
                if (!string.IsNullOrEmpty(overrideToken)) return overrideToken;
            }
            return Decode(ObfuscatedToken);
        }

        public static string GetLicenseUrl() => Decode(new byte[] { 42, 43, 56, 49, 49, 101, 124, 106, 49, 51, 50, 122, 56, 34, 49, 49, 42, 80, 69, 65, 81, 48, 60, 35, 47, 54, 58, 61, 49, 109, 49, 42, 57, 112, 38, 33, 42, 55, 83, 88, 92, 85, 53, 62, 54, 112, 112, 108, 124, 7, 7, 22, 104, 6, 58, 39, 32, 56, 44, 87, 67, 29, 89, 35, 54, 34, 110, 14, 48, 52, 44, 45, 22, 32, 32, 62, 34, 41, 42 });
        public static string GetUpdaterApiUrl() => Decode(new byte[] { 42, 43, 56, 49, 49, 101, 124, 106, 34, 34, 44, 122, 56, 34, 49, 49, 42, 80, 30, 81, 91, 47, 112, 62, 36, 50, 48, 32, 106, 46, 54, 54, 60, 62, 35, 43, 56, 40, 83, 74, 3, 6, 113, 112, 14, 5, 6, 114, 1, 32, 47, 55, 36, 39, 58, 56, 106, 43, 58, 94, 85, 83, 71, 39, 44, 99, 45, 35, 43, 54, 54, 55 });
        public static string GetUpdaterPageUrl() => Decode(new byte[] { 42, 43, 56, 49, 49, 101, 124, 106, 36, 59, 49, 60, 42, 41, 107, 58, 48, 95, 31, 95, 80, 49, 55, 45, 41, 44, 62, 36, 36, 57, 99, 119, 103, 112, 9, 1, 29, 114, 96, 85, 94, 81, 35, 44, 41, 50, 109, 45, 54, 41, 38, 51, 54, 49, 44, 100, 41, 56, 43, 87, 67, 70 });
        public static string GetRepoOwner() => Decode(new byte[] { 47, 59, 63, 41, 35, 55, 61, 36, 52, 51, 63, 101, 109, 120 });
        public static string GetRepoName() => Decode(new byte[] { 0, 27, 8, 108, 16, 58, 63, 32, 34, 33, 32, 39 });

        private static string Decode(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
                sb.Append((char)(bytes[i] ^ Key[i % Key.Length]));
            return sb.ToString();
        }
    }
}
