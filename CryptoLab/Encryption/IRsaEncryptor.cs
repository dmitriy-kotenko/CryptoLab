namespace CryptoLab.Encryption
{
    public interface IRsaEncryptor
    {
        byte[] Encrypt(string publicKey, string text);
        
        byte[] Decrypt(byte[] encryptedData);

        byte[] Sign(byte[] data);
    }
}