using Opsive.Shared.Events;
using Opsive.UltimateCharacterController.Character;
using UnityEngine;
using CNet;

[RequireComponent(typeof(CNetId))]
public class CNetAnimatorMonitor : AnimatorMonitor, ICNetUpdate
{
    private Animator mAnimator;
    private CNetId id;
    private int abilityIndex;
    private short dirtyBits;
    private short dirtySlot;

    private float netX;
    private float netZ;
    private float netPitch;
    private float netYaw;
    private float netSpeed;
    private float netAbilityFloat;

    protected override void Awake()
    {
        base.Awake();

        id = GetComponent<CNetId>();
        mAnimator = GetComponent<Animator>();
    }

    protected override void Start()
    {
        base.Start();

        if (!id.local) {
            var animators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; ++i) {
                animators[i].updateMode = AnimatorUpdateMode.Normal;
            }

            id.RegisterChild(this);
        } else {
            NetSocket.Instance.RegisterNetObject(this);
        }
    }

    public void Register()
    {
        NetSocket.Instance.RegisterPacket( CNetFlag.Animation, id.id, OnUpdate ); // dynamic packet
        NetSocket.Instance.RegisterPacket( CNetFlag.InitAnimation, id.id, OnSetInitial, 22 ); // static packets, size 22 and 8
        NetSocket.Instance.RegisterPacket( CNetFlag.InitItemAnimation, id.id, OnSetInitialItem, 8 );
    }

    public void OnSetInitial(ulong ts, NetStringReader stream)
    {
        var horizontalMovement = stream.ReadShortFloat();
        var forwardMovement = stream.ReadShortFloat();
        var pitch = stream.ReadShortFloat();
        var yaw = stream.ReadShortFloat();
        var speed = stream.ReadShortFloat();
        var height = stream.ReadInt();
        var moving = stream.ReadBool();
        var aiming = stream.ReadBool();
        var movementSetID = stream.ReadInt();
        var abilityIndex = stream.ReadInt();
        var abilityIntData = stream.ReadInt();
        var abilityFloatData = stream.ReadShortFloat();
        SetInitial(horizontalMovement, forwardMovement, pitch, yaw, speed, height, moving, aiming, movementSetID, abilityIndex, abilityIntData, abilityFloatData);
    }
    private void SetInitial(float horizontalMovement, float forwardMovement, float pitch, float yaw, float speed, int height, bool moving, bool aiming, int movementSetID, int abilityIndex, int abilityIntData, float abilityFloatData)
    {
        SetHorizontalMovementParameter(horizontalMovement, 1);
        SetForwardMovementParameter(forwardMovement, 1);
        SetPitchParameter(pitch, 1);
        SetYawParameter(yaw, 1);
        SetSpeedParameter(speed, 1);
        SetHeightParameter(height);
        SetMovingParameter(moving);
        SetAimingParameter(aiming);
        SetMovementSetIDParameter(movementSetID);
        SetAbilityIndexParameter(abilityIndex);
        SetAbilityIntDataParameter(abilityIntData);
        SetAbilityFloatDataParameter(abilityFloatData, 1);
        SnapAnimator();
    }

    public void OnSetInitialItem(ulong ts, NetStringReader stream)
    {
        var slotID = stream.ReadInt();
        var itemID = stream.ReadInt();
        var itemStateIndex = stream.ReadInt();
        var itemSubstateIndex = stream.ReadInt();
        SetInitialItem(slotID, itemID, itemStateIndex, itemSubstateIndex);
    }

    private void SetInitialItem(int slotID, int itemID, int itemStateIndex, int itemSubstateIndex)
    {
        SetItemIDParameter(slotID, itemID);
        SetItemStateIndexParameter(slotID, itemStateIndex);
        SetItemSubstateIndexParameter(slotID, itemSubstateIndex);
        SnapAnimator();
    }

    protected override void SnapAnimator()
    {
        base.SnapAnimator();
        abilityIndex = AbilityIndex;
    }

    public void Update()
    {
        if( id.local ) { // no.
            return;
        }
        // lock parameters?
        SetHorizontalMovementParameter(netX, 1);
        SetForwardMovementParameter(netZ, 1);
        SetPitchParameter(netPitch, 1);
        SetYawParameter(netYaw, 1);
        SetSpeedParameter(netSpeed, 1);
        SetAbilityFloatDataParameter(netAbilityFloat, 1);
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
            SetHeightParameter(stream.ReadInt());
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.Moving) != 0) {
            SetMovingParameter(stream.ReadBool());
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.Aiming) != 0) {
            SetAimingParameter(stream.ReadBool());
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.MoveSet) != 0) {
            SetMovementSetIDParameter(stream.ReadInt());
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.Ability) != 0) {
            var abilityIndexParam = stream.ReadInt();
            if (abilityIndex == 0 || abilityIndexParam == abilityIndex) {
                SetAbilityIndexParameter(abilityIndexParam);
                abilityIndex = 0;
            }
        }
        if ((dirtyFlag & (short)AnimDirtyFlags.AbilityInt) != 0) {
            SetAbilityIntDataParameter(stream.ReadInt());
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
                SetItemIDParameter(i, stream.ReadInt());
                SetItemStateIndexParameter(i, stream.ReadInt());
                SetItemSubstateIndexParameter(i, stream.ReadInt());
            }
        }
    }

    public override bool SetHorizontalMovementParameter(float value, float timeScale, float dampingTime)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
        if (base.SetHorizontalMovementParameter(value, timeScale, dampingTime)) {
            dirtyBits |= (short)AnimDirtyFlags.X;
            return true;
        }
        return false;
    }

    public override bool SetForwardMovementParameter(float value, float timeScale, float dampingTime)
    {
        if (!mAnimator.isActiveAndEnabled) {
            return false;
        }
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
