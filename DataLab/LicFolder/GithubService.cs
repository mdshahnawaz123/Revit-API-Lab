using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace DataLab.LicFolder
{
    public static class GithubService
    {
        private static string GithubToken => SecretService.GetGithubToken();
        private static readonly string RepoOwner = SecretService.GetRepoOwner();
        private static readonly string RepoName = SecretService.GetRepoName();
        private const string FilePath = "LoginDetails";

        public static async Task<(bool Success, string ErrorMessage)> AddUserToGithubAsync(UserRecord newUser)
        {
            Logger.Log($"GitHub: Adding new user '{newUser.Username}'...");
            var token = GithubToken;
            if (string.IsNullOrEmpty(token) || token.Contains("YOUR_GITHUB_TOKEN"))
            {
                Logger.Log("GitHub: Invalid token provided.", "ERROR");
                return (false, "GitHub token is empty or invalid.");
            }

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RevitAddin", "1.0"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var getUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/{FilePath}";
                    var response = await client.GetAsync(getUrl).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) 
                    {
                        Logger.Log($"GitHub: Failed to fetch database. Status: {response.StatusCode}", "ERROR");
                        return (false, "Failed to fetch current database.");
                    }

                    var jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var fileInfo = JsonConvert.DeserializeObject<GithubContent>(jsonString);
                    string contentJson = Encoding.UTF8.GetString(Convert.FromBase64String(fileInfo.content.Replace("\n", "")));

                    JArray usersArray = JArray.Parse(contentJson);
                    
                    // Check if machine already has a record
                    string currentMachine = newUser.Machines?.FirstOrDefault();
                    if (!string.IsNullOrEmpty(currentMachine))
                    {
                        bool alreadyExists = usersArray.Any(u => 
                            u["Machines"] != null && 
                            u["Machines"].Values<string>().Any(m => m == currentMachine));

                        if (alreadyExists) 
                        {
                            Logger.Log($"GitHub: Request denied. Machine '{currentMachine}' already has a trial.", "WARNING");
                            return (false, "A trial has already been requested from this machine.");
                        }
                    }

                    // SECURE: Hash the password before sending to GitHub
                    newUser.Password = HashUtils.ComputeHash(newUser.Password);

                    JObject newUserObj = JObject.FromObject(newUser);
                    usersArray.Add(newUserObj);
                    
                    var updatedJson = JsonConvert.SerializeObject(usersArray, Formatting.Indented);
                    var updatedContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedJson));

                    var putData = new { message = $"Add user {newUser.Username}", content = updatedContentBase64, sha = fileInfo.sha };
                    var putResponse = await client.PutAsync(getUrl, new StringContent(JsonConvert.SerializeObject(putData), Encoding.UTF8, "application/json")).ConfigureAwait(false);
                    
                    if (!putResponse.IsSuccessStatusCode)
                    {
                        Logger.Log($"GitHub: Failed to update database. Status: {putResponse.StatusCode}", "ERROR");
                        return (false, "Failed to update database.");
                    }

                    Logger.Log($"GitHub: Successfully added user '{newUser.Username}'.");
                    return (true, string.Empty);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GitHub: Unexpected error in AddUserToGithubAsync");
                return (false, $"An unexpected error occurred: {ex.Message}");
            }
        }

        public static async Task<(bool Success, string ErrorMessage)> ResetPasswordAsync(string username, string email, string newPassword)
        {
            Logger.Log($"GitHub: Attempting password reset for user '{username}'...");
            var token = GithubToken;
            if (string.IsNullOrEmpty(token)) return (false, "GitHub token is empty.");

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RevitAddin", "1.0"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var getUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/{FilePath}";
                    var response = await client.GetAsync(getUrl).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) return (false, "Connection error.");

                    var jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var fileInfo = JsonConvert.DeserializeObject<GithubContent>(jsonString);
                    string contentJson = Encoding.UTF8.GetString(Convert.FromBase64String(fileInfo.content.Replace("\n", "")));

                    JArray usersArray = JArray.Parse(contentJson);

                    JObject targetUser = null;
                    foreach (JObject u in usersArray)
                    {
                        if (u["Username"]?.ToString().Equals(username, StringComparison.OrdinalIgnoreCase) == true &&
                            u["EmailId"]?.ToString().Equals(email, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            targetUser = u;
                            break;
                        }
                    }

                    if (targetUser == null) return (false, "Username and EmailId combination not found.");

                    // SECURE: Hash the new password before storing
                    targetUser["Password"] = HashUtils.ComputeHash(newPassword);

                    var updatedJson = JsonConvert.SerializeObject(usersArray, Formatting.Indented);
                    var updatedContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedJson));

                    var putData = new { message = $"Password reset for {username}", content = updatedContentBase64, sha = fileInfo.sha };
                    var putResponse = await client.PutAsync(getUrl, new StringContent(JsonConvert.SerializeObject(putData), Encoding.UTF8, "application/json")).ConfigureAwait(false);
                    
                    if (!putResponse.IsSuccessStatusCode) return (false, "Failed to save to database.");

                    Logger.Log($"GitHub: Successfully reset password for user '{username}'.");
                    return (true, string.Empty);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GitHub: Unexpected error in ResetPasswordAsync");
                return (false, $"Error: {ex.Message}");
            }
        }

        private class GithubContent
        {
            public string sha { get; set; }
            public string content { get; set; }
        }
    }
}
