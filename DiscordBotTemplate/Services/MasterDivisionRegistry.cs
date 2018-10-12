using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using DIBot.Utility;
using Discord.WebSocket;
using HtmlAgilityPack;

namespace DIBot.Services
{
    [DataContract]
    public abstract class DIUnit
    {
        [DataMember] public string UnitId { get; private set; }

        [DataMember] public List<DIUnit> SubUnits = new List<DIUnit>();

        [IgnoreDataMember] public abstract IEnumerable<DIUser> Members { get; }
        
        protected DIUnit(string unitId)
        {
            UnitId = unitId;
        }

        public abstract void AddUser(DIUser user);
    }

    [DataContract]
    public class MasterDivisionRegistry : DIUnit
    {
        [IgnoreDataMember] private static readonly Regex ProfileUrlRegex = new Regex(@"^http(s)?://di\.community/profile/([0-9]{1,6})-([a-zA-Z0-9_\-]+)$");
        
        [IgnoreDataMember] public override IEnumerable<DIUser> Members => SubUnits.SelectMany(x => x.Members);

        public MasterDivisionRegistry() : base("MDR")
        {
            Update();

            Save(this);
        }

        public override void AddUser(DIUser user)
        {
            if (user.House == null) user.House = "Unassigned";

            if (SubUnits.All(x => x.UnitId != user.House))
            {
                SubUnits.Add(new DIHouse(user.House));
            }

            SubUnits.First(x => x.UnitId == user.House).AddUser(user);
        }

        public void Update()
        {
            var oldUsers = Members.ToList();

            SubUnits.Clear();

            try
            {
                var handler = new HttpClientHandler();

                handler.CookieContainer = new CookieContainer();

                ConfigUtil.Config.AuthConfig.Cookies
                    .Select(x => new System.Net.Cookie(x.Name, x.Value, x.Path, x.Domain))
                    .ToList()
                    .ForEach(x => handler.CookieContainer.Add(x));

                var result = new DIHttpClient(handler).GetStringAsync("mdr/").GetAwaiter().GetResult();

                var doc = new HtmlDocument();
                doc.LoadHtml(result);

                foreach (var node in doc.DocumentNode.SelectNodes("//a"))
                {
                    var user = new DIUser();

                    if (!Enum.TryParse(node.Attributes["class"]?.Value.Replace("-", "") ?? "", true, out user.Rank)) continue;
                    
                    user.Name = node.InnerText.Split(' ')[0].Trim();
                    user.Id = Convert.ToInt32(ProfileUrlRegex.Match(node.Attributes["href"].Value).Groups[2].Value);

                    AddDIUnitInformation(user, node);

                    DIUser oldUser = null;

                    if ((oldUser = oldUsers.FirstOrDefault(x => x.Id == user.Id)) != null)
                    {
                        user.DiscordId = oldUser.DiscordId;
                        user.ImageUrl = oldUser.ImageUrl;
                    }

                    AddUser(user);
                }
            }
            catch (Exception e)
            {
                e.ToString();
            }
        }

        public async Task Apply()
        {
            var discord = (DiscordSocketClient)Program.ServiceProvider.GetService(typeof(DiscordSocketClient));

            foreach (var guild in discord.Guilds)
            {
                var roleMap = ConfigUtil.Config.RoleMap.First(x => x.GuildId == guild.Id);

                var rankRoles = roleMap.Ranks.Values.Distinct().Select(x => guild.GetRole(x));
                var positionRoles = roleMap.Positions.Values.Distinct().Select(x => guild.GetRole(x));

                var rosterRoles = roleMap.RosterRoles.Values.Select(x => guild.GetRole(x));

                foreach (var member in Members.Where(x => x.DiscordId > 0))
                {
                    var guildUser = guild.GetUser(member.DiscordId);

                    /*
                    if (!guildUser.Roles.Any(x => x.Id == roleMap.Ranks[member.Rank]))
                    {
                        await guildUser.RemoveRolesAsync(rankRoles);

                        await guildUser.AddRoleAsync(guild.GetRole(roleMap.Ranks[member.Rank]));
                    }

                    if (!guildUser.Roles.Any(x => x.Id == roleMap.Positions[member.Position]))
                    {
                        await guildUser.RemoveRolesAsync(positionRoles);

                        await guildUser.AddRoleAsync(guild.GetRole(roleMap.Positions[member.Position]));
                    }
                    */

                    var rosterKey = member.Team + "," + member.Roster;

                    if (roleMap.RosterRoles.ContainsKey(rosterKey) && !guildUser.Roles.Any(x => x.Id == roleMap.RosterRoles[rosterKey]))
                    {
                        await guildUser.RemoveRolesAsync(rosterRoles.Where(x => guildUser.Roles.Any(y => y.Id == x.Id)));

                        await guildUser.AddRoleAsync(guild.GetRole(roleMap.RosterRoles[rosterKey]));
                    }
                }
            }
        }

        public DIUnit GetUnit(string house = null, string division = null, string team = null, string roster = null)
        {
            DIUnit unit = this; 

            if (house == "*") house = null;
            if (division == "*") division = null;
            if (team == "*") team = null;
            if (roster == "*") roster = null;

            // House
            if (house == null) return unit;

            unit = unit.SubUnits.FirstOrDefault(x => x.UnitId.EndsWith(house));

            if (unit == null) throw new Exception($"House '{house}' not found!");

            // Division
            if (division == null) return unit;
            
            unit = unit.SubUnits.FirstOrDefault(x => x.UnitId == division);

            if (unit == null) throw new Exception($"Division '{division}' not found!");

            // Team
            if (team == null) return unit;
            
            unit = unit.SubUnits.FirstOrDefault(x => x.UnitId.EndsWith(team));

            if (unit == null) throw new Exception($"Team '{team}' not found!");

            // Roster
            if (roster == null) return unit;
            
            unit = unit.SubUnits.FirstOrDefault(x => x.UnitId.EndsWith(roster));

            if (unit == null) throw new Exception($"Roster '{roster}' not found!");

            return unit;
        }

        private static void AddDIUnitInformation(DIUser user, HtmlNode node)
        {
            AddDIUnitInformationFromNode(user, node);

            if (user.House == null)
            {
                throw new Exception("User could not be assigned a house!");
            }

            if (user.Division == null) // House Leadership
            {
                string[] additions = { "HG", "FC" };

                var house = user.House.Split(' ')[1].ToLowerInvariant();

                var rosterAdditionNode = node
                    .SelectSingleNode($"//div[@class='house-container {house}']")
                    .Descendants()
                    .FirstOrDefault(x => x.Name == "a" &&
                                         x.HasClass("roster-addition") &&
                                         additions.Contains(x.InnerText) &&
                                         x.Attributes["title"]?.Value.Contains(user.Name) == true);

                if (rosterAdditionNode != null)
                {
                    user.Roster = Regex
                        .Match(rosterAdditionNode.ParentNode.InnerText.Split('-')[0].Trim(), @"^(.+)\([0-9]+\)$")
                        .Groups[1].Value.Trim();

                    if (rosterAdditionNode.InnerText == "HG")
                    {
                        user.Position = DIPosition.HouseGeneral;
                    }
                    else if (rosterAdditionNode.InnerText == "FC")
                    {
                        user.Position = DIPosition.FirstCommander;
                    }

                    AddDIUnitInformationFromNode(user, rosterAdditionNode);
                }
                else
                {
                    return;
                }
            }

            if (user.Team == null) // Division Leadership
            {
                string[] additions = {"DC", "DV"};
                
                var divisionNode = node
                    .SelectNodes("//div[@class='div-container']")
                    // Find the correct division
                    .FirstOrDefault(x => x
                        .FirstChild(y => y.Name == "div" && y.HasClass("div-header"))
                        .FirstChild(y => y.Name == "h3" && y.HasClass("division-title"))
                        .GetClasses()
                        .First(y => y != "division-title") == user.Division);

                var rosterAdditionNode = divisionNode?
                    .Descendants()?
                    .FirstOrDefault(x => x.Name == "a" &&
                                         x.HasClass("roster-addition") &&
                                         additions.Contains(x.InnerText) &&
                                         x.Attributes["title"]?.Value.Contains(user.Name) == true);

                if (rosterAdditionNode != null)
                {
                    user.Roster = Regex.Match(rosterAdditionNode.ParentNode.InnerText.Split('-')[0].Trim(),
                        @"^(.+)\([0-9]+\)$").Groups[1].Value.Trim();

                    if (rosterAdditionNode.InnerText == "DC")
                    {
                        user.Position = DIPosition.DivisionCommander;
                    }
                    else if (rosterAdditionNode.InnerText == "DV")
                    {
                        user.Position = DIPosition.DivisionVice;
                    }

                    AddDIUnitInformationFromNode(user, rosterAdditionNode);
                }
                else
                {
                    return;
                }
            }

            if (user.Roster == null) // Team Leadership
            {
                string[] additions = {"TL", "2IC", "COACH"};
                
                var divisionNode = node
                    .SelectNodes("//div[contains(@class,'div-container')]")
                    // Find the correct division
                    .FirstOrDefault(x => x
                         .FirstChild(y => y.Name == "div" && y.HasClass("div-header"))
                         .FirstChild(y => y.Name == "h3" && y.HasClass("division-title"))
                         .GetClasses()
                         .First(y => y != "division-title") == user.Division);

                var teamNode = divisionNode?
                    .Descendants()?
                    .Where(x => x.Name == "div" && x.GetClasses().Contains("team-wrapper"))
                    .FirstOrDefault(x => x.FirstPreviousSibling(y => y.Name == "h4").InnerText.StartsWith(user.Team));

                var rosterAdditionNode = teamNode?
                    .Descendants()?
                    .FirstOrDefault(x => x.Name == "a" &&
                                         x.HasClass("roster-addition") &&
                                         additions.Contains(x.InnerText) &&
                                         x.Attributes["title"]?.Value.Contains(user.Name) == true);

                if (rosterAdditionNode != null)
                {
                    user.Roster = Regex.Match(rosterAdditionNode.ParentNode.InnerText.Split('-')[0].Trim(), @"^(.+)\([0-9]+\)").Groups[1].Value.Trim();

                    if (rosterAdditionNode.InnerText == "TL")
                    {
                        user.Position = DIPosition.TeamLeader;
                    }
                    else if (rosterAdditionNode.InnerText == "2IC")
                    {
                        user.Position = DIPosition.SecondInCommand;
                    }
                }
            }
        }

        private static void AddDIUnitInformationFromNode(DIUser user, HtmlNode node)
        {
            AddDIPosition(user, node);

            do
            {
                node = node.ParentNode;

                if (node == node.OwnerDocument.DocumentNode) break;

                if (node.Name == "li" && node.HasClass("RL-li"))
                {
                    user.Position = DIPosition.RosterLeader;
                    continue;
                }

                if (user.Roster == null && node.Name == "div" && node.HasClass("roster-container"))
                {
                    var header = node.FirstPreviousSibling(x => x.Name == "h5" && x.HasClass("roster-header"));

                    user.Roster = Regex.Match(header.InnerHtml, @"^(.+)\([0-9]+\)").Groups[1].Value.TrimEnd();
                }
                else if (user.Team == null && node.Name == "div" && node.HasClass("team-wrapper"))
                {
                    var header = node.FirstPreviousSibling(x => x.Name == "h4");

                    user.Team = Regex.Match(header.InnerHtml, @"^(.+)\([0-9]+\)").Groups[1].Value.TrimEnd();
                }
                else if (user.Division == null && node.Name == "div" && node.HasClass("div-container"))
                {
                    user.Division = node
                        .FirstChild(x => x.Name == "div" && x.HasClass("div-header"))
                        .FirstChild(x => x.Name == "h3" && x.HasClass("division-title"))
                        .GetClasses()
                        .First(x => x != "division-title");
                }
                else if (user.House == null && node.Name == "div" && node.HasClass("house-container"))
                {
                    var house = node
                        .GetClasses()
                        .First(x => x != "house-container");

                    user.House = $"House {char.ToUpper(house[0]) + house.Substring(1)}";

                    break;
                }
            } while (true);
        }

        private static void AddDIPosition(DIUser user, HtmlNode node)
        {
            var position = node
                .FirstPreviousSibling(x => x.Name == "a" && x.HasClass("position"))?
                .GetClasses()?
                .First(x => x != "topdisplay" && x != "position");

            if (position == "HG")
            {
                user.Position = DIPosition.HouseGeneral;
            }
            else if (position == "FC")
            {
                user.Position = DIPosition.FirstCommander;
            }
            else if (position == "DC")
            {
                user.Position = DIPosition.DivisionCommander;
            }
            else if (position == "DV")
            {
                user.Position = DIPosition.DivisionVice;
            }
            else if (position == "TL")
            {
                user.Position = DIPosition.TeamLeader;
            }
            else if (position == "twoIC")
            {
                user.Position = DIPosition.SecondInCommand;
            }
            else if (position == "RL")
            {
                user.Position = DIPosition.RosterLeader;
            }
        }

        public static void Save(MasterDivisionRegistry mdr)
        {
            using (var fileStream = new FileStream("mdr.json", FileMode.Create))
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(fileStream, Encoding.UTF8, true, true, "    "))
                {
                    var settings = new DataContractJsonSerializerSettings();
                    settings.EmitTypeInformation = EmitTypeInformation.AsNeeded;
                    settings.KnownTypes = new []
                    {
                        typeof(DIUnit),
                        typeof(MasterDivisionRegistry),
                        typeof(DIHouse),
                        typeof(DIDivision),
                        typeof(DITeam),
                        typeof(DIRoster),
                        typeof(DIUser)
                    };

                    new DataContractJsonSerializer(typeof(MasterDivisionRegistry), settings).WriteObject(writer, mdr);
                }
            }
        }

        public static MasterDivisionRegistry Load()
        {
            if (!File.Exists("mdr.json")) return null;

            using (var fileStream = new FileStream("mdr.json", FileMode.Open))
            {
                var settings = new DataContractJsonSerializerSettings();
                settings.EmitTypeInformation = EmitTypeInformation.AsNeeded;
                settings.KnownTypes = new[]
                {
                    typeof(DIUnit),
                    typeof(MasterDivisionRegistry),
                    typeof(DIHouse),
                    typeof(DIDivision),
                    typeof(DITeam),
                    typeof(DIRoster),
                    typeof(DIUser)
                };

                return (MasterDivisionRegistry)new DataContractJsonSerializer(typeof(MasterDivisionRegistry), settings).ReadObject(fileStream);
            }
        }
    }

    [DataContract]
    public class DIHouse : DIUnit
    {
        [DataMember] private int _houseGeneralId;

        [IgnoreDataMember] public DIUser HouseGeneral => Members.FirstOrDefault(x => x.Id == _houseGeneralId);

        [DataMember] private int _firstCommanderId;

        [IgnoreDataMember] public DIUser FirstCommander => Members.FirstOrDefault(x => x.Id == _firstCommanderId);

        [DataMember] private int _houseAideId;

        [IgnoreDataMember] public DIUser HouseAide => Members.FirstOrDefault(x => x.Id == _houseAideId);

        [IgnoreDataMember] public override IEnumerable<DIUser> Members => SubUnits.SelectMany(x => x.Members);

        public DIHouse(string unitId) : base(unitId)
        {
        }

        public override void AddUser(DIUser user)
        {
            if (user.Division == null) user.Division = "Unassigned";

            if (SubUnits.All(x => x.UnitId != user.Division))
            {
                SubUnits.Add(new DIDivision(user.Division));
            }

            SubUnits.First(x => x.UnitId == user.Division).AddUser(user);

            if (user.Position == DIPosition.HouseGeneral)
            {
                _houseGeneralId = user.Id;
            }
            else if (user.Position == DIPosition.FirstCommander)
            {
                _firstCommanderId = user.Id;
            }
            else if (user.Position == DIPosition.HouseAide)
            {
                _houseAideId = user.Id;
            }
        }
    }

    [DataContract]
    public class DIDivision : DIUnit
    {
        [DataMember] private int _divisionCommanderId;

        [IgnoreDataMember] public DIUser DivisionCommander => Members.FirstOrDefault(x => x.Id == _divisionCommanderId);

        [DataMember] private int _divisionViceId;

        [IgnoreDataMember] public DIUser Vice => Members.FirstOrDefault(x => x.Id == _divisionViceId);

        [IgnoreDataMember] public override IEnumerable<DIUser> Members => SubUnits.SelectMany(x => x.Members);

        public DIDivision(string unitId) : base(unitId)
        {
        }

        public override void AddUser(DIUser user)
        {
            if (user.Team == null) user.Team = "Unassigned";

            if (SubUnits.All(x => x.UnitId != user.Team))
            {
                SubUnits.Add(new DITeam(user.Team));
            }

            SubUnits.First(x => x.UnitId == user.Team).AddUser(user);

            if (user.Position == DIPosition.DivisionCommander)
            {
                _divisionCommanderId = user.Id;
            }
            else if (user.Position == DIPosition.DivisionVice)
            {
                _divisionViceId = user.Id;
            }
        }
    }

    [DataContract]
    public class DITeam : DIUnit
    {
        [DataMember] private int _teamLeaderId;

        [IgnoreDataMember] public DIUser TeamLeader => Members.FirstOrDefault(x => x.Id == _teamLeaderId);

        [DataMember] private int _secondInCommandId;

        [IgnoreDataMember] public DIUser SecondInCommand => Members.FirstOrDefault(x => x.Id == _secondInCommandId);

        [IgnoreDataMember] public override IEnumerable<DIUser> Members => SubUnits.SelectMany(x => x.Members);

        public DITeam(string unitId) : base(unitId)
        {
        }

        public override void AddUser(DIUser user)
        {
            if (user.Roster == null) user.Roster = "Unassigned";

            if (SubUnits.All(x => x.UnitId != user.Roster))
            {
                SubUnits.Add(new DIRoster(user.Roster));
            }

            SubUnits.First(x => x.UnitId == user.Roster).AddUser(user);

            if (user.Position == DIPosition.TeamLeader)
            {
                _teamLeaderId = user.Id;
            }
            else if (user.Position == DIPosition.SecondInCommand)
            {
                _secondInCommandId = user.Id;
            }
        }
    }

    [DataContract]
    public class DIRoster : DIUnit
    {
        [DataMember] private int _rosterLeaderId;

        [IgnoreDataMember] public DIUser RosterLeader => Members.FirstOrDefault(x => x.Id == _rosterLeaderId);

        [DataMember] private readonly List<DIUser> _members;

        [IgnoreDataMember] public override IEnumerable<DIUser> Members => _members;

        public DIRoster(string unitId) : base(unitId)
        {
            _members = new List<DIUser>();
        }

        public override void AddUser(DIUser user)
        {
            _members.Add(user);

            if (user.Position == DIPosition.RosterLeader)
            {
                _rosterLeaderId = user.Id;
            }
        }
    }

    [DataContract]
    public class DIUser
    {
        public string ProfileUrl => $"https://di.community/profile/{Id}-{Name}";

        [DataMember] public string Name;
        [DataMember] public int Id;
        [DataMember] public ulong DiscordId;
        [DataMember] public DIRank Rank;
        [DataMember] public DIPosition Position;

        [DataMember] public string ImageUrl;

        [DataMember] public string House;
        [DataMember] public string Division;
        [DataMember] public string Team;
        [DataMember] public string Roster;
    }


    [DataContract]
    public enum DIRank
    {
        [EnumMember]
        Initiate,
        [EnumMember]
        InitiateStar,
        [EnumMember]
        Member,
        [EnumMember]
        Veteran,
        [EnumMember]
        Elite,
        [EnumMember]
        Mentor,
        [EnumMember]
        General,
        [EnumMember]
        Commander,
        [EnumMember]
        Vice,
        [EnumMember]
        Captain,
        [EnumMember]
        Warden,
        [EnumMember]
        Associate,
        [EnumMember]
        AwayST,
        [EnumMember]
        AwayLT
    }

    public enum DIPosition
    {
        [EnumMember]
        None,
        [EnumMember]
        Leader,
        [EnumMember]
        ChiefOfStaff,
        [EnumMember]
        ChiefAdministrator,
        [EnumMember]
        ChiefAide,
        [EnumMember]
        HouseGeneral,
        [EnumMember]
        FirstCommander,
        [EnumMember]
        HouseAide,
        [EnumMember]
        DivisionCommander,
        [EnumMember]
        DivisionVice,
        [EnumMember]
        TeamLeader,
        [EnumMember]
        SecondInCommand,
        [EnumMember]
        RosterLeader
    }
}
