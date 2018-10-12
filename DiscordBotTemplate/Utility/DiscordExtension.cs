using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DIBot;
using Discord.WebSocket;

namespace DiscordBotTemplate.Utility
{
    public static class DiscordExtension
    {
        public static bool HasPermission(this IUser user, string command, SocketGuild guild = null)
        {
            return ConfigUtil.Config.Permissions.HasPermission(user, command, guild);
        }

        public static bool HasPermission(this IRole role, string command)
        {
            return ConfigUtil.Config.Permissions.HasPermission(role, command);
        }
    }
}
