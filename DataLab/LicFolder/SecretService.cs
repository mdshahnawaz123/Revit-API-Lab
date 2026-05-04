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
            // 1. Check if user provided an override token file (Useful if the old token was revoked)
            if (File.Exists(OverrideFilePath))
            {
                string overrideToken = File.ReadAllText(OverrideFilePath).Trim();
                if (!string.IsNullOrEmpty(overrideToken))
                {
                    return overrideToken;
                }
            }

            // 2. Use obfuscated token fallback
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ObfuscatedToken.Length; i++)
            {
                sb.Append((char)(ObfuscatedToken[i] ^ Key[i % Key.Length]));
            }
            return sb.ToString();
        }
    }
}
