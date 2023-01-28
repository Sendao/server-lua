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

#include <fcntl.h>
#include <errno.h>

#include <set>

using namespace std;


// lua.cpp
extern sol::state lua;

// user.cpp
typedef class User User;

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


// sockets.cpp
void InitSocket(int port);
void ExitSocket(void);
char *GetSocketError(int fSocket);
void sock_close(int lsock);
User *InitConnection(void);
int OutputConnection(User *);
int InputConnection(User *);
void Output(User *, const char *, uint16_t);
void Input(User *);
extern int fSock;


// util.cpp
void lprintf(const char *fmt, ...);
void setlog(const char *p);
void debuglogflags(int16_t fla); // I suggest DBG_TOGGLE, not DBG_MAIN :L=)
void lprintfx(uint16_t flx, const char *fmt, ...); // for use with USER_BUG
bool strprefix( const char *longstr, const char *shortstr );
// File ops
void fpackf( FILE *fp, const char *fmt, ... );
void funpackf( FILE *fp, const char *fmt, ... );
void fpackd( int fd, const char *fmt, ... );
void funpackd( int fd, const char *fmt, ... );
// Strings
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

    char *inbuf;
	int inbufmax;
	uint32_t inbufsz;

    bool bQuitting;
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

#endif
