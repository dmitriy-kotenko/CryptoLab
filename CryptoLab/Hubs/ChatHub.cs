using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CryptoLab.Hubs
{
    public class ChatHub : Hub
    {
        public static ConcurrentDictionary<string, List<string>> ConnectedUsers = new ConcurrentDictionary<string, List<string>>();
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
            ConnectedUsers.TryAdd(userName, existingUserConnectionIds);

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
                    ConnectedUsers.TryRemove(userName, out List<string> _);
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
    }
}
