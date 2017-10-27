using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.MySql;
using UnityEngine;
using Assets.Scripts.Core;

/*
Changelog
	TODO:
		* Arena 2vs2 3vs3
		* Te cura cuando entras a la arena
		* No ganas infamia en la arena
    v0.0.3
        * Llamado remoto al plugin WebUserStatus
        * Se quito lo relacionado con la BD
	v0.0.2
		* Arreglado problema al desconectarse un usuario en medio de una arena
		* Salvar a la BD las estadisticas de la arena
		* Al morir en la arena pierdes todo lo que llevas arriba
		* Guardar posicion de los jugadores en la arena
*/

namespace Oxide.Plugins
{
    [Info("Arena", "RiKr2", "0.0.3")]
    [Description("Arena. Time to Fight.")]

    class Arena : HurtworldPlugin
    {
		// Arena settings
		private Vector3 arenapos1;
		private Vector3 arenapos2;
		private bool arena = false;
	
		// Simple-Arena settings
		private PlayerSession player1;
		private PlayerSession player2;
		private Vector3 pos1;
		private Vector3 pos2;
		
		// Multi-Arena settings
		private List<PlayerSession> teamA = new List<PlayerSession>();
		private List<PlayerSession> teamB = new List<PlayerSession>();
		private List<Vector3> teamPosA = new List<Vector3>();
		private List<Vector3> teamPosB = new List<Vector3>();

        // Reference to WebUserStatus Plugin
        [PluginReference] Plugin WebUserStatus;

        string Msg(string msg, string SteamId = null) => lang.GetMessage(msg, this, SteamId);
		
		void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"nombre","TEXTO AQUI"}
            };
			
			lang.RegisterMessages(messages, this);
        }
		protected override void LoadDefaultConfig()
        {
			if(Config["saveToDatabase"] == null) Config.Set("saveToDatabase", true);
			if(Config["arenapos1x"] == null) Config.Set("arenapos1x", -537.4f);
			if(Config["arenapos1y"] == null) Config.Set("arenapos1y", 208.1f);
			if(Config["arenapos1z"] == null) Config.Set("arenapos1z", -3714.1f);
			if(Config["arenapos2x"] == null) Config.Set("arenapos2x", -538.6f);
			if(Config["arenapos2y"] == null) Config.Set("arenapos2y", 210.3f);
			if(Config["arenapos2z"] == null) Config.Set("arenapos2z", -3687.2f);
			SaveConfig();
			arenapos1 = new Vector3(Convert.ToSingle(Config["arenapos1x"]), Convert.ToSingle(Config["arenapos1y"]), Convert.ToSingle(Config["arenapos1z"]));
			arenapos2 = new Vector3(Convert.ToSingle(Config["arenapos2x"]), Convert.ToSingle(Config["arenapos2y"]), Convert.ToSingle(Config["arenapos2z"]));
        }

		void Loaded()
		{
			LoadDefaultConfig();
			LoadDefaultMessages();			
		}

        void OnServerInitialized()
        {
            if (WebUserStatus == null)
            {
                PrintWarning("Plugin 'WebUserStatus' was not found!");
            }
        }

        [ChatCommand("arena")]
        void cmdRequestArena(PlayerSession session, string command, string[] args)
        {
			if(args.Length == 0)
			{		
				if(player1 == null)
				{
					player1 = session;
					pos1 = session.WorldPlayerEntity.transform.position;
					hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> " + session.IPlayer.Name + " ha marcado ARENA. Quien se atreve!");				
				}
				else if(player2 == null)
				{
					if(player1.IPlayer.Name == session.IPlayer.Name)
					{
						hurt.SendChatMessage(session, "<color=#800000ff>[ARENA]</color> Estas esperando un contrincante. Estate listo.");
						return;
					}
					player2 = session;
					pos2 = session.WorldPlayerEntity.transform.position;
					hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> " + session.IPlayer.Name + " ha marcado ARENA.");
					player2 = session;
					hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> " + player1.IPlayer.Name + " vs. " + player2.IPlayer.Name);
					InitArena();
				}
				else
				{
					hurt.SendChatMessage(session, "<color=#800000ff>[ARENA]</color> " + player1.IPlayer.Name + " y " + player2.IPlayer.Name + " estan en una arena tienes que esperar a que terminen.");
				}
				return;
			}
			else if(args.Length == 1)
			{
				switch(args[0])
				{
					case "help":
						hurt.SendChatMessage(session, "<color=#800000ff>[ARENA]</color> Escribe <color=#00ffffff>/arena</color> para entrar en un evento de arena.");
						hurt.SendChatMessage(session, "<color=#800000ff>[ARENA]</color> Escribe <color=#00ffffff>/arena cancel</color> para cancelar un evento de arena.");
						if(session.IsAdmin)
						{
							hurt.SendChatMessage(session, "<color=#800000ff>[ARENA]</color> Escribe <color=#00ffffff>/arena set [1|2]</color> para establecer la posicion del 1er o 2do jugador en la arena.");
						}
						return;
					case "cancel":
						if(arena)
						{
							hurt.SendChatMessage(session, "<color=#800000ff>[ARENA]</color> No puedes cancelar una arena en curso.");
						}
						else
						{
							if(player1 != null && player1.IPlayer.Name == session.IPlayer.Name)
							{
								player1 = null;
								hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> " + session.IPlayer.Name + " se ha apencado y ha abandonado la arena. LOL");
							}
							else
							{
								hurt.SendChatMessage(session, "<color=#800000ff>[ARENA]</color> No estas inscrito en ninguna arena.");
							}
						}
						return;
				}
			}
			else if(args.Length == 2 && args[0] == "set" && (args[1] == "1" || args[1] == "2") && session.IsAdmin)
			{
				if(args[1] == "1")
				{
					arenapos1 = session.WorldPlayerEntity.transform.position;
					hurt.SendChatMessage(session, "<color=#800000ff>[ARENA]</color> Posicion del jugador 1 establecida correctamente.");
					Config.Set("arenapos1x", arenapos1.x);
					Config.Set("arenapos1y", arenapos1.y);
					Config.Set("arenapos1z", arenapos1.z);
					SaveConfig();
				}
				else
				{
					arenapos2 = session.WorldPlayerEntity.transform.position;
					hurt.SendChatMessage(session, "<color=#800000ff>[ARENA]</color> Posicion del jugador 2 establecida correctamente.");
					Config.Set("arenapos2x", arenapos2.x);
					Config.Set("arenapos2y", arenapos2.y);
					Config.Set("arenapos2z", arenapos2.z);
					SaveConfig();
				}
			}
			else 
			{
				hurt.SendChatMessage(session, "<color=#800000ff>[ARENA]</color> Comando incorrecto. Escriba <color=#00ffffff>/arena help</color> para mas informacion.");
			}
		}
		
		void OnPlayerDeath(PlayerSession session, EntityEffectSourceData source)
        {
			if(arena)
			{
				if(session.IPlayer.Name == player1.IPlayer.Name)
					EndArena(false);
				else if(session.IPlayer.Name == player2.IPlayer.Name)
					EndArena(true);
			}
		}
		
		void OnPlayerDisconnected(PlayerSession session)
		{
            if(arena)
			{
				if(session.IPlayer.Name == player1.IPlayer.Name)
				{
					hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> " + player1.IPlayer.Name + " ha abandonado la ARENA. Se ha mandado a correr el muy miedoso.");
					EndArena(false);					
				}
				else if(session.IPlayer.Name == player2.IPlayer.Name)
				{
					hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> " + player2.IPlayer.Name + " ha abandonado la ARENA. Se ha mandado a correr el muy miedoso.");
					EndArena(true);
				}
			}
			else if(player1 != null && session.IPlayer.Name == player1.IPlayer.Name)
			{
				hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> " + player1.IPlayer.Name + " ha abandonado la ARENA. No quiere pelear ahora.");
				player1 = null;
			}
		}
		
		void InitArena()
		{
			timer.Once(1f, () =>
			{
				hurt.SendChatMessage(player1, "<color=#800000ff>[ARENA]</color> Combate comienza en 3.");
				hurt.SendChatMessage(player2, "<color=#800000ff>[ARENA]</color> Combate comienza en 3.");
			});
			timer.Once(2f, () =>
			{
				hurt.SendChatMessage(player1, "<color=#800000ff>[ARENA]</color> Combate comienza en 2.");
				hurt.SendChatMessage(player2, "<color=#800000ff>[ARENA]</color> Combate comienza en 2.");
			});
			timer.Once(3f, () =>
			{
				hurt.SendChatMessage(player1, "<color=#800000ff>[ARENA]</color> Combate comienza en 1.");
				hurt.SendChatMessage(player2, "<color=#800000ff>[ARENA]</color> Combate comienza en 1.");
			});
			timer.Once(4f, () =>
			{
				player1.WorldPlayerEntity.transform.position = arenapos1;
				player2.WorldPlayerEntity.transform.position = arenapos2;
				hurt.SendChatMessage(player1, "<color=#800000ff>[ARENA]</color> Pelea!!!");
				hurt.SendChatMessage(player2, "<color=#800000ff>[ARENA]</color> Pelea!!!");
				arena = true;
			});
		}
		
		void EndArena(bool winner1)
		{
			// Ganador 1er jugador
			if(winner1) 
			{
				hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> " + player1.IPlayer.Name + " ha ganado la ARENA");
				hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> La ARENA estara lista en 10 segundos.");
				hurt.SendChatMessage(player2, "<color=#800000ff>[ARENA]</color> Seras teletransportado a donde estabas en 15 seg.");
				//DropItemsLoser(player2);
				timer.Once(15f, () =>
				{
					player1.WorldPlayerEntity.transform.position = pos1;
					arena = false;
					player1 = null;
					player2 = null;
					hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> ARENA LISTA PARA UN PROXIMO COMBATE.");
				});
			}
			else // Ganador 2do jugador
			{
				hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> " + player2.IPlayer.Name + " ha ganado la ARENA");
				hurt.SendChatMessage(player2, "<color=#800000ff>[ARENA]</color> Seras teletransportado a donde estabas en 15 seg.");
				//DropItemsLoser(player1);
				timer.Once(15f, () =>
				{
					player2.WorldPlayerEntity.transform.position = pos2;
					arena = false;
					player1 = null;
					player2 = null;
					hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> ARENA LISTA PARA UN PROXIMO COMBATE.");
				});
			}
            if ((bool)Config["saveToDatabase"] && WebUserStatus != null)
            {
                WebUserStatus?.Call("SetArena", player1.SteamId, player2.SteamId, winner1);
            }
        }

        /* TODO
		void DropItemsLoser(PlayerSession killerSession)
		{
			PlayerInventory killerInventory = killerSession.WorldPlayerEntity.GetComponent<PlayerInventory>();
			var killerItems = killerInventory.Items;
			GameObject cache = Singleton<HNetworkManager>.Instance?.NetInstantiate("LootCache", killerSession.WorldPlayerEntity.transform.position, Quaternion.identity, GameManager.GetSceneTime());			
			IStorable cacheInventory = cache?.GetComponentByInterface<IStorable>();
			cacheInventory.Capacity = killerInventory.Capacity;

			for (int i = 0; i < killerItems.Length; i++)
			{
				var itemInstance = killerItems[i];

				if (itemInstance != null)
				{
					IItem item = itemInstance.Item;
					GlobalItemManager.Instance.GiveItem(item, itemInstance.StackSize, cacheInventory);
					itemInstance.ReduceStackSize(itemInstance.StackSize);
				}
			}
			killerInventory.Invalidate(false);
		}
		*/


        /* TODO
		[ChatCommand("arena2")]
        void cmdRequestArena2(PlayerSession session, string command, string[] args)
        {
			if(args.Length == 1)
			{
				if(args[0] == '1') // Pelear por el equipo 1
				{
					if(teamA.Count < 2)
					{
						teamA.Add(session);
						teamPosA.Add(session.WorldPlayerEntity.transform.position);
						hurt.BroadcastChat("<color=#800000ff>[ARENA 2vs2]</color> " + session.IPlayer.Name + " ha marcado ARENA por el equipo 1");
					}
					else
					{
						hurt.SendChatMessage(session, "<color=#800000ff>[ARENA 2vs2]</color> .");
					}
				}
				else if(args[0] == '2') // Pelear por el equipo 2
				{
					if(teamB.Count < 2)
					{
						teamB.Add(session);
						teamPosB.Add(session.WorldPlayerEntity.transform.position);
						hurt.BroadcastChat("<color=#800000ff>[ARENA 2vs2]</color> " + session.IPlayer.Name + " ha marcado ARENA por el equipo 2");
					}
				}
				if(player1 == null)
				{
					player1 = session;
					pos1 = session.WorldPlayerEntity.transform.position;
					hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> " + session.IPlayer.Name + " ha marcado ARENA. Quien se atreve!");				
				}
				else if(player2 == null)
				{
					if(player1.IPlayer.Name == session.IPlayer.Name)
					{
						hurt.SendChatMessage(session, "<color=#800000ff>[ARENA]</color> Estas esperando un contrincante. Estate listo.");
						return;
					}
					player2 = session;
					pos2 = session.WorldPlayerEntity.transform.position;
					hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> " + session.IPlayer.Name + " ha marcado ARENA.");
					player2 = session;
					hurt.BroadcastChat("<color=#800000ff>[ARENA]</color> " + player1.IPlayer.Name + " vs. " + player2.IPlayer.Name);
					InitArena();
				}
				else
				{
					hurt.SendChatMessage(session, "<color=#800000ff>[ARENA]</color> " + player1.IPlayer.Name + " y " + player2.IPlayer.Name + " estan en una arena tienes que esperar a que terminen.");
				}
				return;
				
			}
		}
		*/

    }
}