namespace CNet
{
    public enum SCommand {
        SetKeyValue,
        RunLuaFile,
        RunLuaCommand,
        GetFileList,
        GetFile,
        IdentifyVar,
        SetVar,
        ClockSync,
        SetObjectPositionRotation,
        Register,
        DynPacket,
        Packet,
		Quit,
		PacketTo,
		DynPacketTo,
    };

    public enum CCommand {
        VarInfo,
        FileInfo,
        EndOfFileList,
        FileData,
        NextFile,
        TimeSync,
        LinkToObject,
        SetObjectPositionRotation,
        RegisterUser,
        ChangeUserRegistration,
        DynPacket,
        Packet,
        NewUser,
        UserQuit
    };

	public enum CNetFlag : byte {
		InvalidMessage,
		// CHARACTER
		CharacterAbility,
		CharacterItemAbility,
		CharacterItemActive,
		CharacterPickup,
		CharacterEquipItem,
		CharacterLoadDefaultLoadout,
		CharacterDropAll,
		Fire,
		StartReload,
		Reload,
		ReloadComplete,
		MeleeHitCollider,
		ThrowItem,
		EnableThrowable,
		MagicAction,
		MagicCast,
		MagicImpact,
		MagicStop,
		FlashlightToggle,
		PushRigidbody,
		SetRotation,
		SetPosition,
		ResetPositionRotation,
		SetPositionAndRotation,
		SetActive,

		// ANIMATOR
		Animation,
		RequestAnimation,
		RequestItemAnimation,
		InitAnimation,
		InitItemAnimation,

		// LOOKSOURCE
		PlayerLook,

		// TRANSFORM
		Transform,

		// OBJECTS - RIGIDBODY
		RigidbodyUpdate,

		// OBJECTS - TRANSFORM
		ObjTransformUpdate,

		// MECANIM
		MecContinuousUpdate,
		MecDiscreteUpdate,
		MecSetup,

		// ATTRIBUTES
		RequestAttributeSet,
		AttributeSet,

		// HEALTH
		Damage,
		Death,
		Heal,

		// INTERACTABLES
		Interact,
		Respawn,
		
		// MORE CHARACTER
		RequestCharacter,
		CharacterPickup1,
		CharacterPickupUsable,
		CharacterStartAbility,
		CharacterStartItemAbility
	};

	public enum CNetEvent : byte
	{
		NewPlayer,
	};

	public enum AnimDirtyFlags : short
	{
		X = 1,
		Z = 2,
		Pitch = 4,
		Yaw = 8,
		Speed = 16,
		Height = 32,
		Moving = 64,
		Aiming = 128,
		MoveSet = 256,
		Ability = 512,
		AbilityInt = 1024,
		AbilityFloat = 2048
	};

	public enum LookDirtyFlags : byte
	{
		Distance = 1,  // The Look Direction Distance has changed.
		Pitch = 2,                  // The Pitch has changed.
		Position = 4,           // The Look Position has changed.
		Direction = 8          // The Look Direction has changed.
	};

	public enum TransformDirtyFlags : byte
	{
		Position = 1,               // The Position has changed.
		Rotation = 2,               // The Rotation has changed.
		Scale = 4,                  // The Scale has changed.
		Platform = 8               // The Platform has changed.
	};
}
