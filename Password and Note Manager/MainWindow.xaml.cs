using Password_and_Note_Manager.Helpers;
using Password_and_Note_Manager.Models;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Password_and_Note_Manager
{
    public partial class MainWindow : Window
    {
        private string _masterKey;
        private List<Credential> _credentialsList = new List<Credential>();

        public MainWindow(string masterKey)
        {
            InitializeComponent();
            _masterKey = masterKey;
            CheckVaultHash(); // Verify the integrity of the vault when opening the screen
            // Load the passwords from the database right after the screen is opened
            LoadCredentials();
        }

        private void CheckVaultHash()
        {
            try
            {
                // Verifies which vault is active and executes the corresponding logic
                if (DatabaseHelper.CurrentDbName == "vault_onedrive.db")
                {
                    // Synchronize with OneDrive
                    Task.Run(async () =>
                    {
                        string localVaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseHelper.CurrentDbName);
                        await OneDriveHelper.SynchronizeVaultAsync(localVaultPath);
                    }).Wait();
                }
                else if (DatabaseHelper.CurrentDbName == "vault_gdrive.db")
                {
                    // Synchronize with Google Drive
                    Task.Run(async () =>
                    {
                        string localVaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseHelper.CurrentDbName);
                        await GoogleDriveHelper.UploadBackupToGoogleDriveAsync(localVaultPath);
                    }).Wait();
                }
                else
                {
                    // Local Mode (vault_local.db or vault.db): Only operates locally, without calls to the cloud
                    // No extra action needed here.
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to synchronize. Using the local version.\n\nDetails: {ex.Message}", "Network Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 1. Load and list the credentials in the left sidebar
        private void LoadCredentials()
        {
            _credentialsList.Clear();
            lstCredentials.ItemsSource = null;

            using (var connection = DatabaseHelper.GetConnection())
            {
                string query = "SELECT Id, Title, WebsiteUrl, Username, EncryptedPassword, Notes, CreatedAt FROM Credentials ORDER BY Title;";
                using (var command = new SQLiteCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _credentialsList.Add(new Credential
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            Title = reader["Title"].ToString(),
                            WebsiteUrl = reader["WebsiteUrl"].ToString(),
                            Username = reader["Username"].ToString(),
                            EncryptedPassword = reader["EncryptedPassword"].ToString(),
                            Notes = reader["Notes"].ToString(),
                            CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                        });
                    }
                }
            }

            lstCredentials.ItemsSource = _credentialsList;
        }

        // 2. Search and filter the credentials in real-time based on Title or Username
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = txtSearch.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(searchText))
            {
                // If the search is empty, display all credentials
                lstCredentials.ItemsSource = _credentialsList;
            }
            else
            {
                // Filter the list based on Title or Username in real-time
                var filteredList = _credentialsList
                    .Where(c => (c.Title != null && c.Title.ToLower().Contains(searchText)) ||
                                (c.Username != null && c.Username.ToLower().Contains(searchText)))
                    .ToList();

                lstCredentials.ItemsSource = filteredList;
            }
        }

        // 3. When a credential is selected, display its details on the right side of the screen
        private void LstCredentials_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstCredentials.SelectedItem is Credential selected)
            {
                // Show details panel
                pnlDetails.Visibility = Visibility.Visible;

                lblSelectedTitle.Text = selected.Title;
                lblSelectedUrl.Text = string.IsNullOrWhiteSpace(selected.WebsiteUrl) ? "No URL" : selected.WebsiteUrl;
                txtDisplayUsername.Text = selected.Username;
                txtDisplayNotes.Text = selected.Notes;

                // Reset the display to hidden format when changing items
                selected.IsPasswordRevealed = false;
                btnShowPassword.Content = "Reveal";
                txtDisplayPassword.Text = "••••••••••••";
            }
            else
            {
                // Hide the panel if no item is selected
                pnlDetails.Visibility = Visibility.Collapsed;
                lblSelectedTitle.Text = "Select an item from the list";
                lblSelectedUrl.Text = string.Empty;
            }
        }

        // 4. Open the Add Credential window to allow the user to add a new credential
        private void BtnNewCredential_Click(object sender, RoutedEventArgs e)
        {
            // Instantiate the add credential window, passing the master key for encryption
            Views.AddCredentialWindow addWindow = new Views.AddCredentialWindow(_masterKey);
            addWindow.Owner = this; // Center the child window over the main window

            addWindow.ShowDialog();

            // If the user added at least one credential during the session, update the list when closing
            if (addWindow.HasAddedCredentials)
            {
                LoadCredentials();
                SaveChangesAndSync();
            }
        }

        // 5. Reveal or hide the password of the selected credential in real-time
        private void BtnShowPassword_Click(object sender, RoutedEventArgs e)
        {
            if (lstCredentials.SelectedItem is Credential selected)
            {
                if (selected.IsPasswordRevealed)
                {
                    // Hide again
                    txtDisplayPassword.Text = "••••••••••••";
                    btnShowPassword.Content = "Reveal";
                    selected.IsPasswordRevealed = false;
                }
                else
                {
                    try
                    {
                        // Decrypt in real-time using the CryptoHelper and the logged-in master key
                        string decrypted = CryptoHelper.Decrypt(selected.EncryptedPassword, _masterKey);
                        txtDisplayPassword.Text = decrypted;
                        btnShowPassword.Content = "Hide";
                        selected.IsPasswordRevealed = true;
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Error decrypting password. Master key might be corrupted.", "Security Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // 6. Delete the selected credential from the database and update the list
        private void BtnDeleteCredential_Click(object sender, RoutedEventArgs e)
        {
            if (lstCredentials.SelectedItem is Credential selected)
            {
                var result = MessageBox.Show($"Are you sure you want to delete the credential '{selected.Title}'?",
                                             "Confirm Deletion",
                                             MessageBoxButton.YesNo,
                                             MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var connection = DatabaseHelper.GetConnection())
                        {
                            string query = "DELETE FROM Credentials WHERE Id = @Id;";
                            using (var command = new SQLiteCommand(query, connection))
                            {
                                command.Parameters.AddWithValue("@Id", selected.Id);
                                command.ExecuteNonQuery();
                            }
                        }

                        LoadCredentials();
                        SaveChangesAndSync();
                        lstCredentials.SelectedItem = null;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to delete the credential: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnEditCredential_Click(object sender, RoutedEventArgs e)
        {
            if (lstCredentials.SelectedItem is Credential selected)
            {
                // Open the edit window passing the selected item and the master key
                Views.EditCredentialWindow editWindow = new Views.EditCredentialWindow(selected, _masterKey);
                editWindow.Owner = this;
                editWindow.ShowDialog();

                if (editWindow.HasEdited)
                {
                    LoadCredentials();
                    SaveChangesAndSync();
                }
            }
        }

        // 7. Logout and return to the login screen, clearing the current vault session
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Do you want to close the current vault and return to the login screen?",
                                         "Change Vault",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                System.Data.SQLite.SQLiteConnection.ClearAllPools();

                Views.LoginWindow loginWindow = new Views.LoginWindow();
                loginWindow.Show();

                this.Close();
            }
        }

        // 8. Save changes and synchronize with the cloud if applicable
        private void SaveChangesAndSync()
        {
            LoadCredentials(); // Update the local interface

            // Upload the database backup to the cloud in the background only when there are changes
            Task.Run(async () =>
            {
                string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseHelper.CurrentDbName);
                if (DatabaseHelper.CurrentDbName == "vault_gdrive.db")
                {
                    await GoogleDriveHelper.UploadBackupToGoogleDriveAsync(localPath);
                }
                else if (DatabaseHelper.CurrentDbName == "vault_onedrive.db")
                {
                    await OneDriveHelper.UploadBackupToOneDriveAsync(localPath);
                }
            });
        }
    }
}
