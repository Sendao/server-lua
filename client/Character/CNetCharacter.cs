using Opsive;
using Opsive.Shared.Events;
using Opsive.Shared.Game;
using Opsive.Shared.Inventory;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character.Abilities.Items;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Items;
using Opsive.UltimateCharacterController.Items.Actions;
using Opsive.UltimateCharacterController.Items.Actions.PerspectiveProperties;
using Opsive.UltimateCharacterController.Networking.Character;
using Opsive.UltimateCharacterController.Traits;
using Opsive.UltimateCharacterController;
using Opsive.UltimateCharacterController.Objects.CharacterAssist;
using Opsive.UltimateCharacterController.Demo.Character.Abilities;
using CNet;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CNetId))]
public class CNetCharacter : MonoBehaviour, INetworkCharacter, ICNetReg
{
	private UltimateCharacterLocomotion characterLocomotion;
	private InventoryBase inventory;
    private List<IItemIdentifier> itemIds;
	private CNetId id;

	private bool itemsPickedUp;

	private void Awake()
	{
		id = gameObject.GetComponent<CNetId>();
	}

	private void Start()
	{
		characterLocomotion = gameObject.GetComponent<UltimateCharacterLocomotion>();
		inventory = gameObject.GetComponent<InventoryBase>();

		Debug.Log("Cnetchar: loco=" + characterLocomotion + " inv=" + inventory + ", id=" + id.id);
		itemIds = inventory.GetAllItemIdentifiers();

		id.RegisterChild( this );
	}

	public void Delist()
	{
		if( !id.local ) {
			NetSocket.Instance.UnregisterPacket( CNetFlag.CharacterLoadDefaultLoadout, id.id );

			NetSocket.Instance.UnregisterPacket( CNetFlag.CharacterAbility, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.CharacterItemAbility, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.MoveTowards, id.id );
			//NetSocket.Instance.UnregisterPacket( CNetFlag.CharacterItemActive, id.id );

			NetSocket.Instance.UnregisterPacket( CNetFlag.CharacterEquipItem, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.CharacterPickup, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.CharacterDropAll, id.id );

			NetSocket.Instance.UnregisterPacket( CNetFlag.Fire, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.StartReload, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.Reload, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.ReloadComplete, id.id );

			NetSocket.Instance.UnregisterPacket( CNetFlag.MeleeHitCollider, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.ThrowItem, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.EnableThrowable, id.id );

			NetSocket.Instance.UnregisterPacket( CNetFlag.MagicAction, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.MagicCast, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.MagicImpact, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.MagicStop, id.id );

			NetSocket.Instance.UnregisterPacket( CNetFlag.FlashlightToggle, id.id );

			NetSocket.Instance.UnregisterPacket( CNetFlag.PushRigidbody, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.SetRotation, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.SetPosition, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.ResetPositionRotation, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.SetPositionAndRotation, id.id );

			NetSocket.Instance.UnregisterPacket( CNetFlag.SetActive, id.id );

			NetSocket.Instance.UnregisterPacket( CNetFlag.CharacterPickup1, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.CharacterPickupUsable, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.CharacterStartAbility, id.id );
			NetSocket.Instance.UnregisterPacket( CNetFlag.CharacterStartItemAbility, id.id );

		} else {
			EventHandler.UnregisterEvent<Ability, bool>(gameObject, "OnCharacterAbilityActive", EvtAbilityActive);
			EventHandler.UnregisterEvent<ItemAbility, bool>(gameObject, "OnCharacterItemAbilityActive", EvtItemAbilityActive);

			NetSocket.Instance.UnregisterPacket( CNetFlag.RequestCharacter, id.id );
		}
	}

	public void Register()
	{
		inventory = gameObject.GetComponent<InventoryBase>();
		characterLocomotion = gameObject.GetComponent<UltimateCharacterLocomotion>();
		Debug.Log("Character " + id.id + " registering for packet events.");
		itemIds = inventory.GetAllItemIdentifiers();
		if( !id.local ) {
			NetSocket.Instance.RegisterPacket( CNetFlag.CharacterLoadDefaultLoadout, id.id, OnLoadDefaultLoadout, 0 );

			NetSocket.Instance.RegisterPacket( CNetFlag.CharacterAbility, id.id, OnAbilityActive, 3 );
			NetSocket.Instance.RegisterPacket( CNetFlag.CharacterItemAbility, id.id, OnItemAbilityActive, 3 );
			NetSocket.Instance.RegisterPacket( CNetFlag.MoveTowards, id.id, OnMoveTowards, 12 );
			//NetSocket.Instance.RegisterPacket( CNetFlag.CharacterItemActive, id.id, OnItemActive, 40 );

			NetSocket.Instance.RegisterPacket( CNetFlag.CharacterEquipItem, id.id, OnEquipItem, 5 );
			NetSocket.Instance.RegisterPacket( CNetFlag.CharacterPickup, id.id, OnPickup, 8 );
			NetSocket.Instance.RegisterPacket( CNetFlag.CharacterDropAll, id.id, OnRemoveAllItems, 0 );

			NetSocket.Instance.RegisterPacket( CNetFlag.Fire, id.id, OnFire, 0 );
			NetSocket.Instance.RegisterPacket( CNetFlag.StartReload, id.id, OnStartReload, 4 );
			NetSocket.Instance.RegisterPacket( CNetFlag.Reload, id.id, OnReload, 1 );
			NetSocket.Instance.RegisterPacket( CNetFlag.ReloadComplete, id.id, OnReloadComplete, 6 );

			NetSocket.Instance.RegisterPacket( CNetFlag.MeleeHitCollider, id.id, OnMeleeHitCollider, 36 );
			NetSocket.Instance.RegisterPacket( CNetFlag.ThrowItem, id.id, OnThrowItem, 4 );
			NetSocket.Instance.RegisterPacket( CNetFlag.EnableThrowable, id.id, OnEnableThrowable, 4 );

			NetSocket.Instance.RegisterPacket( CNetFlag.MagicAction, id.id, OnMagicAction, 6 );
			NetSocket.Instance.RegisterPacket( CNetFlag.MagicCast, id.id, OnMagicCast, 32 );
			NetSocket.Instance.RegisterPacket( CNetFlag.MagicImpact, id.id, OnMagicImpact, 34 );
			NetSocket.Instance.RegisterPacket( CNetFlag.MagicStop, id.id, OnMagicStop, 8 );

			NetSocket.Instance.RegisterPacket( CNetFlag.FlashlightToggle, id.id, OnFlashlightToggle, 1 );

			NetSocket.Instance.RegisterPacket( CNetFlag.PushRigidbody, id.id, OnPushRigidbody, 26 );
			NetSocket.Instance.RegisterPacket( CNetFlag.SetRotation, id.id, OnSetRotation, 13 );
			NetSocket.Instance.RegisterPacket( CNetFlag.SetPosition, id.id, OnSetPosition, 13 );
			NetSocket.Instance.RegisterPacket( CNetFlag.ResetPositionRotation, id.id, OnResetPositionRotation, 0 );
			NetSocket.Instance.RegisterPacket( CNetFlag.SetPositionAndRotation, id.id, OnSetPositionAndRotation, 32 );

			NetSocket.Instance.RegisterPacket( CNetFlag.SetActive, id.id, OnSetActive, 2 );

			NetSocket.Instance.RegisterPacket( CNetFlag.CharacterPickup1, id.id, OnPickup1, 4 );
			NetSocket.Instance.RegisterPacket( CNetFlag.CharacterPickupUsable, id.id, OnPickupUsable, 10 );
			NetSocket.Instance.RegisterPacket( CNetFlag.CharacterStartAbility, id.id, OnStartAbility );
			NetSocket.Instance.RegisterPacket( CNetFlag.CharacterStartItemAbility, id.id, OnStartItemAbility );

			NetStringBuilder sb = new NetStringBuilder();
			sb.AddUint(NetSocket.Instance.local_uid);
			NetSocket.Instance.SendPacketTo( id.id, CNetFlag.RequestCharacter, id.id, sb );

		} else {
			EventHandler.RegisterEvent<Ability, bool>(gameObject, "OnCharacterAbilityActive", EvtAbilityActive);
			EventHandler.RegisterEvent<ItemAbility, bool>(gameObject, "OnCharacterItemAbilityActive", EvtItemAbilityActive);

			NetSocket.Instance.RegisterPacket( CNetFlag.RequestCharacter, id.id, OnRequestCharacter, 2 );
		}
	}
	
	public void LoadDefaultLoadout()
	{
		if( id.local ) {
			Debug.Log("Cnetchar: LoadDefaultLoadout()");
			NetSocket.Instance.SendPacket( CNetFlag.CharacterLoadDefaultLoadout, id.id, (NetStringBuilder)null, true );
		}
	}
	private void OnLoadDefaultLoadout(ulong ts, NetStringReader stream)
	{
		DoLoadDefaultLoadout();
	}
	private void DoLoadDefaultLoadout()
	{
		Debug.Log("LoadDefaultLoadout()");
		inventory.LoadDefaultLoadout();
	}

	public void OnRequestCharacter(ulong ts, NetStringReader stream)
	{
		uint target = stream.ReadUint();
		NetStringBuilder sb;
		if (inventory != null) {

			// Notify the joining player of the ItemIdentifiers that the player has within their inventory.
			var items = inventory.GetAllItems();
			for (int i = 0; i < items.Count; ++i) {
				var item = items[i];

				sb = new NetStringBuilder();
				sb.AddUint( item.ItemIdentifier.ID );
				sb.AddInt( inventory.GetItemIdentifierAmount(item.ItemIdentifier) );
				NetSocket.Instance.SendPacket( CNetFlag.CharacterPickup1, id.id, sb, true );

				if (item.DropPrefab != null) {
					// Usable Items have a separate ItemIdentifiers amount.
					var itemActions = item.ItemActions;
					for (int j = 0; j < itemActions.Length; ++j) {
						var usableItem = itemActions[j] as IUsableItem;
						if (usableItem == null) {
							continue;
						}

						var consumableItemIdentifierAmount = usableItem.GetConsumableItemIdentifierAmount();
						if (consumableItemIdentifierAmount > 0 || consumableItemIdentifierAmount == -1) { // -1 is used by the grenade to indicate that there is only one item.
							sb = new NetStringBuilder();
							sb.AddUint( item.ItemIdentifier.ID );
							sb.AddInt( item.SlotID );
							sb.AddInt( itemActions[j].ID );
							sb.AddInt( inventory.GetItemIdentifierAmount(usableItem.GetConsumableItemIdentifier()) );
							sb.AddInt( consumableItemIdentifierAmount );
							NetSocket.Instance.SendPacketTo( target, CNetFlag.CharacterPickupUsable, id.id, sb );
						}
					}
				}
			}

			// Ensure the correct item is equipped in each slot.
			for (int i = 0; i < inventory.SlotCount; ++i) {
				var item = inventory.GetActiveItem(i);
				if (item == null) {
					continue;
				}
				sb = new NetStringBuilder();
				sb.AddUint( item.ItemIdentifier.ID );
				sb.AddInt( i );
				sb.AddBool( true );
				NetSocket.Instance.SendPacketTo( target, CNetFlag.CharacterEquipItem, id.id, sb );
			}
		}
		object[] objs;
		for (int i = 0; i < characterLocomotion.ActiveAbilityCount; ++i) {
			var activeAbility = characterLocomotion.ActiveAbilities[i];
			if( InvalidAbility(activeAbility) ) {
				Debug.Log("Skip sending activeAbility " + activeAbility);
				continue;
			}
			sb = new NetStringBuilder();

			sb.AddInt( activeAbility.Index );
			objs = activeAbility.GetNetworkStartData();
			sb.AddObjects( objs );
			if( objs != null ) {
				Debug.Log("Send StartAbility " + activeAbility + " objs: " + objs.Length + ", " + ( objs.Length > 0 ? objs[0] : -1 ) );
			} else {
				Debug.Log("Send StartAbility " + activeAbility + " objs: null" );
			}
			NetSocket.Instance.SendDynPacketTo( target, CNetFlag.CharacterStartAbility, id.id, sb );
		}
		for (int i = 0; i < characterLocomotion.ActiveItemAbilityCount; ++i) {
			var activeItemAbility = characterLocomotion.ActiveItemAbilities[i];
			sb = new NetStringBuilder();
			sb.AddInt( activeItemAbility.Index );
			objs = activeItemAbility.GetNetworkStartData();
			sb.AddObjects(objs );
			if( objs != null ) {
				Debug.Log("Send StartAbility " + activeItemAbility + " objs: " + objs.Length + ", " + ( objs.Length > 0 ? objs[0] : -1 ) );
			} else {
				Debug.Log("Send StartAbility " + activeItemAbility + " objs: null" );
			}
			NetSocket.Instance.SendDynPacketTo( target, CNetFlag.CharacterStartItemAbility, id.id, sb );
		}
	}

	private bool InvalidAbility( Ability ability )
	{
		return ( ability is MoveTowards || ability is SpeedChange || ability is HeightChange || ability is QuickStart || ability is QuickStop || ability is QuickTurn );
	}

	public void EquipUnequipItem(uint itemID, int slotID, bool equip)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddUint( itemID );
		stream.AddInt( slotID );
		stream.AddBool( equip );

		Debug.Log("EquipUnequipItem");
		NetSocket.Instance.SendPacket( CNetFlag.CharacterEquipItem, id.id, stream, true );
	}
	private void OnEquipItem(ulong ts, NetStringReader stream)
	{
		uint itemID = stream.ReadUint();
		int slotID = stream.ReadInt();
		bool equip = stream.ReadBool();

		Debug.Log("Received EquipUnequipItem");

		if( equip && !characterLocomotion.Alive) {
			Debug.Log("Not alive!");
			return;
		}

		DoEquipItem(itemID, slotID, equip);
	}
	private void DoEquipItem(uint itemIdentifier, int slotID, bool equip)
	{
		//var invId = GetInventoryID(itemIdentifier);
		var invId = GetItemID(itemIdentifier);
		var item = inventory.GetItem(invId, slotID);
		if (item == null) {
			Debug.LogError("Error: Item not found in inventory.");
			return;
		}

		if (equip) {
			if (inventory.GetActiveItem(slotID) != item) {
				EventHandler.ExecuteEvent<Item, int>(gameObject, "OnAbilityWillEquipItem", item, slotID);
				inventory.EquipItem(invId, slotID, true);
			}
		} else {
			EventHandler.ExecuteEvent<Item, int>(gameObject, "OnAbilityUnequipItemComplete", item, slotID);
			inventory.UnequipItem(invId, slotID);
		}
	}

	public IItemIdentifier GetItemID(uint itemIdentifierID)
	{
		for( int i = 0; i < itemIds.Count; i++ ) {
			if( itemIds[i].ID == itemIdentifierID ) {
				return itemIds[i];
			}
		}
		return null;
	}

	public void ItemIdentifierPickup(uint itemIdentifierID, int amount, int slotID, bool immediatePickup, bool forceEquip)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( (int)itemIdentifierID );
		stream.AddInt( amount );
		stream.AddInt( slotID );
		stream.AddByte( immediatePickup ? (byte)1 : (byte)0 );
		stream.AddByte( forceEquip ? (byte)1 : (byte)0 );

		NetSocket.Instance.SendPacket( CNetFlag.CharacterPickup, id.id, stream, true );
	}

	private void OnPickup(ulong ts, NetStringReader stream)
	{
		uint itemIdentifierID = (uint)stream.ReadInt();
		int amount = stream.ReadInt();
		int slotID = stream.ReadInt();
		bool immediatePickup = stream.ReadByte() == 1;
		bool forceEquip = stream.ReadByte() == 1;

		DoPickup(itemIdentifierID, amount, slotID, immediatePickup, forceEquip);
	}
	private void DoPickup(uint itemIdentifierID, int amount, int slotID, bool immediatePickup, bool forceEquip)
	{        
		var itemIdentifier = GetItemID(itemIdentifierID);
		if (itemIdentifier == null) {
			return;
		}

		Debug.Log("DoPickup()");
		inventory.Pickup(itemIdentifier, amount, slotID, immediatePickup, forceEquip);
	}

	private void OnPickup1(ulong ts, NetStringReader stream)
	{
		uint itemIdentifierID = (uint)stream.ReadInt();
		int amount = stream.ReadInt();

		DoPickup1(itemIdentifierID, amount);
	}
	private void DoPickup1(uint itemIdentifierID, int amount)
	{
		var itemIdentifier = GetItemID(itemIdentifierID);
		inventory.Pickup(itemIdentifier, amount, -1, false, false, false);
	}

	private void OnPickupUsable(ulong ts, NetStringReader stream)
	{
		uint itemIdentifierID = (uint)stream.ReadInt();
		int slotID = stream.ReadInt();
		int itemActionID = stream.ReadInt();
		int amount = stream.ReadInt();
		int consumableAmount = stream.ReadInt();

		DoPickupUsable(itemIdentifierID, slotID, itemActionID, amount, consumableAmount);
	}
	private void DoPickupUsable(uint itemIdentifierID, int slotID, int itemActionID, int amount, int consumableAmount)
	{
		var itemType = GetItemID(itemIdentifierID);
		if (itemType == null) {
			Debug.Log("PickupUsable: Can't find item id " + itemIdentifierID);
			return;
		}

		var item = inventory.GetItem(itemType, slotID);
		if (item == null) {
			return;
		}

		var usableItemAction = item.GetItemAction(itemActionID) as IUsableItem;
		if (usableItemAction == null) {
			return;
		}

		// The IUsableItem has two counts: the first count is from the inventory, and the second count is set on the actual ItemAction.
		inventory.Pickup(usableItemAction.GetConsumableItemIdentifier(), amount, -1, false, false, false);
		usableItemAction.SetConsumableItemIdentifierAmount(consumableAmount);
	}

	private void OnStartAbility(ulong ts, NetStringReader stream)
	{
		int abilityIndex = stream.ReadInt();
		object[] data = stream.ReadObjects();
		DoCharacterStartAbility(abilityIndex, data);
	}
	private void DoCharacterStartAbility(int abilityIndex, object[] startData)
	{
		Debug.Log("DoCharacterStartAbility " + abilityIndex + ", " + (startData == null ? -1 : startData.Length));
		var ability = characterLocomotion.Abilities[abilityIndex];
		if( startData != null && startData.Length != 0 ) {
			ability.SetNetworkStartData(startData);		
			Debug.Log("Set start data " + startData.Length + ", " + startData[0]);
		}
		if( !characterLocomotion.TryStartAbility(ability, true, true) ) {
			Debug.Log("Failed to start ability " + abilityIndex + ": " + ability);
		} else {
			Debug.Log("Started ability " + abilityIndex + ": " + ability);
		}
	}

	private void OnStartItemAbility(ulong ts, NetStringReader stream)
	{
		int abilityIndex = stream.ReadInt();
		object[] data = stream.ReadObjects();
		DoCharacterStartItemAbility(abilityIndex, data);
	}
	private void DoCharacterStartItemAbility(int abilityIndex, object[] startData)
	{
		var ability = characterLocomotion.ItemAbilities[abilityIndex];
		if( startData != null && startData.Length != 0 ) {
			ability.SetNetworkStartData(startData);
			Debug.Log("Set item start data " + startData.Length + ", " + startData[0]);
		}
		if( !characterLocomotion.TryStartAbility(ability, true, true) ) {
			Debug.Log("Failed to start item ability " + abilityIndex + ": " + ability);
		} else {
			Debug.Log("Started item ability " + abilityIndex + ": " + ability);
		}
	}

	public void RemoveAllItems()
	{
		if (!id.local) {
			return;
		}
		
		NetSocket.Instance.SendPacket( CNetFlag.CharacterDropAll, id.id, (NetStringBuilder)null, true );
	}
	private void OnRemoveAllItems(ulong ts, NetStringReader stream)
	{
		DoRemoveAllItems();
	}
	private void DoRemoveAllItems()
	{
		Debug.Log("DoRemoveAllItems()");
		inventory.RemoveAllItems(true);
	}


	private void PickupItems()
	{
		if (itemsPickedUp) {
			return;
		}
		itemsPickedUp = true;

		var items = gameObject.GetComponentsInChildren<Item>(true);
		for (int i = 0; i < items.Length; ++i) {
			Debug.Log("PickupItems: " + i);
			items[i].Pickup();
		}
		foreach (Item item in inventory.GetAllItems())
		{
			Debug.Log("Found " + item.ItemDefinition.name);
		}
	}

	public void EvtAbilityActive(Ability ability, bool active)
	{
		NetStringBuilder stream = new NetStringBuilder();
		if( InvalidAbility(ability) ) {
			Debug.Log("Skip sending ability " + ability);
			return;
		}
		stream.AddUint( (uint)ability.Index );
		if( active ) {
			object[] startData = ability.GetNetworkStartData();
			if( startData != null && startData.Length != 0 ) {
				stream.AddObjects( startData );
			}
			NetSocket.Instance.SendDynPacket( CNetFlag.CharacterStartAbility, id.id, stream );
		} else {
			stream.AddBool( active );
			NetSocket.Instance.SendPacket( CNetFlag.CharacterAbility, id.id, stream, true );
		}
	}
	public void AbilityActive(uint abilityIndex, bool active)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddUint( abilityIndex );
		stream.AddBool( active );
		Debug.Log("AbilityActive with only index");
		NetSocket.Instance.SendPacket( CNetFlag.CharacterAbility, id.id, stream, true );
	}
	private void OnAbilityActive(ulong ts, NetStringReader stream)
	{
		uint abilityIndex = stream.ReadUint();
		bool active = stream.ReadBool();

		DoAbilityActive(abilityIndex, active);
	}
	private void DoAbilityActive(uint abilityIndex, bool active)
	{
		if (active) {
			Ability a = characterLocomotion.Abilities[abilityIndex];
			if( a is MoveTowards ) {
				Debug.Log("Skip MoveTowards ability");
				return;
			}
			if( a is IntDataSetter ) {
				Debug.Log("Skip IntSetter");
				return;
			}
			if( !characterLocomotion.TryStartAbility(characterLocomotion.Abilities[abilityIndex]) ) {
				Debug.Log("TryStartAbility " + characterLocomotion.Abilities[abilityIndex] + "(" + abilityIndex + ") failed");
			} else {
				Debug.Log("TryStartAbility " + characterLocomotion.Abilities[abilityIndex] + "(" + abilityIndex + ") succeeded");
			}
		} else {
			characterLocomotion.TryStopAbility(characterLocomotion.Abilities[abilityIndex], true);
		}
	}

	private void OnMoveTowards( ulong ts, NetStringReader stream )
	{
		Vector3 target = stream.ReadVector3();
		/*
		uint objid = stream.ReadUint();
		int slot = stream.ReadInt();

		GameObject target = NetSocket.Instance.GetView(objid);
		if( target == null ) {
			Debug.Log("OnMoveTowards: target " + objid + " = null");
			return;
		}
		GameObject mtl = NetSocket.Instance.GetMoveTowards(target, slot);
		if( mtl == null ) {
			Debug.Log("OnMoveTowards: mtl " + objid + " " + slot + " = null");
			return;
		}
		MoveTowardsLocation loc = mtl.GetComponent<MoveTowardsLocation>();
		*/

		Debug.Log("OnMoveTowards: " + target);
		// don't do it though
		return;
		//characterLocomotion.MoveTowardsAbility.MoveTowardsLocation(target);
	}

	public void EvtItemAbilityActive(ItemAbility ability, bool active)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddUint( (uint)ability.Index );
		stream.AddBool( active );

		Debug.Log("Cnet: ItemAbilityActive1: " + ability + ": " + ability.Index + " " + active);

		NetSocket.Instance.SendPacket( CNetFlag.CharacterItemAbility, id.id, stream, true );
	}
	public void ItemAbilityActive(uint abilityIndex, bool active)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddUint( abilityIndex );
		stream.AddBool( active );

		Debug.Log("Cnet: ItemAbilityActive2: " + abilityIndex + " " + active);

		NetSocket.Instance.SendPacket( CNetFlag.CharacterItemAbility, id.id, stream, true );
	}
	private void OnItemAbilityActive(ulong ts, NetStringReader stream)
	{
		uint abilityIndex = stream.ReadUint();
		bool active = stream.ReadBool();

		Debug.Log("OnItemAbilityActive: " + abilityIndex + " " + active);

		DoItemAbilityActive(abilityIndex, active);
	}
	private void DoItemAbilityActive(uint abilityIndex, bool active)
	{
		if (active) {
			characterLocomotion.TryStartAbility(characterLocomotion.ItemAbilities[abilityIndex]);
		} else {
			characterLocomotion.TryStopAbility(characterLocomotion.ItemAbilities[abilityIndex], true);
		}
	}

	private ItemAction GetItemAction(int slotID, int actionID)
	{
		var item = inventory.GetActiveItem(slotID);
		if (item == null) {
			return null;
		}
		return item.GetItemAction(actionID);
	}

	public void Fire(ItemAction itemAction, float strength)
	{
		if (!id.local) {
			return;
		}
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( itemAction.Item.SlotID );
		stream.AddInt( itemAction.ID );
		stream.AddFloat( strength );

		NetSocket.Instance.SendPacket( CNetFlag.Fire, id.id, stream, true );
	}
	private void OnFire(ulong ts, NetStringReader stream)
	{
		int slotID = stream.ReadInt();
		int actionID = stream.ReadInt();
		float strength = stream.ReadFloat();

		DoFire(slotID, actionID, strength);
	}
	private void DoFire(int slotID, int actionID, float strength)
	{
		var itemAction = GetItemAction(slotID, actionID) as ShootableWeapon;
		if (itemAction == null) {
			return;
		}
		itemAction.Fire(strength);
	}

	public void StartItemReload(ItemAction itemAction)
	{
		if (!id.local) {
			return;
		}
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( itemAction.Item.SlotID );
		stream.AddInt( itemAction.ID );

		NetSocket.Instance.SendPacket( CNetFlag.StartReload, id.id, stream, true );
	}
	private void OnStartReload(ulong ts, NetStringReader stream)
	{
		int slotID = stream.ReadInt();
		int actionID = stream.ReadInt();

		DoStartReload(slotID, actionID);
	}
	private void DoStartReload(int slotID, int actionID)
	{
		var itemAction = GetItemAction(slotID, actionID);
		if (itemAction == null) {
			return;
		}
		(itemAction as ShootableWeapon).StartItemReload();
	}

	public void ReloadItem(ItemAction itemAction, bool fullClip)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( itemAction.Item.SlotID );
		stream.AddInt( itemAction.ID );
		stream.AddByte( fullClip ? (byte)1 : (byte)0 );

		NetSocket.Instance.SendPacket( CNetFlag.Reload, id.id, stream, true );
	}
	private void OnReload(ulong ts, NetStringReader stream)
	{
		int slotID = stream.ReadInt();
		int actionID = stream.ReadInt();
		bool fullClip = stream.ReadByte() == 1;

		DoReload(slotID, actionID, fullClip);
	}
	private void DoReload(int slotID, int actionID, bool fullClip)
	{
		var itemAction = GetItemAction(slotID, actionID) as ShootableWeapon;
		if (itemAction == null) {
			return;
		}
		itemAction.ReloadItem(fullClip);
	}

	public void ItemReloadComplete(ItemAction itemAction, bool success, bool immediateReload)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( itemAction.Item.SlotID );
		stream.AddInt( itemAction.ID );
		stream.AddByte( success ? (byte)1 : (byte)0 );
		stream.AddByte( immediateReload ? (byte)1 : (byte)0 );

		NetSocket.Instance.SendPacket( CNetFlag.ReloadComplete, id.id, stream, true );
	}
	private void OnReloadComplete(ulong ts, NetStringReader stream)
	{
		int slotID = stream.ReadInt();
		int actionID = stream.ReadInt();
		bool success = stream.ReadByte() == 1;
		bool immediateReload = stream.ReadByte() == 1;

		DoReloadComplete(slotID, actionID, success, immediateReload);
	}
	private void DoReloadComplete(int slotID, int actionID, bool success, bool immediateReload)
	{
		var itemAction = GetItemAction(slotID, actionID) as ShootableWeapon;
		if (itemAction == null) {
			return;
		}
		itemAction.ItemReloadComplete(success, immediateReload);
	}
	
	public void MeleeHitCollider(ItemAction itemAction, int hitboxIndex, RaycastHit rayhit, GameObject hitGo, UltimateCharacterLocomotion hitLoco)
	{
		NetStringBuilder stream = new NetStringBuilder();

		stream.AddInt( itemAction.Item.SlotID );
		stream.AddInt( itemAction.ID );
		stream.AddInt( hitboxIndex );
		stream.AddVector3( rayhit.point );
		stream.AddVector3( rayhit.normal );

		// retrieve these from various places
		int slotID = 0;
		uint hitgoID = NetSocket.Instance.GetIdent( hitGo, out slotID );
		stream.AddUint( hitgoID );
		stream.AddInt( slotID );

		if( hitLoco != null ) {
			CNetId other = hitLoco.GetComponent<CNetId>();
			stream.AddUint( other.id );
		} else {
			stream.AddUint( 0 );
		}

		NetSocket.Instance.SendPacket( CNetFlag.MeleeHitCollider, id.id, stream, true );
	}
	private void OnMeleeHitCollider(ulong ts, NetStringReader stream)
	{
		int slotID = stream.ReadInt();
		int actionID = stream.ReadInt();
		int hitboxIndex = stream.ReadInt();
		Vector3 point = stream.ReadVector3();
		Vector3 normal = stream.ReadVector3();
		uint hitgoID = stream.ReadUint();
		int hitgoslotID = stream.ReadInt();
		uint otherID = stream.ReadUint();

		CNetCharacter hitLoco;
		UltimateCharacterLocomotion charLoco;

		if( otherID == 0 ) {
			hitLoco = null;
			charLoco = null;
		} else {
		 	hitLoco = NetSocket.Instance.GetUser( otherID );
			charLoco = hitLoco.GetComponent<UltimateCharacterLocomotion>();
		}

		Debug.Log("Hitgoid: " + hitgoID + ", slotid: " + hitgoslotID);
		GameObject hitgo = NetSocket.Instance.GetIdObj( hitLoco==null?null:hitLoco.gameObject, hitgoID, hitgoslotID );
		Collider collider = null;
		if( hitgo != null ) {
			collider = hitgo.GetComponent<Collider>();
		}

		DoMeleeHitCollider(slotID, actionID, hitboxIndex, point, normal, collider, hitgo, charLoco);
	}

	private void DoMeleeHitCollider(int slotID, int actionID, int hitboxIndex, Vector3 raycastHitPoint, Vector3 raycastHitNormal, Collider hitCollider, GameObject hitGameObject, UltimateCharacterLocomotion characterLocomotion)
	{
		var meleeWeapon = GetItemAction(slotID, actionID) as MeleeWeapon;
		if (meleeWeapon == null) {
			return;
		}

		var ray = new Ray(raycastHitPoint + raycastHitNormal * 1f, -raycastHitNormal);
		if (!hitCollider || !hitCollider.Raycast(ray, out var hit, 2f)) {
			// The object has moved. Do a larger cast to try to find the object.
			if (!hitGameObject || !Physics.SphereCast(ray, 1f, out hit, 2f, 1 << hitGameObject.layer, QueryTriggerInteraction.Ignore)) {
				// The object can't be found. Return.
				return;
			}
		}

		Debug.Log("Found weapon " + meleeWeapon + " : and target " + hitGameObject + ": " + hitCollider);
		var hitHealth = hitGameObject.GetCachedParentComponent<Health>();
		var hitbox = (meleeWeapon.ActivePerspectiveProperties as IMeleeWeaponPerspectiveProperties).Hitboxes[hitboxIndex];
		meleeWeapon.HitCollider(hitbox, hit, hitGameObject, hitCollider, hitHealth);
	}

	public void ThrowItem(ItemAction itemAction)
	{
		if (!id.local) {
			return;
		}

		var throwableItem = itemAction as ThrowableItem;
		if (throwableItem == null) {
			return;
		}
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( itemAction.Item.SlotID );
		stream.AddInt( itemAction.ID );
		NetSocket.Instance.SendPacket( CNetFlag.ThrowItem, id.id, stream, true );
	}
	private void OnThrowItem(ulong ts, NetStringReader stream)
	{
		int slotID = stream.ReadInt();
		int actionID = stream.ReadInt();

		DoThrowItem(slotID, actionID);
	}
	private void DoThrowItem(int slotID, int actionID)
	{
		var itemAction = GetItemAction(slotID, actionID) as ThrowableItem;
		if (itemAction == null) {
			return;
		}

		itemAction.ThrowItem();
	}

	public void EnableThrowableObjectMeshRenderers(ItemAction itemAction)
	{
		if (!id.local) {
			return;
		}

		var throwableItem = itemAction as ThrowableItem;
		if (throwableItem == null) {
			return;
		}
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( itemAction.Item.SlotID );
		stream.AddInt( itemAction.ID );
		NetSocket.Instance.SendPacket( CNetFlag.EnableThrowable, id.id, stream, true );
	}
	private void OnEnableThrowable(ulong ts, NetStringReader stream)
	{
		int slotID = stream.ReadInt();
		int actionID = stream.ReadInt();

		DoEnableThrowable(slotID, actionID);
	}
	private void DoEnableThrowable(int slotID, int actionID)
	{
		var itemAction = GetItemAction(slotID, actionID) as ThrowableItem;
		if (itemAction == null) {
			return;
		}

		itemAction.EnableObjectMeshRenderers(true);
	}


	public void StartStopBeginEndMagicActions(ItemAction itemAction, bool beginActions, bool start)
	{
		if (!id.local) {
			return;
		}

		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( itemAction.Item.SlotID );
		stream.AddInt( itemAction.ID );
		stream.AddBool( beginActions );
		stream.AddBool( start );
		
		NetSocket.Instance.SendPacket( CNetFlag.MagicAction, id.id, stream, true );
	}
	private void OnMagicAction(ulong ts, NetStringReader stream)
	{
		int slotID = stream.ReadInt();
		int actionID = stream.ReadInt();
		bool beginActions = stream.ReadBool();
		bool start = stream.ReadBool();

		DoMagicAction(slotID, actionID, beginActions, start);
	}
	private void DoMagicAction(int slotID, int actionID, bool beginActions, bool start)
	{
		var itemAction = GetItemAction(slotID, actionID) as MagicItem;
		if (itemAction == null) {
			return;
		}
		itemAction.StartStopBeginEndActions(beginActions, start, false);
	}

	public void MagicCast(ItemAction itemAction, int index, uint castID, Vector3 direction, Vector3 targetPosition)
	{
		if (!id.local) {
			return;
		}

		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( itemAction.Item.SlotID );
		stream.AddInt( itemAction.ID );
		stream.AddInt( index );
		stream.AddUint( castID );
		stream.AddVector3( direction );
		stream.AddVector3( targetPosition );

		NetSocket.Instance.SendPacket( CNetFlag.MagicCast, id.id, stream, true );
	}
	private void OnMagicCast(ulong ts, NetStringReader stream)
	{
		int slotID = stream.ReadInt();
		int actionID = stream.ReadInt();
		int index = stream.ReadInt();
		uint castID = stream.ReadUint();
		Vector3 direction = stream.ReadVector3();
		Vector3 targetPosition = stream.ReadVector3();

		DoMagicCast(slotID, actionID, index, castID, direction, targetPosition);
	}
	private void DoMagicCast(int slotID, int actionID, int index, uint castID, Vector3 direction, Vector3 targetPosition)
	{
		var itemAction = GetItemAction(slotID, actionID) as MagicItem;
		if (itemAction == null) {
			return;
		}

		var castAction = itemAction.CastActions[index];
		if (castAction == null) {
			return;
		}
		castAction.CastID = castID;
		castAction.Cast(itemAction.MagicItemPerspectiveProperties.OriginLocation, direction, targetPosition);
	}

	public void MagicImpact(ItemAction itemAction, uint castID, GameObject source, GameObject target, Vector3 position, Vector3 normal)
	{
		CNetId sourceID = source.GetComponent<CNetId>();
		CNetId targetID = target.GetComponent<CNetId>();
		if (sourceID == null) {
			Debug.LogError($"Error: Unable to retrieve the ID of the {source.name} GameObject. Ensure a CNetId has been added.");
			return;
		}
		if (targetID == null) {
			Debug.LogError($"Error: Unable to retrieve the ID of the {target.name} GameObject. Ensure a CNetId has been added.");
			return;
		}

		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( itemAction.Item.SlotID );
		stream.AddInt( itemAction.ID );
		stream.AddUint( castID );
		stream.AddUint( sourceID.id );
		stream.AddUint( targetID.id );
		stream.AddVector3( position );
		stream.AddVector3( normal );

		NetSocket.Instance.SendPacket( CNetFlag.MagicImpact, id.id, stream, true );
	}
	public void OnMagicImpact(ulong ts, NetStringReader stream)
	{
		int slotID = stream.ReadInt();
		int actionID = stream.ReadInt();
		uint castID = stream.ReadUint();
		uint sourceID = stream.ReadUint();
		uint targetID = stream.ReadUint();
		Vector3 position = stream.ReadVector3();
		Vector3 normal = stream.ReadVector3();

		DoMagicImpact(slotID, actionID, castID, sourceID, targetID, -1, position, normal);
	}

	private void DoMagicImpact(int slotID, int actionID, uint castID, uint sourceID, uint targetID, int targetSlotID, Vector3 position, Vector3 normal)
	{
		var itemAction = GetItemAction(slotID, actionID) as MagicItem;
		if (itemAction == null) {
			return;
		}

		var source = NetSocket.Instance.GetObject(sourceID);
		if( !source ) {
			Debug.LogWarning("MagicImpact: source has no id");
			return;
		}

		var target = NetSocket.Instance.GetObject(targetID);
		if( !target ) {
			Debug.LogWarning("MagicImpact: target has no id");
			return;
		}

		var targetCollider = target.GetCachedComponent<Collider>();
		if (targetCollider == null) {
			return;
		}

		// A RaycastHit cannot be sent over the network. Try to recreate it locally based on the position and normal values.
		var ray = new Ray(position + normal * 1f, -normal);
		if (!targetCollider.Raycast(ray, out var hit, 2f)) {
			// The object has moved. Do a larger cast to try to find the object.
			if (!Physics.SphereCast(ray, 1f, out hit, 2f, 1 << targetCollider.gameObject.layer, QueryTriggerInteraction.Ignore)) {
				// The object can't be found. Return.
				return;
			}
		}

		itemAction.PerformImpact(castID, source, target, hit);
	}


	public void StopMagicCast(ItemAction itemAction, int index, uint castID)
	{
		if (!id.local) {
			return;
		}
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( itemAction.Item.SlotID );
		stream.AddInt( itemAction.ID );
		stream.AddInt( index );
		stream.AddUint( castID );

		NetSocket.Instance.SendPacket( CNetFlag.MagicStop, id.id, stream, true );
	}
	private void OnMagicStop(ulong ts, NetStringReader stream)
	{
		int slotID = stream.ReadInt();
		int actionID = stream.ReadInt();
		int index = stream.ReadInt();
		uint castID = stream.ReadUint();

		DoStopMagicCast(slotID, actionID, index, castID);
	}

	private void DoStopMagicCast(int slotID, int actionID, int index, uint castID)
	{
		var itemAction = GetItemAction(slotID, actionID) as MagicItem;
		if (itemAction == null) {
			return;
		}
		var castAction = itemAction.CastActions[index];
		if (castAction == null) {
			return;
		}
		castAction.Stop(castID);
	}

	public void ToggleFlashlight(ItemAction itemAction, bool active)
	{
		if (!id.local) {
			return;
		}
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( itemAction.Item.SlotID );
		stream.AddInt( itemAction.ID );
		stream.AddBool( active );

		NetSocket.Instance.SendPacket( CNetFlag.FlashlightToggle, id.id, stream, true );
	}
	private void OnFlashlightToggle(ulong ts, NetStringReader stream)
	{
		int slotID = stream.ReadInt();
		int actionID = stream.ReadInt();
		bool active = stream.ReadBool();

		DoToggleFlashlight(slotID, actionID, active);
	}

	private void DoToggleFlashlight(int slotID, int actionID, bool active)
	{
		var itemAction = GetItemAction(slotID, actionID) as Flashlight;
		if (itemAction == null) {
			Debug.Log("Flashlight not found");
			return;
		}
		itemAction.ToggleFlashlight(active);
	}

	public void PushRigidbody(Rigidbody targetRigidbody, Vector3 force, Vector3 point)
	{

		var targetId = targetRigidbody.gameObject.GetComponent<CNetId>();
		if( !targetId ) {
			Debug.Log("PushRigidbody: target has no id");
			return;
		}

		NetStringBuilder stream = new NetStringBuilder();
		stream.AddUint( targetId.id );
		stream.AddVector3( force );
		stream.AddVector3( point );

		NetSocket.Instance.SendPacket( CNetFlag.PushRigidbody, id.id, stream, true );
	}
	private void OnPushRigidbody(ulong ts, NetStringReader stream)
	{
		uint targetID = stream.ReadUint();
		Vector3 force = stream.ReadVector3();
		Vector3 point = stream.ReadVector3();

		DoPushRigidbody(targetID, force, point);
	}

	private void DoPushRigidbody(uint targetID, Vector3 force, Vector3 point)
	{
		var target = NetSocket.Instance.GetObject(targetID);
		if (target == null) {
			Debug.Log("PushRigidbody: could not find target");
			return;
		}

		var rb = target.gameObject.GetComponent<Rigidbody>();
		if (rb == null) {
			Debug.Log("PushRigidbody: target has no rigidbody");
			return;
		}

		rb.AddForceAtPosition(force, point, ForceMode.VelocityChange);
		Debug.Log("Cnet add force (pushrigidbody)");
	}

	public void SetRotation(Quaternion rotation, bool snapAnimator)
	{
		if (!id.local) {
			return;
		}
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddVector3( rotation.eulerAngles );
		stream.AddBool( snapAnimator );

		NetSocket.Instance.SendPacket( CNetFlag.SetRotation, id.id, stream, true );
	}
	private void OnSetRotation(ulong ts, NetStringReader stream)
	{
		Quaternion rotation = Quaternion.Euler( stream.ReadVector3() );
		bool snapAnimator = stream.ReadBool();

		DoSetRotation(rotation, snapAnimator);
	}

	public void DoSetRotation(Quaternion rotation, bool snapAnimator)
	{
		Debug.Log("Cnetchar: SetRot");
		characterLocomotion.SetRotation(rotation, snapAnimator);
	}

	public void SetPosition(Vector3 position, bool snapAnimator)
	{
		if (!id.local) {
			return;
		}

		NetStringBuilder stream = new NetStringBuilder();
		stream.AddVector3( position );
		stream.AddBool( snapAnimator );

		NetSocket.Instance.SendPacket( CNetFlag.SetPosition, id.id, stream, true );
	}
	public void SendPosition()
	{
		if (!id.local) {
			return;
		}

		NetStringBuilder stream = new NetStringBuilder();
		stream.AddVector3( transform.position );
		stream.AddBool( false );

		NetSocket.Instance.SendPacket( CNetFlag.SetPosition, id.id, stream, true );
	}
	private void OnSetPosition(ulong ts, NetStringReader stream)
	{
		Vector3 position = stream.ReadVector3();
		bool snapAnimator = stream.ReadBool();

		DoSetPosition(position, snapAnimator);
	}

	public void DoSetPosition(Vector3 position, bool snapAnimator)
	{
		Debug.Log("Cnetchar: SetPos");
		characterLocomotion.SetPosition(position, snapAnimator);
	}


	public void ResetRotationPosition()
	{
		NetSocket.Instance.SendPacket( CNetFlag.ResetPositionRotation, id.id, (NetStringBuilder)null, true );
	}
	private void OnResetPositionRotation(ulong ts, NetStringReader stream)
	{
		DoResetPositionRotation();
	}
	public void DoResetPositionRotation()
	{
		Debug.Log("Cnetchar: ResetPosAndRot");
		characterLocomotion.ResetRotationPosition();
	}

	public void SetPositionAndRotation(Vector3 position, Quaternion rotation, bool snapAnimator, bool stopAllAbilities)
	{
		NetStringBuilder sb = new NetStringBuilder();
		sb.AddVector3( position );
		sb.AddVector3( rotation.eulerAngles );
		sb.AddBool( snapAnimator );
		sb.AddBool( stopAllAbilities );

		NetSocket.Instance.SendPacket( CNetFlag.SetPositionAndRotation, id.id, sb, true );
	}
	private void OnSetPositionAndRotation(ulong ts, NetStringReader stream)
	{
		Vector3 position = stream.ReadVector3();
		float rx = stream.ReadFloat();
		float ry = stream.ReadFloat();
		float rz = stream.ReadFloat();
		Quaternion rotation = Quaternion.Euler(rx, ry, rz);
		bool snapAnimator = stream.ReadBool();
		bool stopAllAbilities = stream.ReadBool();

		DoSetPositionAndRotation(position, rotation, snapAnimator, stopAllAbilities);
	}
	public void DoSetPositionAndRotation(Vector3 position, Quaternion rotation, bool snapAnimator, bool stopAllAbilities)
	{
		Debug.Log("Cnetchar: SetPosAndRot(snap = " + snapAnimator + ", stop = " + stopAllAbilities + ")");
		characterLocomotion.SetPositionAndRotation(position, rotation, snapAnimator, stopAllAbilities, false);
	}

	public void SetActive(bool active, bool uiEvent)
	{
		Debug.Log("Cnetchar: SetActive: " + active + " " + uiEvent);
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddBool( active );
		stream.AddBool( uiEvent );

		NetSocket.Instance.SendPacket( CNetFlag.SetActive, id.id, stream, true );
	}
	private void OnSetActive(ulong ts, NetStringReader stream)
	{
		bool active = stream.ReadBool();
		bool uiEvent = stream.ReadBool();

		DoSetActive(active, uiEvent);
	}
	private void DoSetActive(bool active, bool uiEvent)
	{
		gameObject.SetActive(active);

		if (uiEvent) {
			EventHandler.ExecuteEvent(gameObject, "OnShowUI", active);
		}
	}

	/// <summary>
	/// The character has been destroyed.
	/// </summary>
	private void OnDestroy()
	{
		//EventHandler.UnregisterEvent<Ability, bool>(gameObject, "OnCharacterAbilityActive", OnAbilityActive);
		//EventHandler.UnregisterEvent<ItemAbility, bool>(gameObject, "OnCharacterItemAbilityActive", OnItemAbilityActive);
		//EventHandler.UnregisterEvent<Player, GameObject>("OnPlayerEnteredRoom", OnPlayerEnteredRoom);
		//EventHandler.UnregisterEvent<Player, GameObject>("OnPlayerLeftRoom", OnPlayerLeftRoom);
	}
}
