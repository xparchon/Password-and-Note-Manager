using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Desktop;
using Password_and_Note_Manager.Views;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Password_and_Note_Manager.Helpers
{
    public static class OneDriveHelper
    {
        private static readonly string ClientId = Environment.GetEnvironmentVariable("ONEDRIVE_CLIENT_ID")
            ?? throw new InvalidOperationException("A variável de ambiente ONEDRIVE_CLIENT_ID não foi configurada.");
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

        private static async Task<AuthenticationResult> AcquireInteractiveWithParentAsync(IPublicClientApplication pca)
        {
            return await pca.AcquireTokenInteractive(Scopes)
                .WithUseEmbeddedWebView(true)
                .ExecuteAsync();
        }

        // 1. Fazer Upload do Ficheiro `vault_onedrive_user_com.db` para a pasta isolada da app no OneDrive
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
                    // Obtém o nome exato do cofre atual (ex: vault_onedrive_user_com.db)
                    string fileName = Path.GetFileName(localDbPath);

                    // Utiliza a variável no URL do Microsoft Graph
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

        // 2. Fazer Download do Ficheiro `vault_onedrive_user_com.db` da pasta isolada da app no OneDrive para o PC local
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

                // Obtém o nome exato do cofre dinâmico (ex: vault_onedrive_user_com.db)
                string fileName = Path.GetFileName(localDestinationPath);

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);

                    // Endpoint do OneDrive utilizando o nome dinâmico na pasta approot
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
                        // O ficheiro ainda não existe na nuvem (primeiro acesso)
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

        // 3. Sincroniza o cofre local com o OneDrive
        public static async Task SynchronizeVaultAsync(string localDbPath)
        {
            string accessToken = await LoginAndGetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken)) return;

            bool localExists = File.Exists(localDbPath);
            bool remoteExists = false;
            DateTimeOffset remoteTime = DateTimeOffset.MinValue;

            // Extrai o nome do ficheiro atual para garantir que o OneDrive procura pelo ficheiro correto na pasta approot
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