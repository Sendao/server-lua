#ifndef __WHIRLWIND_SERVER_H
#define __WHIRLWIND_SERVER_H

#include <string.h>
#include <sys/types.h>
#define SOL_ALL_SAFETIES_ON 1
#include <sol/sol.hpp>
#include <iostream>

#ifdef WIN32
#include <winsock2.h>
#include <chrono>
#else
#define LINUX
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <sys/time.h>
#include <unistd.h>
#endif

#include <ctime>

#include <fcntl.h>
#include <errno.h>

#include <set>
#include <unordered_set>
#include <queue>
#include "system.h"

#include "clock.h"
using namespace std;


typedef class User User;
typedef class Object Object;
typedef class Game Game;
typedef class Animation Animation;

typedef struct _AnimParam AnimParam;
typedef struct _LookSource LookSource;

typedef struct _VarData VarData;
typedef struct _Primitive Primitive;

struct _VarData
{
	uint16_t objid;
	char *name;
	int type;
};

enum SCmd {
	SCmdSetKeyValue,
	SCmdRunLuaFile,
	SCmdRunLuaCommand,
	SCmdGetFileList,
	SCmdGetFile,
	SCmdIdentifyVar,
	SCmdSetVar,
	SCmdClockSync,
	SCmdSetObjectPositionRotation,
	SCmdRegister,
	SCmdDynPacket,
	SCmdPacket,
	SCmdQuit,
	SCmdPacketTo,
	SCmdDynPacketTo,
	SCmdActivateLua,
	SCmdEchoRTT,
	SCmdObjectTop,
	SCmdObjectClaim,
	SCmdLast
};

enum CCmd {
	CCmdVarInfo,
	CCmdFileInfo,
	CCmdEndOfFileList,
	CCmdFileData,
	CCmdNextFile,
	CCmdTimeSync,
	CCmdTopObject,
	CCmdSetObjectPositionRotation,
	CCmdRegisterUser,
	CCmdChangeUserRegistration,
	CCmdDynPacket,
	CCmdPacket,
	CCmdUser,
	CCmdUserQuit,
	CCmdClockSetHour,
	CCmdClockSetTotalDaySec,
	CCmdClockSetDaySpeed,
	CCmdClockSync,
	CCmdRTTEcho,
	CCmdObjectClaim,
	CCmdLast
};

enum CNet {
	CNetInvalidMessage,//0
	// CHARACTER
	CNetCharacterAbility,
	CNetCharacterItemAbility,
	CNetCharacterItemActive,
	CNetCharacterPickup,
	CNetCharacterEquipItem,
	CNetCharacterLoadDefaultLoadout,
	CNetCharacterDropAll,
	CNetFire,
	CNetStartReload,
	CNetReload,
	CNetReloadComplete,
	CNetMeleeHitCollider,
	CNetThrowItem,
	CNetEnableThrowable,
	CNetMagicAction,
	CNetMagicCast,
	CNetMagicImpact,
	CNetMagicStop,
	CNetFlashlightToggle,
	CNetPushRigidbody,
	CNetSetRotation,
	CNetSetPosition,
	CNetResetPositionRotation,
	CNetSetPositionAndRotation,
	CNetSetActive,


	// ANIMATOR
	CNetAnimation,
	CNetRequestAnimation,
	CNetRequestItemAnimation,
	CNetInitAnimation,
	CNetInitItemAnimation,

	// LOOK SOURCE
	CNetPlayerLook,

	// TRANSFORM
	CNetTransform,
	// OBJECTS - RIGIDBODY
	CNetRigidbodyUpdate,

	// OBJECTS - TRANSFORM
	CNetObjTransform,

	// MECANIM
	CNetMecContinuousUpdate,
	CNetMecDiscreteUpdate,
	CNetMecSetup,

	// ATTRIBUTES
	CNetRequestAttributeSet,
	CNetAttributeSet,

	// HEALTH
	CNetDamage,
	CNetDeath,
	CNetHeal,

	// INTERACTABLES
	CNetInteract,
	CNetRespawn,


	CNetLast
};

enum ServerEvent
{
	Login,
	Logout,
	Move,
	Spawn,
	Destroy,
	Activate,
	ChangeHost,

	ServerEventLast
};
enum
{
	ParamX = 1,
	ParamZ = 2,
	ParamPitch = 4,
	ParamYaw = 8,
	ParamSpeed = 16,
	ParamHeight = 32,
	ParamMoving = 64,
	ParamAiming = 128,
	ParamMoveSet = 256,
	ParamAbility = 512,
	ParamAbilityInt = 1024,
	ParamAbilityFloat = 2048
};

enum
{
	LookDistance = 1,  // The Look Direction Distance has changed.
	LookPitch = 2,                  // The Pitch has changed.
	LookPosition = 4,           // The Look Position has changed.
	LookDirection = 8,          // The Look Direction has changed.
};

enum
{
	TransformPosition = 1,  // The Position has changed.
	TransformRotation = 2,  // The Rotation has changed.
	TransformScale = 4,     // The Scale has changed.
	TransformPlatform = 8,  // The Platform has changed.
};



// lua.cpp
extern sol::state lua;
void init_lua(void);
vector<sol::function> &LuaEvent( uint16_t cmd );
vector<sol::function> &LuaUserEvent( User *obj, uint16_t cmd );
vector<sol::function> &LuaObjEvent( Object *obj, uint16_t cmd );
void LuaActivate( Object *obj, uint16_t cmd );


// user.cpp
typedef void (User::*cmdcall)(char *,uint16_t);

void init_commands(void);

// pools.cpp
typedef class StringMemory StringMemory;
typedef class StringMemoryItem StringMemoryItem;
typedef class StringMemoryItem2 StringMemoryItem2;
//typedef class StringTrie StringTrie;
extern unordered_map<size_t, vector<void*>*> pools;
extern StringMemory *strmem;
extern char *strbuf;
extern long long strbufsz;

void init_pools(void);
void *halloc(size_t);
void hfree(void *, size_t);


// lua.cpp
void LuaActivate( Object *obj, uint16_t cmd );


// main.cpp
typedef struct _CompressionPacket CompressionPacket;
int smalltimeofday(struct timeval* tp, void* tzp );

// sockets.cpp
void InitSocket(int port);
void ExitSocket(void);
char *GetSocketError(int fSocket);
void sock_close(int lsock);
User *InitConnection(void);
int OutputConnection(User *);
int InputConnection(User *);
void Output(User *, const char *, unsigned long);
void Input(User *);
extern int fSock;
extern bool firstUser;

// system.cpp
void GetFileList(void);

// util.cpp
void lprintf(const char *fmt, ...);
void setlog(const char *p);
void debuglogflags(int16_t fla); // I suggest DBG_TOGGLE, not DBG_MAIN :L=)
void lprintfx(uint16_t flx, const char *fmt, ...); // for use with USER_BUG
bool strprefix( const char *longstr, const char *shortstr );
// Strings
long spackf(char **target, unsigned long *alloced, const char *fmt, ... );
char *sunpackf(char *buffer, const char *fmt, ... );
char *str_copy(const char *);
const char *findfirst( const char *pat, const char *tests[], int cnt, const char **resptr );
const char *findfrom( const char *pat, const char *tests );
bool isalphastr( const char *str );
char *str_replace( const char *needle, const char *newform, const char *haystack );
char *substr(const char *, int, int);
void uncleanbuf( char *buf );
void strexpand( char **buf, const char *add );
int mystrpos( const char *haystack, const char *needle, int start_offset=0 );
char *strtolower( const char *src );
char *strtoupper( const char *src );
char *htmlspecialchars_decode( char *ptr );
void mystrim( char **pbuf );
char *strdupsafe( const char * );
char *strndupsafe( const char *, int );
unsigned int crc32( const char *ptr, int len );

class Game
{
	public:
	Game();
	~Game();

	public:
	static void InitialiseAPITable(void);

	public:
	unordered_map<string,VarData*> varmap;
	uint16_t top_var_id = 1;
	unordered_map<uint16_t,VarData*> varmap_by_id;

	unordered_map<uint16_t,Primitive*> datamap;
	unordered_map<uint16_t,User*> datamap_whichuser;
	unordered_set<uint16_t> dirtyset;

	bool reading_files = false;
	unordered_map<string, FileInfo*> files;

	unordered_map<uint16_t, User*> usermap;
	unordered_map<uint16_t, Object*> objects;

	public:
	uint64_t last_update, last_timestamp;
	Clock clock;

	public:
	void mainloop(void);
	uint64_t GetTime(void);
	void IdentifyVar( char *name, int type, User *sender );
	Object *FindObject( uint16_t uid );
	void SendMsg( char cmd, unsigned int size, char *data, User *exclude=NULL );
	void PickNewAuthority( User *exclude );
};
extern Game *game;

class Object
{
	public:
	Object();
	~Object();

	public:
	static void InitialiseAPITable(void);

	public:
	uint16_t uid;
	char *name;

	uint64_t last_update;
	float x, y, z;
	float r0, r1, r2;
	float scalex, scaley, scalez;
	uint64_t prev_update;
	float prev_x, prev_y, pre_z;
	float prev_r0, prev_r1, prev_r2, prev_r3;
};


struct _LookSource
{
	float distance;
	float pitch;
	float x,y,z;
	float dirx, diry, dirz;
};

struct _AnimParam
{
	int itemid;
	int stateindex;
	int substateindex;
};

class Animation
{
	public:
	Animation();
	~Animation();

	public:
	float x, z;
	float pitch, yaw;
	float speed;
	int height;
	bool moving, aiming;
	int moveSetID;
	int abilityIndex;
	int abilityInt;
	float abilityFloat;
	vector<AnimParam> params;
};

class User
{
	public:
	User();
	~User();

	public:
	static void InitialiseAPITable(void);

	public:
	unsigned int fSock;
	int state;
    int iHost[4];
    char *sHost;
    char *outbuf, *outbuf_memory;
	int outbufsz; // usage
	int outbufalloc; // allocation
	int outbufmax; // bandwidth

	char *compbuf, *compbuf_memory;
	int compbufsz; // usage
	int compbufalloc; // allocation

    char *inbuf, *inbuf_memory;
	int inbufmax;
	int inbufsz;
	vector<char*> messages;

    bool bQuitting;
	bool authority;
	
	char *reading_ptr;
	long reading_sz;
	FILE *fReading;
	queue<char*> reading_file_q; // todo: this should be a queue (FIFO)
	int64_t clocksync;
	uint64_t last_update;

	uint16_t uid;
	float x, y, z;
	float r0, r1, r2, r3;
	bool snapAnimator;
	bool stopAllAbilities;

	Animation *anim;
	LookSource *look;

	bool hasplatform;
	uint16_t platid;
	float px, py, pz;
	float vx, vy, vz;
	float pr0, pr1, pr2, pr3;
	float scalex, scaley, scalez;

	public:
	void Close(void);
	void ProcessMessages(void);
	void SendQuit(void);
	void SendMsg( char cmd, unsigned int size, char *data );
	void SendTo( User * ); // sends all data

	public: // commands (client controlled)
	void Quit(char *data, uint16_t sz);
	void SetKeyValue(char *data, uint16_t sz);
	void RunLuaFile(char *data, uint16_t sz);
	void RunLuaCommand(char *data, uint16_t sz);
	void GetFileList(char *data, uint16_t sz);
	void GetFile(char *data, uint16_t sz);
	void GetFileS(char *filename);
	void IdentifyVar(char *data, uint16_t sz);
	void SetVar(char *data, uint16_t sz);
	void ClockSync(char *data, uint16_t sz);
	void CreateObject(char *data, uint16_t sz);
	void SetObjectPositionRotation(char *data, uint16_t sz);
	void Register(char *data, uint16_t sz);
	void DynPacket(char *data, uint16_t sz);
	void Packet(char *data, uint16_t sz);
	void DynPacketTo(char *data, uint16_t sz);
	void PacketTo(char *data, uint16_t sz);
	void ActivateLua(char *data, uint16_t sz);
	void Echo(char *data, uint16_t sz);
	void ObjectTop(char *data, uint16_t sz);
	void ObjectClaim(char *data, uint16_t sz);
};


// pools.cpp
void *halloc( size_t sz );

class StringMemoryItem
{
	public:
	char *ptr;
	size_t size;
	StringMemoryItem() {}
	StringMemoryItem( char *p, size_t s ) : ptr(p), size(s) { }
	StringMemoryItem( const StringMemoryItem &b ) : ptr(b.ptr), size(b.size) { }
	StringMemoryItem( const StringMemoryItem2 &a );

	public:
	bool operator< ( const StringMemoryItem &b ) const
	{
		return (ptr < b.ptr);
	}
	bool operator> ( const StringMemoryItem &b ) const
	{
		return (ptr > b.ptr);
	}
	bool operator== ( const StringMemoryItem &b ) const
	{
		return (ptr == b.ptr);
	}
};

class StringMemoryItem2
{
	public:
	char *ptr;
	size_t size;
	StringMemoryItem2() {}
	StringMemoryItem2( char *p, size_t s ) : ptr(p), size(s) { }
	StringMemoryItem2( const StringMemoryItem &a ) : ptr(a.ptr), size(a.size) { }
	StringMemoryItem2( const StringMemoryItem2 &b ) : ptr(b.ptr), size(b.size) { }
	
	public:
	bool operator< ( const StringMemoryItem2 &b ) const
	{
		return (size < b.size);
	}
	bool operator> ( const StringMemoryItem2 &b ) const
	{
		return (size > b.size);
	}
};

class StringMemory
{
	public:
	StringMemory();
	~StringMemory();
	
	public:
	std::set<StringMemoryItem> items_ptr; // set is implemented as a binary tree
	std::set<StringMemoryItem2> items_sz;

	public:
	char *Alloc( size_t sz );
	char *Realloc( char *ptr, size_t orig_sz, size_t new_sz );
	char *ReallocStr( char *ptr, size_t orig_sz, size_t new_sz );
	void Free( char *ptr, size_t sz );
};


/*
class StringTrie
{
	public:
	StringTrie();
	~StringTrie();

	public:
	StringTrie *t[27];
	vector<char*> strings;

	public:
	char *Add( const char *str, int offset=0 );
	char *Find( const char *str );
}
*/


struct _Primitive
{
	char type;
	union {
		char c;
		int i;
		long n;
		float f;
		char *s;
		void *p;
	} data;
};

#endif
