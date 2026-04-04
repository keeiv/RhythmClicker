using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClickerGame
{
    public record Account(string Username, string PasswordHash);

    public class AccountsManager
    {
        readonly string rcPath;   // encrypted .rc
        readonly string jsonPath; // legacy fallback
        List<Account> accounts = new();

        // Currently logged-in user (null = guest)
        public string? LoggedInUser { get; private set; }

        public AccountsManager(string dataPath = "Accounts/accounts.rc")
        {
            rcPath = dataPath;
            jsonPath = Path.ChangeExtension(dataPath, ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(rcPath) ?? "Accounts");
            Load();
        }

        void Load()
        {
            // Try encrypted .rc first
            if (File.Exists(rcPath))
            {
                try
                {
                    accounts = RcFileManager.ReadEncrypted<List<Account>>(rcPath);
                    return;
                }
                catch { /* fall through to legacy */ }
            }
            // Legacy JSON migration
            if (File.Exists(jsonPath))
            {
                try
                {
                    var s = File.ReadAllText(jsonPath);
                    accounts = JsonSerializer.Deserialize<List<Account>>(s) ?? new List<Account>();
                    Save(); // migrate to .rc
                    return;
                }
                catch { }
            }
            accounts = new List<Account>();
        }

        void Save()
        {
            RcFileManager.WriteEncrypted(rcPath, accounts);
        }

        static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var b = Encoding.UTF8.GetBytes(password);
            var h = sha.ComputeHash(b);
            return Convert.ToHexString(h);
        }

        public bool Register(string username, string password, out string message)
        {
            username = username?.Trim() ?? "";
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) { message = "Username and password required"; return false; }
            if (accounts.Any(a => string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase))) { message = "Username already exists"; return false; }
            var h = HashPassword(password);
            accounts.Add(new Account(username, h));
            Save();
            LoggedInUser = username;
            message = "Registered";
            return true;
        }

        public bool Login(string username, string password, out string message)
        {
            username = username?.Trim() ?? "";
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) { message = "Username and password required"; return false; }
            var h = HashPassword(password);
            var found = accounts.FirstOrDefault(a => string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase) && a.PasswordHash == h);
            if (found == null) { message = "Invalid username or password"; return false; }
            LoggedInUser = found.Username;
            message = "Login successful";
            return true;
        }

        public void Logout() => LoggedInUser = null;

        public bool Authenticate(string username, string password)
        {
            var h = HashPassword(password);
            return accounts.Any(a => string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase) && a.PasswordHash == h);
        }
    }
}
