#ifndef __WHIRLWIND_SERVER_H
#define __WHIRLWIND_SERVER_H

#include <string.h>
#include <sys/types.h>
#define SOL_ALL_SAFETIES_ON 1
#include <sol/sol.hpp>
#include <iostream>

#ifdef WIN32
#include <winsock2.h>
#else
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

using namespace std;


typedef class User User;
typedef class Object Object;
typedef class Game Game;

// lua.cpp
extern sol::state lua;
void init_lua(void);

// user.cpp
typedef struct primitive Primitive;
typedef void (User::*cmdcall)(char *,long);


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

// main.cpp
typedef struct _CompressionPacket CompressionPacket;

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
	unordered_map<string,u_long> varmap;
	unordered_map<string,u_int> objmap;
	u_long top_var_id = 1;
	unordered_map<u_long,Primitive> datamap;
	unordered_map<u_long,User*> datamap_whichuser;
	unordered_set<u_long> dirtyset;
	bool reading_files = false;
	unordered_map<string, FileInfo*> files;
	vector<User*> userL;
	unordered_map<u_long, Object*> objects;

	public:
	void mainloop(void);
	long long GetTime(void);
	void SetPosition( u_long id, float x, float y, float z, float r0, float r1, float r2, float r3 );
	void CreateObject( u_long uid, char *name );
	void SendMsg( char cmd, unsigned int size, char *data, User *exclude );
};
extern Game *game;

class Object
{
	public:
	Object();
	~Object();

	public:
	u_long uid;
	float x, y, z;
	float r0, r1, r2, r3;
	char *name;
};


class User
{
	public:
	User();
	~User();

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
	
	char *reading_ptr;
	long reading_sz;
	FILE *fReading;
	queue<char*> reading_file_q; // todo: this should be a queue (FIFO)
	long long clocksync;

	public:
	void ProcessMessages(void);
	void SendMsg( char cmd, unsigned int size, char *data );

	public: // commands (client controlled)
	void SetKeyValue(char *data, long sz);
	void RunLuaFile(char *data, long sz);
	void RunLuaCommand(char *data, long sz);
	void GetFileList(char *data, long sz);
	void GetFile(char *data, long sz);
	void GetFileS(char *filename);
	void IdentifyVar(char *data, long sz);
	void IdentifyObj(char *data, long sz);
	void ClockSync(char *data, long sz);
	void SetPosition(char *data, long sz);
	void CreateObject(char *data, long sz);
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


struct primitive
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

struct _StreamMessage
{
};


#endif
