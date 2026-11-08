using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Password_and_Note_Manager.Views;

namespace Password_and_Note_Manager.Helpers
{
    public static class GoogleDriveHelper
    {
        private static string ClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
            ?? throw new InvalidOperationException("A variável de ambiente GOOGLE_CLIENT_ID não foi configurada.");
        private static string ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") 
            ?? throw new InvalidOperationException("A variável de ambiente GOOGLE_CLIENT_SECRET não foi configurada.");
        private static readonly string RedirectUri = "http://localhost";
        private static readonly string Scope = "https://www.googleapis.com/auth/drive.file https://www.googleapis.com/auth/userinfo.email openid";

        private static string _cachedAccessToken;

        // 1. Google authentication (utilizing pop-up window with existent WebView2)
        public static async Task<string> LoginAndGetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_cachedAccessToken))
            {
                return _cachedAccessToken;
            }

            string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={ClientId}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&response_type=code&scope={Uri.EscapeDataString(Scope)}&access_type=offline";

            string redirectedUrl = null;
            bool? dialogResult = false;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var popup = new LoginPopupWnd(authUrl, RedirectUri);
                if (Application.Current.MainWindow != null)
                {
                    popup.Owner = Application.Current.MainWindow;
                }
                dialogResult = popup.ShowDialog();
                if (dialogResult == true)
                {
                    redirectedUrl = popup.RedirectedUrl;
                }
            });

            if (dialogResult != true || string.IsNullOrEmpty(redirectedUrl))
            {
                throw new Exception("Autenticação com o Google cancelada ou janela fechada pelo utilizador.");
            }

            var uri = new Uri(redirectedUrl);
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
            string code = queryParams["code"];

            if (string.IsNullOrEmpty(code))
            {
                throw new Exception("Código de autorização do Google não foi encontrado.");
            }

            using (var httpClient = new HttpClient())
            {
                var tokenParameters = new Dictionary<string, string>
                {
                    { "code", code },
                    { "client_id", ClientId },
                    { "client_secret", ClientSecret },
                    { "redirect_uri", RedirectUri },
                    { "grant_type", "authorization_code" }
                };

                var content = new FormUrlEncodedContent(tokenParameters);
                var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token", content);

                if (tokenResponse.IsSuccessStatusCode)
                {
                    var jsonString = await tokenResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
                    {
                        _cachedAccessToken = tokenProp.GetString();
                        return _cachedAccessToken;
                    }
                }
            }

            return null;
        }

        // 2. Upload backup file to Google Drive
        public static async Task<bool> UploadBackupToGoogleDriveAsync(string localDbPath)
        {
            try
            {
                string accessToken = await LoginAndGetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken)) return false;

                if (!File.Exists(localDbPath)) return false;

                string fileName = Path.GetFileName(localDbPath);
                string tempUploadPath = localDbPath + ".upload";

                // Copy the active file securely to avoid lock conflicts with SQLite WAL
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

                    // Search for the file ID dynamically by name (e.g., vault_gdrive.db)
                    string encodedFileName = Uri.EscapeDataString(fileName);
                    string searchUrl = $"https://www.googleapis.com/drive/v3/files?q=name%3D%27{encodedFileName}%27%20and%20trashed%3Dfalse";

                    var searchResponse = await httpClient.GetAsync(searchUrl);
                    if (!searchResponse.IsSuccessStatusCode) return false;

                    var searchJson = await searchResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(searchJson);
                    var files = doc.RootElement.GetProperty("files");

                    if (files.GetArrayLength() > 0)
                    {
                        // File already exists: Update content (PATCH)
                        string fileId = files[0].GetProperty("id").GetString();
                        string updateUrl = $"https://www.googleapis.com/upload/drive/v3/files/{fileId}?uploadType=media";

                        using (var fileContent = new ByteArrayContent(fileBytes))
                        {
                            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                            var response = await httpClient.PatchAsync(updateUrl, fileContent);
                            return response.IsSuccessStatusCode;
                        }
                    }
                    else
                    {
                        // File does not exist: Create new file with multipart upload (POST)
                        return await CreateNewFileMultipartAsync(httpClient, fileBytes, fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error uploading backup to Google Drive: {ex.Message}", "Cloud Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        private static async Task<bool> CreateNewFileMultipartAsync(HttpClient httpClient, byte[] fileBytes, string fileName)
        {
            string boundary = "-------314159265358979323846";
            var request = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart");

            var metadata = new { name = fileName };
            string metadataJson = JsonSerializer.Serialize(metadata);

            string bodyHeader =
                $"--{boundary}\r\n" +
                $"Content-Type: application/json; charset=UTF-8\r\n\r\n" +
                $"{metadataJson}\r\n" +
                $"--{boundary}\r\n" +
                $"Content-Type: application/octet-stream\r\n\r\n";

            byte[] headerBytes = System.Text.Encoding.UTF8.GetBytes(bodyHeader);
            byte[] footerBytes = System.Text.Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");

            var totalBytes = new byte[headerBytes.Length + fileBytes.Length + footerBytes.Length];
            Buffer.BlockCopy(headerBytes, 0, totalBytes, 0, headerBytes.Length);
            Buffer.BlockCopy(fileBytes, 0, totalBytes, headerBytes.Length, fileBytes.Length);
            Buffer.BlockCopy(footerBytes, 0, totalBytes, headerBytes.Length + fileBytes.Length, footerBytes.Length);

            request.Content = new ByteArrayContent(totalBytes);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/related; boundary={boundary}");

            var response = await httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        // 3. Download backup file from Google Drive to local PC
        public static async Task<bool> DownloadBackupFromGoogleDriveAsync(string localDestinationPath)
        {
            try
            {
                string accessToken = await LoginAndGetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken)) return false;

                string fileName = Path.GetFileName(localDestinationPath);

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    string encodedFileName = Uri.EscapeDataString(fileName);
                    string searchUrl = $"https://www.googleapis.com/drive/v3/files?q=name%3D%27{encodedFileName}%27%20and%20trashed%3Dfalse";

                    var searchResponse = await httpClient.GetAsync(searchUrl);
                    if (!searchResponse.IsSuccessStatusCode) return false;

                    var searchJson = await searchResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(searchJson);
                    var files = doc.RootElement.GetProperty("files");

                    if (files.GetArrayLength() == 0)
                    {
                        return false; // File does not exist in the cloud
                    }

                    string fileId = files[0].GetProperty("id").GetString();
                    string downloadUrl = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media";

                    var downloadResponse = await httpClient.GetAsync(downloadUrl);
                    if (downloadResponse.IsSuccessStatusCode)
                    {
                        byte[] fileBytes = await downloadResponse.Content.ReadAsByteArrayAsync();

                        string tempVaultPath = localDestinationPath + ".tmp";
                        await File.WriteAllBytesAsync(tempVaultPath, fileBytes);

                        // Replace the local vault securely
                        if (File.Exists(localDestinationPath))
                        {
                            File.Delete(localDestinationPath);
                        }
                        File.Move(tempVaultPath, localDestinationPath);
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            return false;
        }

        // 4. Synchronize the local vault with Google Drive based on modification date
        public static async Task SynchronizeVaultAsync(string localDbPath)
        {
            string accessToken = await LoginAndGetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken)) return;

            string fileName = Path.GetFileName(localDbPath);
            bool localExists = File.Exists(localDbPath);
            bool remoteExists = false;
            DateTimeOffset remoteTime = DateTimeOffset.MinValue;

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                string encodedFileName = Uri.EscapeDataString(fileName);
                string metadataUrl = $"https://www.googleapis.com/drive/v3/files?q=name%3D%27{encodedFileName}%27%20and%20trashed%3Dfalse&fields=files(id,name,modifiedTime)";

                var response = await httpClient.GetAsync(metadataUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(jsonString))
                    {
                        var files = doc.RootElement.GetProperty("files");
                        if (files.GetArrayLength() > 0)
                        {
                            remoteExists = true;
                            if (files[0].TryGetProperty("modifiedTime", out var dateProp))
                            {
                                remoteTime = dateProp.GetDateTimeOffset();
                            }
                        }
                    }
                }
            }

            if (!remoteExists && localExists)
            {
                await UploadBackupToGoogleDriveAsync(localDbPath);
            }
            else if (remoteExists && !localExists)
            {
                await DownloadBackupFromGoogleDriveAsync(localDbPath);
            }
            else if (remoteExists && localExists)
            {
                DateTime localTime = File.GetLastWriteTimeUtc(localDbPath);
                DateTimeOffset localTimeUtc = new DateTimeOffset(localTime, TimeSpan.Zero);

                // Secure margin to avoid system conflicts - a margin of tolerance of 3 seconds to avoid network/system clock conflicts
                if (remoteTime > localTimeUtc.AddSeconds(3))
                {
                    await DownloadBackupFromGoogleDriveAsync(localDbPath);
                }
                else
                {
                    // If the local file is equal to or more recent, ensure immediate upload to the cloud
                    await UploadBackupToGoogleDriveAsync(localDbPath);
                }
            }
        }

        // 5. Retrieve the user's email from Google Drive for account verification
        public static async Task<string> GetUserEmailAsync()
        {
            try
            {
                string token = await LoginAndGetAccessTokenAsync();
                if (string.IsNullOrEmpty(token)) return null;

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var response = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("email", out var emailProp))
                        {
                            return emailProp.GetString();
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}