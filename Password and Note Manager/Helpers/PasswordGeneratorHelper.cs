using System;
using System.Security.Cryptography;
using System.Text;

namespace Password_and_Note_Manager.Helpers
{
    public static class PasswordGeneratorHelper
    {
        private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
        private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string Numbers = "0123456789";
        private const string Specials = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        // Generates a random password based on the specified criteria
        public static string GeneratePassword(int length = 16, bool useUpper = true, bool useLower = true, bool useNumbers = true, bool useSpecials = true)
        {
            var charSet = new StringBuilder();
            if (useLower) charSet.Append(Lowercase);
            if (useUpper) charSet.Append(Uppercase);
            if (useNumbers) charSet.Append(Numbers);
            if (useSpecials) charSet.Append(Specials);

            if (charSet.Length == 0)
                throw new ArgumentException("Selecione pelo menos um conjunto de caracteres.");

            char[] chars = charSet.ToString().ToCharArray();
            byte[] data = new byte[length];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }

            var result = new StringBuilder(length);
            foreach (byte b in data)
            {
                result.Append(chars[b % chars.Length]);
            }

            return result.ToString();
        }
    }
}