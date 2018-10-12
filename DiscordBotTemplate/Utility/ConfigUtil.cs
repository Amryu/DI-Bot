using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using DIBot.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DIBot
{
    public static class ConfigUtil
    {
        private static IConfig<DIConfig> _config;

        public static DIConfig Config => _config.Data;

        public static void Load()
        {
            if (_config == null)
            {
                _config = new JSONConfig<DIConfig>("DIBot.config.json");
            }
        }

        public static void Save()
        {
            _config?.Save();
        }
    }

    public interface IConfig<T> where T : new()
    {
        T Data { get; }

        void Save();
    }

    public class JSONConfig<T> : IConfig<T> where T : new()
    {
        private string _filename;

        public T Data { get; private set; }

        public JSONConfig(string filename)
        {
            _filename = filename;

            if (File.Exists(filename))
            {
                using (var reader = new FileStream(filename, FileMode.Open))
                {
                    Data = (T)new DataContractJsonSerializer(typeof(DIConfig)).ReadObject(reader);
                }
            }
            else
            {
                Data = new T();
            }
        }

        public void Save()
        {
            using (var writer = new FileStream(_filename, FileMode.Create))
            {
                using (var jsonWriter = JsonReaderWriterFactory.CreateJsonWriter(writer, Encoding.UTF8, true, true, "    "))
                {
                    new DataContractJsonSerializer(typeof(DIConfig)).WriteObject(jsonWriter, Data);
                }
            }
        }
    }

    [DataContract]
    public class DIConfig
    {
        [DataMember] public AuthConfig AuthConfig = new AuthConfig();

        [DataMember] public List<string> Calendars = new List<string>();

        [DataMember] public List<RoleMap> RoleMap = new List<RoleMap>();

        [DataMember] public Permissions Permissions = new Permissions();

        [DataMember] public string DiscordToken = "";

        [DataMember] public string Prefix = "!";
    }

    [KnownType(typeof(DIRank))]
    [KnownType(typeof(DIPosition))]
    [DataContract]
    public class RoleMap
    {
        [DataMember] public ulong GuildId;

        [DataMember] public ulong DefaultRole;

        [DataMember] public List<ulong> SelfAssignableRoles = new List<ulong>();

        [DataMember] public Dictionary<string, ulong> RosterRoles = new Dictionary<string, ulong>();

        [DataMember] public Dictionary<DIRank, ulong> Ranks = new Dictionary<DIRank, ulong>();

        [DataMember] public Dictionary<DIPosition, ulong> Positions = new Dictionary<DIPosition, ulong>();
    }

    [DataContract]
    public class AuthConfig
    {
        [DataMember] public List<Cookie> Cookies = new List<Cookie>();

        [DataMember] public string Username = "";

        [DataMember] public string Password = "";

        [DataMember] public int MemberId;

        [DataMember] public string MemberKey = "";
    }

    [DataContract]
    public class Cookie
    {
        [DataMember] public string Name = "";

        [DataMember] public string Value = "";

        [DataMember] public string Path = "/";

        [DataMember] public string Domain = "di.community";
    }

    [DataContract]
    public class Permissions
    {
        [DataMember] public readonly Dictionary<ulong, List<PermissionModule>> ModulePermissionUser = new Dictionary<ulong, List<PermissionModule>>();

        [DataMember] public readonly Dictionary<ulong, List<PermissionModule>> ModulePermissionRole = new Dictionary<ulong, List<PermissionModule>>();

        /// <summary>
        /// Checks if a user has permission to execute a specific command.
        /// 
        /// This method checks per-user permissions and per-role permissions
        /// using the guilds roles. If the guild is null only global permissions
        /// are checked.
        /// </summary>
        /// <param name="user">The user which the permission is checked for</param>
        /// <param name="command">The command which the permission is checked for</param>
        /// <param name="guild">The guild which the permission is checked for</param>
        /// <returns></returns>
        public bool HasPermission(IUser user, string command, SocketGuild guild = null)
        {
            if (guild != null)
            {
                // Check guild and global permissions for user
                if (ModulePermissionUser.ContainsKey(user.Id) &&
                    ModulePermissionUser[user.Id].Any(x =>
                    x.HasPermission(command, guild.Id) ||
                    x.HasPermission(command, null)))
                {
                    return true;
                }
                
                // Check role permissions for user
                return guild.Roles
                    .Where(x => x.Members.Any(y => y.Id == user.Id))
                    .Any(x => HasPermission(x, command));
            }
            else
            {
                if (!ModulePermissionUser.ContainsKey(user.Id)) return false;

                return ModulePermissionUser[user.Id].Any(x => x.HasPermission(command, null));
            }
        }

        public bool AddPermission(IUser user, string command, SocketGuild guild = null)
        {
            if (!ModulePermissionUser.ContainsKey(user.Id))
            {
                ModulePermissionUser.Add(user.Id, new List<PermissionModule>());
            }

            var module = PermissionModule.GetModuleByCommand(command);

            if (ModulePermissionUser[user.Id].All(x => x.Module != module || x.GuildId != guild?.Id))
            {
                ModulePermissionUser[user.Id].Add(new PermissionModule(module, guild?.Id));
            }

            return ModulePermissionUser[user.Id].First(x => x.Module == module && x.GuildId == guild?.Id).AddCommand(command);
        }

        public bool DeletePermission(IUser user, string command, SocketGuild guild = null)
        {
            if (!ModulePermissionUser.ContainsKey(user.Id))
            {
                return false;
            }

            var module = PermissionModule.GetModuleByCommand(command);

            if (ModulePermissionUser[user.Id].All(x => x.Module != module || x.GuildId != guild?.Id))
            {
                return false;
            }
            
            var ret = ModulePermissionUser[user.Id].First(x => x.Module == module && x.GuildId == guild?.Id).DeleteCommand(command);

            if (ModulePermissionUser[user.Id].First(x => x.Module == module && x.GuildId == guild?.Id).Commands.Count == 0)
            {
                ModulePermissionUser.Remove(user.Id);
            }

            return ret;
        }

        public bool HasPermission(IRole role, string command)
        {
            if (!ModulePermissionRole.ContainsKey(role.Id)) return false;

            return ModulePermissionRole[role.Id].Any(x => x.HasPermission(command, role.Guild.Id));
        }

        public bool AddPermission(IRole role, string command)
        {
            if (!ModulePermissionRole.ContainsKey(role.Id))
            {
                ModulePermissionRole.Add(role.Id, new List<PermissionModule>());
            }

            var module = PermissionModule.GetModuleByCommand(command);

            if (ModulePermissionRole[role.Id].All(x => x.Module != module || x.GuildId != role.Guild.Id))
            {
                ModulePermissionRole[role.Id].Add(new PermissionModule(module, role.Guild.Id));
            }

            return ModulePermissionRole[role.Id].First(x => x.Module == module && x.GuildId == role.Guild.Id).AddCommand(command);
        }

        public bool DeletePermission(IRole role, string command)
        {
            if (!ModulePermissionRole.ContainsKey(role.Id))
            {
                return false;
            }

            var module = PermissionModule.GetModuleByCommand(command);

            if (ModulePermissionRole[role.Id].All(x => x.Module != module || x.GuildId != role.Guild.Id))
            {
                return false;
            }

            var ret = ModulePermissionRole[role.Id].First(x => x.Module == module && x.GuildId == role.Guild.Id).DeleteCommand(command);

            if (ModulePermissionRole[role.Id].First(x => x.Module == module && x.GuildId == role.Guild.Id).Commands.Count == 0)
            {
                ModulePermissionRole.Remove(role.Id);
            }

            return ret;
        }
    }

    [DataContract]
    public class PermissionModule
    {
        [DataMember] public readonly ulong? GuildId;

        [DataMember] public readonly string Module;

        [DataMember] public List<string> Commands = new List<string>();

        public bool HasPermission(string command, ulong? guildId)
        {
            return (GuildId == 0 || GuildId == guildId) && Commands.Contains(command.ToLowerInvariant());
        }

        public PermissionModule(string moduleName, ulong? guildId)
        {
            GuildId = guildId;
            Module = moduleName;
        }

        public bool AddCommand(string command)
        {
            // Check if command is defined in module
            if (GetModuleByCommand(command.ToLowerInvariant()) == Module)
            {
                Commands.Add(command);

                return true;
            }

            return false;
        }

        public bool DeleteCommand(string command)
        {
            return Commands.Remove(command);
        }
        
        public static string GetModuleByCommand(string command)
        {
            var method = typeof(Program)
                .Assembly
                .GetTypes()
                .SelectMany(t => t.GetMethods())
                .FirstOrDefault(m => m.GetCustomAttributes(typeof(CommandAttribute), false).Length == 1 &&
                                     ((CommandAttribute)m.GetCustomAttributes(typeof(CommandAttribute), false)[0]).Text == command.ToLowerInvariant());

            if (method == null) return null;

            var attributes = method.DeclaringType.GetCustomAttributes(typeof(NameAttribute), true);

            if (attributes.Length != 1) return null;

            return ((NameAttribute)attributes[0]).Text;
        }
    }
}
