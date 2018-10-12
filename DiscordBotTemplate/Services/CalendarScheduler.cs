using DIBot;
using Discord.WebSocket;
using Ical.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;

namespace DIBot.Services
{
    public class CalendarScheduler
    {
        private readonly DiscordSocketClient _discord;
        private readonly MasterDivisionRegistry _mdr;

        private Dictionary<string, DICalendar> _diCals;

        public DICalendar this[string calendarName] => _diCals[calendarName];

        public IEnumerable<DICalendar> Calendars => _diCals.Values;

        private Timer _timer;

        public Dictionary<ulong, Queue<Func<Task>>> ScheduledPosts = new Dictionary<ulong, Queue<Func<Task>>>();

        public CalendarScheduler(DiscordSocketClient discord, MasterDivisionRegistry mdr)
        {
            _discord = discord;
            _mdr = mdr;

            _timer = new Timer();
            _timer.Interval = 10000;
            _timer.Elapsed += CalendarLoop;
            _timer.Enabled = true;

            var postTimer = new Timer();
            postTimer.Interval = 10000;
            postTimer.Elapsed += (sender, evt) =>
            {
                postTimer.Enabled = false;

                foreach (var guild in _discord.Guilds)
                {
                    if (!ScheduledPosts.ContainsKey(guild.Id))
                    {
                        ScheduledPosts.Add(guild.Id, new Queue<Func<Task>>());
                    }

                    if (ScheduledPosts[guild.Id].Count() == 0) continue;
                    
                    ScheduledPosts[guild.Id].Dequeue().Invoke().GetAwaiter().GetResult();
                }

                postTimer.Enabled = true;
            };
            postTimer.Enabled = true;
        }

        ~CalendarScheduler()
        {
            foreach (var calendar in _diCals.Values)
            {
                DICalendar.SaveCalendar(calendar);
            }
        }

        private void CalendarLoop(object sender, ElapsedEventArgs evt)
        {
            _timer.Enabled = false;
            _timer.Interval = 300000;

            if (_diCals == null)
            {
                _diCals = new Dictionary<string, DICalendar>();

                foreach (var cal in ConfigUtil.Config.Calendars)
                {
                    _diCals.Add(cal, File.Exists("calendars/" + cal) ? DICalendar.LoadCalendar(cal) : new DICalendar(cal));
                }
            }

            var handler = new HttpClientHandler();

            handler.CookieContainer = new CookieContainer();

            ConfigUtil.Config.AuthConfig.Cookies
                .Select(x => new System.Net.Cookie(x.Name, x.Value, x.Path, x.Domain))
                .ToList()
                .ForEach(x => handler.CookieContainer.Add(x));

            var client = new DIHttpClient(handler);

            foreach (var cal in ConfigUtil.Config.Calendars)
            {
                var vCal = Calendar.Load(client
                    .GetCalendarAsync(cal)
                    .GetAwaiter().GetResult());

                lock (_diCals[cal])
                {
                    _diCals[cal].ProcessEvents(vCal, _discord, _mdr);

                    DICalendar.SaveCalendar(_diCals[cal]);
                }
            }

            _timer.Enabled = true;
        }
    }
}
