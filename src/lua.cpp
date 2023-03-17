#include "main.h"
#include <vector>
#include <map>

sol::state lua;

unordered_map<uint16_t, vector<sol::function>> lua_callbacks;
unordered_map<string, unordered_map<uint16_t, vector<sol::function>>> lua_obj_callbacks;
unordered_map<uint16_t, unordered_map<uint16_t, vector<sol::function>>> lua_user_callbacks;

std::map< CNet, const char * > CNetN = {
	{ CNetInvalidMessage, "CNetInvalidMessage" },
	{ CNetCharacterAbility, "CNetCharacterAbility" },
	{ CNetCharacterItemAbility, "CNetCharacterItemAbility" },
	{ CNetCharacterItemActive, "CNetCharacterItemActive" },
	{ CNetCharacterPickup, "CNetCharacterPickup" },
	{ CNetCharacterEquipItem, "CNetCharacterEquipItem" },
	{ CNetCharacterLoadDefaultLoadout, "CNetCharacterLoadDefaultLoadout" },
	{ CNetCharacterDropAll, "CNetCharacterDropAll" },
	{ CNetFire, "CNetFire" },
	{ CNetStartReload, "CNetStartReload" },
	{ CNetReload, "CNetReload" },
	{ CNetReloadComplete, "CNetReloadComplete" },
	{ CNetMeleeHitCollider, "CNetMeleeHitCollider" },
	{ CNetThrowItem, "CNetThrowItem" },
	{ CNetEnableThrowable, "CNetEnableThrowable" },
	{ CNetMagicAction, "CNetMagicAction" },
	{ CNetMagicCast, "CNetMagicCast" },
	{ CNetMagicImpact, "CNetMagicImpact" },
	{ CNetMagicStop, "CNetMagicStop" },
	{ CNetFlashlightToggle, "CNetFlashlightToggle" },
	{ CNetPushRigidbody, "CNetPushRigidbody" },
	{ CNetSetRotation, "CNetSetRotation" },
	{ CNetSetPosition, "CNetSetPosition" },
	{ CNetResetPositionRotation, "CNetResetPositionRotation" },
	{ CNetSetPositionAndRotation, "CNetSetPositionAndRotation" },
	{ CNetSetActive, "CNetSetActive" },
	{ CNetAnimation, "CNetAnimation" },
	{ CNetRequestAnimation, "CNetRequestAnimation" },
	{ CNetRequestItemAnimation, "CNetRequestItemAnimation" },
	{ CNetInitAnimation, "CNetInitAnimation" },
	{ CNetInitItemAnimation, "CNetInitItemAnimation" },
	{ CNetPlayerLook, "CNetPlayerLook" },
	{ CNetTransform, "CNetTransform" },
	{ CNetRigidbodyUpdate, "CNetRigidbodyUpdate" },

	// OBJECTS - TRANSFORM
	{ CNetObjTransform, "CNetObjTransform" },

	// MECANIM
	{ CNetMecContinuousUpdate, "CNetMecContinuousUpdate" },
	{ CNetMecDiscreteUpdate, "CNetMecDiscreteUpdate" },
	{ CNetMecSetup, "CNetMecSetup" },

	// ATTRIBUTES
	{ CNetRequestAttributeSet, "CNetRequestAttributeSet" },
	{ CNetAttributeSet, "CNetAttributeSet" },

	// HEALTH
	{ CNetDamage, "CNetDamage" },
	{ CNetDeath, "CNetDeath" },
	{ CNetHeal, "CNetHeal" },

	// INTERACTABLES
	{ CNetInteract, "CNetInteract" },
	{ CNetRespawn, "CNetRespawn" }
};

std::map< CCmd, const char * > CCmdN = {	
	{ CCmdVarInfo, "CCmdVarInfo" },
	{ CCmdFileInfo, "CCmdFileInfo" },
	{ CCmdEndOfFileList, "CCmdEndOfFileList" },
	{ CCmdFileData, "CCmdFileData" },
	{ CCmdNextFile, "CCmdNextFile" },
	{ CCmdTimeSync, "CCmdTimeSync" },
	{ CCmdTopObject, "CCmdTopObject" },
	{ CCmdSetObjectPositionRotation, "CCmdSetObjectPositionRotation" },
	{ CCmdRegisterUser, "CCmdRegisterUser" },
	{ CCmdChangeUserRegistration, "CCmdChangeUserRegistration" },
	{ CCmdDynPacket, "CCmdDynPacket" },
	{ CCmdPacket, "CCmdPacket" },
	{ CCmdUser, "CCmdUser" },
	{ CCmdUserQuit, "CCmdUserQuit" },
	{ CCmdClockSetHour, "CCmdClockSetHour" },
	{ CCmdClockSetTotalDaySec, "CCmdClockSetTotalDaySec" },
	{ CCmdClockSetDaySpeed, "CCmdClockSetDaySpeed" },
	{ CCmdClockSync, "CCmdClockSync" },
	{ CCmdRTTEcho, "CCmdRTTEcho" },
	{ CCmdObjectClaim, "CCmdObjectClaim" },
	{ CCmdNPCRecipe, "CCmdNPCRecipe" },
	{ CCmdSpawn, "CCmdSpawn" },
};

std::map< SCmd, const char * > SCmdN = {
	{ SCmdSetKeyValue, "SCmdSetKeyValue" },
	{ SCmdRunLuaFile, "SCmdRunLuaFile" },
	{ SCmdRunLuaCommand, "SCmdRunLuaCommand" },
	{ SCmdGetFileList, "SCmdGetFileList" },
	{ SCmdGetFile, "SCmdGetFile" },
	{ SCmdIdentifyVar, "SCmdIdentifyVar" },
	{ SCmdSetVar, "SCmdSetVar" },
	{ SCmdClockSync, "SCmdClockSync" },
	{ SCmdSetObjectPositionRotation, "SCmdSetObjectPositionRotation" },
	{ SCmdRegister, "SCmdRegister" },
	{ SCmdDynPacket, "SCmdDynPacket" },
	{ SCmdPacket, "SCmdPacket" },
	{ SCmdQuit, "SCmdQuit" },
	{ SCmdPacketTo, "SCmdPacketTo" },
	{ SCmdDynPacketTo, "SCmdDynPacketTo" },
	{ SCmdActivateLua, "SCmdActivateLua" },
	{ SCmdEchoRTT, "SCmdEchoRTT" },
	{ SCmdObjectTop, "SCmdObjectTop" },
	{ SCmdObjectClaim, "SCmdObjectClaim" },
	{ SCmdNPCRecipe, "SCmdNPCRecipe" },
	{ SCmdSpawn, "ScmdSpawn" },
};

std::map< ServerEvent, const char * > ServerEventN = {
	{ Login, "Login" },
	{ Logout, "Logout" },
	{ Move, "Move" },
	{ Spawn, "Spawn" },
	{ Destroy, "Destroy" },
	{ Activate, "Activate" },
	{ ChangeHost, "ChangeHost" },
};

void LuaLog( string msg )
{
	lprintf("Lua: %s", msg.c_str());
}
void LuaRegister( uint16_t cmd, sol::function func )
{
	vector<sol::function> &v = lua_callbacks[cmd];
	v.push_back(func);
}
void LuaRegisterObj( uint16_t cmd, string objname, sol::function func )
{
	lprintf("Add command for %s:%u", objname.c_str(), cmd);
	unordered_map<uint16_t,vector<sol::function>> &vs = lua_obj_callbacks[objname];
	vector<sol::function> &v = vs[cmd];
	v.push_back(func);
}
void LuaRegisterUser( uint16_t cmd, uint16_t uid, sol::function func )
{
	lprintf("Add command for %u:%u", uid, cmd);
	unordered_map<uint16_t,vector<sol::function>> &vs = lua_user_callbacks[uid];
	vector<sol::function> &v = vs[cmd];
	v.push_back(func);
}
vector<sol::function> &LuaEvent( uint16_t cmd )
{
	return lua_callbacks[cmd];
}
vector<sol::function> &LuaObjEvent( Object *obj, uint16_t cmd )
{
	static vector<sol::function> empty;
	if( !obj->name ) return empty;
	unordered_map<string,unordered_map<uint16_t,vector<sol::function>>>::iterator it = lua_obj_callbacks.find( obj->name );
	if( it != lua_obj_callbacks.end() )
	{
		unordered_map<uint16_t,vector<sol::function>>::iterator it2 = it->second.find(cmd);
		if( it2 != it->second.end() )
		{
			return it2->second;
		}
	}
	return empty;
}
vector<sol::function> &LuaUserEvent( User *obj, uint16_t cmd )
{
	static vector<sol::function> empty;
	unordered_map<uint16_t,unordered_map<uint16_t,vector<sol::function>>>::iterator it = lua_user_callbacks.find( obj->uid );
	if( it != lua_user_callbacks.end() )
	{
		unordered_map<uint16_t,vector<sol::function>>::iterator it2 = it->second.find(cmd);
		if( it2 != it->second.end() )
		{
			return it2->second;
		}
	}
	return empty;
}

void LuaActivate( Object *obj, uint16_t cmd )
{
	if( !obj->name ) return;

	unordered_map<string,unordered_map<uint16_t,vector<sol::function>>>::iterator it = lua_obj_callbacks.find( obj->name );
	if( it != lua_obj_callbacks.end() )
	{
		unordered_map<uint16_t,vector<sol::function>>::iterator it2 = it->second.find(cmd);
		if( it2 != it->second.end() )
		{
			vector<sol::function>::iterator it3;
			for( it3 = it2->second.begin(); it3 != it2->second.end(); it3++ )
			{
				sol::function func = *it3;
				func(obj);
			}
		} else {
			lprintf("Not found registration on object %s for command %u", obj->name, cmd);
		}
	} else {
		lprintf("Not found registration on object %s", obj->name);
	}
}

Object *LuaGetObject( uint16_t id )
{
	unordered_map<uint16_t,Object*>::iterator it = game->objects.find(id);
	if( it != game->objects.end() )
		return it->second;
	return NULL;
}
Object *LuaGetObjectByName( string name )
{
	unordered_map<string,VarData*>::iterator it = game->varmap.find(name);
	if( it != game->varmap.end() )
	{
		VarData *var = it->second;
		return LuaGetObject( var->objid );
	}
	return NULL;
}
User *LuaGetUser( uint16_t id )
{
	unordered_map<uint16_t,User*>::iterator it = game->usermap.find(id);
	if( it != game->usermap.end() )
		return it->second;
	return NULL;
}

void init_lua(void)
{
	uint16_t i;
	const char *ptr;
	sol::table enumTable;

	lua.open_libraries(sol::lib::base);

	enumTable = lua["CNet"] = lua.create_table();
	for( i=0; i<CNetLast; i++) {
		enumTable[CNetN[(CNet)i]] = i;
	}
	
	enumTable = lua["CCmd"] = lua.create_table();
	for( i=0; i<CCmdLast; i++)
		enumTable[CCmdN[(CCmd)i]] = i;

	enumTable = lua["SCmd"] = lua.create_table();
	for( i=0; i<SCmdLast; i++)
		enumTable[SCmdN[(SCmd)i]] = i;

	enumTable = lua["Event"] = lua.create_table();
	for( i=0; i<ServerEventLast; i++) {
		enumTable[ServerEventN[(ServerEvent)i]] = i;
	}
	
	Clock::InitialiseAPITable();
	User::InitialiseAPITable();
	Game::InitialiseAPITable();

	lua.set_function("RegisterEvent", LuaRegister);
	lua.set_function("RegisterObjEvent", LuaRegisterObj);
	lua.set_function("RegisterUserEvent", LuaRegisterUser);
	lua.set_function("GetUser", LuaGetUser);
	lua.set_function("GetObject", LuaGetObject);
	lua.set_function("FindObject", LuaGetObjectByName);
	lua.set_function("Log", LuaLog);
}

