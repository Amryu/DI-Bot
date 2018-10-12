using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using DIBot;

namespace DiscordBotTemplate.Modules
{
    [Name("Permission")]
    public class ModulePermission : ModuleBase<SocketCommandContext>
    {
        public ModulePermission()
        {

        }

        [Command("perm.grant")]
        public async Task PermGrant(IUser user, string module, string command = "*", bool global = false)
        {
            if (!ConfigUtil.Config.Permissions.HasPermission(Context.User, "perm.grant") && global)
            {
                await ReplyAsync("You are not authorized to edit global permissions!");

                return;
            }

            if (user == null)
            {
                await ReplyAsync("User does not exist!");

                return;
            }

            var moduleType = GetType().Assembly
                .GetTypes()
                .FirstOrDefault(x => x.GetCustomAttributes(typeof(NameAttribute), true).Length == 1 &&
                                     ((NameAttribute)x.GetCustomAttributes(typeof(NameAttribute), true)[0]).Text == module);

            if (moduleType == null)
            {
                await ReplyAsync($"The module '{module}' does not exist!");

                return;
            }

            if (command == "*")
            {
                var commands = moduleType.GetMethods()
                    .Where(x => x.GetCustomAttributes(typeof(CommandAttribute), true).Length == 1)
                    .Select(x => ((CommandAttribute)x.GetCustomAttributes(typeof(CommandAttribute), true)[0]).Text)
                    .Distinct();

                foreach (var cmd in commands)
                {

                    if (!ConfigUtil.Config.Permissions.HasPermission(user, cmd, global ? null : Context.Guild) &&
                        !ConfigUtil.Config.Permissions.AddPermission(user, cmd, global ? null : Context.Guild))
                    {
                        await ReplyAsync($"Could not grant permission for command {cmd}!");
                    }
                }

                await ReplyAsync("Permissions granted!");
            }
            else
            {
                if (!moduleType.GetMethods()
                    .Any(x => x.GetCustomAttributes(typeof(CommandAttribute), true).Length == 1 &&
                              ((CommandAttribute)x.GetCustomAttributes(typeof(CommandAttribute), true)[0]).Text ==
                              command.ToLowerInvariant()))
                {
                    await ReplyAsync($"No such command in module '{module}'!");

                    return;
                }

                if (ConfigUtil.Config.Permissions.AddPermission(user, command.ToLowerInvariant(), global ? null : Context.Guild))
                {
                    await ReplyAsync("Permission granted!");
                }
                else
                {
                    await ReplyAsync("Could not grant permission!");
                }
            }

            ConfigUtil.Save();
        }

        [Command("perm.revoke")]
        public async Task PermRevoke(IUser user, string module, string command = "*", bool global = false)
        {
            if (!ConfigUtil.Config.Permissions.HasPermission(Context.User, "perm.grant") && global)
            {
                await ReplyAsync("You are not authorized to edit global permissions!");

                return;
            }

            if (user == null)
            {
                await ReplyAsync("User does not exist!");

                return;
            }

            var moduleType = GetType().Assembly
                .GetTypes()
                .FirstOrDefault(x => x.GetCustomAttributes(typeof(NameAttribute), true).Length == 1 &&
                                     ((NameAttribute)x.GetCustomAttributes(typeof(NameAttribute), true)[0]).Text == module);

            if (moduleType == null)
            {
                await ReplyAsync($"The module '{module}' does not exist!");

                return;
            }

            if (command == "*")
            {
                var commands = moduleType.GetMethods()
                    .Where(x => x.GetCustomAttributes(typeof(CommandAttribute), true).Length == 1)
                    .Select(x => ((CommandAttribute)x.GetCustomAttributes(typeof(CommandAttribute), true)[0]).Text)
                    .Distinct();

                foreach (var cmd in commands)
                {
                    ConfigUtil.Config.Permissions.DeletePermission(user, cmd, global ? null : Context.Guild);
                }

                await ReplyAsync("Permissions revoked!");
            }
            else
            {
                if (!moduleType.GetMethods()
                    .Any(x => x.GetCustomAttributes(typeof(CommandAttribute), true).Length == 1 &&
                              ((CommandAttribute)x.GetCustomAttributes(typeof(CommandAttribute), true)[0]).Text ==
                              command.ToLowerInvariant()))
                {
                    await ReplyAsync($"No such command in module '{module}'!");

                    return;
                }

                if (ConfigUtil.Config.Permissions.DeletePermission(user, command.ToLowerInvariant(), global ? null : Context.Guild))
                {
                    await ReplyAsync("Permission revoked!");
                }
                else
                {
                    await ReplyAsync("Could not revoke permission!");
                }
            }

            ConfigUtil.Save();
        }

        [Command("perm.grant")]
        public async Task PermGrant(IRole role, string module, string command = "*")
        {
            if (role == null)
            {
                await ReplyAsync("Role does not exist!");

                return;
            }

            var moduleType = GetType().Assembly
                .GetTypes()
                .FirstOrDefault(x => x.GetCustomAttributes(typeof(NameAttribute), true).Length == 1 &&
                                     ((NameAttribute)x.GetCustomAttributes(typeof(NameAttribute), true)[0]).Text == module);

            if (moduleType == null)
            {
                await ReplyAsync($"The module '{module}' does not exist!");

                return;
            }

            if (command == "*")
            {
                var commands = moduleType.GetMethods()
                    .Where(x => x.GetCustomAttributes(typeof(CommandAttribute), true).Length == 1)
                    .Select(x => ((CommandAttribute)x.GetCustomAttributes(typeof(CommandAttribute), true)[0]).Text)
                    .Distinct();

                foreach (var cmd in commands)
                {
                    if (!ConfigUtil.Config.Permissions.HasPermission(role, cmd) &&
                        !ConfigUtil.Config.Permissions.AddPermission(role, cmd))
                    {
                        await ReplyAsync($"Could not grant permission for command {cmd}!");
                    }
                }

                await ReplyAsync("Permissions granted!");
            }
            else
            {
                if (!moduleType.GetMethods()
                    .Any(x => x.GetCustomAttributes(typeof(CommandAttribute), true).Length == 1 &&
                              ((CommandAttribute)x.GetCustomAttributes(typeof(CommandAttribute), true)[0]).Text ==
                              command.ToLowerInvariant()))
                {
                    await ReplyAsync($"No such command in module '{module}'!");

                    return;
                }

                if (ConfigUtil.Config.Permissions.AddPermission(role, command.ToLowerInvariant()))
                {
                    await ReplyAsync("Permission granted!");
                }
                else
                {
                    await ReplyAsync("Could not grant permission!");
                }
            }

            ConfigUtil.Save();
        }

        [Command("perm.revoke")]
        public async Task PermRevoke(IRole role, string module, string command = "*")
        {
            if (role == null)
            {
                await ReplyAsync("User does not exist!");

                return;
            }

            var moduleType = GetType().Assembly
                .GetTypes()
                .FirstOrDefault(x => x.GetCustomAttributes(typeof(NameAttribute), true).Length == 1 &&
                                     ((NameAttribute)x.GetCustomAttributes(typeof(NameAttribute), true)[0]).Text == module);

            if (moduleType == null)
            {
                await ReplyAsync($"The module '{module}' does not exist!");

                return;
            }

            if (command == "*")
            {
                var commands = moduleType.GetMethods()
                    .Where(x => x.GetCustomAttributes(typeof(CommandAttribute), true).Length == 1)
                    .Select(x => ((CommandAttribute)x.GetCustomAttributes(typeof(CommandAttribute), true)[0]).Text)
                    .Distinct();

                foreach (var cmd in commands)
                {
                    if (ConfigUtil.Config.Permissions.HasPermission(role, cmd) &&
                        !ConfigUtil.Config.Permissions.DeletePermission(role, cmd))
                    {
                        await ReplyAsync($"Could not grant permission for command {cmd}!");
                    }
                }

                await ReplyAsync("Permissions revoked!");
            }
            else
            {
                if (!moduleType.GetMethods()
                    .Any(x => x.GetCustomAttributes(typeof(CommandAttribute), true).Length == 1 &&
                              ((CommandAttribute)x.GetCustomAttributes(typeof(CommandAttribute), true)[0]).Text ==
                              command.ToLowerInvariant()))
                {
                    await ReplyAsync($"No such command in module '{module}'!");

                    return;
                }

                if (ConfigUtil.Config.Permissions.DeletePermission(role, command.ToLowerInvariant()))
                {
                    await ReplyAsync("Permission revoked!");
                }
                else
                {
                    await ReplyAsync("Could not revoke permission!");
                }
            }

            ConfigUtil.Save();
        }

        [Command("perm.list")]
        public async Task PermList(IUser user)
        {
            var guildUser = Context.Guild.GetUser(user.Id);

            var permModules = new List<PermissionModule>();

            if (ConfigUtil.Config.Permissions.ModulePermissionUser.ContainsKey(user.Id))
            {
                permModules.AddRange(ConfigUtil.Config.Permissions.ModulePermissionUser[user.Id].Where(x => x.GuildId == null || x.GuildId == Context.Guild.Id).ToList());
            }

            permModules.AddRange(ConfigUtil.Config.Permissions.ModulePermissionRole.Where(x => guildUser.Roles.Select(y => y.Id).Contains(x.Key)).SelectMany(x => x.Value));

            var responseDic = new Dictionary<string, List<string>>();

            foreach (var module in permModules)
            {
                if (!responseDic.ContainsKey(module.Module))
                {
                    responseDic.Add(module.Module, new List<string>());
                }

                foreach (var cmd in module.Commands)
                {
                    if (responseDic[module.Module].Contains(cmd)) continue;

                    responseDic[module.Module].Add(cmd);
                }
            }

            if (responseDic.Count() == 0)
            {
                await ReplyAsync("This user has no permissions!");

                return;
            }

            await ReplyAsync("User has following permission:\n\n" +
                responseDic
                .Select(x => x.Key + "\n" + x.Value.Select(y => "    " + y).Aggregate((a, b) => a + "\n" + b))
                .Aggregate((a, b) => a + "\n" + b));
        }

        [Command("perm.list")]
        public async Task PermList(IRole role)
        {
            if (!ConfigUtil.Config.Permissions.ModulePermissionRole.ContainsKey(role.Id))
            {
                await ReplyAsync("This role has no permissions!");

                return;
            }

            var permModules = ConfigUtil.Config.Permissions.ModulePermissionRole[role.Id].Where(x => x.GuildId == null || x.GuildId == Context.Guild.Id);

            var responseDic = new Dictionary<string, List<string>>();

            foreach (var module in permModules)
            {
                if (!responseDic.ContainsKey(module.Module))
                {
                    responseDic.Add(module.Module, new List<string>());
                }

                foreach (var cmd in module.Commands)
                {
                    if (responseDic[module.Module].Contains(cmd)) continue;

                    responseDic[module.Module].Add(cmd);
                }
            }

            await ReplyAsync("Role has following permission:\n\n" +
                responseDic
                .Select(x => x.Key + "\n" + x.Value.Select(y => "    " + y).Aggregate((a, b) => a + "\n" + b))
                .Aggregate((a, b) => a + "\n" + b));
        }
    }
}
