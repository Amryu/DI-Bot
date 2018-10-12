using System;
using System.Collections.Generic;
using System.Linq;
using Discord.Commands;
using DIBot.Services;
using System.Threading.Tasks;
using Discord;


namespace DIBot.Modules
{
    /// <summary>
    /// Defines a command module.
    /// </summary>
    [Name("MDR")]
    public class ModuleMDR : ModuleBaseExtended<SocketCommandContext>
    {
        private MasterDivisionRegistry _mdr;

        public ModuleMDR(CalendarScheduler calendar, MasterDivisionRegistry mdr)
        {
            _mdr = mdr;
        }

        [Command("mdr.update")]
        public async Task MDRUpdate()
        {
            lock (_mdr)
            {
                _mdr.Update();
            }

            await ReplyAsync($"The MDR was successfully refreshed! ({_mdr.Members.Count()} users loaded)");
        }

        [Command("mdr.apply")]
        public async Task MDRApply()
        {
            await _mdr.Apply();

            await ReplyAsync($"The MDR was successfully applied!");
        }

        [Command("mdr.list")]
        public async Task MDRApply(string house = null, string division = null, string team = null, string roster = null)
        {
            DIUnit unit = _mdr.GetUnit(house, division, team, roster);

            try
            {
                var response = "Following members were found:\n\n" + unit.Members
                    .Select(x => x.Name + (x.DiscordId > 0 ? $" - ({Context.Guild.GetUser(x.DiscordId)?.Mention ?? ""})" : ""))
                    .Aggregate((a, b) => a + "\n" + b);

                await ReplyMultiMessageAsync(response);
            }
            catch(Exception e)
            {
                e.ToString();
            }
        }

        [Command("mdr.rank.list")]
        public async Task MDRRankList()
        {
            await ReplyAsync($"Currently defined DI ranks: \n\n" + Enum.GetNames(typeof(DIRank)).Aggregate((a,b) => a + "\n" + b));
        }

        [Command("mdr.rank.link")]
        public async Task MDRRankLink(IRole role, string rank)
        {
            if (!Enum.TryParse(rank, out DIRank diRank))
            {
                await ReplyAsync($"Invalid DI rank provided! Check the available ranks using '!mdr.rank.list'.");

                return;
            }

            var roleMap = ConfigUtil.Config.RoleMap.First(x => x.GuildId == Context.Guild.Id);

            if (roleMap.Ranks.ContainsKey(diRank))
            {
                roleMap.Ranks[diRank] = role.Id;
            }
            else
            {
                roleMap.Ranks.Add(diRank, role.Id);
            }

            await ReplyAsync($"Successfully linked rank with role!");

            ConfigUtil.Save();
        }

        [Command("mdr.position.list")]
        public async Task MDRPositionList()
        {
            await ReplyAsync($"Currently defined DI positions: \n\n" + Enum.GetNames(typeof(DIPosition)).Aggregate((a, b) => a + "\n" + b));
        }

        [Command("mdr.position.link")]
        public async Task MDRPositionLink(IRole role, string position)
        {
            if (!Enum.TryParse(position, out DIPosition diPosition))
            {
                await ReplyAsync($"Invalid DI position provided! Check the available positions using '!mdr.position.list'.");

                return;
            }

            var roleMap = ConfigUtil.Config.RoleMap.First(x => x.GuildId == Context.Guild.Id);

            if (roleMap.Positions.ContainsKey(diPosition))
            {
                roleMap.Positions[diPosition] = role.Id;
            }
            else
            {
                roleMap.Positions.Add(diPosition, role.Id);
            }

            await ReplyAsync($"Successfully linked position with role!");

            ConfigUtil.Save();
        }
    }
}
