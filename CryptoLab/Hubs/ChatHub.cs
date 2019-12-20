using CryptoLab.Encryption;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CryptoLab.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly Dictionary<string, List<string>> ConnectedUsers = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, string> UserPublicKeys = new Dictionary<string, string>();

        private readonly UserManager<IdentityUser> _userManager;
        private readonly IRsaEncryptor _rsaEncryptor;

        private string UserName => Context.User.Identity.Name;

        public ChatHub(UserManager<IdentityUser> userManager, IRsaEncryptor rsaEncryptor)
        {
            _userManager = userManager;
            _rsaEncryptor = rsaEncryptor ?? throw new ArgumentNullException(nameof(rsaEncryptor));
        }

        public override async Task OnConnectedAsync()
        {
            if (!ConnectedUsers.ContainsKey(UserName))
            {
                ConnectedUsers.Add(UserName, new List<string>());
                await Clients.Others.SendAsync("ClientConnected", UserName);
            }

            ConnectedUsers[UserName].Add(Context.ConnectionId);

            await Clients.Caller.SendAsync("UserList", ConnectedUsers.Keys);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (ConnectedUsers.TryGetValue(UserName, out List<string> existingUserConnectionIds))
            {
                existingUserConnectionIds.Remove(Context.ConnectionId);

                if (existingUserConnectionIds.Count == 0)
                {
                    ConnectedUsers.Remove(UserName);
                    await Clients.Others.SendAsync("ClientDisconnected", UserName);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public void SetClientPublicKey(string encryptedClientPublicKey)
        {
            var encryptedClientPublicKeyBytes = Convert.FromBase64String(encryptedClientPublicKey);
            byte[] decryptedClientPublicKeyBytes = _rsaEncryptor.Decrypt(encryptedClientPublicKeyBytes);

            UserPublicKeys[UserName] = Encoding.UTF8.GetString(decryptedClientPublicKeyBytes);
        }

        public async Task<object> StartHandshake(string withUserName)
        {
            if (!UserPublicKeys.ContainsKey(withUserName))
            {
                throw new ArgumentException("User is not connected.");
            }

            string currentUserPublicKey = UserPublicKeys[UserName];
            string withUserPublicKey = UserPublicKeys[withUserName];

            byte[] currentUserEncryptedPublicKey = _rsaEncryptor.Encrypt(withUserPublicKey, currentUserPublicKey);
            byte[] currentUserPublicKeySignature = _rsaEncryptor.Sign(currentUserEncryptedPublicKey);

            string userId = await GetUserId(withUserName);
            await Clients.User(userId).SendAsync(
                "StartHandshakeRequested",
                UserName,
                Convert.ToBase64String(currentUserEncryptedPublicKey),
                Convert.ToBase64String(currentUserPublicKeySignature));

            byte[] withUserEncryptedPublicKeyBytes = _rsaEncryptor.Encrypt(currentUserPublicKey, withUserPublicKey);
            byte[] withUserPublicKeySignature = _rsaEncryptor.Sign(withUserEncryptedPublicKeyBytes);
            return new
            {
                selectedUserEncryptedPublicKey = Convert.ToBase64String(withUserEncryptedPublicKeyBytes),
                signature = Convert.ToBase64String(withUserPublicKeySignature)
            };
        }

        public async Task SubmitAesKey(string toUser, string encryptedAesKey)
        {
            var encryptedAesKeyBytes = Convert.FromBase64String(encryptedAesKey);
            byte[] aesKeyBytes = _rsaEncryptor.Decrypt(encryptedAesKeyBytes);
            byte[] signature = _rsaEncryptor.Sign(aesKeyBytes);

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
    }
}
