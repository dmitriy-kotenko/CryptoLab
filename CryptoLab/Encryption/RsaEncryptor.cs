using CryptoLab.Extensions;
using Org.BouncyCastle.OpenSsl;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoLab.Encryption
{
    public class RsaEncryptor : IRsaEncryptor
    {
        private readonly byte[] _privateKeyBytes;

        public RsaEncryptor()
        {
            string rsaPrivateKey = File.ReadAllText(@"server_2048_rsa_priv.pem");
            _privateKeyBytes = ExtractKeyBytes(rsaPrivateKey);
        }

        public byte[] Encrypt(string publicKey, string text)
        {
            var encryptedBytes = new List<byte>();

            byte[] publicKeyBytes = ExtractKeyBytes(publicKey);

            using (RSA rsa = RSA.Create())
            {
                rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out int _);

                int chunkLength = CalculatePkcs1MaxChunkLength(rsa.KeySize);

                foreach (string textChunk in text.Split(chunkLength))
                {
                    byte[] textBytes = Encoding.UTF8.GetBytes(textChunk);
                    byte[] encryptedTextBytes = rsa.Encrypt(textBytes, RSAEncryptionPadding.Pkcs1);
                    encryptedBytes.AddRange(encryptedTextBytes);
                }
            }

            return encryptedBytes.ToArray();
        }

        public byte[] Decrypt(byte[] encryptedData)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPrivateKey(_privateKeyBytes, out int _);
                return rsa.Decrypt(encryptedData, RSAEncryptionPadding.Pkcs1);
            }
        }

        public byte[] Sign(byte[] data)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPrivateKey(_privateKeyBytes, out int _);
                return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        private static byte[] ExtractKeyBytes(string pemFormattedKey)
        {
            var stringReader = new StringReader(pemFormattedKey);
            var pemReader = new PemReader(stringReader);
            var pem = pemReader.ReadPemObject();

            return pem.Content;
        }

        private static int CalculatePkcs1MaxChunkLength(int keySize)
        {
            return keySize / 8 - 11;
        }
    }
}
