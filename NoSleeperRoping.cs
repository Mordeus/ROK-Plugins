using System.Collections.Generic;
using System;
using System.Linq;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using CodeHatch.StarForge.Sleeping;
using CodeHatch.Networking.Events;
using CodeHatch.Thrones.Capture;

namespace Oxide.Plugins
{
    [Info("NoSleeperRoping", "Mordeus", "1.0.3")]
    public class NoSleeperRoping : ReignOfKingsPlugin
    {
        //config
        public string ChatTitle;       
        public bool AdminCanRope;
        public bool LoggingOn;

        #region Lang API
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["noRope"] = "{Title} [FF0000]You can not rope a sleeper![FFFFFF]",
                ["logNoRope"] = "player {0} attempted to rope a sleeper, cancelling."
                

            }, this);
        }
        #endregion Lang API
        #region Config

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating new configurationfile...");
        }

        private new void LoadConfig()
        {
            ChatTitle = GetConfig<string>("Title", "[4F9BFF]Server:");            
            AdminCanRope = GetConfig<bool>("Admins Can Rope On", false);
            LoggingOn = GetConfig<bool>("Logging On", true);

            SaveConfig();
        }
        #endregion Config
        #region Oxide
        private void Init()
        {
            permission.RegisterPermission("nosleeperroping.admin", this);
            LoadConfig();            
        }        
        private void OnPlayerCapture(PlayerCaptureEvent Event)
        {
            if (Event == null) return;
            if (Event.Captor == null) return;
            if (Event.TargetEntity == null) return;
            if (Event.Captor == Event.TargetEntity) return;
            if (!Event.Captor.IsPlayer) return;
            var sleeper = Event.TargetEntity.GetComponentInChildren<PlayerSleeperObject>();
            if (sleeper == null) return;
            Player player = Event.Captor.Owner;
            string playerId = player.Id.ToString();
            if (hasPermission(player) && AdminCanRope) return;            
            if (sleeper)
            {
                CaptureType currentType = Event.Captor.Get<PlayerCaptureManager>().CurrentType;
                if (currentType == CaptureType.Rope || currentType == CaptureType.Chain)
                {
                    Event.Cancel(Message("logNoRope"), player);
                    SendReply(player, Message("noRope"));
                    if (LoggingOn)
                        Puts(Message("logNoRope"), player);
                }               
            }
        }
        private void OnPlayerRelease(PlayerEscapeEvent Event)
        {
            //using this hook just to stop the sound from playing when switching items, for some reason it does without it.
            if (Event == null) return;
            if (Event.Escapee == null) return;
            if (Event.Escapee.IsPlayer) return;
            if (hasPermission(Event.Sender) && AdminCanRope) return;
            var sleeper = Event.Escapee.GetComponentInChildren<PlayerSleeperObject>();
            if (sleeper == null) return;
            if (sleeper)
            {
                CaptureType currentType = Event.Escapee.Get<PlayerCaptureManager>().CurrentType;
                if (currentType == CaptureType.Rope || currentType == CaptureType.Chain) 
                {                    
                    Event.Cancel();
                }                
            }
        }
        #endregion Oxide
        #region Helpers        
        private string Message(string key, string id = null, params object[] args)
        {
            return lang.GetMessage(key, this, id).Replace("{Title}", ChatTitle);
        }

        private T GetConfig<T>(params object[] pathAndValue)
        {
            List<string> pathL = pathAndValue.Select((v) => v.ToString()).ToList();
            pathL.RemoveAt(pathAndValue.Length - 1);
            string[] path = pathL.ToArray();

            if (Config.Get(path) == null)
            {
                Config.Set(pathAndValue);
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            return (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }
        private bool hasPermission(Player player)
        {
            string playerId = player.Id.ToString();
            if (!(player.HasPermission("admin") || player.HasPermission("nosleeperroping.admin")))
            {
                player.SendError(Message("notAllowed"));
                return false;
            }
            return true;
        }
        #endregion Helpers
    }
}
