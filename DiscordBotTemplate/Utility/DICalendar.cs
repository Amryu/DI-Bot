using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using DIBot.Services;
using Calendar = Ical.Net.Calendar;
using Microsoft.Extensions.DependencyInjection;
using Ical.Net.CalendarComponents;

namespace DIBot
{
    [DataContract]
    public class DICalendar
    {
        [DataMember]
        public List<DICalendarTrigger> Triggers = new List<DICalendarTrigger>();

        [DataMember]
        public List<DIEvent> Events = new List<DIEvent>();

        [DataMember]
        public string Name;

        public DICalendar(string name)
        {
            Name = name;
        }

        public void ProcessEvents(Calendar vCal, DiscordSocketClient discord, MasterDivisionRegistry mdr)
        {
            foreach (var calEvt in vCal.Events.OrderBy(x => x.Start.Ticks))
            {
                DIEvent evt = null;

                if (Events.Any(x => x.UID == calEvt.Uid))
                {
                    evt = Events.First(x => x.UID == calEvt.Uid);

                    var description = DIEvent.DetailsRegex.Match(calEvt.Description).Groups[1].Value.Replace("\n\n", "\n").Trim();
                    if (description == string.Empty) description = calEvt.Description.Replace("\n\n", "\n").Trim();

                    if (evt.Start != calEvt.DtStart.AsUtc ||
                        evt.End != (calEvt.DtEnd?.AsUtc ?? DateTime.MinValue.ToUniversalTime()) ||
                        evt.RawTitle != calEvt.Summary ||
                        evt.Description != description)
                    {
                        evt.CopyFrom(calEvt);

                        ProcessTriggers(evt, discord, mdr, true);
                    }

                    continue;
                }

                evt = new DIEvent();

                evt.CopyFrom(calEvt);

                Events.Add(evt);

                if (evt.End > DateTime.UtcNow)
                {
                    ProcessTriggers(evt, discord, mdr);
                }
            }
        }

        private void ProcessTriggers(DIEvent evt, DiscordSocketClient discord, MasterDivisionRegistry mdr, bool isEdit = false)
        {
            foreach (DICalendarTrigger trigger in Triggers)
            {
                if (trigger.EventMatches(evt))
                {
                    evt.PostToChannel(trigger.GuildID, trigger.ChannelID, trigger, isEdit);
                }
            }
        }

        public static DICalendar LoadCalendar(string calendarName)
        {
            if (!File.Exists("calendars/" + calendarName)) return new DICalendar(calendarName);

            var serializer = new DataContractJsonSerializer(typeof(DICalendar));

            Directory.CreateDirectory("calendars");

            using (var fileStream = new FileStream("calendars/" + calendarName, FileMode.Open))
            {
                return (DICalendar)serializer.ReadObject(fileStream);
            }
        }

        public static void SaveCalendar(DICalendar calendar)
        {
            var serializer = new DataContractJsonSerializer(typeof(DICalendar));

            Directory.CreateDirectory("calendars");

            using (var fileStream = new FileStream("calendars/" + calendar.Name, FileMode.Create))
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(fileStream, Encoding.UTF8, true, true, "    "))
                {
                    serializer.WriteObject(writer, calendar);
                }
            }
        }
    }

    [DataContract]
    public class DICalendarTrigger
    {
        [DataMember] public ulong GuildID;
        [DataMember] public ulong ChannelID;

        [DataMember] public string TagPrimary;
        [DataMember] public string TagSecondary;
        [DataMember] public string Host;

        [DataMember] public ulong RoleId;
        [DataMember] public bool Everyone;

        public bool EventMatches(DIEvent evt)
        {
            return
                (TagPrimary == null || evt.TagPrimary.ToLowerInvariant() == TagPrimary.ToLowerInvariant()) &&
                (TagSecondary == null || evt.TagSecondary.ToLowerInvariant() == TagSecondary.ToLowerInvariant()) &&
                (Host == null || evt.Hosts.Contains(Host));
        }
    }

    [DataContract]
    public class DIEvent
    {
        public static readonly Regex HostRegex = new Regex(@"^\s*(?:(?:Hosting Officer)|(?:Host)): *(.+)$", RegexOptions.Multiline);
        public static readonly Regex MentionRegex = new Regex(@"@([a-zA-Z_0-9\-]+)");
        public static readonly Regex TitleRegex = new Regex(@"^(?:\[([^\]]+)\]\s*(?:\[([^\]]+)\])?)?.+$", RegexOptions.Singleline);
        public static readonly Regex UidRegex = new Regex(@"([0-9]+)-([0-9]+)-([a-z0-9]{32})@di.community");
        public static readonly Regex DetailsRegex = new Regex(@"\s*(?:(?:Details:)|(?:Details -)|(?:Details)|(?:Description:)|(?:Description -)|(?:Description))(.+)$", RegexOptions.Singleline);

        public string TitleUrl
        {
            get
            {
                var simpleTitle = new StringBuilder();

                foreach (var c in RawTitle.ToLowerInvariant())
                {
                    if (c >= '0' && c <= '9' ||
                        c >= 'a' && c <= 'z' ||
                        c == '_' || c == '-' || c == ' ')
                    {
                        simpleTitle.Append(c);
                    }
                }

                var ret = simpleTitle.ToString().Replace(' ', '-');

                while (ret.Contains("--")) ret = ret.Replace("--", "-");

                return $"{EventID}-{ret}";
            }
        }

        public string Url
        {
            get
            {
                var url = $"https://di.community/calendar/event/";
                
                return (url + $"{TitleUrl}/");
            }
        }

        public void PostToChannel(ulong guildId, ulong channelId, DICalendarTrigger trigger = null, bool isEdit = false)
        {
            var calScheduler = (CalendarScheduler)Program.ServiceProvider.GetService(typeof(CalendarScheduler));
            var discord = (DiscordSocketClient)Program.ServiceProvider.GetService(typeof(DiscordSocketClient));
            var mdr = (MasterDivisionRegistry)Program.ServiceProvider.GetService(typeof(MasterDivisionRegistry));

            try
            {
                var message = "";

                if (trigger?.Everyone == true)
                {
                    message += "@everyone ";
                }

                if (trigger?.RoleId > 0)
                {
                    message += discord.GetGuild(trigger.GuildID).GetRole(trigger.RoleId).Mention;
                }

                var embed = GetEmbed(isEdit ? "Event updated!" : "New event!");

                var channel = discord
                    .Guilds
                    .First(x => x.Id == guildId)
                    .TextChannels
                    .First(x => x.Id == channelId);

                calScheduler.ScheduledPosts[guildId].Enqueue(async () =>
                {
                    try
                    {
                        await channel.SendMessageAsync(message.Trim(), false, embed, new RequestOptions()
                            {
                                RetryMode = RetryMode.AlwaysRetry,
                                Timeout = 300000
                            });
                    }
                    catch(Exception e)
                    {
                        Console.Write(e.StackTrace);
                    }
                });
            }
            catch (Exception e)
            {
                e.ToString();
            }
        }

        public Embed GetEmbed(string title = null)
        {
            var mdr = (MasterDivisionRegistry)Program.ServiceProvider.GetService(typeof(MasterDivisionRegistry));

            var eb = new EmbedBuilder();

            var user = Hosts.Count > 0 ? mdr.Members.FirstOrDefault(x => x.Name == Hosts[0]) : null;

            eb.Author = new EmbedAuthorBuilder
            {
                Name = user?.Name ?? "Unknown",
                IconUrl = user?.ImageUrl,
                Url = user?.ProfileUrl
            };

            eb.Title = title ?? "Open event in browser";
            eb.Url = Url;
            eb.Description = RawTitle;

            eb.AddField("Time", Start.ToString("dd MMMM yyyy | HH:mm", CultureInfo.InvariantCulture) + " - " + End.ToString("HH:mm", CultureInfo.InvariantCulture) + " GMT/UTC");
            eb.AddField("Description", Description == string.Empty ? "-" : Description);

            eb.Footer = new EmbedFooterBuilder
            {
                Text = Url
            };

            return eb.Build();
        }

        internal void CopyFrom(CalendarEvent calEvt)
        {
            Start = calEvt.DtStart.AsUtc;
            End = calEvt.DtEnd?.AsUtc ?? DateTime.MinValue.ToUniversalTime();

            var hosts = HostRegex.Match(calEvt.Description).Groups[1].Value;

            foreach (Match match in MentionRegex.Matches(hosts))
            {
                Hosts.Add(match.Groups[1].Value);
            }

            var titleMatch = TitleRegex.Match(calEvt.Summary);

            EventID = Convert.ToInt32(UidRegex.Match(calEvt.Uid).Groups[1].Value);
            UID = calEvt.Uid;
            TagPrimary = titleMatch.Groups[1].Value;
            TagSecondary = titleMatch.Groups[2].Value;
            Title = calEvt.Summary;
            RawTitle = calEvt.Summary;
            Description = DetailsRegex.Match(calEvt.Description).Groups[1].Value.Replace("\n\n", "\n").Trim();

            if (Description == string.Empty) Description = calEvt.Description.Replace("\n\n", "\n").Trim();

            LastRefresh = DateTime.UtcNow;

            if (TagPrimary != null)
            {
                Title = Title.Replace("[" + TagPrimary + "]", "").Trim();
            }

            if (TagSecondary != null)
            {
                Title = Title.Replace("[" + TagSecondary + "]", "").Trim();
            }
        }

        [DataMember]
        public int EventID;

        [DataMember]
        public string UID;

        [DataMember]
        public DateTime Start;

        [DataMember]
        public DateTime End;

        [DataMember]
        public DateTime LastRefresh;

        [DataMember]
        public List<string> Hosts = new List<string>();

        [DataMember]
        public string TagPrimary;

        [DataMember]
        public string TagSecondary;

        [DataMember]
        public string Title;

        [DataMember]
        public string RawTitle;

        [DataMember]
        public string Description;
    }
}