using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;
using Org.BouncyCastle.OpenSsl;
using System.Text;
using System.Linq;

namespace CryptoLab.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly Dictionary<string, List<string>> _connectedUsers = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, string> _userPublicKeys = new Dictionary<string, string>();

        private readonly UserManager<IdentityUser> _userManager;

        public ChatHub(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public override async Task OnConnectedAsync()
        {
            string userName = Context.User.Identity.Name;

            _connectedUsers.TryGetValue(userName, out List<string> existingUserConnectionIds);
            if (existingUserConnectionIds == null)
            {
                existingUserConnectionIds = new List<string>();
                await Clients.Others.SendAsync("ClientConnected", userName);
            }

            existingUserConnectionIds.Add(Context.ConnectionId);
            _connectedUsers.Add(userName, existingUserConnectionIds);

            await Clients.Caller.SendAsync("UserList", _connectedUsers.Keys);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            string userName = Context.User.Identity.Name;

            _connectedUsers.TryGetValue(userName, out List<string> existingUserConnectionIds);

            if (existingUserConnectionIds != null)
            {
                existingUserConnectionIds.Remove(Context.ConnectionId);

                if (existingUserConnectionIds.Count == 0)
                {
                    _connectedUsers.Remove(userName);
                    await Clients.Others.SendAsync("ClientDisconnected", userName);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public void SetClientPublicKey(string encryptedClientPublicKey)
        {
            var decryptedClientPublicKeyBytes = Decrypt(encryptedClientPublicKey);

            string userName = Context.User.Identity.Name;
            _userPublicKeys[userName] = Encoding.UTF8.GetString(decryptedClientPublicKeyBytes);
        }

        public async Task<object> StartHandshake(string withUserName)
        {
            if (!_userPublicKeys.ContainsKey(withUserName))
            {
                throw new ArgumentException("User is not connected.");
            }

            var currentUserName = Context.User.Identity.Name;
            var currentUserPublicKey = _userPublicKeys[currentUserName];
            var withUserPublicKey = _userPublicKeys[withUserName];

            IdentityUser user = await _userManager.FindByEmailAsync(withUserName);
            byte[] currentUserEncryptedPublicKey = Encrypt(withUserPublicKey, currentUserPublicKey);
            byte[] currentUserPublicKeySignature = Sign(currentUserEncryptedPublicKey);

            await Clients.User(user.Id).SendAsync(
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
            var aesKeyBytes = Decrypt(encryptedAesKey);

            var signature = Sign(aesKeyBytes);

            IdentityUser user = await _userManager.FindByEmailAsync(toUser);
            await Clients.User(user.Id).SendAsync("SetAesKey", aesKeyBytes, signature);
        }

        public async Task SendMessage(string toUser, string message)
        {
            IdentityUser user = await _userManager.FindByEmailAsync(toUser);

            await Clients.User(user.Id).SendAsync("ReceiveMessage", message);
        }

        private byte[] ExtractKeyBytes(string pemFormattedKey)
        {
            var stringReader = new StringReader(pemFormattedKey);
            var pemReader = new PemReader(stringReader);
            var pem = pemReader.ReadPemObject();

            return pem.Content;
        }

        private byte[] Encrypt(string publicKey, string text)
        {
            var encryptedBytes = new List<byte>();

            byte[] publicKeyBytes = ExtractKeyBytes(publicKey);

            using (RSA rsa = RSA.Create())
            {
                rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out int _);

                int chunkLength = CalculatePkcs1MaxChunkLength(rsa.KeySize);

                var textChunks = SplitString(text, chunkLength);
                foreach (var textChunk in textChunks)
                {
                    var textBytes = Encoding.UTF8.GetBytes(textChunk);
                    var encryptedTextBytes = rsa.Encrypt(textBytes, RSAEncryptionPadding.Pkcs1);
                    encryptedBytes.AddRange(encryptedTextBytes);
                }
            }

            return encryptedBytes.ToArray();
        }

        private byte[] Decrypt(string encryptedText)
        {
            var encryptedDataBytes = Convert.FromBase64String(encryptedText);

            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPrivateKey(GetServerPrivateKey(), out int _);
                return rsa.Decrypt(encryptedDataBytes, RSAEncryptionPadding.Pkcs1);
            }
        }

        private byte[] Sign(byte[] data)
        {
            byte[] privateKeyBytes = GetServerPrivateKey();

            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPrivateKey(privateKeyBytes, out int _);

                return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        private byte[] GetServerPrivateKey()
        {
            string rsaPrivateKey = File.ReadAllText(@"server_2048_rsa_priv.pem");
            return ExtractKeyBytes(rsaPrivateKey);
        }

        private int CalculatePkcs1MaxChunkLength(int keySize)
        {
            return keySize / 8 - 11;
        }

        private IEnumerable<string> SplitString(string str, int chunkSize)
        {
            return Enumerable.Range(0, (int)Math.Ceiling((double)str.Length / chunkSize))
                .Select(i => str.Substring(
                    i * chunkSize,
                    (i * chunkSize + chunkSize <= str.Length)
                        ? chunkSize
                        : str.Length - i * chunkSize));
        }
    }
}
