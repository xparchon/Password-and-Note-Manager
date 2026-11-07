using Password_and_Note_Manager.Helpers;
using System;
using System.Data.SQLite;
using System.Windows;

namespace Password_and_Note_Manager.Views
{
    public partial class AddCredentialWindow : Window
    {
        private string _masterKey;
        // Propriedade pública para indicar à MainWindow se alguma credencial foi salva
        public bool HasAddedCredentials { get; private set; } = false;

        public AddCredentialWindow(string masterKey)
        {
            InitializeComponent();
            _masterKey = masterKey;
        }

        // Fecha a janela sem salvar nada
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Processa, criptografa e salva os dados no SQLite
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string title = txtTitle.Text.Trim();
            string url = txtUrl.Text.Trim();
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text; // Mantém espaços se houver na senha
            string notes = txtNotes.Text.Trim();

            // Validação simples de campos obrigatórios
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Title, Username, and Password are required fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // CRIPTOGRAFIA: Tranca a senha usando AES-256 e a chave mestre obtida no login
                string encryptedPassword = CryptoHelper.Encrypt(password, _masterKey);

                // Gravação segura no banco de dados SQLite
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

                // Marca que foi adicionada pelo menos uma credencial na sessão
                HasAddedCredentials = true;

                // Limpa os campos para o próximo cadastro em massa
                txtTitle.Clear();
                txtUrl.Clear();
                txtUsername.Clear();
                txtPassword.Clear();
                txtNotes.Clear();
                txtTitle.Focus(); // Retorna o foco para o primeiro campo

                // Exibe o aviso verde subtil na interface
                txtSuccessMessage.Visibility = Visibility.Visible;

                // Faz o aviso desaparecer automaticamente após 3 segundos
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
                // Gera uma senha segura de 16 caracteres usando o helper que criámos
                string generatedPassword = Password_and_Note_Manager.Helpers.PasswordGeneratorHelper.GeneratePassword(16);

                // Preenche a TextBox com a senha gerada
                txtPassword.Text = generatedPassword;

                // Copia automaticamente para a área de transferência do Windows
                Clipboard.SetText(generatedPassword);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao gerar senha: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
