using System;
using System.Collections.Generic;
using System.Linq;
using Discord.Commands;
using DIBot.Services;
using System.Threading.Tasks;
using Discord;
using System.Net.Http;
using System.Net;
using DiscordBotTemplate.Modules;

namespace DIBot.Modules
{
    /// <summary>
    /// Defines a command module.
    /// </summary>
    [Name("Calendar")]
    public class ModuleAdmin : ModuleBaseExtended<SocketCommandContext>
    {
        private CalendarScheduler _calendar;
        private MasterDivisionRegistry _mdr;

        public ModuleAdmin(CalendarScheduler calendar, MasterDivisionRegistry mdr)
        {
            _calendar = calendar;
            _mdr = mdr;
        }

        [Command("cal.trigger.add")]
        public async Task TriggerAdd(string calendarName, string tagPrimary = null, string tagSecondary = null, bool mentionEveryone = false, IRole mentionRole = null)
        {
            if (tagPrimary == "*") tagPrimary = null;
            if (tagSecondary == "*") tagSecondary = null;

            if (_calendar[calendarName] != null)
            {
                if (_calendar[calendarName].Triggers.Any(x => x.GuildID == Context.Guild.Id && x.ChannelID == Context.Channel.Id))
                {
                    await ReplyAsync("This calendar already has a trigger in this channel!");

                    return;
                }

                if (mentionRole != null && !mentionRole.IsMentionable)
                {
                    await ReplyAsync("The specified role is not mentionable!");

                    return;
                }

                _calendar[calendarName].Triggers.Add(new DICalendarTrigger()
                {
                    GuildID = Context.Guild.Id,
                    ChannelID = Context.Channel.Id,
                    TagPrimary = tagPrimary,
                    TagSecondary = tagSecondary,
                    RoleId = mentionRole?.Id ?? 0,
                    Everyone = mentionEveryone
                });

                DICalendar.SaveCalendar(_calendar[calendarName]);

                await ReplyAsync("The trigger was successfully added!");
            }
            else
            {
                await ReplyAsync("That calendar is not configured!");
            }
        }

        [Command("cal.trigger.delete")]
        public async Task TriggerDelete(string calendarName)
        {
            if (_calendar[calendarName] != null)
            {
                if (!_calendar[calendarName].Triggers.Any(x => x.GuildID == Context.Guild.Id && x.ChannelID == Context.Channel.Id))
                {
                    await ReplyAsync("This channel has no trigger for this calendar!");

                    return;
                }

                _calendar[calendarName].Triggers.RemoveAll(x => x.GuildID == Context.Guild.Id && x.ChannelID == Context.Channel.Id);

                DICalendar.SaveCalendar(_calendar[calendarName]);

                await ReplyAsync("The trigger was successfully deleted!");
            }
            else
            {
                await ReplyAsync("That calendar is not configured!");
            }
        }

        [Command("cal.trigger.list")]
        public async Task TriggerList()
        {
            var calendars = new List<string>();

            foreach (var cal in _calendar.Calendars)
            {
                if (cal.Triggers.All(x => x.ChannelID != Context.Channel.Id))
                {
                    continue;
                }

                calendars.Add(cal.Name);
            }

            if (!calendars.Any())
            {
                await ReplyAsync("There are no triggers for this channel!");
            }
            else
            {
                await ReplyAsync("Following triggers are configured:\n" +
                                 "\n" +
                                 calendars.Aggregate((a, b) => a + "\n" + b));
            }
        }

        [Command("cal.event.show")]
        public async Task EventShow(int eventId)
        {
            var evt = _calendar.Calendars.SelectMany(x => x.Events).FirstOrDefault(x => x.EventID == eventId);

            if (evt != null)
            {
                evt.PostToChannel(Context.Guild.Id, Context.Channel.Id);
            }
            else
            {
                await ReplyAsync("No event with that ID could be found!");
            }
        }

        [Command("cal.event.rsvp.check")]
        public async Task EventRsvpCheck(int eventId, string house = null, string division = null, string team = null, string roster = null, bool mention = false)
        {
            var client = DIHttpClient.CreateWithAuthCookies(ConfigUtil.Config.AuthConfig.Cookies);

            var evt = _calendar.Calendars.SelectMany(x => x.Events).FirstOrDefault(x => x.EventID == eventId);

            if (evt == null)
            {
                await ReplyAsync("No event with that ID could be found!");

                return;
            }

            var rsvpMembers = await client.GetEventRsvpAsync(evt.TitleUrl);

            DIUnit unit = _mdr.GetUnit(house, division, team, roster);

            var members = unit.Members.Where(x => x.Rank != DIRank.Associate && x.Rank != DIRank.AwayST && x.Rank != DIRank.AwayLT);

            var memberCount = members.Count();
            var memberRsvpCount = members.Select(x => x.Name).Intersect(rsvpMembers).Count();

            var response = $"RSVP Rate: {memberRsvpCount}/{memberCount} ({((memberRsvpCount / (float)memberCount) * 100).ToString("0.00")}%). People missing:\n\n" +
                members
                .Select(x => x.Name)
                .Except(rsvpMembers)
                .Select(x => mention && members.First(y => y.Name == x)?.DiscordId > 0 ? Context.Guild.GetUser(members.First(y => y.Name == x).DiscordId)?.Mention ?? x : x)
                .Aggregate((a, b) => a + "\n" + b);

            await ReplyAsync("", false, evt.GetEmbed());

            await ReplyMultiMessageAsync(response);
        }

        [Command("cal.event.rsvp.remind")]
        public async Task EventRsvpRemind(int eventId, string house, string division, string team, string roster)
        {
            var client = DIHttpClient.CreateWithAuthCookies(ConfigUtil.Config.AuthConfig.Cookies);

            var evt = _calendar.Calendars.SelectMany(x => x.Events).FirstOrDefault(x => x.EventID == eventId);

            if (evt == null)
            {
                await ReplyAsync("No event with that ID could be found!");

                return;
            }

            var rsvpMembers = await client.GetEventRsvpAsync(evt.TitleUrl);

            DIUnit unit = _mdr.GetUnit(house, division, team, roster);

            var members = unit.Members.Where(x => x.Rank != DIRank.Associate && x.Rank != DIRank.AwayST && x.Rank != DIRank.AwayLT);

            var guildUsers = members
                .Where(x => !rsvpMembers.Contains(x.Name) && x.DiscordId > 0)
                .Select(x => Context.Guild.GetUser(x.DiscordId))
                .Where(x => x != null);

            foreach (var guildUser in guildUsers)
            {
                await guildUser.SendMessageAsync($"Hello {guildUser.Username}!\n" +
                    $"\n" +
                    $"This is a reminder message from {Context.User.Username} to" +
                    $"RSVP to the shown event.\n" +
                    $"\n" +
                    $"What is RSVP? - Open the event and you will find 3 buttons" +
                    $"on the left to click: \"Going\", \"Maybe\" and \"Decline\"." +
                    $"RSVP means literally \"Respond please\" meaning you should" +
                    $"click any of those buttons to inform the event host about" +
                    $"whether you will attend or not.\n" +
                    $"\n" +
                    $"Why do I get reminders for this? - Some events are mandatory" +
                    $"to RSVP too and you might end up getting a strike for it.\n" +
                    $"\n" +
                    $"Any questions left? Feel free to ask @Amryu#1337 about it!",
                    false,
                    evt.GetEmbed("Open event in browser"));
            }

            await ReplyAsync($"{guildUsers.Count()} users were successfully notified!");
        }
    }
}
