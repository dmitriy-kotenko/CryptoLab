using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;
using Org.BouncyCastle.OpenSsl;

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

        public async Task SendMessage(string toUser, string message)
        {
            IdentityUser user = await _userManager.FindByEmailAsync(toUser);

            await Clients.User(user.Id).SendAsync("ReceiveMessage", message);
        }

        public void SetClientPublicKey(string encryptedClientPublicKey)
        {
            string rsaPrivateKey = File.ReadAllText(@"server_2048_rsa_priv.pem");

            var privateKeyStringReader = new StringReader(rsaPrivateKey);
            var privateKeyPemReader = new PemReader(privateKeyStringReader);
            var privateKeyPem = privateKeyPemReader.ReadPemObject();

            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPrivateKey(privateKeyPem.Content, out int _);

                var encryptedDataBytes = Convert.FromBase64String(encryptedClientPublicKey);
                var decryptedClientPublicKeyBytes = rsa.Decrypt(encryptedDataBytes, RSAEncryptionPadding.Pkcs1);

                var decryptedClientPublicKey = System.Text.Encoding.UTF8.GetString(decryptedClientPublicKeyBytes);

                string userName = Context.User.Identity.Name;
                _userPublicKeys[userName] = decryptedClientPublicKey;
            }
        }
    }
}
