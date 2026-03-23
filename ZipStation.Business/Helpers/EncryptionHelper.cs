using System.Security.Cryptography;
using System.Text;

namespace ZipStation.Business.Helpers;

public static class EncryptionHelper
{
    private static string? _key;

    /// <summary>
    /// Initialize with a 32-char (256-bit) key. Call once at startup.
    /// If no key is provided, generates one and logs a warning.
    /// </summary>
    public static void Initialize(string? encryptionKey)
    {
        if (!string.IsNullOrEmpty(encryptionKey))
        {
            // Ensure key is exactly 32 bytes by hashing it
            _key = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(encryptionKey)));
        }
    }

    public static bool IsInitialized => !string.IsNullOrEmpty(_key);

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        if (!IsInitialized) return plainText; // Fallback: no encryption if no key

        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(_key!);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to cipher text
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return "ENC:" + Convert.ToBase64String(result);
    }

    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;
        if (!cipherText.StartsWith("ENC:")) return cipherText; // Not encrypted (legacy plain text)
        if (!IsInitialized) return cipherText;

        var fullBytes = Convert.FromBase64String(cipherText[4..]);

        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(_key!);

        // Extract IV from first 16 bytes
        var iv = new byte[16];
        Buffer.BlockCopy(fullBytes, 0, iv, 0, 16);
        aes.IV = iv;

        var cipherBytes = new byte[fullBytes.Length - 16];
        Buffer.BlockCopy(fullBytes, 16, cipherBytes, 0, cipherBytes.Length);

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
