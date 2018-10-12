using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DIBot.Modules
{
    public class ModuleBaseExtended<T> : ModuleBase<T> where T : class, ICommandContext
    {
        public async Task ReplyMultiMessageAsync(string message)
        {
            var msgList = new List<string>();

            var msg = string.Empty;

            foreach (var line in message.Split('\n'))
            {
                if (msg.Length + line.Length + 1 > 2000)
                {
                    msgList.Add(msg);

                    msg = string.Empty;
                }
                else
                {
                    if (msg.Length > 0) msg += "\n";

                    msg += line;
                }
            }

            if (msg.Length > 0) msgList.Add(msg);

            for (var i = 0; i < msgList.Count; i++)
            {
                await ReplyAsync(msgList[i]);
            }
        }
    }
}
