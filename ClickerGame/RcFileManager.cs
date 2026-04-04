using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClickerGame
{
    /// <summary>
    /// Custom encrypted file format for RhythmClicker.
    ///   .rcm  – beatmap
    ///   .rcp  – replay
    ///   .rc   – confidential / settings
    ///
    /// File layout:
    ///   [4 bytes] magic  ("RCM\x01" / "RCP\x01" / "RCF\x01")
    ///   [2 bytes] version (LE uint16 = 1)
    ///   [16 bytes] IV
    ///   [remaining] AES-256-CBC encrypted payload (PKCS7 padded JSON UTF-8)
    /// </summary>
    public static class RcFileManager
    {
        // Fixed key derived from a passphrase — keeps assets opaque to casual editing.
        // For a shipping game you would use per-user or per-build keys.
        private static readonly byte[] DefaultKey = DeriveKey("RhythmClicker-2026-RC");

        static byte[] DeriveKey(string passphrase)
        {
            // PBKDF2 with a fixed salt (acceptable for game-asset obfuscation)
            byte[] salt = Encoding.UTF8.GetBytes("RC_SALT_v1");
            using var kdf = new Rfc2898DeriveBytes(passphrase, salt, 100_000, HashAlgorithmName.SHA256);
            return kdf.GetBytes(32); // 256-bit key
        }

        static readonly byte[] MagicRcm = Encoding.ASCII.GetBytes("RCM\x01");
        static readonly byte[] MagicRcp = Encoding.ASCII.GetBytes("RCP\x01");
        static readonly byte[] MagicRcf = Encoding.ASCII.GetBytes("RCF\x01");

        static byte[] MagicFor(string ext)
        {
            return ext.ToLowerInvariant() switch
            {
                ".rcm" => MagicRcm,
                ".rcp" => MagicRcp,
                ".rc"  => MagicRcf,
                _      => MagicRcf,
            };
        }

        // ── Write ──────────────────────────────────────────────

        public static void WriteEncrypted<T>(string path, T data)
        {
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });
            byte[] plain = Encoding.UTF8.GetBytes(json);
            string ext = Path.GetExtension(path);
            byte[] magic = MagicFor(ext);

            using var aes = Aes.Create();
            aes.Key = DefaultKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            byte[] cipher;
            using (var enc = aes.CreateEncryptor())
                cipher = enc.TransformFinalBlock(plain, 0, plain.Length);

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);
            bw.Write(magic);                     // 4 bytes magic
            bw.Write((ushort)1);                 // 2 bytes version
            bw.Write(aes.IV);                    // 16 bytes IV
            bw.Write(cipher);                    // encrypted payload
        }

        // ── Read ───────────────────────────────────────────────

        public static T ReadEncrypted<T>(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            if (raw.Length < 22) // 4+2+16 minimum header
                throw new InvalidDataException($"File too small: {path}");

            string ext = Path.GetExtension(path);
            byte[] expectedMagic = MagicFor(ext);
            for (int i = 0; i < 4; i++)
            {
                if (raw[i] != expectedMagic[i])
                    throw new InvalidDataException($"Invalid file magic for {path}");
            }

            ushort version = BitConverter.ToUInt16(raw, 4);
            if (version != 1)
                throw new InvalidDataException($"Unsupported version {version} in {path}");

            byte[] iv = new byte[16];
            Array.Copy(raw, 6, iv, 0, 16);

            int cipherLen = raw.Length - 22;
            byte[] cipher = new byte[cipherLen];
            Array.Copy(raw, 22, cipher, 0, cipherLen);

            using var aes = Aes.Create();
            aes.Key = DefaultKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            byte[] plain;
            using (var dec = aes.CreateDecryptor())
                plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);

            string json = Encoding.UTF8.GetString(plain);
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? throw new InvalidDataException($"Deserialization returned null for {path}");
        }

        // ── Convenience: Beatmap (.rcm) ────────────────────────

        public static void WriteBeatmap(string path, Beatmap bm) => WriteEncrypted(path, bm);
        public static Beatmap ReadBeatmap(string path) => ReadEncrypted<Beatmap>(path);

        // ── Check if a legacy JSON beatmap exists and needs migration ──

        public static bool MigrateJsonToRcm(string jsonPath, string rcmPath)
        {
            if (!File.Exists(jsonPath)) return false;
            if (File.Exists(rcmPath)) return true; // already migrated

            var json = File.ReadAllText(jsonPath);
            var bm = Beatmap.LoadFromString(json);
            WriteBeatmap(rcmPath, bm);
            return true;
        }
    }
}
