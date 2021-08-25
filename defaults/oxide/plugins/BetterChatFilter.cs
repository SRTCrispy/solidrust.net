﻿// Requires: BetterChat


using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Text.RegularExpressions;
using Oxide.Game.Rust.Libraries;

namespace Oxide.Plugins
{
    [Info("Better Chat Filter", "NooBlet", "1.5.1", ResourceId = 2403)]
    [Description("Filter for Better Chat")]
    public class BetterChatFilter : CovalencePlugin
    {
        [PluginReference] private Plugin BetterChat, BetterChatMute, EnhancedBanSystem;

        //////////////////////////////////////////////////////////////////////////////////

        #region BetterChatHook

        private object OnBetterChat(Dictionary<string, object> messageData) => Filter(messageData);

        private object Filter(Dictionary<string, object> messageData)
        {
            IPlayer player = (IPlayer)messageData["Player"];
            
            if (ExcludeTeamChat)
            {
                if (messageData["ChatChannel"].ToString() == "Team") { return messageData; }

            }

            if (WordFilter_Enabled)
            {
               
                var message = (string)messageData["Message"];
                messageData["Message"] = FilterText(player, message);               
                return messageData;
            }
            
            return messageData;
        }

        #endregion

        //////////////////////////////////////////////////////////////////////////////////

        #region Data
        private readonly Player Player = Interface.Oxide.GetLibrary<Player>();
        private static OffenseData offensedata;
        public Dictionary<string, OffenseData> PlayerOffenses = new Dictionary<string, OffenseData>();
        public class OffenseData
        {
            public int offenses { get; set; }
            public int muteCount { get; set; }
            public DateTime timesinsoffense { get; set; }
            public OffenseData()
            {
                offenses = 1;
                muteCount = 0;
                timesinsoffense = new DateTime(2200, 1, 1, 0, 0, 0);
            }
            public OffenseData(int offenses, int muteCount, DateTime timesinsoffence)
            {
                this.offenses = offenses;
                this.muteCount = muteCount;
                this.timesinsoffense = timesinsoffence;
            }
        }
        #endregion

        #region Cached Variables
        private Timer ot;
        private bool WordFilter_Enabled = true;
        private string WordFilter_Replacement = "*";
        private bool WordFilter_UseCustomReplacement = false;
        private string WordFilter_CustomReplacement = "Unicorn";
        private List<object> WordFilter_Phrases = new List<object> {
                "bitch",
                "cunt",
                "nigger",
                "faggot",
                "fuck"
        };
        private int MuteCount = 3;
        private int KickCount = 3;
        private int BanCount = 20;
        private int BanTimeMin = 30;
        private bool BroadcastKick = true;
        private bool BroadcastBan = true;
        private int TimeToMute = 300;
        private bool UseRegex = false;
        private string regextouse = @"";
        private int clear = 0;
        private bool ExcludeTeamChat = false;
        private bool warnoffenseamount = false;
        private bool useclearontime = false;
        private int timetoclear = 60;

        #endregion

        #region Plugin General


        private string ListToString<T>(List<T> list, int first = 0, string seperator = ", ")
        {
            return string.Join(seperator, (from val in list select val.ToString()).Skip(first).ToArray());
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("BetterChatFilter", PlayerOffenses);

        private string GetLang(string key, string id) => lang.GetMessage(key, this, id);

        private void Loaded()
        {
            LoadConfiguration();
            LoadData();
            permission.RegisterPermission(Name + ".admin", this);
            if (useclearontime)
            {
                OffenseTimer();
            }
            
        }

        void Unload()
        {
            if (ot != null)
            {
                ot.Destroy();
            }            
        }

        private void Offsense(IPlayer player)
        {
            var p = player.Object as BasePlayer;

            int offenseCount = 0;
           
            if (PlayerOffenses.ContainsKey(player.Id))
            {
                PlayerOffenses[player.Id].offenses++;
                offenseCount = PlayerOffenses[player.Id].offenses;
                SaveData();
            }
            else if (!PlayerOffenses.ContainsKey(player.Id))
            {
                PlayerOffenses.Add(player.Id, new OffenseData());
                offenseCount = PlayerOffenses[player.Id].offenses;
                SaveData();
            }
            if (offenseCount >= MuteCount && MuteCount != 0)
            {
                if (clear == 1 || clear == 3)
                {
                    ClearOffense(player);
                }
                //mute check suggested OuTSMoKE
                if (!GetIsMuted(ref p))
                {
                    if (BetterChatMute)
                    {
                        server.Command("mute", player.Id, $"{TimeToMute}s", $"{GetLang("MuteReason", player.Id)}");
                    }

                }

                Mutes(player);
            }
            if (offenseCount >= KickCount && KickCount != 0)
            {
                if (clear == 1 || clear == 2)
                {
                    ClearOffense(player);
                }
                if (BroadcastKick) server.Broadcast(string.Format(GetLang("BroadcastKickFormat", null), player.Name, GetLang("KickReason", null)));
                player.Kick(GetLang("KickReason", player.Id));
            }

            if (offenseCount >= BanCount && BanCount != 0)
            {
                if (clear == 1 || clear == 4)
                {
                    ClearOffense(player);
                }

                if (BroadcastBan) server.Broadcast(string.Format(GetLang("BroadcastBanFormat", null), player.Name, GetLang("BanReason", null)));

                BanPlayer(player, BanTimeMin, GetLang("BanReason", player.Id));
            }
            PlayerOffenses[player.Id].timesinsoffense = DateTime.Now;
            SaveData();
            return;
        }

        private void BanPlayer(IPlayer player, int time, string reason)
        {
            if (BanTimeMin == 0)
            {
                if (EnhancedBanSystem) { server.Command($"ban {player.Id} {reason}"); return; }
                player.Ban(reason);
            }
            else
            {
                if (EnhancedBanSystem) { server.Command($"ban {player.Id} {time}m {reason}"); return; }
                Player.Ban(player.Id, reason);
                float t = (float)BanTimeMin * 60;
                timer.Once(t, () =>
                {
                    Player.Unban(player.Id);
                });
            }
        }

        private void WarnPlayer(IPlayer player)
        {
            if (warnoffenseamount)
            {
                if (PlayerOffenses.ContainsKey(player.Id))
                {
                    if (KickCount > PlayerOffenses[player.Id].offenses)
                    {
                        if (KickCount <= 0)
                        {
                            player.Reply(string.Format(GetLang("OffenseWarning", player.Id), PlayerOffenses[player.Id].offenses + 1));
                        }
                        else
                        {

                            player.Reply(string.Format(GetLang("OffenseWarningKick", player.Id), PlayerOffenses[player.Id].offenses + 1, KickCount));

                        }

                    }
                    if (BanCount != 0)
                    {
                        player.Reply(string.Format(GetLang("OffenseWarningBan", player.Id), PlayerOffenses[player.Id].offenses + 1, BanCount));
                    }

                }

            }
        }

        private bool GetIsMuted(ref BasePlayer aPlayer)
        {
            bool isServerMuted = aPlayer.HasPlayerFlag(BasePlayer.PlayerFlags.ChatMute);

            if (BetterChatMute != null && !BetterChatMute.IsLoaded)
            {
                return isServerMuted || (bool)BetterChatMute.Call("API_IsMuted", aPlayer.IPlayer);
            }
            else
            {
                return isServerMuted;
            }
        }

        private void Mutes(IPlayer player)
        {
            if (!(PlayerOffenses.ContainsKey(player.Id)))
            {
                return;
            }
            else
            {
                int muteCount = PlayerOffenses[player.Id].muteCount;
                PlayerOffenses[player.Id].muteCount++;
                SaveData();
                // TODO: Bigger punishments.
            }
        }
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TimeClear"] = "Your Chat Offenses has been cleared for good behavior",
                ["KickReason"] = "Bad Language",
                ["BanReason"] = "Bad Language",
                ["MuteReason"] = "Bad Language",
                ["BroadcastKickFormat"] = "{0} was kicked for {1}",
                ["BroadcastBanFormat"] = "{0} was banned for {1}",
                ["NoOffenses"] = "You have no offenses.",
                ["OffenseCount"] = "You have {0} offenses.",
                ["SyntaxError"] = "Invalid Syntax",
                ["HasOffenseCount"] = "{0} has {1} offenses.",
                ["HasNoOffenses"] = "{0} has no offenses.",
                ["Cleared"] = "Offenses for {0} cleared.",
                ["SelfCleared"] = "Your offenses have been cleared by {0}",
                ["NoPermission"] = "You do not have permission to use this.",
                ["OffenseWarning"] = "You currently have {0} Bad language offenses",
                ["OffenseWarningKick"] = "You currently have {0} of {1} Bad language Offenses till kick",
                ["OffenseWarningBan"] = "You currently have {0} of {1} Bad language Offenses till Ban"
            }, this, "en");
        }
        private void ClearOffense(IPlayer player)
        {
            if (PlayerOffenses.ContainsKey(player.Id))
            {
                PlayerOffenses[player.Id].offenses = 0;
                PlayerOffenses[player.Id].timesinsoffense = new DateTime(2200, 1, 1, 0,0,0);
                SaveData();
            }
            else
            {
                return;
            }
        }

        private void LoadData()
        {
            PlayerOffenses = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, OffenseData>>(Name);
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating new config file...");
        }

        private void LoadConfiguration()
        {
            CheckCfg<bool>("Use Offenses clear timer", ref useclearontime);
            CheckCfg<int>("Time to Clear Offense in minutes", ref timetoclear);
            CheckCfg<bool>("Exclude Team Chat", ref ExcludeTeamChat);
            CheckCfg<bool>("Warn Players with offense amount", ref warnoffenseamount);
            CheckCfg<int>("Offenses - Time To Mute", ref TimeToMute);
            CheckCfg<int>("Offenses - Count To Mute", ref MuteCount);
            CheckCfg<int>("Offenses - Count To Kick", ref KickCount);
            CheckCfg<int>("Offenses - Count To Ban", ref BanCount);
            CheckCfg<int>("Time to Ban in Minutes", ref BanTimeMin);
            CheckCfg<bool>("Word Filter - Enabled", ref WordFilter_Enabled);
            CheckCfg<string>("Word Filter - Replacement", ref WordFilter_Replacement);
            CheckCfg<bool>("Word Filter - Use Custom Replacement", ref WordFilter_UseCustomReplacement);
            CheckCfg<string>("Word Filter - Custom Replacement", ref WordFilter_CustomReplacement);
            CheckCfg<bool>("Advanced - Use REGEX", ref UseRegex);
            CheckCfg<string>("Advanced - Regex to use", ref regextouse);
            CheckCfg<List<object>>("Word Filter - Phrases", ref WordFilter_Phrases);
            CheckCfg<int>("Clear Offense After (0 - Disabled, 1 - All Kick/Mute/ban, 2 - Kick,  3 - Mute,  4 - Ban", ref clear);
            CheckCfg<bool>("Offenses - Broadcast kick", ref BroadcastKick);
            CheckCfg<bool>("Offenses - Broadcast Ban", ref BroadcastBan);
            SaveConfig();
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        #endregion

        //////////////////////////////////////////////////////////////////////////////////


        #region Command 

        [Command("clearfilters")]
        private void ClearFilter(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission("betterchatfilter.admin"))
            {
                foreach (var p in players.Connected)
                {
                    ClearOffense(p);
                    player.Reply(string.Format(GetLang("Cleared", player.Id), p.Name));
                    p.Reply(string.Format(GetLang("SelfCleared", player.Id), player.Name));
                }
            }
        }

        [Command("filter")]
        private void CmdFilter(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission("betterchatfilter.admin"))
            {

                if (args.Length == 0)
                {
                    int offenseCount = PlayerOffenses.ContainsKey(player.Id) == true ? PlayerOffenses[player.Id].offenses : 0;
                    if (offenseCount == 0)
                    {
                        player.Reply(GetLang("NoOffenses", player.Id));
                    }
                    else
                    {
                        player.Reply(string.Format(GetLang("OffenseCount", player.Id), offenseCount));
                    }
                    return;
                }
                if (args.Length != 2)
                {
                    player.Reply(GetLang("SyntaxError", player.Id));
                    return;
                }
                IPlayer target = GetPlayer(args[1], player);
                if (target == null)
                {
                    return;
                }
                switch (args[0])
                {

                    case "check":
                    case "info":
                        int offenseCount = PlayerOffenses.ContainsKey(target.Id) == true ? PlayerOffenses[target.Id].offenses : 0;
                        if (offenseCount == 0)
                        {
                            player.Reply(string.Format(GetLang("HasNoOffenses", player.Id), target.Name));
                        }
                        else
                        {
                            player.Reply(string.Format(GetLang("HasOffenseCount", player.Id), target.Name, offenseCount));
                        }
                        break;
                    case "remove":
                    case "clear":
                    case "delete":
                        ClearOffense(target);
                        player.Reply(string.Format(GetLang("Cleared", player.Id), target.Name));
                        target.Reply(string.Format(GetLang("SelfCleared", player.Id), player.Name));
                        break;
                }

            }
            else
            {
                if (args.Length > 0)
                {
                    player.Reply(GetLang("NoPermission", player.Id));
                    return;
                }
                int offenseCount = PlayerOffenses.ContainsKey(player.Id) == true ? PlayerOffenses[player.Id].offenses : 0;
                if (offenseCount == 0)
                {
                    player.Reply(GetLang("NoOffenses", player.Id));
                }
                else
                {
                    player.Reply(string.Format(GetLang("OffenseCount", player.Id), offenseCount));
                }
            }
        }


        private BasePlayer findPlayer(string name)
        {
            BasePlayer target = BasePlayer.FindAwakeOrSleeping(name);
            return target;
        }


        #endregion

        #region Word Filter

        private string FilterText(IPlayer player, string original)
        {
            var filtered = original;
            int count = 0;
            Regex r = new Regex(regextouse, RegexOptions.IgnoreCase);
            foreach (var word in original.Split(' '))
            {
                if (UseRegex)
                {
                    Match m = r.Match(word);
                    if (m.Success)
                    {
                        Puts($"REGEX MATCH : {player.Name} said: \"{original}\" which contained a bad word: \"{word}\"");
                        filtered = filtered.Replace(word, Replace(word));
                        count++;

                    }
                }
                foreach (string bannedword in WordFilter_Phrases)
                    if (TranslateLeet(word).ToLower().Contains(bannedword.ToLower()))
                    {
                        Puts($"BANNED WORDS MATCH :| {player.Name} said: \"{original}\" which contained a bad word: \"{word}\"");
                        filtered = filtered.Replace(word, Replace(word));
                        count++;
                        WarnPlayer(player);

                    }
            }

            if (count > 0)
            {
                Offsense(player);
            }

            return filtered;
        }



        private string Replace(string original)
        {
            var filtered = string.Empty;

            if (!WordFilter_UseCustomReplacement)
                for (; filtered.Count() < original.Count();)
                    filtered += WordFilter_Replacement;
            else
                filtered = WordFilter_CustomReplacement;

            return filtered;
        }

        private string TranslateLeet(string original)
        {
            var translated = original;

            var leetTable = new Dictionary<string, string>
            {
                {"}{", "h"},
                {"|-|", "h"},
                {"]-[", "h"},
                {"/-/", "h"},
                {"|{", "k"},
                {"/\\/\\", "m"},
                {"|\\|", "n"},
                {"/\\/", "n"},
                {"()", "o"},
                {"[]", "o"},
                {"vv", "w"},
                {"\\/\\/", "w"},
                {"><", "x"},
                {"2", "z"},
                {"4", "a"},
                {"@", "a"},
                {"8", "b"},
                {"ß", "b"},
                {"(", "c"},
                {"<", "c"},
                {"{", "c"},
                {"3", "e"},
                {"€", "e"},
                {"6", "g"},
                {"9", "g"},
                {"&", "g"},
                {"#", "h"},
                {"$", "s"},
                {"7", "t"},
                {"|", "l"},
                {"1", "i"},
                {"!", "i"},
                {"0", "o"}
            };

            foreach (var leet in leetTable)
                translated = translated.Replace(leet.Key, leet.Value);

            return translated;
        }

        #endregion

        //////////////////////////////////////////////////////////////////////////////////


        #region Finding Helper

        private IPlayer GetPlayer(string nameOrID, IPlayer player)
        {
            if (IsParseableTo<string, ulong>(nameOrID) && nameOrID.StartsWith("7656119") && nameOrID.Length == 17)
            {
                IPlayer result = players.All.ToList().Find((p) => p.Id == nameOrID);

                if (result == null)
                    player.Reply($"Could not find player with ID '{nameOrID}'");

                return result;
            }

            List<IPlayer> foundPlayers = new List<IPlayer>();

            foreach (IPlayer current in players.Connected)
            {
                if (current.Name.ToLower() == nameOrID.ToLower())
                    return current;

                if (current.Name.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(current);
            }

            switch (foundPlayers.Count)
            {
                case 0:
                    player.Reply($"Could not find player with name '{nameOrID}'");
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    string[] names = (from current in foundPlayers select current.Name).ToArray();
                    player.Reply("Multiple matching players found: \n" + string.Join(", ", names));
                    break;
            }

            return null;
        }

        #endregion

        #region Convert Helper

        private bool IsParseableTo<S, R>(S s)
        {
            R result;
            return TryParse(s, out result);
        }

        private bool TryParse<S, R>(S s, out R c)
        {
            try
            {
                c = (R)Convert.ChangeType(s, typeof(R));
                return true;
            }
            catch
            {
                c = default(R);
                return false;
            }
        }

        #endregion

        #region Timed Offense Helpers
        private bool istimeup(DateTime start,string id)
        {
            int h = 0;
            int m = 0;
            if (timetoclear == 0) { PlayerOffenses[id].timesinsoffense = new DateTime(2200, 1, 1, 0, 0, 0); return false; }
            if (timetoclear == 60) { h = 1;m = 0; }
            if (timetoclear > 60) { h = 1; m = timetoclear - 60; }
            if (timetoclear < 60) { h = 0; m = timetoclear; }
            if (timetoclear == 120) { h = 2; m = 0; }
            if (timetoclear > 120) { PlayerOffenses[id].timesinsoffense = new DateTime(2200, 1, 1, 0, 0, 0); return false; }
            TimeSpan time = new TimeSpan(h, m, 0);
            DateTime end = start.Add(time);
            if (DateTime.Now >= end)
            {
                return true;
            }

            return false;
        }

        private void ClearTimedOffences()
        {
            foreach (var p in PlayerOffenses)
            {
                if (p.Value.timesinsoffense == null) { continue; }
                if (istimeup(p.Value.timesinsoffense,p.Key))
                {
                    var player = findPlayer(p.Key).IPlayer;
                    if (player == null) { continue; }
                    ClearOffense(player);
                    player.Reply(string.Format(GetLang("TimeClear", player.Id)));
                    Puts($"{player.Name} Offenses cleared for good behavior");
                }

            }
        }
        private void OffenseTimer()
        {
            ot = timer.Every(60f, () =>
            {
                ClearTimedOffences();
               
            });
        }

        #endregion Timed Offence Helpers
    }
}
