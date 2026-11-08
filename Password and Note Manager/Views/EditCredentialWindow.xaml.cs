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

            // Fill the fields with the current data
            txtTitle.Text = _credential.Title;
            txtWebsite.Text = _credential.WebsiteUrl;
            txtUsername.Text = _credential.Username;
            txtNotes.Text = _credential.Notes;

            try
            {
                // Decrypt the password for display in the edit field
                txtPassword.Text = CryptoHelper.Decrypt(_credential.EncryptedPassword, _masterKey);
            }
            catch
            {
                txtPassword.Text = string.Empty;
            }
        }

        // Saves the updated credential to the database
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text) || string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Please fill in the required fields.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Encrypt the new password
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
                MessageBox.Show($"Error updating the credential: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}