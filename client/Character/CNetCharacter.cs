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
using CNet;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CNetId))]
public class CNetCharacter : MonoBehaviour, INetworkCharacter, ICNetUpdate
{
	private UltimateCharacterLocomotion characterLocomotion;
	private InventoryBase inventory;
    //private List<IItemIdentifier> itemIds;
	private CNetId id;

	private bool itemsPickedUp;

	private void Awake()
	{
		characterLocomotion = gameObject.GetComponent<UltimateCharacterLocomotion>();
		inventory = gameObject.GetComponent<InventoryBase>();
		id = gameObject.GetComponent<CNetId>();
		//itemIds = inventory.GetAllItemIdentifiers();
	}

	public void NetUpdate()
	{

	}


	public void Register()
	{
		NetSocket.Instance.RegisterPacket( CNetFlag.CharacterLoadDefaultLoadout, id.id, OnLoadDefaultLoadout, 0 );

		NetSocket.Instance.RegisterPacket( CNetFlag.CharacterAbility, id.id, OnAbilityActive, 3 );
		NetSocket.Instance.RegisterPacket( CNetFlag.CharacterItemAbility, id.id, OnItemAbilityActive, 3 );
		//NetSocket.Instance.RegisterPacket( CNetFlag.CharacterItemActive, id.id, OnItemActive, 40 );

		NetSocket.Instance.RegisterPacket( CNetFlag.CharacterEquipItem, id.id, OnEquipItem, 5 );
		NetSocket.Instance.RegisterPacket( CNetFlag.CharacterPickup, id.id, OnPickup, 8 );
		NetSocket.Instance.RegisterPacket( CNetFlag.CharacterDropAll, id.id, OnRemoveAllItems, 0 );

		NetSocket.Instance.RegisterPacket( CNetFlag.Fire, id.id, OnFire, 0 );
		NetSocket.Instance.RegisterPacket( CNetFlag.StartReload, id.id, OnStartReload, 4 );
		NetSocket.Instance.RegisterPacket( CNetFlag.Reload, id.id, OnReload, 1 );
		NetSocket.Instance.RegisterPacket( CNetFlag.ReloadComplete, id.id, OnReloadComplete, 6 );

		NetSocket.Instance.RegisterPacket( CNetFlag.MeleeHitCollider, id.id, OnMeleeHitCollider, 34 );
		NetSocket.Instance.RegisterPacket( CNetFlag.ThrowItem, id.id, OnThrowItem, 4 );
		NetSocket.Instance.RegisterPacket( CNetFlag.EnableThrowable, id.id, OnEnableThrowable, 4 );

		NetSocket.Instance.RegisterPacket( CNetFlag.MagicAction, id.id, OnMagicAction, 6 );
		NetSocket.Instance.RegisterPacket( CNetFlag.MagicCast, id.id, OnMagicCast, 32 );
		NetSocket.Instance.RegisterPacket( CNetFlag.MagicImpact, id.id, OnMagicImpact, 34 );
		NetSocket.Instance.RegisterPacket( CNetFlag.MagicStop, id.id, OnMagicStop, 8 );

		NetSocket.Instance.RegisterPacket( CNetFlag.FlashlightToggle, id.id, OnFlashlightToggle, 1 );

		NetSocket.Instance.RegisterPacket( CNetFlag.PushRigidbody, id.id, OnPushRigidbody, 36 );
		NetSocket.Instance.RegisterPacket( CNetFlag.SetRotation, id.id, OnSetRotation, 13 );
		NetSocket.Instance.RegisterPacket( CNetFlag.SetPosition, id.id, OnSetPosition, 13 );
		NetSocket.Instance.RegisterPacket( CNetFlag.ResetPositionRotation, id.id, OnResetPositionRotation, 0 );
		NetSocket.Instance.RegisterPacket( CNetFlag.SetPositionAndRotation, id.id, OnSetPositionAndRotation, 32 );

		NetSocket.Instance.RegisterPacket( CNetFlag.SetActive, id.id, OnSetActive, 2 );
		Debug.Log("Character registered for packet events.");
	}

	private void Start()
	{
		characterLocomotion = gameObject.GetComponent<UltimateCharacterLocomotion>();
		inventory = gameObject.GetComponent<InventoryBase>();

		Debug.Log("Cnetchar: loco=" + characterLocomotion + " inv=" + inventory + ", id=" + id.id);
		//itemIds = inventory.GetAllItemIdentifiers();

		if( !id.local ) {
			id.RegisterChild( this );
			Debug.Log("PickupItems()");
			PickupItems();
			Debug.Log("LoadDefaultLoadout()");
	        DoLoadDefaultLoadout();
			Debug.Log("done");
		} else {
			EventHandler.RegisterEvent<Ability, bool>(gameObject, "OnCharacterAbilityActive", EvtAbilityActive);
			EventHandler.RegisterEvent<ItemAbility, bool>(gameObject, "OnCharacterItemAbilityActive", EvtItemAbilityActive);
		}
	}
	
	public void LoadDefaultLoadout()
	{
		if( id.local ) {
			Debug.Log("Cnetchar: LoadDefaultLoadout()");
			NetSocket.Instance.SendPacket( CNetFlag.CharacterLoadDefaultLoadout, id.id );
		}
		DoLoadDefaultLoadout();
	}
	private void OnLoadDefaultLoadout(ulong ts, NetStringReader stream)
	{
		DoLoadDefaultLoadout();
	}
	private void DoLoadDefaultLoadout()
	{
		inventory.LoadDefaultLoadout();
	}

	public void EquipUnequipItem(uint itemID, int slotID, bool equip)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddUint( itemID );
		stream.AddInt( slotID );
		stream.AddByte( equip ? (byte)1 : (byte)0 );

		Debug.Log("EquipUnequipItem");
		NetSocket.Instance.SendPacket( CNetFlag.CharacterEquipItem, id.id, stream );

		DoEquipItem(itemID, slotID, equip);
	}
	private void OnEquipItem(ulong ts, NetStringReader stream)
	{
		uint itemID = stream.ReadUint();
		int slotID = stream.ReadInt();
		bool equip = stream.ReadByte() == 1;

		if( equip && !characterLocomotion.Alive) {
			return;
		}

		DoEquipItem(itemID, slotID, equip);
	}
	private void DoEquipItem(uint itemIdentifier, int slotID, bool equip)
	{
		//var invId = GetInventoryID(itemIdentifier);
		var invId = CNetItemTracker.GetItem(itemIdentifier);
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

	public void ItemIdentifierPickup(uint itemIdentifierID, int amount, int slotID, bool immediatePickup, bool forceEquip)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( (int)itemIdentifierID );
		stream.AddInt( amount );
		stream.AddInt( slotID );
		stream.AddByte( immediatePickup ? (byte)1 : (byte)0 );
		stream.AddByte( forceEquip ? (byte)1 : (byte)0 );

		NetSocket.Instance.SendPacket( CNetFlag.CharacterPickup, id.id, stream );

		DoPickup(itemIdentifierID, amount, slotID, immediatePickup, forceEquip);
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
		var itemIdentifier = CNetItemTracker.GetItem(itemIdentifierID);
		if (itemIdentifier == null) {
			return;
		}

		inventory.Pickup(itemIdentifier, amount, slotID, immediatePickup, forceEquip);
	}

	public void RemoveAllItems()
	{
		if (!id.local) {
			return;
		}
		
		NetSocket.Instance.SendPacket( CNetFlag.CharacterDropAll, id.id );

		DoRemoveAllItems();
	}
	private void OnRemoveAllItems(ulong ts, NetStringReader stream)
	{
		DoRemoveAllItems();
	}
	private void DoRemoveAllItems()
	{
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
	}

	public void EvtAbilityActive(Ability ability, bool active)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddUint( (uint)ability.Index );
		stream.AddBool( active );

		Debug.Log("Cnet: AbilityActive1: " + ability.Index + " " + active);
		NetSocket.Instance.SendPacket( CNetFlag.CharacterAbility, id.id, stream, true );
	}
	public void AbilityActive(uint abilityIndex, bool active)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddUint( abilityIndex );
		stream.AddBool( active );

		NetSocket.Instance.SendPacket( CNetFlag.CharacterAbility, id.id, stream, true );

		DoAbilityActive(abilityIndex, active);
	}
	private void OnAbilityActive(ulong ts, NetStringReader stream)
	{
		uint abilityIndex = stream.ReadUint();
		bool active = stream.ReadBool();

		Debug.Log("OnAbilityActive: " + abilityIndex + " " + active);

		DoAbilityActive(abilityIndex, active);
	}
	private void DoAbilityActive(uint abilityIndex, bool active)
	{
		if (active) {
			characterLocomotion.TryStartAbility(characterLocomotion.Abilities[abilityIndex]);
		} else {
			characterLocomotion.TryStopAbility(characterLocomotion.Abilities[abilityIndex], true);
		}
	}

	public void EvtItemAbilityActive(ItemAbility ability, bool active)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddUint( (uint)ability.Index );
		stream.AddBool( active );

		Debug.Log("Cnet: ItemAbilityActive1: " + ability.Index + " " + active);

		NetSocket.Instance.SendPacket( CNetFlag.CharacterItemAbility, id.id, stream, true );
	}
	public void ItemAbilityActive(uint abilityIndex, bool active)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddUint( abilityIndex );
		stream.AddBool( active );

		Debug.Log("Cnet: ItemAbilityActive2: " + abilityIndex + " " + active);

		NetSocket.Instance.SendPacket( CNetFlag.CharacterItemAbility, id.id, stream, true );

		DoItemAbilityActive(abilityIndex, active);
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

		NetSocket.Instance.SendPacket( CNetFlag.Fire, id.id, stream );

		DoFire(itemAction.Item.SlotID, itemAction.ID, strength);
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

		NetSocket.Instance.SendPacket( CNetFlag.StartReload, id.id, stream );

		DoStartReload(itemAction.Item.SlotID, itemAction.ID);
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

		NetSocket.Instance.SendPacket( CNetFlag.Reload, id.id, stream );

		DoReload(itemAction.Item.SlotID, itemAction.ID, fullClip);
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

		NetSocket.Instance.SendPacket( CNetFlag.ReloadComplete, id.id, stream );

		DoReloadComplete(itemAction.Item.SlotID, itemAction.ID, success, immediateReload);
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
	
	public void MeleeHitCollider(ItemAction itemAction, int hitboxIndex, RaycastHit rayhit, GameObject hitgo, UltimateCharacterLocomotion charLoco)
	{
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddInt( itemAction.Item.SlotID );
		stream.AddInt( itemAction.ID );
		stream.AddInt( hitboxIndex );
		stream.AddVector3( rayhit.point );
		stream.AddVector3( rayhit.normal );

		// retrieve these from various places
		CNetId hitgocni = hitgo.GetComponent<CNetId>();
		CNetId collidercni = rayhit.collider.GetComponent<CNetId>();
		CNetId other = charLoco.GetComponent<CNetId>();

		stream.AddUint( collidercni.id );
		stream.AddUint( hitgocni.id );
		stream.AddUint( other.id );

		NetSocket.Instance.SendPacket( CNetFlag.MeleeHitCollider, id.id, stream );

		DoMeleeHitCollider(itemAction.Item.SlotID, itemAction.ID, hitboxIndex, rayhit.point, rayhit.normal, rayhit.collider, hitgo, charLoco );
	}
	private void OnMeleeHitCollider(ulong ts, NetStringReader stream)
	{
		int slotID = stream.ReadInt();
		int actionID = stream.ReadInt();
		int hitboxIndex = stream.ReadInt();
		Vector3 point = stream.ReadVector3();
		Vector3 normal = stream.ReadVector3();
		Collider collider = NetSocket.Instance.GetCollider( stream.ReadUint() );
		GameObject hitgo = NetSocket.Instance.GetObject( stream.ReadUint() );
		CNetCharacter charTarget = NetSocket.Instance.GetUser( stream.ReadUint() );
		UltimateCharacterLocomotion charLoco = charTarget.GetComponent<UltimateCharacterLocomotion>();

		DoMeleeHitCollider(slotID, actionID, hitboxIndex, point, normal, collider, hitgo, charLoco);
	}

	private void DoMeleeHitCollider(int slotID, int actionID, int hitboxIndex, Vector3 raycastHitPoint, Vector3 raycastHitNormal, Collider hitCollider, GameObject hitGameObject, UltimateCharacterLocomotion characterLocomotion)
	{
		var meleeWeapon = GetItemAction(slotID, actionID) as MeleeWeapon;
		if (meleeWeapon == null) {
			return;
		}

		var ray = new Ray(raycastHitPoint + raycastHitNormal * 1f, -raycastHitNormal);
		if (!hitCollider.Raycast(ray, out var hit, 2f)) {
			// The object has moved. Do a larger cast to try to find the object.
			if (!Physics.SphereCast(ray, 1f, out hit, 2f, 1 << hitGameObject.layer, QueryTriggerInteraction.Ignore)) {
				// The object can't be found. Return.
				return;
			}
		}

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
		NetSocket.Instance.SendPacket( CNetFlag.ThrowItem, id.id, stream );

		DoThrowItem(itemAction.Item.SlotID, itemAction.ID);
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
		NetSocket.Instance.SendPacket( CNetFlag.EnableThrowable, id.id, stream );

		DoEnableThrowable(itemAction.Item.SlotID, itemAction.ID);
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
		
		NetSocket.Instance.SendPacket( CNetFlag.MagicAction, id.id, stream );
		DoMagicAction(itemAction.Item.SlotID, itemAction.ID, beginActions, start);
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

		NetSocket.Instance.SendPacket( CNetFlag.MagicCast, id.id, stream );
		DoMagicCast(itemAction.Item.SlotID, itemAction.ID, index, castID, direction, targetPosition);
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

		NetSocket.Instance.SendPacket( CNetFlag.MagicImpact, id.id, stream );
		DoMagicImpact(itemAction.Item.SlotID, itemAction.ID, castID, sourceID.id, targetID.id, -1, position, normal);
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

		NetSocket.Instance.SendPacket( CNetFlag.MagicStop, id.id, stream );
		DoStopMagicCast(itemAction.Item.SlotID, itemAction.ID, index, castID);
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

		NetSocket.Instance.SendPacket( CNetFlag.FlashlightToggle, id.id, stream );
		DoToggleFlashlight(itemAction.Item.SlotID, itemAction.ID, active);
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
			return;
		}
		itemAction.ToggleFlashlight(active);
	}

	public void PushRigidbody(Rigidbody targetRigidbody, Vector3 force, Vector3 point)
	{

		var targetId = targetRigidbody.gameObject.GetComponent<CNetId>();
		if( !targetId ) {
			return;
		}

		NetStringBuilder stream = new NetStringBuilder();
		stream.AddUint( targetId.id );
		stream.AddVector3( force );
		stream.AddVector3( point );

		NetSocket.Instance.SendPacket( CNetFlag.PushRigidbody, id.id, stream );
		DoPushRigidbody(targetId.id, force, point);
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
			return;
		}

		var rb = target.gameObject.GetComponent<Rigidbody>();
		if (rb == null) {
			return;
		}

		rb.AddForceAtPosition(force, point, ForceMode.VelocityChange);
	}

	public void SetRotation(Quaternion rotation, bool snapAnimator)
	{
		if (!id.local) {
			return;
		}
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddVector3( rotation.eulerAngles );
		stream.AddBool( snapAnimator );

		NetSocket.Instance.SendPacket( CNetFlag.SetRotation, id.id, stream );
		DoSetRotation(rotation, snapAnimator);
	}
	private void OnSetRotation(ulong ts, NetStringReader stream)
	{
		Quaternion rotation = Quaternion.Euler( stream.ReadVector3() );
		bool snapAnimator = stream.ReadBool();

		DoSetRotation(rotation, snapAnimator);
	}

	public void DoSetRotation(Quaternion rotation, bool snapAnimator)
	{
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

		NetSocket.Instance.SendPacket( CNetFlag.SetPosition, id.id, stream );
		DoSetPosition(position, snapAnimator);
	}
	public void SendPosition()
	{
		if (!id.local) {
			return;
		}

		NetStringBuilder stream = new NetStringBuilder();
		stream.AddVector3( transform.position );
		stream.AddBool( false );

		NetSocket.Instance.SendPacket( CNetFlag.SetPosition, id.id, stream );
	}
	private void OnSetPosition(ulong ts, NetStringReader stream)
	{
		Vector3 position = stream.ReadVector3();
		bool snapAnimator = stream.ReadBool();

		DoSetPosition(position, snapAnimator);
	}

	public void DoSetPosition(Vector3 position, bool snapAnimator)
	{
		characterLocomotion.SetPosition(position, snapAnimator);
	}


	public void ResetRotationPosition()
	{

		NetSocket.Instance.SendPacket( CNetFlag.ResetPositionRotation, id.id );
		DoResetPositionRotation();
	}
	private void OnResetPositionRotation(ulong ts, NetStringReader stream)
	{
		DoResetPositionRotation();
	}
	public void DoResetPositionRotation()
	{
		characterLocomotion.ResetRotationPosition();
	}

	public void SetPositionAndRotation(Vector3 position, Quaternion rotation, bool snapAnimator, bool stopAllAbilities)
	{

		NetStringBuilder sb = new NetStringBuilder();
		sb.AddVector3( position );
		sb.AddVector3( rotation.eulerAngles );
		sb.AddBool( snapAnimator );
		sb.AddBool( stopAllAbilities );

		NetSocket.Instance.SendPacket( CNetFlag.SetPositionAndRotation, id.id, sb );
		DoSetPositionAndRotation(position, rotation, snapAnimator, stopAllAbilities);
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
		characterLocomotion.SetPositionAndRotation(position, rotation, snapAnimator, stopAllAbilities);
	}

	public void SetActive(bool active, bool uiEvent)
	{
		Debug.Log("Cnetchar: SetActive: " + active + " " + uiEvent);
		NetStringBuilder stream = new NetStringBuilder();
		stream.AddBool( active );
		stream.AddBool( uiEvent );

		NetSocket.Instance.SendPacket( CNetFlag.SetActive, id.id, stream );
		DoSetActive(active, uiEvent);
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
