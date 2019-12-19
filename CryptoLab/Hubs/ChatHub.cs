using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Org.BouncyCastle.OpenSsl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryptoLab.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly Dictionary<string, List<string>> ConnectedUsers = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, string> UserPublicKeys = new Dictionary<string, string>();

        private readonly UserManager<IdentityUser> _userManager;

        public ChatHub(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public override async Task OnConnectedAsync()
        {
            string userName = Context.User.Identity.Name;

            ConnectedUsers.TryGetValue(userName, out List<string> existingUserConnectionIds);
            if (existingUserConnectionIds == null)
            {
                existingUserConnectionIds = new List<string>();
                await Clients.Others.SendAsync("ClientConnected", userName);
            }

            existingUserConnectionIds.Add(Context.ConnectionId);
            ConnectedUsers.Add(userName, existingUserConnectionIds);

            await Clients.Caller.SendAsync("UserList", ConnectedUsers.Keys);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            string userName = Context.User.Identity.Name;

            ConnectedUsers.TryGetValue(userName, out List<string> existingUserConnectionIds);

            if (existingUserConnectionIds != null)
            {
                existingUserConnectionIds.Remove(Context.ConnectionId);

                if (existingUserConnectionIds.Count == 0)
                {
                    ConnectedUsers.Remove(userName);
                    await Clients.Others.SendAsync("ClientDisconnected", userName);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public void SetClientPublicKey(string encryptedClientPublicKey)
        {
            byte[] decryptedClientPublicKeyBytes = Decrypt(encryptedClientPublicKey);

            string userName = Context.User.Identity.Name;
            UserPublicKeys[userName] = Encoding.UTF8.GetString(decryptedClientPublicKeyBytes);
        }

        public async Task<object> StartHandshake(string withUserName)
        {
            if (!UserPublicKeys.ContainsKey(withUserName))
            {
                throw new ArgumentException("User is not connected.");
            }

            string currentUserName = Context.User.Identity.Name;
            string currentUserPublicKey = UserPublicKeys[currentUserName];
            string withUserPublicKey = UserPublicKeys[withUserName];

            byte[] currentUserEncryptedPublicKey = Encrypt(withUserPublicKey, currentUserPublicKey);
            byte[] currentUserPublicKeySignature = Sign(currentUserEncryptedPublicKey);

            string userId = await GetUserId(withUserName);
            await Clients.User(userId).SendAsync(
                "StartHandshakeRequested",
                currentUserName,
                Convert.ToBase64String(currentUserEncryptedPublicKey),
                Convert.ToBase64String(currentUserPublicKeySignature));

            byte[] withUserEncryptedPublicKeyBytes = Encrypt(currentUserPublicKey, withUserPublicKey);
            byte[] withUserPublicKeySignature = Sign(withUserEncryptedPublicKeyBytes);
            return new
            {
                selectedUserEncryptedPublicKey = Convert.ToBase64String(withUserEncryptedPublicKeyBytes),
                signature = Convert.ToBase64String(withUserPublicKeySignature)
            };
        }

        public async Task SubmitAesKey(string toUser, string encryptedAesKey)
        {
            byte[] aesKeyBytes = Decrypt(encryptedAesKey);
            byte[] signature = Sign(aesKeyBytes);

            string userId = await GetUserId(toUser);
            await Clients.User(userId).SendAsync("SetAesKey", aesKeyBytes, signature);
        }

        public async Task SendMessage(string toUser, string message)
        {
            string userId = await GetUserId(toUser);
            await Clients.User(userId).SendAsync("ReceiveMessage", message);
        }

        private async Task<string> GetUserId(string userEmail)
        {
            IdentityUser user = await _userManager.FindByEmailAsync(userEmail);
            return user.Id;
        }

        private static byte[] Encrypt(string publicKey, string text)
        {
            var encryptedBytes = new List<byte>();

            byte[] publicKeyBytes = ExtractKeyBytes(publicKey);

            using (RSA rsa = RSA.Create())
            {
                rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out int _);

                int chunkLength = CalculatePkcs1MaxChunkLength(rsa.KeySize);

                string[] textChunks = SplitString(text, chunkLength);
                foreach (string textChunk in textChunks)
                {
                    byte[] textBytes = Encoding.UTF8.GetBytes(textChunk);
                    byte[] encryptedTextBytes = rsa.Encrypt(textBytes, RSAEncryptionPadding.Pkcs1);
                    encryptedBytes.AddRange(encryptedTextBytes);
                }
            }

            return encryptedBytes.ToArray();
        }

        private static byte[] Decrypt(string encryptedText)
        {
            byte[] encryptedDataBytes = Convert.FromBase64String(encryptedText);

            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPrivateKey(GetServerPrivateKey(), out int _);
                return rsa.Decrypt(encryptedDataBytes, RSAEncryptionPadding.Pkcs1);
            }
        }

        private static byte[] Sign(byte[] data)
        {
            byte[] privateKeyBytes = GetServerPrivateKey();

            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPrivateKey(privateKeyBytes, out int _);

                return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        private static byte[] GetServerPrivateKey()
        {
            string rsaPrivateKey = File.ReadAllText(@"server_2048_rsa_priv.pem");
            return ExtractKeyBytes(rsaPrivateKey);
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

        private static string[] SplitString(string str, int chunkSize)
        {
            return Enumerable.Range(0, (int)Math.Ceiling((double)str.Length / chunkSize))
                .Select(i => str.Substring(
                    i * chunkSize,
                    (i * chunkSize + chunkSize <= str.Length)
                        ? chunkSize
                        : str.Length - i * chunkSize))
                .ToArray();
        }
    }
}
