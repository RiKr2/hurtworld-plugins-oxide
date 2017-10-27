// UPDATE v0.1
// * Added Config File
// * Remote calls
//      ** Online/Offline
//      ** Suicides
//      ** Deaths
//      ** Kills
//      ** Arena
// TODO
// * Animals Hunt
// * Money


using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;
using Oxide.Core;
using Oxide.Core.MySql;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Web Users Status", "RiKr2", "0.1")]
    [Description("Server status and players statistics.")]
    class WebUserStatus : HurtworldPlugin
    {
		private readonly Core.MySql.Libraries.MySql _mySql = Interface.GetMod().GetLibrary<Core.MySql.Libraries.MySql>();
        private Core.Database.Connection _mySqlConnection;

        #region QUERIES
        private const string InsertData = "INSERT INTO hw_o_users (`user_id`, `user_name`, `user_ip`, `user_online`) VALUES (@0, @1, @2, @3);";
        
		private const string QPlayerOnline = "UPDATE hw_o_users SET `user_online` = 1 WHERE `user_id` = @0;";
		private const string QPlayerOffline = "UPDATE hw_o_users SET `user_online` = 0 WHERE `user_id` = @0;";
		private const string QPlayerSuicide = "UPDATE hw_o_users SET `user_suicide` = `user_suicide` + 1 WHERE `user_id` = @0;";
		private const string QPlayerKill = "UPDATE hw_o_users SET `user_kills` = `user_kills` + 1 WHERE `user_id` = @0;";
		private const string QPlayerDeath = "UPDATE hw_o_users SET `user_deaths` = `user_deaths` + 1 WHERE `user_id` = @0;";
        
        private const string QArenaWin = "UPDATE hw_o_users SET `user_arenaWin` = `user_arenaWin` + 1 WHERE `user_id` = @0;";
        private const string QArenaLose = "UPDATE hw_o_users SET `user_arenaLose` = `user_arenaLose` + 1 WHERE `user_id` = @0;";
        #endregion

        private string dbIP;
        private int dbPort;
        private string dbName;
        private string dbUser;
        private string dbPass;

        // Called when a plugin has finished loading
        void Loaded()
        {
            LoadDefaultConfig();
        }

        // Called when the config for a plugin should be initialized
        // Only called if the config file does not already exist
        protected override void LoadDefaultConfig()
        {
            // DB CONFIG
            if (Config["dbIP"] == null) Config.Set("dbIP", "127.0.0.1");
            if (Config["dbPort"] == null) Config.Set("dbPort", 3306);
            if (Config["dbName"] == null) Config.Set("dbName", "hwstats");
            if (Config["dbUser"] == null) Config.Set("dbUser", "user");
            if (Config["dbPass"] == null) Config.Set("dbPass", "password");

            // OTHERS CONFIGS
            if (Config["manageOnlinePlayers"] == null) Config.Set("manageOnlinePlayers", true);
            if (Config["manageKills"] == null) Config.Set("manageKills", true);
            if (Config["manageDeaths"] == null) Config.Set("manageDeaths", true);
            if (Config["manageSuicides"] == null) Config.Set("manageSuicides", true);

            SaveConfig();

            // Loading config
            dbIP = (string)Config["dbIP"];
            dbPort = (int)Config["dbPort"];
            dbName = (string)Config["dbName"];
            dbUser = (string)Config["dbUser"];
            dbPass = (string)Config["dbPass"];
        }

        

        // Called after the server startup has been completed and is awaiting connections
        private void OnServerInitialized()
        {
            _mySqlConnection = _mySql.OpenDb(dbIP, dbPort, dbName, dbUser, dbPass, this);
			
            // Create DB if not exist
            var sql = Core.Database.Sql.Builder.Append(@"CREATE TABLE IF NOT EXISTS `hw_o_users` (
								`user_id` bigint(20) NOT NULL PRIMARY KEY,
								`user_name` varchar(50) NOT NULL,
								`user_ip` varchar(16) NOT NULL,
								`user_online` BOOLEAN NOT NULL DEFAULT FALSE,
								`user_kills` INT NOT NULL,
								`user_deaths` INT NOT NULL,
								`user_suicide` INT NOT NULL,
								`user_arenaWin` INT NOT NULL,
								`user_arenaLose` INT NOT NULL
                               );");
            _mySql.Query(sql, _mySqlConnection, list => { });
        }

        // Called when the player has connected to the server
        void OnPlayerConnected(PlayerSession session)		
        {
            string uid = session.SteamId.ToString();

            #region Para las versiones sin STEAM
            if(uid == "0")
			{
				Singleton<GameManager>.Instance.KickPlayer(uid,  "Cambiate el ID, no puedes tener puesto 0.");
                return;
			}
            #endregion

            // If not exist, add player to DB
            try
            {
                var sql = Core.Database.Sql.Builder.Append(InsertData, uid, session.IPlayer.Name, session.Player.ipAddress, true);
                _mySql.Insert(sql, _mySqlConnection);
            }
            catch (Exception ex)
            {
                Puts(ex.Message);
            }

            if ((bool)Config["manageOnlinePlayers"])
                SetOnlineMode(uid, true);
        }

        // Called when the player has disconnected from the server
        void OnPlayerDisconnected(PlayerSession session)
        {
            string uid = session.SteamId.ToString();

            if ((bool)Config["manageOnlinePlayers"])
                SetOnlineMode(uid, false);
        }

        // Called when the player suicides
        void OnPlayerSuicide(PlayerSession session)
        {
            string uid = session.SteamId.ToString();

            if ((bool)Config["manageSuicides"])
                SetSuicide(uid);
        }

        // Called when the player dies
        void OnPlayerDeath(PlayerSession session, EntityEffectSourceData source)
		{
			string uid = session.SteamId.ToString();

            if ((bool)Config["manageDeaths"])
                SetDeath(uid);

            if ((bool)Config["manageKills"])
            {
                uid = GetKiller(source);
                SetKill(uid);
            }
		}

        // Called when an entity has died
        // TODO: Animals Stats
        void OnEntityDeath(AnimalStatManager stats, EntityEffectSourceData source)
        {   
            Puts("OnEntityDeath works!");
        }
        
        // --------------------------------------PRIVATE METHODS----------------------------------------

        string GetKiller(EntityEffectSourceData source)
		{
            UnityEngine.GameObject killer = source.EntitySource;			

            string descrp = GameManager.Instance.GetDescriptionKey(killer);
			if (descrp.Length >= 3)
				descrp = descrp.Substring(0, descrp.Length - 3);

			return GetSteamID(killer.name);
		}
		
		string GetSteamID(string identifier)
        {
            foreach (PlayerIdentity identity in GameManager.Instance.GetIdentifierMap().Values)
            {
                PlayerSession session = identity.ConnectedSession;
                if (session.IPlayer.Name.ToLower().Equals(identifier.ToLower()))
                {
                    return session.SteamId.ToString();
                }
            }
            return "0";
        }
		

        // -----------------------------------------CHAT COMMANDS------------------------------------------

        // Update DB Players (only add a player to the DB)
		[ChatCommand("updateplayers")]
        void cmdUpdatePlayers(PlayerSession session, string command, string[] args)
        {
			// For test only
			foreach (var item in covalence.Players.All)
            { 
				if (item == null) continue;
				try
				{
				    var sql = Core.Database.Sql.Builder.Append(InsertData, item.Id, item.Name, "", false);
				    _mySql.Query(sql, _mySqlConnection, list => {});
				}
				catch (Exception ex)
				{
					Puts(ex.Message);
				}
			}
			hurt.SendChatMessage(session, "Players update!");
		}
	

        // ----------------------------------------------PUBLIC CALLS--------------------------------------

        // Set ONLINE/OFFLINE
        public void SetOnlineMode(string uid, bool isOnline)
        {
            var sql = Core.Database.Sql.Builder.Append(isOnline ? QPlayerOnline : QPlayerOffline, uid);
            
            _mySql.Query(sql, _mySqlConnection, list => { });
        }

        // Set Suicide
        public void SetSuicide(string uid)
        {
            var sql = Core.Database.Sql.Builder.Append(QPlayerSuicide, uid);

            _mySql.Query(sql, _mySqlConnection, list => { });
        }

        // Set Death
        public void SetDeath(string uid)
        {
            var sql = Core.Database.Sql.Builder.Append(QPlayerDeath, uid);

            _mySql.Query(sql, _mySqlConnection, list => { });
        }

        // Set Kill
        public void SetKill(string uid)
        {
            var sql = Core.Database.Sql.Builder.Append(QPlayerKill, uid);

            _mySql.Query(sql, _mySqlConnection, list => { });
        }

        public void SetArena(string uidPlayer1, string uidPlayer2, bool player1Win)
        {
            // Winner
            var sql = Core.Database.Sql.Builder.Append(QArenaWin, player1Win ? uidPlayer1 : uidPlayer2);

            _mySql.Query(sql, _mySqlConnection, list => { });

            // Loser
            sql = Core.Database.Sql.Builder.Append(QArenaLose, player1Win ? uidPlayer2 : uidPlayer1);

            _mySql.Query(sql, _mySqlConnection, list => { });            
        }
    }
}