using System;
using System.IO;
using System.Data.SQLite;

namespace Password_and_Note_Manager.Helpers
{
    public static class DatabaseHelper
    {
        // Default vault name and dynamic update (ex: vault_local.db, vault_onedrive.db)
        public static string CurrentDbName { get; set; } = "vault.db";

        // Dynamic connection string maintaining WAL mode active
        private static string ConnectionString => $"Data Source={CurrentDbName};Version=3;journal_mode=WAL;";

        // Ensures that the database file and essential tables exist whenever any part of the app requests a connection
        public static void InitializeDatabase()
        {
            // Create the database file if it doesn't exist
            if (!File.Exists(CurrentDbName))
            {
                SQLiteConnection.CreateFile(CurrentDbName);
            }

            // Create the necessary tables if they don't exist
            using (var connection = new SQLiteConnection(ConnectionString))
            {
                connection.Open();

                string createUsersTable = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT UNIQUE NOT NULL,
                        PasswordHash TEXT NOT NULL,
                        Salt TEXT NOT NULL
                    );";

                string createCredentialsTable = @"
                    CREATE TABLE IF NOT EXISTS Credentials (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        WebsiteUrl TEXT,
                        Username TEXT NOT NULL,
                        EncryptedPassword TEXT NOT NULL,
                        Notes TEXT,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    );";

                // Execute the table creation command
                using (var command = new SQLiteCommand(createUsersTable, connection))
                {
                    command.ExecuteNonQuery();
                }

                // Execute the table creation command for adding Credentials
                using (var command = new SQLiteCommand(createCredentialsTable, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        // Provides a connection to the SQLite database, ensuring that the database and tables are initialized first
        public static SQLiteConnection GetConnection()
        {
            InitializeDatabase();
            var connection = new SQLiteConnection(ConnectionString);
            connection.Open();
            return connection;
        }
    }
}