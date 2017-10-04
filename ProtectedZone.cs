﻿/*ToDo: 
 1. Add the ability to edit a zone by using zoneId instead of only standing in zone.
 2. Add API(currently working on it)  
 Known Bugs: 
 2 zones overlapping causes enter/exit messages to not work properly, but otherwise work fine. 
  */
using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using CodeHatch.Engine.Networking;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Common;
using CodeHatch.StarForge.Sleeping;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using CodeHatch.Thrones.Weapons.Salvage;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Blocks.Inventory;
using CodeHatch.Blocks;
using CodeHatch.ItemContainer;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Engine.Events.Prefab;
using CodeHatch.Inventory.Blueprints;

namespace Oxide.Plugins
{
    [Info("ProtectedZone", "Mordeus", "1.0.2")]
    public class ProtectedZone : ReignOfKingsPlugin
    {
        private DynamicConfigFile ProtectedZoneData;
        private StoredData storedData;
        private readonly Dictionary<string, ZoneInfo> ZoneDefinitions = new Dictionary<string, ZoneInfo>();
        private Dictionary<Player, PlayerData> PData;
        //config
        private bool MessagesOn => GetConfig("MessagesOn", false);
        private bool ZoneCheckOn => GetConfig("ZoneCheckOn", false);
        private float MessageInterval => GetConfig("MessageInterval", 100f);
        private float ZoneCheckInterval => GetConfig("ZoneCheckInterval", 1f);
        private bool CrestCheckOn => GetConfig("CrestCheckOn", false);
        private bool AdminCanBuild => GetConfig("AdminCanBuild", true);
        private bool AdminCanKill => GetConfig("AdminCanKill", true);
        List<Vector2> zones = new List<Vector2>();
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();
        private Dictionary<string, Timer> ZoneCheckTimer = new Dictionary<string, Timer>();
        

        #region Data
        protected override void LoadDefaultConfig()
        {              
            Config["MessageInterval"] = MessageInterval;
            Config["ZoneCheckInterval"] = ZoneCheckInterval;
            Config["MessagesOn"] = MessagesOn;
            Config["ZoneCheckOn"] = ZoneCheckOn;
            Config["CrestCheckOn"] = CrestCheckOn;
            Config["AdminCanBuild"] = AdminCanBuild;
            Config["AdminCanKill"] = AdminCanKill;
            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                
                { "notAllowed", "[F5D400]You are not allowed to do this![FFFFFF]" },
                { "areaProtected", "[F5D400]This area is Protected![FFFFFF]" },
                { "noBuild", "[F5D400]No building in this area![FFFFFF]" },
                { "noPlace", "[F5D400]You cannot place a {0} Here![FFFFFF]" },
                { "noPvP", "[F5D400]This area is Protected, no PvP![FFFFFF]" },
                { "noSleeper", "[F5D400]This area is Protected, You can not damage a sleeper![FFFFFF]" },
                { "noCrest", "[F5D400]This area is Protected, You can not damage a crest![FFFFFF]" },
                { "help", "[F5D400]type /zone help to open the help menu[FFFFFF]"},
                { "synError", "[F5D400]Syntax Error: [FFFFFF]Type '/zone help' to view available options" },
                { "nameExists", "[0000FF]The Name {0} already exists[FFFFFF]" },
                { "zoneAdded", "[4F9BFF]Zone [FFFFFF]{0}[4F9BFF] sucessfully addded, named [FFFFFF]{1}." },
                { "zoneInfo", "[FFFFFF]This is ZoneID [4F9BFF]{0}[FFFFFF], Zone Name [4F9BFF]{1}[FFFFFF]" },
                { "zoneList", "[FFFFFF]ZoneID [4F9BFF]{0}[FFFFFF], Zone Name [4F9BFF]{1}[FFFFFF], Location [4F9BFF]{2}[FFFFFF]" },
                { "zoneEdited", "[4F9BFF]You have changed the {0} of ZoneID {1} to {2}.[FFFFFF]" },
                { "zoneLocError", "[F5D400]You are not standing in a zone.[FFFFFF]" },
                { "noZoneError",  "[F5D400]No Zones loaded.[FFFFFF]" },
                { "zoneError", "[F5D400]That zone does not exist.[FFFFFF]" },
                { "inZoneError", "[F5D400]You are currently to close to a zone, you cannot make another.[FFFFFF]" },
                { "zoneRemove", "[0000FF]ZoneID {0} was removed.[FFFFFF]" },
                { "zoneMessage", "[4F9BFF]You have entered {0} zone.[FFFFFF]" },
                { "zoneFlag1", "[4F9BFF]radius: [FFFFFF]{0}" },
                { "zoneFlag2", "[4F9BFF]pve Flag: [FFFFFF]{0}" },
                { "zoneFlag3", "[4F9BFF]nobuild Flag: [FFFFFF]{0}" },
                { "zoneFlag4", "[4F9BFF]nodamage Flag: [FFFFFF]{0}" },
                { "zoneFlag5", "[4F9BFF]nosleeperdamage Flag: [FFFFFF]{0}" },
                { "zoneFlag6", "[4F9BFF]nocrestdamage Flag: [FFFFFF]{0}" },
                { "zoneFlag7", "[4F9BFF]messageon Flag: [FFFFFF]{0}" },
                { "zoneFlag8", "[4F9BFF]entermessageon Flag: [FFFFFF]{0}" },
                { "zoneFlag9", "[4F9BFF]exitmessageon Flag: [FFFFFF]{0}" },
                { "zoneFlag10", "[4F9BFF]zonemessage: [FFFFFF]{0}" },
                { "zoneFlag11", "[4F9BFF]enterzonemessage: [FFFFFF]{0}" },
                { "zoneFlag12", "[4F9BFF]exitzonemessage: [FFFFFF]{0}" },
                { "logPvP", "player {0} attempted to a harm a player ,cancelling damage." },
                { "logSleeper", "player {0} attempted to kill a Sleeper ,cancelling damage." },
                { "logCrest", "player {0} attempted to damage a crest ,cancelling damage." },
                { "logNoBuild", "player {0} attempted to build in a no-build zone,cancelling placement."},
                { "logNoDamage", "player {0} attempted to damage a block ,cancelling damage."},
                { "logCrestPlace", "player {0} attempted to place a {1} in a no-build zone, cancelling placement."},
                { "helpTitle", $"[4F9BFF]{Title}  v{Version}"},
                { "helpHelp", "[4F9BFF]/zone help[FFFFFF] - Display the help menu"},
                { "helpAdd", "[4F9BFF]/zone add <name> [FFFFFF]- Sets Zone."},
                { "helpList", "[4F9BFF]/zone list [FFFFFF]- Lists all zones"},
                { "helpRemove", "[4F9BFF]/zone remove <num> [FFFFFF]- Removes zone."},
                { "helpInfo", "[4F9BFF]/zone info [FFFFFF]- Zone info"},
                { "helpEdit", "[4F9BFF]/zone edit <name>[FFFFFF]- Edit zone values."},            

        }, this);
        }       
        void OnServerInitialized()
        {
            CacheAllOnlinePlayers();
        }		
		private void Init()
		{
		    LoadDefaultConfig();
            LoadDefaultMessages();
            ProtectedZoneData = Interface.Oxide.DataFileSystem.GetFile("ProtectedZone");
            ProtectedZoneData.Settings.Converters = new JsonConverter[] { new StringEnumConverter(), new UnityVector3Converter(), };                          
            LoadZones();
            LoadData();
            PData = new Dictionary<Player, PlayerData>();	
		}
        private void LoadData()
        {

            ZoneDefinitions.Clear();
            try
            {
                ProtectedZoneData.Settings.NullValueHandling = NullValueHandling.Ignore;
                storedData = ProtectedZoneData.ReadObject<StoredData>();
                Puts("Loaded {0} Zone definitions", storedData.ZoneDefinitions.Count);
            }
            catch
            {
                Puts("Failed to load StoredData");
                storedData = new StoredData();
            }
            ProtectedZoneData.Settings.NullValueHandling = NullValueHandling.Include;
            foreach (var zonedef in storedData.ZoneDefinitions)
                ZoneDefinitions[zonedef.Id] = zonedef;
            
        }        
        private class StoredData
        {
            public readonly HashSet<ZoneInfo> ZoneDefinitions = new HashSet<ZoneInfo>();
        }
        private void SaveData()
        {
            ProtectedZoneData.WriteObject(storedData);
            PrintWarning("Saved ProtectedZone data");            
        }
        #endregion
        #region Zone Definition
        public class ZoneInfo
        {
            public string Name;
            public string Id;
            public Vector3 Location;
            public string ZoneName;
            public float ZoneX;
            public float ZoneY;
            public float ZoneZ;
            public string ZoneCreatorName;
            public float ZoneRadius;
            public bool ZonePVE = false;
            public bool ZoneNoBuild = false;
            public bool ZoneNoDamage = false;
            public bool ZoneNoSleeperDamage = false;
            public bool ZoneNoCrestDamage = false;
            public bool ZoneMessageOn = false;
            public bool ZoneEnterMessageOn = false;
            public bool ZoneExitMessageOn = false;
            public string ZoneMessage = "This is a no PvP zone.";
            public string EnterZoneMessage = "You have entered a no PvP zone.";
            public string ExitZoneMessage = "You have exited a no PvP zone.";
            public ZoneInfo()
            {
            }

            public ZoneInfo(Vector3 position)
            {
                ZoneRadius = 20f;
                Location = position;
            }

        }
        #endregion
        #region Player Data
        class PlayerData
        {
            public ulong PlayerId;
            public bool EnterZone;
            public bool ExitZone;
            public bool InZone;
            public string ZoneId;
            public DateTime TimeEnterZone;

            public PlayerData(ulong playerId)
            {
                PlayerId = playerId;                
                ZoneId = "0";
                EnterZone = false;
                ExitZone = false;
                InZone = false;
                TimeEnterZone = DateTime.Now;               
            }
        }
        #endregion

        #region Commands

        [ChatCommand("zone")]
        private void ZoneCommand(Player player, string cmd, string[] args)
        {
			string playerId = player.Id.ToString();
            if (!player.HasPermission("admin"))
            {
                player.SendError(lang.GetMessage("notAllowed", this, playerId));
                return;
            }            
            if (args == null || args.Length == 0)
            {
                player.SendError(lang.GetMessage("help", this, playerId));
                return;
            }
            switch (args[0])
            {
                case "help":
                    {
                        
                        SendReply(player, lang.GetMessage("helpTitle", this, playerId));                        
                        SendReply(player, lang.GetMessage("helpHelp", this, playerId));                        
                        SendReply(player, lang.GetMessage("helpAdd", this, playerId));                        
                        SendReply(player, lang.GetMessage("helpList", this, playerId));                        
                        SendReply(player, lang.GetMessage("helpRemove", this, playerId));                        
                        SendReply(player, lang.GetMessage("helpInfo", this, playerId));                        
                        SendReply(player, lang.GetMessage("helpEdit", this, playerId));
                    }
                    return;

                case "add":
                    {
                        PlayerData Player = GetCache(player);
                        if (args.Length != 2)
                        {
                            SendReply(player, lang.GetMessage("helpAdd", this, playerId));
                            return;
                        }
                        string name = args[1];
                        foreach (var zoneDef in ZoneDefinitions)
                        {
                            if (zoneDef.Value.Name == name)
                            {
                                SendReply(player, lang.GetMessage("nameExists", this, playerId), name);
                                return;
                            }
                            if (IsInZone(player, zoneDef.Value.Id, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                            {
                                SendReply(player, lang.GetMessage("inZoneError", this, playerId));
                                return;
                            }
                        }                       
                        var newzoneinfo = new ZoneInfo(player.Entity.Position) { Id = UnityEngine.Random.Range(1, 99999999).ToString() };                       
                        if (ZoneDefinitions.ContainsKey(newzoneinfo.Id)) storedData.ZoneDefinitions.Remove(ZoneDefinitions[newzoneinfo.Id]);
                        ZoneDefinitions[newzoneinfo.Id] = newzoneinfo;                        
                        storedData.ZoneDefinitions.Add(newzoneinfo);
                        SaveData();                        
                        float zonex = player.Entity.Position.x;
                        float zoney = player.Entity.Position.y;
                        float zonez = player.Entity.Position.z;
                        ZoneDefinitions[newzoneinfo.Id].ZoneX = zonex;
                        ZoneDefinitions[newzoneinfo.Id].ZoneZ = zonez;
                        ZoneDefinitions[newzoneinfo.Id].Name = name;
                        ZoneDefinitions[newzoneinfo.Id].ZoneCreatorName = player.ToString();
                        SendReply(player, lang.GetMessage("zoneAdded", this, playerId), newzoneinfo.Id, name);
                        SaveData();
                        LoadZones();
                        return;
                    }
                case "list":
                    foreach (var zoneDef in ZoneDefinitions)
                    {
                        SendReply(player, lang.GetMessage("zoneList", this, playerId), zoneDef.Value.Id, zoneDef.Value.Name, zoneDef.Value.Location);
                    }
                    
                    return;
                case "remove":

                    var id = args[1];
                    if (ZoneDefinitions.ContainsKey(id))
                    {
                        storedData.ZoneDefinitions.Remove(ZoneDefinitions[id]); 
                        SendReply(player, lang.GetMessage("zoneRemove", this, playerId), id);
                        SaveData();
                        LoadData();
                        LoadZones();
                    }
                    else
                        SendReply(player, lang.GetMessage("zoneError", this, playerId));
                    return;
                case "info":
                    int count = 0;
                    int zcount = 0;                    
                    foreach (var zoneDef in ZoneDefinitions)
                    {
                        count++;
                        if (IsInZone(player, zoneDef.Value.Id, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                        {
                            zcount++;
                            SendReply(player, lang.GetMessage("zoneInfo", this, playerId), zoneDef.Value.Id, zoneDef.Value.Name);
                            SendReply(player, lang.GetMessage("zoneFlag1", this, playerId), zoneDef.Value.ZoneRadius);
                            SendReply(player, lang.GetMessage("zoneFlag2", this, playerId), zoneDef.Value.ZonePVE);
                            SendReply(player, lang.GetMessage("zoneFlag3", this, playerId), zoneDef.Value.ZoneNoBuild);
                            SendReply(player, lang.GetMessage("zoneFlag4", this, playerId), zoneDef.Value.ZoneNoDamage);
                            SendReply(player, lang.GetMessage("zoneFlag5", this, playerId), zoneDef.Value.ZoneNoSleeperDamage);
                            SendReply(player, lang.GetMessage("zoneFlag6", this, playerId), zoneDef.Value.ZoneNoCrestDamage);
                            SendReply(player, lang.GetMessage("zoneFlag7", this, playerId), zoneDef.Value.ZoneMessageOn);
                            SendReply(player, lang.GetMessage("zoneFlag8", this, playerId), zoneDef.Value.ZoneEnterMessageOn);
                            SendReply(player, lang.GetMessage("zoneFlag9", this, playerId), zoneDef.Value.ZoneExitMessageOn);
                            SendReply(player, lang.GetMessage("zoneFlag10", this, playerId), zoneDef.Value.ZoneMessage);
                            SendReply(player, lang.GetMessage("zoneFlag11", this, playerId), zoneDef.Value.EnterZoneMessage);
                            SendReply(player, lang.GetMessage("zoneFlag12", this, playerId), zoneDef.Value.ExitZoneMessage);
                            return;
                        }
                                           
                    }
                    if (zcount == 0 && count > 1)
                        SendReply(player, lang.GetMessage("zoneLocError", this, playerId));
                    if (count == 0)
                        SendReply(player, lang.GetMessage("noZoneError", this, playerId));

                    return;
                case "edit":
                    count = 0;
                    zcount = 0;
                    string currentzone;
                    foreach (var zoneDef in ZoneDefinitions)
                    {
                        count++;
                        if (IsInZone(player, zoneDef.Value.Id, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                        {                       
                            zcount++;
                            currentzone = zoneDef.Value.Id;
                            if (args[1] == "radius")
                            {
                                ZoneDefinitions[currentzone].ZoneRadius = Convert.ToUInt64(args[2]);
                                SendReply(player, lang.GetMessage("zoneEdited", this, playerId), args[1], zoneDef.Value.Id, zoneDef.Value.ZoneRadius);
                                return;
                            }
                            if (args[1] == "name")
                            {
                                ZoneDefinitions[currentzone].ZoneName = args[2];
                                SaveData();
                                LoadZones();
                                SendReply(player, lang.GetMessage("zoneEdited", this, playerId), args[1], zoneDef.Value.Id, zoneDef.Value.ZoneName);
                                return;
                            }
                            if (args[1] == "pve")
                            {
                                ZoneDefinitions[currentzone].ZonePVE = Convert.ToBoolean(args[2]);
                                SaveData();
                                LoadZones();
                                SendReply(player, lang.GetMessage("zoneEdited", this, playerId), args[1], zoneDef.Value.Id, zoneDef.Value.ZonePVE);
                                return;
                            }
                            if (args[1] == "nobuild")
                            {
                                ZoneDefinitions[currentzone].ZoneNoBuild = Convert.ToBoolean(args[2]);
                                SaveData();
                                LoadZones();
                                SendReply(player, lang.GetMessage("zoneEdited", this, playerId), args[1], zoneDef.Value.Id, zoneDef.Value.ZoneNoBuild);
                                return;
                            }
                            if (args[1] == "nodamage")
                            {
                                ZoneDefinitions[currentzone].ZoneNoDamage = Convert.ToBoolean(args[2]);
                                SaveData();
                                LoadZones();
                                SendReply(player, lang.GetMessage("zoneEdited", this, playerId), args[1], zoneDef.Value.Id, zoneDef.Value.ZoneNoDamage);
                                return;
                            }
                            if (args[1] == "nosleeperdamage")
                            {
                                ZoneDefinitions[currentzone].ZoneNoSleeperDamage = Convert.ToBoolean(args[2]);
                                SaveData();
                                LoadZones();
                                SendReply(player, lang.GetMessage("zoneEdited", this, playerId), args[1], zoneDef.Value.Id, zoneDef.Value.ZoneNoSleeperDamage);
                                return;
                            }
                            if (args[1] == "nocrestdamage")
                            {
                                ZoneDefinitions[currentzone].ZoneNoCrestDamage = Convert.ToBoolean(args[2]);
                                SaveData();
                                LoadZones();
                                SendReply(player, lang.GetMessage("zoneEdited", this, playerId), args[1], zoneDef.Value.Id, zoneDef.Value.ZoneNoCrestDamage);
                                return;
                            }
                            if (args[1] == "messageon")
                            {
                                ZoneDefinitions[currentzone].ZoneMessageOn = Convert.ToBoolean(args[2]);
                                SaveData();
                                LoadZones();
                                SendReply(player, lang.GetMessage("zoneEdited", this, playerId), args[1], zoneDef.Value.Id, zoneDef.Value.ZoneMessageOn);
                                return;
                            }
                            if (args[1] == "message")
                            {
                                ZoneDefinitions[currentzone].ZoneMessage = Convert.ToString(args[2]);
                                SaveData();
                                LoadZones();
                                SendReply(player, lang.GetMessage("zoneEdited", this, playerId), args[1], zoneDef.Value.Id, zoneDef.Value.ZoneMessage);
                                return;
                            }
                            if (args[1] == "entermessageon")
                            {
                                ZoneDefinitions[currentzone].ZoneEnterMessageOn = Convert.ToBoolean(args[2]);
                                SaveData();
                                LoadZones();
                                SendReply(player, lang.GetMessage("zoneEdited", this, playerId), args[1], zoneDef.Value.Id, zoneDef.Value.ZoneEnterMessageOn);
                                return;
                            }
                            if (args[1] == "entermessage")
                            {
                                ZoneDefinitions[currentzone].EnterZoneMessage = Convert.ToString(args[2]);
                                SaveData();
                                LoadZones();
                                SendReply(player, lang.GetMessage("zoneEdited", this, playerId), args[1], zoneDef.Value.Id, zoneDef.Value.EnterZoneMessage);
                                return;
                            }
                            if (args[1] == "exitmessageon")
                            {
                                ZoneDefinitions[currentzone].ZoneExitMessageOn = Convert.ToBoolean(args[2]);
                                SaveData();
                                LoadZones();
                                SendReply(player, lang.GetMessage("zoneEdited", this, playerId), args[1], zoneDef.Value.Id, zoneDef.Value.ZoneExitMessageOn);
                                return;
                            }
                            if (args[1] == "exitmessage")
                            {
                                ZoneDefinitions[currentzone].ExitZoneMessage = Convert.ToString(args[2]);
                                SaveData();
                                LoadZones();
                                SendReply(player, lang.GetMessage("zoneEdited", this, playerId), args[1], zoneDef.Value.Id, zoneDef.Value.ExitZoneMessage);
                                return;
                            }
                            else
                                SendReply(player, lang.GetMessage("synError", this, playerId));
                            return;                            
                        }
                    }
                    if (zcount == 0 && count > 1)
                        SendReply(player, lang.GetMessage("zoneLocError", this, playerId));
                    if (count == 0)
                        SendReply(player, lang.GetMessage("noZoneError", this, playerId));

                    return;
                default:
                    break;

            }
            SendReply(player, lang.GetMessage("synError", this, playerId));
            
        }

        #endregion
        private void OnEntityHealthChange(EntityDamageEvent damageEvent)
        {
            if (damageEvent == null) return;
            if (damageEvent.Damage == null) return;
            if (damageEvent.Damage.DamageSource == null) return;
            if (!damageEvent.Damage.DamageSource.IsPlayer) return;
            if (damageEvent.Damage.DamageSource.Owner == null) return;
            if (damageEvent.Entity == null) return;                        
            //sleeper   
            var sleeper = damageEvent.Entity.GetComponentInChildren<PlayerSleeperObject>();
            Player attacker = damageEvent.Damage.DamageSource.Owner;
            var victim = damageEvent.Entity.Owner.DisplayName;            
            if (damageEvent.Damage.Amount < 0) return;
            if (attacker.HasPermission("admin") && AdminCanKill) return;
            if (damageEvent.Entity.IsPlayer && attacker != null || damageEvent.Entity.name.Contains("Crest") || sleeper == true)
            {                
                if (victim == damageEvent.Damage.DamageSource.Owner.DisplayName) return; //allows dehydration and hunger, as well as healing.

                foreach (var zoneDef in ZoneDefinitions)
                {
                    if (IsInZone(damageEvent.Damage.DamageSource.Owner, zoneDef.Value.Id, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                    {
                        if (zoneDef.Value.ZonePVE == true && sleeper == false && !damageEvent.Entity.name.Contains("Crest"))
                        {
                            damageEvent.Cancel(lang.GetMessage("logPvP", this, attacker.ToString()), attacker);                            
                            Puts(lang.GetMessage("logPvP", this, attacker.ToString()), attacker);
                            damageEvent.Damage.Amount = 0f;
                            SendReply(attacker, lang.GetMessage("noPvP", this, attacker.ToString()));
                        }
                        if (zoneDef.Value.ZoneNoSleeperDamage == true && sleeper == true)
                        {
                            damageEvent.Cancel(lang.GetMessage("logSleeper", this, attacker.ToString()), attacker);                            
                            Puts(lang.GetMessage("logSleeper", this, attacker.ToString()), attacker);
                            damageEvent.Damage.Amount = 0f;
                            SendReply(attacker, lang.GetMessage("noSleeper", this, attacker.ToString()));

                        }
                        if (damageEvent.Entity.name.Contains("Crest") && zoneDef.Value.ZoneNoCrestDamage == true)
                        {
                            damageEvent.Cancel(lang.GetMessage("logCrest", this, attacker.ToString()), attacker);                            
                            Puts(lang.GetMessage("logCrest", this, attacker.ToString()), attacker);
                            damageEvent.Damage.Amount = 0f;
                            SendReply(attacker, lang.GetMessage("noCrest", this, attacker.ToString()));
                        }
                    }

                }
            }
        }
        private void OnCubePlacement(CubePlaceEvent Event)
        {
            if (Event == null) return;
            if (Event.Entity == null) return;
            Player player = Event.Entity.Owner;
            string playerId = player.Id.ToString();
            if (player.HasPermission("admin") && AdminCanBuild) return;                       
            foreach (var zoneDef in ZoneDefinitions)
            {
                if (IsInZone(Event.Entity.Owner, zoneDef.Value.Id, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                {

                    if (zoneDef.Value.ZoneNoBuild == true)
                    {
                        if (Event.Material != CubeInfo.Air.MaterialID)
                        {
                            if (CrestCheckOn)
                            {
                                if (SocialAPI.Get<CrestScheme>().IsEmpty(Event.Grid.LocalToWorldCoordinate(Event.Position)))//allows crest owners to remove/add blocks
                                {
                                    InventoryUtil.CollectTileset(Event.Sender, Event.Material, 1, Event.PrefabId);
                                    Event.Cancel(lang.GetMessage("logNoBuild", this, playerId), player);
                                    Puts(lang.GetMessage("logNoBuild", this, playerId), player);
                                    SendReply(player, lang.GetMessage("noBuild", this, playerId));
                                }
                                else
                                    return;
                            }
                            else
                            {
                                InventoryUtil.CollectTileset(Event.Sender, Event.Material, 1, Event.PrefabId);
                                Event.Cancel(lang.GetMessage("logNoBuild", this, playerId), player);
                                Puts(lang.GetMessage("logNoBuild", this, playerId), player);
                                SendReply(player, lang.GetMessage("noBuild", this, playerId));
                            }

                        }
                    }

                }
            }
        }
        private void OnCubeTakeDamage(CubeDamageEvent Event)
        {
            Player player = Event.Entity.Owner;
            string playerId = player.Id.ToString();
            TilesetColliderCube centralPrefabAtLocal = BlockManager.DefaultCubeGrid.GetCentralPrefabAtLocal(Event.Position);            
            foreach (var zoneDef in ZoneDefinitions)
            {
                if (IsInZone(Event.Entity.Owner, zoneDef.Value.Id, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                {
                    if (zoneDef.Value.ZoneNoDamage == true)
                    {
                        Event.Cancel(lang.GetMessage("logNoDamage", this, playerId), player);
                        SalvageModifier component = centralPrefabAtLocal.GetComponent<SalvageModifier>();
                        if (component != null && !component.info.NotSalvageable)
                        {
                            component.info.SalvageAmount = 0;                            
                        }
                        
                        Event.Damage.Amount = 0f;
                        Event.Damage.ImpactDamage = 0f;
                        Event.Damage.MiscDamage = 0f;                       
                        Puts(lang.GetMessage("logNoDamage", this, playerId), player);
                        SendReply(player, lang.GetMessage("areaProtected", this, playerId));                      
                        return;
                    }
                }
            }
        }
        private void OnObjectDeploy(NetworkInstantiateEvent Event)
        {
            Player player = Server.GetPlayerById(Event.SenderId);
            string playerId = player.Id.ToString();
            if (player == null) return;
            if (player.HasPermission("admin") && AdminCanBuild) return;
            foreach (var zoneDef in ZoneDefinitions)
            {
                if (IsInZone(Event.Sender, zoneDef.Value.Id, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                {
                    if (zoneDef.Value.ZoneNoBuild == true)
                    {
                        InvItemBlueprint bp = InvDefinitions.Instance.Blueprints.GetBlueprintForID(Event.BlueprintId);                        
                        if (bp.Name.Contains("Crest"))
                        {
                            timer.In(1, () => ObjectRemove(player, Event.Position, bp.name));
                            Puts(lang.GetMessage("logCrestPlace", this, playerId), player, bp.Name);
                            SendReply(Event.Sender, lang.GetMessage("noPlace", this, playerId), bp.Name);
                        }

                    }

                }
            }
        }        
        private void OnPlayerDisconnected(Player player)
        {
			string playerId = player.Id.ToString();
            if (PData.ContainsKey(player))
            {
                PlayerData Player = GetCache(player);
                PData.Remove(player);
            }
            if (timers.ContainsKey(playerId))
            {
                timers[playerId].Destroy();
                timers.Remove(playerId);
            }
            if (ZoneCheckTimer.ContainsKey(playerId))
            {
                ZoneCheckTimer[playerId].Destroy();
                ZoneCheckTimer.Remove(playerId);
            }
        }        
        private void OnPlayerConnected(Player player)
        {            
		    string playerId = player.Id.ToString();
            if (!PData.ContainsKey(player)) 
            {
                PData.Add(player, new PlayerData (player.Id));                
            }
            if (ZoneCheckOn == true && player.Name != "Server" && player.Id != 9999999999) //fixes error
            {
                if (!ZoneCheckTimer.ContainsKey(playerId))
                {
                    ZoneCheckTimer.Add(playerId, timer.Repeat(ZoneCheckInterval, 0, () => CheckPlayerLocation(player)));
                }
            }

        }
        #region Functions
        private void ObjectRemove(Player player, Vector3 position, string itemname)
        {            
            foreach (var entity in Entity.TryGetAll())
            {
                if (entity.Position == position)
                {                    
                    EntityKiller.Kill(entity);
                    GiveInventoryStack(player, itemname);
                }
            }
        }
        
        void GiveInventoryStack(Player player, string itemname)
        {
            var inventory = player.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(itemname, true, true);           
            var invGameItemStack = new InvGameItemStack(blueprintForName, 1, null);
            ItemCollection.AutoMergeAdd(inventory.Contents, invGameItemStack);
        }
        void LoadZones()
        {           
            timer.In(1, () => {
                foreach (var zoneDef in ZoneDefinitions)
                    zones.Add(new Vector2(zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ));
            });

        }
        private void CheckPlayerLocation(Player player)        
        {
            if (player.Name == "Server" && player.Id == 9999999999) return; //fixes error
            if (player == null) return;
            if (player.Entity == null) return; //fixed NRE            
            if (PData.ContainsKey(player))
            {
                foreach (var zoneDef in ZoneDefinitions)
                {
                    PlayerData Player = GetCache(player);

                    if (IsInZone(player, zoneDef.Value.Id, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                    {
                        Player.ZoneId = zoneDef.Value.Id;

                        if (Player.EnterZone == false)
                        {
                            if (zoneDef.Value.ZoneEnterMessageOn == true && Player.EnterZone == false)
                            {
                                SendMessage(player, zoneDef.Value.EnterZoneMessage, false, true);
                            }
                            if (zoneDef.Value.ZoneMessageOn == true)
                            {
                                SendMessage(player, zoneDef.Value.ZoneMessage, true, true);
                            }
                            Player.EnterZone = true;
                            Player.ExitZone = false;
                            return;
                        }

                    }
                    else
                    {
                        if (Player.EnterZone == true && Player.ZoneId == zoneDef.Value.Id && Player.ExitZone == false)
                        {
                            Player.EnterZone = false;
                            Player.ExitZone = true;

                            if (zoneDef.Value.ZoneExitMessageOn == true)
                            {                                
                                SendMessage(player, zoneDef.Value.ExitZoneMessage, false, false);
                            }

                        }

                        if (Player.EnterZone == false && Player.ExitZone == true)
                        {
                            Player.ExitZone = false;
                        }                        
                    }
                }
            }
        }

        private bool IsInZone(Player player, string zoneId, float zoneX, float zoneZ, float radius)
        {            
            if (PData.ContainsKey(player))
            {
                PlayerData Player = GetCache(player);
                if (Server.PlayerIsOnline(player.DisplayName))
                {
                    foreach (Vector2 zone in zones)
                    {
                       
                        Vector2 vector = new Vector2(zoneX, zoneZ);
                        float distance = Math.Abs(Vector2.Distance(vector, new Vector2(player.Entity.Position.x, player.Entity.Position.z)));
                        if (distance <= radius)
                        {
                            return true;
                        }
                        else
                            return false;
                    }

                    return false;
                }
                return false;
            }
            return false;
        }
        private void SendMessage(Player player, string message , bool repeat, bool inZone)
        {
			string playerId = player.Id.ToString();
            if (MessagesOn == true)
            {
                if (repeat == true && !timers.ContainsKey(playerId))
                {                              
                        timers.Add(playerId, timer.Repeat(MessageInterval, 0, () => SendReply(player, message)));
                }
                else
                {
                    if (repeat == false)
                    {
                        if (inZone == false && timers.ContainsKey(playerId)) 
                        {
                            timers[playerId].Destroy();
                            timers.Remove(playerId);
                        }
                        SendReply(player, message);
                    }
                }
            }
        }
        PlayerData GetCache(Player Player)
        {
            PlayerData CachedPlayer = null;
            return PData.TryGetValue(Player, out CachedPlayer) ? CachedPlayer : null;
        }
        void CacheAllOnlinePlayers()
        {           
            if (Server.AllPlayers.Count > 0)
            {
                foreach (Player Player in Server.AllPlayers)
                {
                    if (Player.Name.ToLower() == "server")
                    {
                        continue;
                    }
                   if (Player.Id == 9999999999) continue; //fixes error

                    if (Server.PlayerIsOnline(Player.DisplayName))
                    {
                        string playerId = Player.Id.ToString();
                        if (!PData.ContainsKey(Player))
                        {
                            PData.Add(Player, new PlayerData(Player.Id));
                            if (ZoneCheckOn == true)
                            {
                                if (!ZoneCheckTimer.ContainsKey(playerId))
                                {
                                    ZoneCheckTimer.Add(playerId, timer.Repeat(ZoneCheckInterval, 0, () => CheckPlayerLocation(Player)));
                                }
                            }

                        }
                    }
                }
            }
            
        }

        #endregion
        
        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        #region Vector3 Json Converter         
        private class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }
                var o = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(o["x"]), Convert.ToSingle(o["y"]), Convert.ToSingle(o["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        }
        #endregion
    }
}