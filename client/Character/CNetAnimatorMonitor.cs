using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;
using CNet;

[RequireComponent(typeof(CNetId))]
public class CNetAnimatorMonitor : AnimatorMonitor, ICNetReg, ICNetUpdate
{
    private Animator mAnimator;
    private CNetId id;
    private int abilityIndex;
    private short dirtyBits;
    private short dirtySlot;

    public float netX;
    public float netZ;
    private float netPitch;
    private float netYaw;
    private float netSpeed;
    private float netAbilityFloat;

    protected override void Awake()
    {
        base.Awake();

        id = GetComponent<CNetId>();
        mAnimator = GetComponent<Animator>();

        if (!id.local) {
            var animators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; ++i) {
                if( animators[i].updateMode != AnimatorUpdateMode.Normal ) {
                    Debug.Log("Animator update mode changed to normal: " + animators[i].name);
                    animators[i].updateMode = AnimatorUpdateMode.Normal;
                }
            }
        }
    }

    protected override void Start()
    {
        base.Start();

        id.RegisterChild(this);
    }

    public void Delist()
    {
        if( id.local ) {
            NetSocket.Instance.UnregisterNetObject(this);
            NetSocket.Instance.UnregisterPacket( CNetFlag.RequestAnimation, id.id );
            NetSocket.Instance.UnregisterPacket( CNetFlag.RequestItemAnimation, id.id );
        } else {
            NetSocket.Instance.UnregisterPacket( CNetFlag.Animation, id.id );
            NetSocket.Instance.UnregisterPacket( CNetFlag.InitAnimation, id.id );
            NetSocket.Instance.UnregisterPacket( CNetFlag.InitItemAnimation, id.id );
        }
    }

    public void Register()
    {
        if( id.local ) {
            NetSocket.Instance.RegisterNetObject(this);
            NetSocket.Instance.RegisterPacket( CNetFlag.RequestAnimation, id.id, OnRequestAnimation, 2 );
            NetSocket.Instance.RegisterPacket( CNetFlag.RequestItemAnimation, id.id, OnRequestItemAnimation, 2 );
        } else {
            NetSocket.Instance.RegisterPacket( CNetFlag.Animation, id.id, OnUpdate ); // dynamic packet
            NetSocket.Instance.RegisterPacket( CNetFlag.InitAnimation, id.id, OnSetInitial, 22 ); // static packet, size 22
            NetSocket.Instance.RegisterPacket( CNetFlag.InitItemAnimation, id.id, OnSetInitialItems ); // dynamic packet

            NetStringBuilder sb = new NetStringBuilder();
            sb.AddUint(NetSocket.Instance.local_uid);
            NetSocket.Instance.SendPacketTo( id.id, CNetFlag.RequestAnimation, id.id, sb );

            sb = new NetStringBuilder();
            sb.AddUint(NetSocket.Instance.local_uid);
            NetSocket.Instance.SendPacketTo( id.id, CNetFlag.RequestItemAnimation, id.id, sb );
        }
    }

    public void OnRequestAnimation(ulong ts, NetStringReader stream)
    {
        uint to = stream.ReadUint();
        NetStringBuilder sb = new NetStringBuilder();
        sb.AddShortFloat( HorizontalMovement, 10.0f );
        sb.AddShortFloat( ForwardMovement, 10.0f );
        sb.AddShortFloat( Pitch, 500.0f );
        sb.AddShortFloat( Yaw, 500.0f );
        sb.AddShortFloat( Speed, 100.0f );
        sb.AddInt( Height );
        sb.AddBool( Moving );
        sb.AddBool( Aiming );
        sb.AddInt( MovementSetID );
        sb.AddInt( AbilityIndex );
        sb.AddInt( AbilityIntData );
        sb.AddShortFloat( AbilityFloatData );
        NetSocket.Instance.SendPacketTo( to, CNetFlag.Animation, id.id, sb );
    }

    public void OnSetInitial(ulong ts, NetStringReader stream)
    {
        var horizontalMovement = stream.ReadShortFloat(10.0f);
        var forwardMovement = stream.ReadShortFloat(10.0f);
        var pitch = stream.ReadShortFloat(500.0f);
        var yaw = stream.ReadShortFloat(500.0f);
        var speed = stream.ReadShortFloat(100.0f);
        var height = stream.ReadInt();
        var moving = stream.ReadBool();
        var aiming = stream.ReadBool();
        var movementSetID = stream.ReadInt();
        var abilityIndex = stream.ReadInt();
        var abilityIntData = stream.ReadInt();
        var abilityFloatData = stream.ReadShortFloat();
        DoSetInitial(horizontalMovement, forwardMovement, pitch, yaw, speed, height, moving, aiming, movementSetID, abilityIndex, abilityIntData, abilityFloatData);
    }
    private void DoSetInitial(float horizontalMovement, float forwardMovement, float pitch, float yaw, float speed, int height, bool moving, bool aiming, int movementSetID, int abilityIndex, int abilityIntData, float abilityFloatData)
    {
        base.SetHorizontalMovementParameter(horizontalMovement, 1);
        base.SetForwardMovementParameter(forwardMovement, 1);
        base.SetPitchParameter(pitch, 1);
        base.SetYawParameter(yaw, 1);
        base.SetSpeedParameter(speed, 1);
        base.SetHeightParameter(height);
        base.SetMovingParameter(moving);
        base.SetAimingParameter(aiming);
        base.SetMovementSetIDParameter(movementSetID);
        base.SetAbilityIndexParameter(abilityIndex);
        base.SetAbilityIntDataParameter(abilityIntData);
        base.SetAbilityFloatDataParameter(abilityFloatData, 1);
        SnapAnimator();
    }

    public void OnRequestItemAnimation(ulong ts, NetStringReader stream)
    {
        uint to = stream.ReadUint();
        NetStringBuilder sb = new NetStringBuilder();
        for (int i = 0; i < ParameterSlotCount && i < 16; ++i) {
            sb.AddInt(ItemSlotID[i]);
            sb.AddInt(ItemSlotStateIndex[i]);
            sb.AddInt(ItemSlotSubstateIndex[i]);
            //Debug.Log("SetInitialItems " + this.id.id + ": get Slot " + i + ", item " + ItemSlotID[i]);
        }
        //Debug.Log("SII Total size " + sb.used + " bytes");
        NetSocket.Instance.SendDynPacketTo( to, CNetFlag.InitItemAnimation, id.id, sb );
    }

    public void OnSetInitialItems(ulong ts, NetStringReader stream)
    {
        int slotID = 0;
        //Debug.Log("SetInitialItems " + this.id.id + ": " + stream.data.Length + " bytes, starting at offset " + stream.offset);
        while( stream.offset < stream.data.Length ) {
            var itemID = stream.ReadInt();
            var itemStateIndex = stream.ReadInt();
            var itemSubstateIndex = stream.ReadInt();
            //Debug.Log("SetInitialItems " + this.id.id + ": Slot " + slotID + ", item " + itemID);
            if( itemID != 0 ) {
                base.SetItemIDParameter(slotID, itemID);
                base.SetItemStateIndexParameter(slotID, itemStateIndex);
                base.SetItemSubstateIndexParameter(slotID, itemSubstateIndex);
            }
            slotID++;
        }
        SnapAnimator();
    }

    protected override void SnapAnimator()
    {
        base.SnapAnimator();
        abilityIndex = AbilityIndex;
    }

    public void LateUpdate()
    {
        if( id.local ) { // no.
            return;
        }
        // lock parameters?
        base.SetHorizontalMovementParameter(netX, 1);
        base.SetForwardMovementParameter(netZ, 1);
        base.SetPitchParameter(netPitch, 1);
        base.SetYawParameter(netYaw, 1);
        base.SetSpeedParameter(netSpeed, 1);
        base.SetAbilityFloatDataParameter(netAbilityFloat, 1);
        //base.SnapAnimator();
    }

    public void NetUpdate()
    {
        if( !id.local ) return;
        NetStringBuilder sb = new NetStringBuilder();

        if( dirtyBits == 0 && dirtySlot == 0 ) {
            return;
        }
        sb.AddInt(dirtyBits);
        if ((dirtyBits & (short)AnimDirtyFlags.X) != 0) {
            sb.AddShortFloat( HorizontalMovement );
        }
        if ((dirtyBits & (short)AnimDirtyFlags.Z) != 0) {
            sb.AddShortFloat( ForwardMovement );
        }
        if ((dirtyBits & (short)AnimDirtyFlags.Pitch) != 0) {
            sb.AddShortFloat( Pitch );
        }
        if ((dirtyBits & (short)AnimDirtyFlags.Yaw) != 0) {
            sb.AddShortFloat( Yaw );
        }
        if ((dirtyBits & (short)AnimDirtyFlags.Speed) != 0) {
            sb.AddShortFloat( Speed );
        }
        if ((dirtyBits & (short)AnimDirtyFlags.Height) != 0) {
            sb.AddInt( Height );
        }
        if ((dirtyBits & (short)AnimDirtyFlags.Moving) != 0) {
            sb.AddBool( Moving );
        }
        if ((dirtyBits & (short)AnimDirtyFlags.Aiming) != 0) {
            sb.AddBool( Aiming );
        }
        if ((dirtyBits & (short)AnimDirtyFlags.MoveSet) != 0) {
            sb.AddInt( MovementSetID );
        }
        if ((dirtyBits & (short)AnimDirtyFlags.Ability) != 0) {
            sb.AddInt( AbilityIndex );
        }
        if ((dirtyBits & (short)AnimDirtyFlags.AbilityInt) != 0) {
            sb.AddInt( AbilityIntData );
        }
        if ((dirtyBits & (short)AnimDirtyFlags.AbilityFloat) != 0) {
            sb.AddShortFloat( AbilityFloatData );
        }
        if (HasItemParameters) {
            sb.AddInt( dirtySlot );
            for (int i = 0; i < ParameterSlotCount && i < 16; ++i) {
                if ((dirtySlot & (1<<i)) == 0) {
                    continue;
                }
                sb.AddInt(ItemSlotID[i]);
                sb.AddInt(ItemSlotStateIndex[i]);
                sb.AddInt(ItemSlotSubstateIndex[i]);
            }
        }

        NetSocket.Instance.SendDynPacket( CNetFlag.Animation, id.id, sb );
        dirtyBits = 0;
        dirtySlot = 0;
    }

    private void OnUpdate( ulong ts, NetStringReader stream )
    {
        int parmFlag;

        var dirtyFlag = stream.ReadInt();
        if ((dirtyFlag & (short)AnimDirtyFlags.X) != 0) {
            netX = stream.ReadShortFloat();
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.Z) != 0) {
            netZ = stream.ReadShortFloat();
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.Pitch) != 0) {
            netPitch = stream.ReadShortFloat();
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.Yaw) != 0) {
            netYaw = stream.ReadShortFloat();
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.Speed) != 0) {
            netSpeed = stream.ReadShortFloat();
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.Height) != 0) {
            base.SetHeightParameter(stream.ReadInt());
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.Moving) != 0) {
            base.SetMovingParameter(stream.ReadBool());
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.Aiming) != 0) {
            base.SetAimingParameter(stream.ReadBool());
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.MoveSet) != 0) {
            base.SetMovementSetIDParameter(stream.ReadInt());
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.Ability) != 0) {
            var abilityIndexParam = stream.ReadInt();
            if (abilityIndex == 0 || abilityIndexParam == abilityIndex) {
                base.SetAbilityIndexParameter(abilityIndexParam);
                abilityIndex = 0;
            }
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.AbilityInt) != 0) {
            base.SetAbilityIntDataParameter(stream.ReadInt());
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.AbilityFloat) != 0) {
            netAbilityFloat = stream.ReadShortFloat();
        }
        if (HasItemParameters) {
            int itemDirtySlot = stream.ReadInt();
            for (int i = 0; i < ParameterSlotCount; ++i) {
                parmFlag = 1 << i;
                if ((itemDirtySlot & parmFlag) == 0) {
                    continue;
                }
                base.SetItemIDParameter(i, stream.ReadInt());
                base.SetItemStateIndexParameter(i, stream.ReadInt());
                base.SetItemSubstateIndexParameter(i, stream.ReadInt());
            }
        }
    }

    public override bool SetHorizontalMovementParameter(float value, float timeScale, float dampingTime)
    {
        if (!mAnimator.isActiveAndEnabled) {
            Debug.Log("SetHorizontalMovementParameter: mAnimator is not active");
            return false;
        }
        //Debug.Log("SHMP: " + value + " " + timeScale + " " + dampingTime);
        if (base.SetHorizontalMovementParameter(value, timeScale, dampingTime)) {
            dirtyBits |= (short)AnimDirtyFlags.X;
            return true;
        }
        return false;
    }

    public override bool SetForwardMovementParameter(float value, float timeScale, float dampingTime)
    {
        if (!mAnimator.isActiveAndEnabled) {
            Debug.Log("SetVerticalMovementParameter: mAnimator is not active");
            return false;
        }
        //Debug.Log("SVMP: " + value + " " + timeScale + " " + dampingTime);
        if (base.SetForwardMovementParameter(value, timeScale, dampingTime)) {
            dirtyBits |= (short)AnimDirtyFlags.Z;
            return true;
        }
        return false;
    }

    public override bool SetPitchParameter(float value, float timeScale, float dampingTime)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetPitchParameter(value, timeScale, dampingTime)) {
            dirtyBits |= (short)AnimDirtyFlags.Pitch;
            return true;
        }
        return false;
    }

    public override bool SetYawParameter(float value, float timeScale, float dampingTime)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetYawParameter(value, timeScale, dampingTime)) {
            dirtyBits |= (short)AnimDirtyFlags.Yaw;
            return true;
        }
        return false;
    }

    public override bool SetSpeedParameter(float value, float timeScale, float dampingTime)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetSpeedParameter(value, timeScale, dampingTime)) {
            dirtyBits |= (short)AnimDirtyFlags.Speed;
            return true;
        }
        return false;
    }

    public override bool SetHeightParameter(int value)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetHeightParameter(value)) {
            dirtyBits |= (short)AnimDirtyFlags.Height;
            return true;
        }
        return false;
    }

    public override bool SetMovingParameter(bool value)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetMovingParameter(value)) {
            dirtyBits |= (short)AnimDirtyFlags.Moving;
            return true;
        }
        return false;
    }

    public override bool SetAimingParameter(bool value)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetAimingParameter(value)) {
            dirtyBits |= (short)AnimDirtyFlags.Aiming;
            return true;
        }
        return false;
    }

    public override bool SetMovementSetIDParameter(int value)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetMovementSetIDParameter(value)) {
            dirtyBits |= (short)AnimDirtyFlags.MoveSet;
            return true;
        }
        return false;
    }

    public override bool SetAbilityIndexParameter(int value)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetAbilityIndexParameter(value)) {
            dirtyBits |= (short)AnimDirtyFlags.Ability;
            return true;
        }
        return false;
    }

    public override bool SetAbilityIntDataParameter(int value)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetAbilityIntDataParameter(value)) {
            dirtyBits |= (short)AnimDirtyFlags.AbilityInt;
            return true;
        }
        return false;
    }

    public override bool SetAbilityFloatDataParameter(float value, float timeScale, float dampingTime)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetAbilityFloatDataParameter(value, timeScale, dampingTime)) {
            dirtyBits |= (short)AnimDirtyFlags.AbilityFloat;
            return true;
        }
        return false;
    }

    public override bool SetItemIDParameter(int slotID, int value)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetItemIDParameter(slotID, value)) {
            dirtySlot |= (short)(1 << slotID);
            return true;
        }
        return false;
    }

    public override bool SetItemStateIndexParameter(int slotID, int value)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetItemStateIndexParameter(slotID, value)) {
            dirtySlot |= (short)(1 << slotID);
            return true;
        }
        return false;
    }

    public override bool SetItemSubstateIndexParameter(int slotID, int value)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetItemSubstateIndexParameter(slotID, value)) {
            dirtySlot |= (short)(1 << slotID);
            return true;
        }
        return false;
    }

    private void OnDestroy()
    {
        //ventHandler.UnregisterEvent<Player, GameObject>("OnPlayerEnteredRoom", OnPlayerEnteredRoom);
        //! do it though
    }
}
