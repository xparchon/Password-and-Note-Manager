using Password_and_Note_Manager.Helpers;
using Password_and_Note_Manager.Models;
using System;
using System.Collections.Generic; // Garanta que esta lista está presente
using System.Linq;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;
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
            CheckVaultHash(); // Verifica a integridade do cofre ao abrir a tela
            // Carrega as senhas do banco logo após a abertura da tela
            LoadCredentials();
        }

        private void CheckVaultHash()
        {
            try
            {
                // Verifica qual cofre está ativo e executa a lógica correspondente
                if (DatabaseHelper.CurrentDbName == "vault_onedrive.db")
                {
                    // Sincroniza apenas se for OneDrive
                    Task.Run(async () =>
                    {
                        string localVaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseHelper.CurrentDbName);
                        await OneDriveHelper.SynchronizeVaultAsync(localVaultPath);
                    }).Wait();
                }
                else if (DatabaseHelper.CurrentDbName == "vault_gdrive.db")
                {
                    // Sincroniza apenas se for Google Drive
                    Task.Run(async () =>
                    {
                        string localVaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseHelper.CurrentDbName);
                        await GoogleDriveHelper.UploadBackupToGoogleDriveAsync(localVaultPath);
                    }).Wait();
                }
                else
                {
                    // Modo Local (vault_local.db ou vault.db): Apenas opera localmente, sem chamadas à nuvem
                    // Nenhuma ação extra necessária aqui.
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to synchronize. Using the local version.\n\nDetails: {ex.Message}", "Network Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 1. Carrega e lista as credenciais na barra lateral esquerda
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

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = txtSearch.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(searchText))
            {
                // Se a pesquisa estiver vazia, exibe todas as credenciais
                lstCredentials.ItemsSource = _credentialsList;
            }
            else
            {
                // Filtra a lista com base no Título ou no Username em tempo real
                var filteredList = _credentialsList
                    .Where(c => (c.Title != null && c.Title.ToLower().Contains(searchText)) ||
                                (c.Username != null && c.Username.ToLower().Contains(searchText)))
                    .ToList();

                lstCredentials.ItemsSource = filteredList;
            }
        }

        // Evento de seleção da lista lateral
        private void LstCredentials_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstCredentials.SelectedItem is Credential selected)
            {
                // Torna o painel de detalhes visível
                pnlDetails.Visibility = Visibility.Visible;

                lblSelectedTitle.Text = selected.Title;
                lblSelectedUrl.Text = string.IsNullOrWhiteSpace(selected.WebsiteUrl) ? "No URL" : selected.WebsiteUrl;
                txtDisplayUsername.Text = selected.Username;
                txtDisplayNotes.Text = selected.Notes;

                // Reseta a exibição para formato oculto ao trocar de item
                selected.IsPasswordRevealed = false;
                btnShowPassword.Content = "Reveal";
                txtDisplayPassword.Text = "••••••••••••";
            }
            else
            {
                // Oculta o painel se nenhum item estiver selecionado
                pnlDetails.Visibility = Visibility.Collapsed;
                lblSelectedTitle.Text = "Select an item from the list";
                lblSelectedUrl.Text = string.Empty;
            }
        }

        // Evento do botão de adicionar nova senha
        private void BtnNewCredential_Click(object sender, RoutedEventArgs e)
        {
            // Instancia a janela de inserção passando a chave mestre para criptografia
            Views.AddCredentialWindow addWindow = new Views.AddCredentialWindow(_masterKey);
            addWindow.Owner = this; // Centraliza a tela filha em cima da tela principal

            addWindow.ShowDialog();

            // Se o usuário adicionou pelo menos uma credencial durante a sessão, atualiza a lista ao fechar
            if (addWindow.HasAddedCredentials)
            {
                LoadCredentials();
                SaveChangesAndSync();
            }
        }

        // Evento do botão de revelar/ocultar senha
        private void BtnShowPassword_Click(object sender, RoutedEventArgs e)
        {
            if (lstCredentials.SelectedItem is Credential selected)
            {
                if (selected.IsPasswordRevealed)
                {
                    // Oculta novamente
                    txtDisplayPassword.Text = "••••••••••••";
                    btnShowPassword.Content = "Reveal";
                    selected.IsPasswordRevealed = false;
                }
                else
                {
                    try
                    {
                        // Descriptografa em tempo real usando a CryptoHelper e a chave mestre logada
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
                // Abre a janela de edição passando o item selecionado e a chave mestre
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

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Deseja fechar o cofre atual e voltar à tela de login?",
                                         "Trocar Cofre",
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

        private void SaveChangesAndSync()
        {
            LoadCredentials(); // Atualiza a interface local

            // Dispara o upload para a nuvem em segundo plano apenas quando há alterações
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
