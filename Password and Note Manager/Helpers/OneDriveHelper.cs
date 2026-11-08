using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Desktop;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Password_and_Note_Manager.Helpers
{
    public static class OneDriveHelper
    {
        private static readonly string ClientId = Environment.GetEnvironmentVariable("ONEDRIVE_CLIENT_ID")
            ?? throw new InvalidOperationException("The environment variable ONEDRIVE_CLIENT_ID is not configured.");
        private static readonly string[] Scopes = { "Files.ReadWrite", "User.Read" };

        private static IPublicClientApplication _pca;

        private static IPublicClientApplication GetPublicClient()
        {
            if (_pca == null)
            {
                _pca = PublicClientApplicationBuilder.Create(ClientId)
                    .WithDefaultRedirectUri()
                    .WithWindowsEmbeddedBrowserSupport()
                    .Build();
            }
            return _pca;
        }

        // 1. Login to OneDrive and get the access token
        public static async Task<string> LoginAndGetAccessTokenAsync()
        {
            var pca = GetPublicClient();
            var accounts = await pca.GetAccountsAsync();
            AuthenticationResult result = null;

            try
            {
                var firstAccount = accounts.FirstOrDefault();
                if (firstAccount != null)
                {
                    result = await pca.AcquireTokenSilent(Scopes, firstAccount).ExecuteAsync();
                }
                else
                {
                    result = await AcquireInteractiveWithParentAsync(pca);
                }
            }
            catch (MsalUiRequiredException)
            {
                result = await AcquireInteractiveWithParentAsync(pca);
            }

            return result?.AccessToken;
        }

        // 2. Acquire token interactively with embedded web view
        private static async Task<AuthenticationResult> AcquireInteractiveWithParentAsync(IPublicClientApplication pca)
        {
            return await pca.AcquireTokenInteractive(Scopes)
                .WithUseEmbeddedWebView(true)
                .ExecuteAsync();
        }

        // 3. Upload the `vault_onedrive_user_com.db` file to the isolated folder in OneDrive
        public static async Task<bool> UploadBackupToOneDriveAsync(string localDbPath)
        {
            try
            {
                string accessToken = await LoginAndGetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken)) return false;

                if (!File.Exists(localDbPath)) return false;

                string tempUploadPath = localDbPath + ".upload";
                File.Copy(localDbPath, tempUploadPath, true);

                byte[] fileBytes;

                try
                {
                    fileBytes = File.ReadAllBytes(tempUploadPath);
                }
                finally
                {
                    if (File.Exists(tempUploadPath))
                    {
                        File.Delete(tempUploadPath);
                    }
                }

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    // Get the exact dynamic vault name (ex: vault_onedrive_user_com.db)
                    string fileName = Path.GetFileName(localDbPath);

                    // Utilize the variable in the Microsoft Graph URL
                    string url = $"https://graph.microsoft.com/v1.0/me/drive/special/approot:/{fileName}:/content";

                    using (var content = new ByteArrayContent(fileBytes))
                    {
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                        var response = await httpClient.PutAsync(url, content);
                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao enviar backup para o OneDrive: {ex.Message}", "Erro Cloud", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        // 4. Download the `vault_onedrive_user_com.db` file from the isolated folder in OneDrive to the local PC
        public static async Task<bool> DownloadBackupFromOneDriveAsync(string localDestinationPath)
        {
            try
            {
                var pca = GetPublicClient();
                var accounts = await pca.GetAccountsAsync();
                AuthenticationResult result = null;

                try
                {
                    var firstAccount = accounts.FirstOrDefault();
                    if (firstAccount != null)
                    {
                        result = await pca.AcquireTokenSilent(Scopes, firstAccount).ExecuteAsync();
                    }
                    else
                    {
                        result = await AcquireInteractiveWithParentAsync(pca);
                    }
                }
                catch (MsalUiRequiredException)
                {
                    result = await AcquireInteractiveWithParentAsync(pca);
                }

                if (result == null || string.IsNullOrEmpty(result.AccessToken))
                {
                    return false;
                }

                // Get the exact dynamic vault name (ex: vault_onedrive_user_com.db)
                string fileName = Path.GetFileName(localDestinationPath);

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);

                    // OneDrive endpoint utilizing the dynamic name in the approot folder
                    string graphUrl = $"https://graph.microsoft.com/v1.0/me/drive/special/approot:/{fileName}:/content";

                    var response = await httpClient.GetAsync(graphUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();

                        string tempVaultPath = localDestinationPath + ".tmp";

                        await System.IO.File.WriteAllBytesAsync(tempVaultPath, fileBytes);

                        if (System.IO.File.Exists(localDestinationPath))
                        {
                            System.IO.File.Delete(localDestinationPath);
                        }
                        System.IO.File.Move(tempVaultPath, localDestinationPath);
                        return true;
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // The file does not exist in the cloud (first access)
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            return false;
        }

        // 5. Synchronize the local vault with OneDrive
        public static async Task SynchronizeVaultAsync(string localDbPath)
        {
            string accessToken = await LoginAndGetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken)) return;

            bool localExists = File.Exists(localDbPath);
            bool remoteExists = false;
            DateTimeOffset remoteTime = DateTimeOffset.MinValue;

            // Extract the current file name to ensure OneDrive searches for the correct file in the approot folder
            string fileName = Path.GetFileName(localDbPath);

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                string metadataUrl = $"https://graph.microsoft.com/v1.0/me/drive/special/approot:/{fileName}";

                try
                {
                    var response = await httpClient.GetAsync(metadataUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        remoteExists = true;
                        var jsonString = await response.Content.ReadAsStringAsync();

                        using (var doc = System.Text.Json.JsonDocument.Parse(jsonString))
                        {
                            if (doc.RootElement.TryGetProperty("lastModifiedDateTime", out var dateProp))
                            {
                                remoteTime = dateProp.GetDateTimeOffset();
                            }
                        }
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        remoteExists = false;
                    }
                }
                catch
                {
                    remoteExists = false;
                }
            }

            if (!remoteExists && localExists)
            {
                await UploadBackupToOneDriveAsync(localDbPath);
            }
            else if (remoteExists && !localExists)
            {
                await DownloadBackupFromOneDriveAsync(localDbPath);
            }
            else if (remoteExists && localExists)
            {
                DateTime localTime = File.GetLastWriteTimeUtc(localDbPath);
                DateTimeOffset localTimeUtc = new DateTimeOffset(localTime, TimeSpan.Zero);

                if (remoteTime > localTimeUtc)
                {
                    await DownloadBackupFromOneDriveAsync(localDbPath);
                }
                else if (localTimeUtc > remoteTime)
                {
                    await UploadBackupToOneDriveAsync(localDbPath);
                }
            }
        }

        // 6. Retrieve the user's email from OneDrive for account verification
        public static async Task<string> GetUserEmailAsync()
        {
            try
            {
                string token = await LoginAndGetAccessTokenAsync();
                if (string.IsNullOrEmpty(token)) return null;

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("mail", out var mailProp) && mailProp.GetString() != null)
                        {
                            return mailProp.GetString();
                        }
                        if (doc.RootElement.TryGetProperty("userPrincipalName", out var upnProp))
                        {
                            return upnProp.GetString();
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}