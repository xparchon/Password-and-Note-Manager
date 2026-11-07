using Password_and_Note_Manager.Helpers;
using System;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace Password_and_Note_Manager.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string password = txtMasterPassword.Password;

            if (string.IsNullOrWhiteSpace(password))
            {
                lblErrorMessage.Foreground = System.Windows.Media.Brushes.Red;
                lblErrorMessage.Text = "Password cannot be empty.";
                return;
            }

            // Desativa o botão no início
            btnLogin.IsEnabled = false;

            try
            {
                bool canProceed = true;

                // 1. SE O MODO FOR LOCAL (Índice 0)
                if (cmbStorageMode != null && cmbStorageMode.SelectedIndex == 0)
                {
                    DatabaseHelper.CurrentDbName = "vault_local.db";
                    canProceed = true;
                }
                // 2. SE O MODO FOR MICROSOFT ONEDRIVE (Índice 1)
                else if (cmbStorageMode != null && cmbStorageMode.SelectedIndex == 1)
                {
                    // Obtém o e-mail para criar um cofre único por conta
                    string email = null;
                    try
                    {
                        email = await OneDriveHelper.GetUserEmailAsync();
                    }
                    catch { }
                    string safeIdentifier = !string.IsNullOrEmpty(email) ? email.Replace("@", "_").Replace(".", "_") : "default";

                    DatabaseHelper.CurrentDbName = $"vault_onedrive_{safeIdentifier}.db";
                    string localDbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseHelper.CurrentDbName);

                    lblErrorMessage.Foreground = System.Windows.Media.Brushes.Blue;
                    lblErrorMessage.Text = "Synchronizing with OneDrive...";

                    try
                    {
                        // Se o cofre local não existir, cria a estrutura básica localmente antes de sincronizar
                        bool localExists = System.IO.File.Exists(localDbPath);
                        if (!localExists)
                        {
                            DatabaseHelper.InitializeDatabase();
                            using (var connection = DatabaseHelper.GetConnection())
                            {
                                string createTablesQuery = @"
                                    CREATE TABLE IF NOT EXISTS Users (
                                        Id INTEGER PRIMARY KEY AUTOINCREMENT, 
                                        Username TEXT, 
                                        PasswordHash TEXT, 
                                        Salt TEXT
                                    );
                                    CREATE TABLE IF NOT EXISTS Credentials (
                                        Id INTEGER PRIMARY KEY AUTOINCREMENT, 
                                        Title TEXT, 
                                        WebsiteUrl TEXT, 
                                        Username TEXT, 
                                        EncryptedPassword TEXT, 
                                        Notes TEXT
                                    );";

                                using (var cmd = new System.Data.SQLite.SQLiteCommand(createTablesQuery, connection))
                                {
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            string salt = CryptoHelper.GenerateSalt();
                            string hash = CryptoHelper.HashPassword(password, salt);

                            string insertQuery = "INSERT INTO Users (Username, PasswordHash, Salt) VALUES ('default_user', @hash, @salt);";
                            using (var connection = DatabaseHelper.GetConnection())
                            using (var insertCommand = new System.Data.SQLite.SQLiteCommand(insertQuery, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@hash", hash);
                                insertCommand.Parameters.AddWithValue("@salt", salt);
                                insertCommand.ExecuteNonQuery();
                            }
                        }

                        // Executa a sincronização inteligente (compara datas entre o PC e o OneDrive na pasta approot)
                        await OneDriveHelper.SynchronizeVaultAsync(localDbPath);

                        if (System.IO.File.Exists(localDbPath))
                        {
                            canProceed = true;
                        }
                        else
                        {
                            lblErrorMessage.Foreground = System.Windows.Media.Brushes.Red;
                            lblErrorMessage.Text = "Failed to synchronize with OneDrive.";
                            canProceed = false;
                        }
                    }
                    catch (Exception exSync)
                    {
                        if (System.IO.File.Exists(localDbPath))
                        {
                            lblErrorMessage.Foreground = System.Windows.Media.Brushes.Orange;
                            lblErrorMessage.Text = "OneDrive sync failed. Using local cache.";
                            canProceed = true;
                        }
                        else
                        {
                            lblErrorMessage.Foreground = System.Windows.Media.Brushes.Red;
                            lblErrorMessage.Text = $"OneDrive error: {exSync.Message}";
                            canProceed = false;
                        }
                    }
                }
                // 3. SE O MODO FOR GOOGLE DRIVE (Índice 2)
                else if (cmbStorageMode != null && cmbStorageMode.SelectedIndex == 2)
                {
                    // Obtém o e-mail para criar um cofre único por conta
                    string email = null;
                    try { email = await GoogleDriveHelper.GetUserEmailAsync(); } catch { }
                    string safeIdentifier = !string.IsNullOrEmpty(email) ? email.Replace("@", "_").Replace(".", "_") : "default";

                    DatabaseHelper.CurrentDbName = $"vault_gdrive_{safeIdentifier}.db";
                    string localDbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseHelper.CurrentDbName);

                    lblErrorMessage.Foreground = System.Windows.Media.Brushes.Blue;
                    lblErrorMessage.Text = "Checking vault on Google Drive...";

                    bool downloaded = false;
                    try
                    {
                        downloaded = await GoogleDriveHelper.DownloadBackupFromGoogleDriveAsync(localDbPath);
                    }
                    catch
                    {
                        downloaded = false;
                    }

                    if (downloaded && System.IO.File.Exists(localDbPath))
                    {
                        canProceed = true;
                    }
                    else
                    {
                        lblErrorMessage.Foreground = System.Windows.Media.Brushes.Blue;
                        lblErrorMessage.Text = "Vault not found. Creating and synchronizing with Google Drive...";

                        try
                        {
                            if (System.IO.File.Exists(localDbPath))
                            {
                                System.IO.File.Delete(localDbPath);
                            }

                            using (var connection = DatabaseHelper.GetConnection())
                            {
                                string createTablesQuery = @"
                                    CREATE TABLE IF NOT EXISTS Users (
                                        Id INTEGER PRIMARY KEY AUTOINCREMENT, 
                                        Username TEXT, 
                                        PasswordHash TEXT, 
                                        Salt TEXT
                                    );
                                    CREATE TABLE IF NOT EXISTS Credentials (
                                        Id INTEGER PRIMARY KEY AUTOINCREMENT, 
                                        Title TEXT, 
                                        WebsiteUrl TEXT, 
                                        Username TEXT, 
                                        EncryptedPassword TEXT, 
                                        Notes TEXT
                                    );";

                                using (var cmd = new System.Data.SQLite.SQLiteCommand(createTablesQuery, connection))
                                {
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            string salt = CryptoHelper.GenerateSalt();
                            string hash = CryptoHelper.HashPassword(password, salt);

                            string insertQuery = "INSERT INTO Users (Username, PasswordHash, Salt) VALUES ('default_user', @hash, @salt);";
                            using (var connection = DatabaseHelper.GetConnection())
                            using (var insertCommand = new System.Data.SQLite.SQLiteCommand(insertQuery, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@hash", hash);
                                insertCommand.Parameters.AddWithValue("@salt", salt);
                                insertCommand.ExecuteNonQuery();
                            }

                            bool uploaded = await GoogleDriveHelper.UploadBackupToGoogleDriveAsync(localDbPath);
                            if (uploaded)
                            {
                                lblErrorMessage.Foreground = System.Windows.Media.Brushes.Green;
                                lblErrorMessage.Text = "Vault created and synchronized with Google Drive successfully!";
                                canProceed = true;
                            }
                            else
                            {
                                lblErrorMessage.Foreground = System.Windows.Media.Brushes.Red;
                                lblErrorMessage.Text = "Failed to upload the new vault to Google Drive.";
                                canProceed = false;
                            }
                        }
                        catch (Exception exCreate)
                        {
                            lblErrorMessage.Foreground = System.Windows.Media.Brushes.Red;
                            lblErrorMessage.Text = $"Critical error while creating vault: {exCreate.Message}";
                            canProceed = false;
                        }
                    }
                }
                else
                {
                    DatabaseHelper.CurrentDbName = "vault_local.db";
                }

                // Validação e carregamento do cofre correspondente
                if (canProceed)
                {
                    using (var connection = DatabaseHelper.GetConnection())
                    {
                        string query = "SELECT PasswordHash, Salt FROM Users LIMIT 1;";
                        using (var command = new SQLiteCommand(query, connection))
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string storedHash = reader["PasswordHash"].ToString();
                                string storedSalt = reader["Salt"].ToString();

                                string computedHash = CryptoHelper.HashPassword(password, storedSalt);

                                if (computedHash == storedHash)
                                {
                                    MainWindow dashboard = new MainWindow(password);
                                    dashboard.Show();
                                    this.Close();
                                    return;
                                }
                                else
                                {
                                    lblErrorMessage.Foreground = System.Windows.Media.Brushes.Red;
                                    lblErrorMessage.Text = "Invalid master password.";
                                }
                            }
                            else
                            {
                                reader.Close();

                                if (CreateLocalVaultAndUser(password))
                                {
                                    MainWindow dashboard = new MainWindow(password);
                                    dashboard.Show();
                                    this.Close();
                                    return;
                                }
                                else
                                {
                                    lblErrorMessage.Foreground = System.Windows.Media.Brushes.Red;
                                    lblErrorMessage.Text = "Error creating the vault automatically.";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lblErrorMessage.Foreground = System.Windows.Media.Brushes.Red;
                lblErrorMessage.Text = $"Critical error: {ex.Message}";
            }
            finally
            {
                if (btnLogin != null)
                {
                    btnLogin.IsEnabled = true;
                }
            }
        }

        private void TxtMasterPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnLogin_Click(sender, e);
            }
        }

        private void CmbStorageMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbStorageMode == null || btnLogin == null) return;

            if (cmbStorageMode.SelectedIndex == 0)
            {
                DatabaseHelper.CurrentDbName = "vault_local.db";
                btnLogin.Content = "Unlock Local Vault";
            }
            else if (cmbStorageMode.SelectedIndex == 1)
            {
                //DatabaseHelper.CurrentDbName = "vault_onedrive.db";
                btnLogin.Content = "Sync and Sign In (Microsoft)";
            }
            else if (cmbStorageMode.SelectedIndex == 2)
            {
                //DatabaseHelper.CurrentDbName = "vault_gdrive.db";
                btnLogin.Content = "Sync and Sign In (Google)";
            }
        }

        private bool CreateLocalVaultAndUser(string password)
        {
            try
            {
                // Garante que o ficheiro da base de dados e as tabelas são criadas
                DatabaseHelper.InitializeDatabase();

                string salt = CryptoHelper.GenerateSalt();
                string hash = CryptoHelper.HashPassword(password, salt);

                using (var connection = DatabaseHelper.GetConnection())
                {
                    string insertQuery = "INSERT INTO Users (Username, PasswordHash, Salt) VALUES ('default_user', @hash, @salt);";
                    using (var insertCommand = new SQLiteCommand(insertQuery, connection))
                    {
                        insertCommand.Parameters.AddWithValue("@hash", hash);
                        insertCommand.Parameters.AddWithValue("@salt", salt);
                        insertCommand.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}