using System;
using System.Collections.Generic;
using CodeHatch.Engine.Networking;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Players;
using System.Linq;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("TopKDR", "PaiN/Mordeus", "0.3.1", ResourceId = 1525)]
    [Description("Kill and death ratio with a top list")]
    public class TopKDR : ReignOfKingsPlugin
    {
        private bool changed;        
        private object tags;
        public string ChatTitle;
        private bool EnableScoreTags;

        private void LoadVariables()
        {
            ChatTitle = Convert.ToString(GetConfig("Settings", "Title", "[4F9BFF]Server:"));
            EnableScoreTags = Convert.ToBoolean(GetConfig("Settings", "EnableScoreTags", false));
            tags = GetConfig("ScoreTags", "Tags", new Dictionary<object, object>{
                {"[Tag1]", 5},
                {"(Tag2)", 10},
                {"[Tag3]", 15},
                {"{Tag4}", 20},
                {"$Tag5$", 25}
            });

            if (changed)
            {
                SaveConfig();
                changed = false;
            }
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                changed = true;
            }
            return value;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file!");
            Config.Clear();
            LoadVariables();
        }

        private StoredData data;

        private class StoredData
        {
            public Dictionary<ulong, int> Kills = new Dictionary<ulong, int>();
            public Dictionary<ulong, int> Deaths = new Dictionary<ulong, int>();
        }

        private void Init()
        {
            LoadVariables();
            LoadDefaultMessages();
            data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("TopKDR_data");
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SynError"] = "{Title} [FF0000]Syntax: /top[number] || ex. /top 5, /top 10[FFFFFF]",
                ["TopList"] = "Name: {0}, Kills: {1}, Deaths: {2}, Score: {3}"
            }, this);
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("TopKDR_data", data);

        private void OnEntityDeath(EntityDeathEvent e)
        {
            if (e.Entity == null) return;
            if (e.KillingDamage.DamageSource == null) return;
            if (e == null) return;
            if (e.KillingDamage.DamageSource == e.Entity) return;
            ulong victimid = e.Entity.OwnerId;
                ulong attackerid = e.KillingDamage.DamageSource.OwnerId;

                if (data.Kills.ContainsKey(attackerid))
                data.Kills[attackerid] = data.Kills[attackerid] + 1;
                else
                data.Kills.Add(attackerid, 1);

                if (data.Deaths.ContainsKey(victimid))
                data.Deaths[victimid] = data.Deaths[victimid] + 1;
                else
                data.Deaths.Add(victimid, 1);

                SaveData();
        }

        private void OnPlayerChat(PlayerEvent e)
        {
            if (!EnableScoreTags) return;

            var player = e.Player;
            player.DisplayNameFormat = $"{GetPlayerTag(player)} %name%";
        }

        [ChatCommand("top")]
        private void TopCommand(Player player, string command, string[] args)
        {
            string playerId = player.Id.ToString();
            var list = data.Kills.OrderByDescending(pair => pair.Value).ToList();
            if (args.Length == 0)
            {
                SendReply(player, Message("SynError", playerId));
                return;
            }

            for (int i = 0; i < Convert.ToInt32(args[0]); i++)
            {
                if (list.Count < i + 1) break;

                var kills = list[i].Value;
                var deaths = 0;
                if (!data.Deaths.ContainsKey(list[i].Key))
                    deaths = 0;
                else
                    deaths = data.Deaths[list[i].Key];

                var score = kills - deaths;
                if (score <= 0) score = 0;
                if (list[i].Key == 9999999999) continue;//removes server from list
                SendReply(player, $"{i+1}. " + Message("TopList", playerId), Server.GetPlayerById(list[i].Key).DisplayName, kills.ToString(), deaths.ToString(), score.ToString());
            }
        }

        private int GetPlayerScore(Player player)
        {
            var kills = 0;
            if (!data.Kills.ContainsKey(player.Id))
                kills = 0;
            else
                kills = data.Kills[player.Id];

            var deaths = 0;
            if (!data.Deaths.ContainsKey(player.Id))
                deaths = 0;
            else
                deaths = data.Deaths[player.Id];

            var score = kills - deaths;
            if (score <= 0) score = 0;

            return score;
        }

        private string GetPlayerTag(Player player)
        {
            var playertag = "";
            foreach (var c in Config["ScoreTags", "Tags"] as Dictionary<string, object>)
            {
                if (GetPlayerScore(player) >= Convert.ToInt32(c.Value)) playertag = c.Key;
                return playertag;
            }
            return null;
        }        
        private string Message(string key, string id = null, params object[] args)
        {
            return lang.GetMessage(key, this, id).Replace("{Title}", ChatTitle);
        }
    }
}
