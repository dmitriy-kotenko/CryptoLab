using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace CryptoLab.Hubs
{
    public class ChatHub : Hub
    {
        private readonly UserManager<IdentityUser> _userManager;

        public ChatHub(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task SendMessage(string toUser, string message)
        {
            IdentityUser user = await _userManager.FindByEmailAsync(toUser);

            await Clients.User(user.Id).SendAsync("ReceiveMessage", message);
        }
    }
}
