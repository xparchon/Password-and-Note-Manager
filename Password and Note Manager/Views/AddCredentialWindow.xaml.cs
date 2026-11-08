using Password_and_Note_Manager.Helpers;
using System;
using System.Data.SQLite;
using System.Windows;

namespace Password_and_Note_Manager.Views
{
    public partial class AddCredentialWindow : Window
    {
        private string _masterKey;
        // public property for indicating to MainWindow if any credential was saved
        public bool HasAddedCredentials { get; private set; } = false;

        public AddCredentialWindow(string masterKey)
        {
            InitializeComponent();
            _masterKey = masterKey;
        }

        // Closes the window without saving anything
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Processes, encrypts, and saves the data in SQLite
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string title = txtTitle.Text.Trim();
            string url = txtUrl.Text.Trim();
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text; // Maintains spaces if present in the password
            string notes = txtNotes.Text.Trim();

            // Simple validation of required fields
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Title, Username, and Password are required fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Cryptography: Lock the password using AES-256 and the master key obtained at login
                string encryptedPassword = CryptoHelper.Encrypt(password, _masterKey);

                // Secure writing to the SQLite database
                using (var connection = DatabaseHelper.GetConnection())
                {
                    string query = @"
                        INSERT INTO Credentials (Title, WebsiteUrl, Username, EncryptedPassword, Notes) 
                        VALUES (@title, @url, @username, @encryptedPassword, @notes);";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@title", title);
                        command.Parameters.AddWithValue("@url", url);
                        command.Parameters.AddWithValue("@username", username);
                        command.Parameters.AddWithValue("@encryptedPassword", encryptedPassword);
                        command.Parameters.AddWithValue("@notes", notes);

                        command.ExecuteNonQuery();
                    }
                }

                // Mark that at least one credential was added in the session
                HasAddedCredentials = true;

                // Clear the fields for the next bulk registration
                txtTitle.Clear();
                txtUrl.Clear();
                txtUsername.Clear();
                txtPassword.Clear();
                txtNotes.Clear();
                txtTitle.Focus(); // Returns the focus to the first field

                // Display the subtle green alert in the interface
                txtSuccessMessage.Visibility = Visibility.Visible;

                // Configure alert message to disappear automatically after 3 seconds
                Task.Delay(3000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => {
                        txtSuccessMessage.Visibility = Visibility.Collapsed;
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving credential: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Generates a secure password of 16 characters using the helper we created
                string generatedPassword = Password_and_Note_Manager.Helpers.PasswordGeneratorHelper.GeneratePassword(16);

                // Fills the TextBox with the generated password
                txtPassword.Text = generatedPassword;

                // Copies the generated password to the Windows clipboard
                Clipboard.SetText(generatedPassword);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating password: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
