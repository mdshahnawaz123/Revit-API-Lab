using System;
using System.Security.Cryptography;
using System.Text;

namespace DataLab.LicFolder
{
    public static class HashUtils
    {
        /// <summary>
        /// Hashes a string using SHA256.
        /// </summary>
        public static string ComputeHash(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// Checks if a string is already a SHA256 hash (64 hex characters).
        /// </summary>
        public static bool IsHash(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length != 64) return false;
            foreach (char c in input)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }
    }
}
