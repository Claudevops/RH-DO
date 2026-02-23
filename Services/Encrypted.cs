using System.Security.Cryptography;
using System.Text;

[AttributeUsage(AttributeTargets.Property)]
public class EncryptedDataAttribute : Attribute
{
    public EncryptedDataAttribute()
    {
    }
}

public class EncryptionService
{
private readonly byte[] _key = Encoding.UTF8.GetBytes("12345678901234567890123456789012");    
    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;
        
        using (Aes aes = Aes.Create())
        {
            aes.Key = _key;
            aes.GenerateIV();
            
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            
            using (MemoryStream msEncrypt = new MemoryStream())
            {
                msEncrypt.Write(aes.IV, 0, aes.IV.Length);
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                {
                    swEncrypt.Write(plaintext);
                }
                return Convert.ToBase64String(msEncrypt.ToArray());
            }
        }
    }
    
    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return ciphertext;
        
        try
        {
            byte[] fullCipher = Convert.FromBase64String(ciphertext);
            
            using (Aes aes = Aes.Create())
            {
                aes.Key = _key;
                byte[] iv = new byte[aes.BlockSize / 8];
                byte[] cipher = new byte[fullCipher.Length - iv.Length];
                
                Array.Copy(fullCipher, iv, iv.Length);
                Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);
                
                aes.IV = iv;
                
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                
                using (MemoryStream msDecrypt = new MemoryStream(cipher))
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }
        catch
        {
            return ciphertext;
        }
    }
}