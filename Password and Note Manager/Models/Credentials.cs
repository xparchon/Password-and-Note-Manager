using System;

namespace Password_and_Note_Manager.Models
{
    public class Credential
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string WebsiteUrl { get; set; }
        public string Username { get; set; }
        public string EncryptedPassword { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }

        // This property is used to determine if the password should be displayed in plain text or masked in the UI
        public bool IsPasswordRevealed { get; set; } = false;
    }
}
