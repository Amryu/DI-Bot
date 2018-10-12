using System;
using System.Linq;
using Discord.Commands;
using DIBot.Services;
using System.Threading.Tasks;
using Discord;
using System.Collections.Generic;

namespace DIBot.Modules
{
    /// <summary>
    /// Defines a command module.
    /// </summary>
    [Name("User")]
    public class ModuleUser : ModuleBase<SocketCommandContext>
    {
        private MasterDivisionRegistry _mdr;

        public ModuleUser(CalendarScheduler calendar, MasterDivisionRegistry mdr)
        {
            _mdr = mdr;
        }

        [Command("user.bind")]
        public async Task UserBind(IUser user, string userId)
        {
            var diUser = _mdr.Members.FirstOrDefault(x => x.Name == userId || x.Id.ToString() == userId);

            if (diUser == null)
            {
                await ReplyAsync("DI user could not be found in the MDR!");
            }
            else
            {
                diUser.DiscordId = user.Id;
                diUser.ImageUrl = user.GetAvatarUrl();

                MasterDivisionRegistry.Save(_mdr);

                await ReplyAsync("User was successfully bound!");
            }
        }

        [Command("user.selfbind")]
        public async Task UserSelfbind(string userId)
        {
            var diUser = _mdr.Members.FirstOrDefault(x => x.Name == userId || x.Id.ToString() == userId);
            
            if (diUser == null)
            {
                await ReplyAsync("DI user could not be found in the MDR! Refreshing...");

                lock(_mdr)
                {
                    _mdr.Update();
                }

                diUser = _mdr.Members.FirstOrDefault(x => x.Name == userId || x.Id.ToString() == userId);
            }

            if (diUser == null)
            {
                await ReplyAsync("User can still not be found... Please try again later!");
            }
            else if (diUser.DiscordId != 0 && diUser.DiscordId != Context.User.Id)
            {
                await ReplyAsync("This user is already bound by someone else!");
            }
            else
            {
                if (diUser.Rank != DIRank.Initiate &&
                    diUser.Rank != DIRank.InitiateStar &&
                    diUser.Rank != DIRank.Member &&
                    diUser.Rank != DIRank.Elite &&
                    diUser.Rank != DIRank.Veteran &&
                    diUser.Rank != DIRank.Mentor &&
                    diUser.Rank != DIRank.Associate)
                {
                    await ReplyAsync("The DI rank of the given user is too high for automatic assignment or you is currently listed as 'Away'!");

                    return;
                }

                if (diUser.Position != DIPosition.None &&
                    diUser.Position != DIPosition.RosterLeader)
                {
                    await ReplyAsync("The DI position of the given user is too high for automatic assignment!");

                    return;
                }

                diUser.DiscordId = Context.User.Id;
                diUser.ImageUrl = Context.User.GetAvatarUrl();

                var roleMap = ConfigUtil.Config.RoleMap.First(x => x.GuildId == Context.Guild.Id);

                var roles = new List<IRole>();

                var rosterKey = diUser.Team + "," + diUser.Roster;

                if (roleMap.Ranks.ContainsKey(diUser.Rank))
                {
                    roles.Add(Context.Guild.GetRole(roleMap.Ranks[diUser.Rank]));
                }

                if (diUser.Position != DIPosition.None && roleMap.Positions.ContainsKey(diUser.Position))
                {
                    roles.Add(Context.Guild.GetRole(roleMap.Positions[diUser.Position]));
                }

                if (roleMap.RosterRoles.ContainsKey(rosterKey))
                {
                    roles.Add(Context.Guild.GetRole(roleMap.RosterRoles[rosterKey]));
                }

                var guildUser = Context.Guild.Users.First(x => x.Id == Context.User.Id);

                try
                {
                    await guildUser.AddRolesAsync(roles.Distinct());

                    if (guildUser.Roles.Contains(Context.Guild.GetRole(roleMap.DefaultRole)))
                    {
                        await guildUser.RemoveRoleAsync(Context.Guild.GetRole(roleMap.DefaultRole));
                    }
                }
                catch(Exception e)
                {
                    e.ToString();
                }

                await guildUser.ModifyAsync((x) => x.Nickname = diUser.Name);

                MasterDivisionRegistry.Save(_mdr);

                await ReplyAsync("User was successfully bound!");
            }
        }

        [Command("user.whois")]
        public async Task UserWhois(IUser user)
        {
            var diUser = _mdr.Members.FirstOrDefault(x => x.DiscordId == user.Id);

            if (diUser == null)
            {
                await ReplyAsync("User is not bound to any member in the MDR!");
            }
            else
            {
                var eb = new EmbedBuilder();

                eb.Author = new EmbedAuthorBuilder()
                {
                    Name = diUser.Name,
                    IconUrl = diUser.ImageUrl,
                    Url = diUser.ProfileUrl
                };

                eb.Title = user.Mention;

                eb.AddField("Rank", diUser.Rank.ToString(), true);
                eb.AddField("Position", diUser.Position.ToString(), true);
                eb.AddField("House", diUser.House.Replace("House ", ""), true);
                eb.AddField("Division", diUser.Division, true);
                eb.AddField("Team", diUser.Team.Replace("Team ", ""), true);
                eb.AddField("Roster", diUser.Roster.Replace("Roster ", ""), true);

                eb.Footer = new EmbedFooterBuilder()
                {
                    Text = diUser.ProfileUrl
                };

                await ReplyAsync("", false, eb.Build());
            }
        }

        [Command("user.iam.add")]
        public async Task UserIamAdd(IRole role)
        {
            var roleMap = ConfigUtil.Config.RoleMap.First(x => x.GuildId == Context.Guild.Id);

            if (roleMap.SelfAssignableRoles.Contains(role.Id))
            {
                await ReplyAsync("That role is already self-assignable!");

                return;
            }

            roleMap.SelfAssignableRoles.Add(role.Id);

            ConfigUtil.Save();

            await ReplyAsync("Successfully added role!");
        }

        [Command("user.iam.remove")]
        public async Task UserIamRemove(IRole role)
        {
            var roleMap = ConfigUtil.Config.RoleMap.First(x => x.GuildId == Context.Guild.Id);

            if (!roleMap.SelfAssignableRoles.Contains(role.Id))
            {
                await ReplyAsync("That role is not self-assignable!");

                return;
            }

            roleMap.SelfAssignableRoles.Remove(role.Id);

            ConfigUtil.Save();

            await ReplyAsync("Successfully removed role!");
        }

        [Command("user.iam.list")]
        public async Task UserIamList()
        {
            var roleMap = ConfigUtil.Config.RoleMap.First(x => x.GuildId == Context.Guild.Id);

            await ReplyAsync("You assign yourself following roles: \n\n" + 
                roleMap.SelfAssignableRoles
                .Select(x => Context.Guild.GetRole(x).Name)
                .Aggregate((a,b) => a + "\n" + b));
        }

        [Command("user.iam")]
        public async Task UserIam(IRole role)
        {
            if (!ConfigUtil.Config.RoleMap
                .First(x => x.GuildId == Context.Guild.Id)
                .SelfAssignableRoles
                .Contains(role.Id))
            {
                await ReplyAsync("That role can't be self-assigned! Use '!user.iam.list' to see self-assignable roles!");

                return;
            }

            var guildUser = Context.Guild.Users.First(x => x.Id == Context.User.Id);

            if (guildUser.Roles.Any(x => x.Id == role.Id))
            {
                await ReplyAsync("You already have that role!");

                return;
            }

            await guildUser.AddRoleAsync(role);

            await ReplyAsync("Role successfully assigned!");
        }
        
        [Command("user.iamnot")]
        public async Task UserIamNot(IRole role)
        {
            var guildUser = Context.Guild.Users.First(x => x.Id == Context.User.Id);

            if (guildUser.Roles.All(x => x.Id != role.Id))
            {
                await ReplyAsync("You don't have that role!");

                return;
            }

            if (!ConfigUtil.Config.RoleMap
                .First(x => x.GuildId == Context.Guild.Id)
                .SelfAssignableRoles
                .Contains(role.Id))
            {
                await ReplyAsync("That role can't be self-removed! Use '!user.iam.list' to see self-removable roles!");

                return;
            }

            await guildUser.RemoveRoleAsync(role);

            await ReplyAsync("Role successfully removed!");
        }

        [Command("user.defaultrole")]
        public async Task UserDefaultRole(IRole role)
        {
            var roleMap = ConfigUtil.Config.RoleMap.First(x => x.GuildId == Context.Guild.Id);

            roleMap.DefaultRole = role.Id;

            ConfigUtil.Save();

            await ReplyAsync("Successfully set default role!");
        }

        [Command("user.image")]
        public async Task UserImage(string userId)
        {
            var diUser = _mdr.Members.FirstOrDefault(x => x.Name == userId || x.Id.ToString() == userId);

            if (diUser == null)
            {
                await ReplyAsync("DI user could not be found in the MDR!");
            }
            else if (diUser.ImageUrl == null)
            {
                await ReplyAsync("No image set for this user!");
            }
            else
            {
                await ReplyAsync(diUser.ImageUrl);
            }
        }

        [Command("user.image")]
        public async Task UserImage(string userId, string imageUrl)
        {
            var diUser = _mdr.Members.FirstOrDefault(x => x.Name == userId || x.Id.ToString() == userId);

            if (diUser == null)
            {
                await ReplyAsync("DI user could not be found in the MDR!");
            }
            else
            {
                try
                {
                    new Uri(imageUrl);
                }
                catch (UriFormatException)
                {
                    await ReplyAsync("The image URL is not valid!");

                    return;
                }

                diUser.ImageUrl = imageUrl;

                MasterDivisionRegistry.Save(_mdr);

                await ReplyAsync("Image url for user successfully set!");
            }
        }
    }
}
