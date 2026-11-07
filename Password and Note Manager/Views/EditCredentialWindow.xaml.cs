using System;
using System.Data.SQLite;
using System.Windows;
using Password_and_Note_Manager.Helpers;
using Password_and_Note_Manager.Models;

namespace Password_and_Note_Manager.Views
{
    public partial class EditCredentialWindow : Window
    {
        private readonly Credential _credential;
        private readonly string _masterKey;
        public bool HasEdited { get; private set; } = false;

        public EditCredentialWindow(Credential credential, string masterKey)
        {
            InitializeComponent();
            _credential = credential;
            _masterKey = masterKey;

            // Preenche os campos com os dados atuais
            txtTitle.Text = _credential.Title;
            txtWebsite.Text = _credential.WebsiteUrl;
            txtUsername.Text = _credential.Username;
            txtNotes.Text = _credential.Notes;

            try
            {
                // Descriptografa a senha para exibição no campo de edição
                txtPassword.Text = CryptoHelper.Decrypt(_credential.EncryptedPassword, _masterKey);
            }
            catch
            {
                txtPassword.Text = string.Empty;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text) || string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Preencha os campos obrigatórios.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Criptografa novamente a senha atualizada
                string encryptedPassword = CryptoHelper.Encrypt(txtPassword.Text, _masterKey);

                using (var connection = DatabaseHelper.GetConnection())
                {
                    string query = @"UPDATE Credentials 
                                     SET Title = @Title, WebsiteUrl = @WebsiteUrl, Username = @Username, EncryptedPassword = @EncryptedPassword, Notes = @Notes 
                                     WHERE Id = @Id;";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Title", txtTitle.Text.Trim());
                        command.Parameters.AddWithValue("@WebsiteUrl", txtWebsite.Text.Trim());
                        command.Parameters.AddWithValue("@Username", txtUsername.Text.Trim());
                        command.Parameters.AddWithValue("@EncryptedPassword", encryptedPassword);
                        command.Parameters.AddWithValue("@Notes", txtNotes.Text.Trim());
                        command.Parameters.AddWithValue("@Id", _credential.Id);

                        command.ExecuteNonQuery();
                    }
                }

                HasEdited = true;
                Close();
            }
            catch(Exception ex)
            {
                MessageBox.Show($"Erro ao atualizar a credencial: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}