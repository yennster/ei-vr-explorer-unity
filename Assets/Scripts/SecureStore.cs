using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace EI.VR
{
    /// <summary>
    /// AES-encrypted wrapper around PlayerPrefs. The key is derived from a
    /// per-install salt stored in PlayerPrefs; this is obfuscation, not real
    /// security against a determined attacker with physical access. Good
    /// enough for a single-user portfolio demo.
    /// </summary>
    public static class SecureStore
    {
        private const string SaltKey = "ei.salt";

        public static void Save(string key, string value)
        {
            if (value == null) { PlayerPrefs.DeleteKey(key); return; }
            var (k, iv) = Material();
            using var aes = Aes.Create();
            aes.Key = k; aes.IV = iv;
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs)) sw.Write(value);
            PlayerPrefs.SetString(key, Convert.ToBase64String(ms.ToArray()));
            PlayerPrefs.Save();
        }

        public static string Load(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return null;
            try
            {
                var (k, iv) = Material();
                using var aes = Aes.Create();
                aes.Key = k; aes.IV = iv;
                var bytes = Convert.FromBase64String(PlayerPrefs.GetString(key));
                using var ms = new MemoryStream(bytes);
                using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                return sr.ReadToEnd();
            }
            catch { return null; }
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        private static (byte[] key, byte[] iv) Material()
        {
            var salt = PlayerPrefs.GetString(SaltKey, "");
            if (string.IsNullOrEmpty(salt))
            {
                var bytes = new byte[16];
                RandomNumberGenerator.Fill(bytes);
                salt = Convert.ToBase64String(bytes);
                PlayerPrefs.SetString(SaltKey, salt);
            }
            using var derive = new Rfc2898DeriveBytes(
                Application.identifier, Encoding.UTF8.GetBytes(salt), 10_000, HashAlgorithmName.SHA256);
            return (derive.GetBytes(32), derive.GetBytes(16));
        }
    }
}
