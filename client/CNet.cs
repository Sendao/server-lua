using UnityEngine;
using Opsive.UltimateCharacterController.Objects.CharacterAssist;
using Opsive.UltimateCharacterController.Objects;

namespace CNet
{
	public class CNet
	{
		// Set the player name
		public static void SetPlayerName(string name)
		{
			NetSocket.Instance.SetPlayerName(name);
		}

		// Connect to a host
        public static bool Connect( string host )
		{
			return NetSocket.Instance.Connect(host);
		}

		// Register for events (unused - does not send events over network)
        public static void RegisterEvent( ICNetEvent obj, CNetEvent evt ) {
			NetSocket.Instance.RegisterEvent(obj, evt);
        }
        public static void UnregisterEvent( ICNetEvent obj, CNetEvent evt ) {
			NetSocket.Instance.UnregisterEvent(obj, evt);
        }
        public static void ExecuteEvent( CNetEvent evt, uint source, NetStringReader stream ) {
			NetSocket.Instance.ExecuteEvent(evt, source, stream);
        }

		// Register for updates every n frames (15 per second - use ICNetUpdate interface to support this)
        public static void RegisterNetObject( ICNetUpdate obj ) {
			NetSocket.Instance.RegisterNetObject( obj );
        }
        public static void UnregisterNetObject( ICNetUpdate obj ) {
			NetSocket.Instance.UnregisterNetObject( obj );
        }

		// Get the rigid body attached to a specific network id
        public static Rigidbody GetRigidbody( uint uid ) {
			return NetSocket.Instance.GetRigidbody( uid );
		}

		// Register a packet for updates. If the packetSize is zero, this will create a dynamic packet. Otherwise, a static packet.
		// Static packets have fixed size and are more efficient. Dynamic packets are more flexible, but require more memory.
        public static void RegisterPacket( CNetFlag cmd, uint tgt, NetSocket.PacketCallback callback, int packetSize=0 )
        {
			NetSocket.Instance.RegisterPacket(cmd, tgt, callback, packetSize);
		}
        public static void UnregisterPacket( CNetFlag cmd, uint tgt )
        {
			NetSocket.Instance.UnregisterPacket(cmd, tgt);
        }

		// Register an object with an ObjectIdentifier type. This is used to identify objects in the scene.
        public static void RegisterObjectIdentifier(ObjectIdentifier cnetobjid)
		{
			NetSocket.Instance.RegisterObjectIdentifier(cnetobjid);
		}
        public static void UnregisterObjectIdentifier(ObjectIdentifier cnetobjid)
		{
			NetSocket.Instance.UnregisterObjectIdentifier(cnetobjid);
		}

		// Register an object with a CNetCharacter type. This is used to identify characters in the scene.
        public static void RegisterId( MonoBehaviour obj, string oname, int type )
		{
			NetSocket.Instance.RegisterId(obj, oname, type);
		}

		// Helper functions to get the network id of specific parts of a parent object
        public static int GetMoveTowardsId( MoveTowardsLocation mcl )
		{
			return NetSocket.Instance.GetMoveTowardsId(mcl);
		}
        public static int GetColliderId( Collider collider )
		{
			return NetSocket.Instance.GetCollider(collider);
		}
		// GetIdent returns the parent id and sets the slot id, slot id is set to -1 if the item is not in a slot.
		// This will work on inventory items, equipped items, and items in the world.
        public static uint GetIdent( GameObject obj, out int itemSlotID )
		{
			return NetSocket.Instance.GetIdent(obj, out itemSlotID);
		}

		// Get the object with the specified network id
        public static GameObject GetMoveTowards( GameObject parent, int slotid )
		{
			return NetSocket.Instance.GetMoveTowards(parent, slotid);
		}
        public static GameObject GetIdObj( GameObject parent, uint id, int slotid )
		{
			return NetSocket.Instance.GetIdObj(parent, id, slotid);
		}
		// GetView will search for players and objects with the specified network id
        public static GameObject GetView( uint id )
		{
			return NetSocket.Instance.GetView(id);
		}
		// GetObject ONLY searches for objects
        public static GameObject GetObject( uint id )
		{
			return NetSocket.Instance.GetObject(id);
		}
		// GetUser ONLY searches for users, and note it returns a CNetCharacter type
        public static CNetCharacter GetUser( uint id )
		{
			return NetSocket.Instance.GetUser(id);
		}

		// Broadcast a dynamic packet to a registered packet handler
        public static void SendDynPacket( CNetFlag cmd, uint tgt, NetStringBuilder dataptr=null )
		{
			NetSocket.Instance.SendDynPacket(cmd, tgt, dataptr);
		}
        public static void SendDynPacket( CNetFlag cmd, uint tgt, byte[] data )
		{
			NetSocket.Instance.SendDynPacket(cmd, tgt, data);
		}

		// Broadcast a static packet to a registered packet handler		
        public static void SendPacket( CNetFlag cmd, uint tgt, NetStringBuilder dataptr=null, bool instantSend=false )
		{
			NetSocket.Instance.SendPacket(cmd, tgt, dataptr, instantSend);
		}
        public static void SendPacket( CNetFlag cmd, uint tgt, byte[] data, bool instantSend=false )
		{
			NetSocket.Instance.SendPacket(cmd, tgt, data, instantSend);
		}

		// Send a dynamic packet to a registered packet handler on a specific client
        public static void SendDynPacketTo( uint playerid, CNetFlag cmd, uint tgt, NetStringBuilder dataptr=null )
		{
			NetSocket.Instance.SendDynPacketTo(playerid, cmd, tgt, dataptr);
		}
		// Send a static packet to a specific client
        public static void SendPacketTo( uint playerid, CNetFlag cmd, uint tgt, NetStringBuilder dataptr=null )
		{
			NetSocket.Instance.SendPacketTo(playerid, cmd, tgt, dataptr);
		}

		// Send a basic message
        public static void SendMessage( SCommand cmd, NetStringBuilder sb, long code=0, bool noLimit=false )
		{
			NetSocket.Instance.SendMessage(cmd, sb, code, noLimit);
		}


		// Setup and interface with other systems.
		public static void BuildHealthMonitors( GameObject obj )
		{
			NetSocket.Instance.BuildHealthMonitors(obj);
		}
        public static void BuildPlayer( GameObject obj )
		{
			NetSocket.Instance.BuildPlayer(obj);
		}
        public static void SetupClock( NetSocket.SetHourCb hourCb, NetSocket.SetSpeedCb speedCb )
		{
			NetSocket.Instance.SetupClock(hourCb, speedCb);
		}
        public static void SetupCharacterManager( NetSocket.CreateRemoteAvatarFunc cb1, NetSocket.CreateRandomAvatarFunc cb2 )
		{
			NetSocket.Instance.SetupCharacterManager(cb1, cb2);
		}

	}
}