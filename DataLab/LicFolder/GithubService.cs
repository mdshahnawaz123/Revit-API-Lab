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
            var token = GithubToken;
            if (string.IsNullOrEmpty(token) || token.Contains("YOUR_GITHUB_TOKEN"))
                return (false, "GitHub token is empty or invalid.");

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15); // Prevent hanging forever
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RevitAddin", "1.0"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    // 1. Get current file content and SHA
                    var getUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/{FilePath}";
                    var response = await client.GetAsync(getUrl).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) 
                    {
                        string err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return (false, $"Failed to fetch current database. Status: {response.StatusCode}. Details: {err}");
                    }

                    var jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var fileInfo = JsonConvert.DeserializeObject<GithubContent>(jsonString);
                    if (fileInfo == null) return (false, "Failed to deserialize GitHub response.");

                    string sha = fileInfo.sha;
                    string contentBase64 = fileInfo.content;
                    string contentJson = Encoding.UTF8.GetString(Convert.FromBase64String(contentBase64.Replace("\n", "")));

                    // 2. Update content using JArray to handle mixed schemas
                    JArray usersArray;
                    try 
                    {
                        usersArray = JArray.Parse(contentJson);
                    } 
                    catch (Exception ex)
                    {
                        return (false, $"Failed to parse existing JSON database: {ex.Message}");
                    }
                    
                    // Check if machine already has a record
                    string currentMachine = newUser.Machines?.FirstOrDefault();
                    if (!string.IsNullOrEmpty(currentMachine))
                    {
                        bool alreadyExists = usersArray.Any(u => 
                            u["Machines"] != null && 
                            u["Machines"].Type == JTokenType.Array && 
                            u["Machines"].Values<string>().Any(m => m == currentMachine));

                        if (alreadyExists) return (false, "A trial has already been requested from this machine.");
                    }

                    JObject newUserObj = JObject.FromObject(newUser);
                    usersArray.Add(newUserObj);
                    
                    var updatedJson = JsonConvert.SerializeObject(usersArray, Formatting.Indented);
                    var updatedContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedJson));

                    // 3. Push back to GitHub
                    var putData = new
                    {
                        message = $"Add user {newUser.Username} via Request Access form",
                        content = updatedContentBase64,
                        sha = sha
                    };

                    var putResponse = await client.PutAsync(getUrl, new StringContent(JsonConvert.SerializeObject(putData), Encoding.UTF8, "application/json")).ConfigureAwait(false);
                    if (!putResponse.IsSuccessStatusCode)
                    {
                        string err = await putResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return (false, $"Failed to update database. Status: {putResponse.StatusCode}. Details: {err}");
                    }

                    return (true, string.Empty);
                }
            }
            catch (TaskCanceledException)
            {
                return (false, "The request timed out. Please check your internet connection.");
            }
            catch (Exception ex)
            {
                return (false, $"An unexpected error occurred: {ex.Message}");
            }
        }
        public static async Task<(bool Success, string ErrorMessage)> ResetPasswordAsync(string username, string email, string newPassword)
        {
            var token = GithubToken;
            if (string.IsNullOrEmpty(token) || token.Contains("YOUR_GITHUB_TOKEN"))
                return (false, "GitHub token is empty or invalid.");

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RevitAddin", "1.0"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var getUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/{FilePath}";
                    var response = await client.GetAsync(getUrl).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) return (false, "Failed to connect to database.");

                    var jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var fileInfo = JsonConvert.DeserializeObject<GithubContent>(jsonString);
                    if (fileInfo == null) return (false, "Failed to read database.");

                    string contentJson = Encoding.UTF8.GetString(Convert.FromBase64String(fileInfo.content.Replace("\n", "")));

                    JArray usersArray;
                    try { usersArray = JArray.Parse(contentJson); } 
                    catch { return (false, "Database is corrupted."); }

                    // Find user
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

                    if (targetUser == null) return (false, "Username and Email combination not found.");

                    // Check if active
                    if (targetUser["Active"]?.ToObject<bool>() != true)
                        return (false, "Account is currently inactive.");

                    // Check if expired (unless Admin)
                    string plan = targetUser["Plan"]?.ToString() ?? "";
                    if (!plan.Equals("admin", StringComparison.OrdinalIgnoreCase) && !plan.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        DateTime expiry = targetUser["Expires"]?.ToObject<DateTime>() ?? DateTime.MinValue;
                        if (expiry.ToUniversalTime() < DateTime.UtcNow)
                            return (false, "Account has expired. Cannot reset password.");
                    }

                    // Update password
                    targetUser["Password"] = newPassword;

                    var updatedJson = JsonConvert.SerializeObject(usersArray, Formatting.Indented);
                    var updatedContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedJson));

                    var putData = new
                    {
                        message = $"Password reset for user {username}",
                        content = updatedContentBase64,
                        sha = fileInfo.sha
                    };

                    var putResponse = await client.PutAsync(getUrl, new StringContent(JsonConvert.SerializeObject(putData), Encoding.UTF8, "application/json")).ConfigureAwait(false);
                    if (!putResponse.IsSuccessStatusCode)
                        return (false, "Failed to save new password to database.");

                    return (true, string.Empty);
                }
            }
            catch (Exception ex)
            {
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
