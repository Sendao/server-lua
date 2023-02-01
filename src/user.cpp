#include "main.h"

cmdcall commands[256];
using namespace std::placeholders; 


void init_commands( void )
{
	User *p=NULL;
	int i;
	for( i=0; i<256; i++ )
		commands[i] = NULL;
	commands[0] = &User::SetKeyValue;
	commands[1] = &User::RunLuaFile;
	commands[2] = &User::RunLuaCommand;
	commands[3] = &User::GetFileList;
	commands[4] = &User::GetFile;
	commands[5] = &User::IdentifyVar;
}

User::User(void)
{
	bQuitting = false;
	outbuf = outbuf_memory = NULL;
	outbufsz = 0;
	outbufalloc = 0;
	outbufmax = 1500; // MTU
	inbufmax = 1500;
	inbuf_memory = inbuf = strmem->Alloc(inbufmax);
	inbufsz = 0;
	sHost = NULL;
	fSock = -1;
	state = 0;
}

User::~User(void)
{
	if( sHost )
		strmem->Free( sHost, strlen(sHost)+1 );
	strmem->Free( inbuf, inbufmax );
	vector<char*>::iterator it;
	long sz;
	char *ptr;
	for( it=messages.begin(); it != messages.end(); it++ ) {
		ptr = *it;
		sz = *(long*)ptr + sizeof(long);
		strmem->Free( ptr, sz );
	}
	messages.clear();
}

void User::SendMessage( char type, long size, char *data )
{
	char *buf;
	
	spackf(&buf, "clv", type, size, data );
	Output( this, buf, size+1+sizeof(long) );
	strmem->Free( buf, size+1+sizeof(long) );
	
}

void User::ProcessMessages(void)
{
	vector<char*>::iterator it;
	char *ptr;
	long sz;
	char code;

	for( it=messages.begin(); it != messages.end(); it++ ) {
		ptr = *it;
		sz = *(long *)ptr + sizeof(long);
		ptr += sizeof(long);
		code = *(char *)ptr;
		ptr ++;

		if( commands[code] != NULL ) {
			std::bind( commands[code], this, _1, _2 )( ptr, sz - (1+sizeof(long)) );
		}
		strmem->Free( *it, sz );
	}
	messages.clear();
}

void User::SetKeyValue( char *data, long sz )
{
	u_long key;
	char *ptr;
	Primitive obj;

	ptr = sunpackf(data, "lc", &key, &obj.type);
	switch( obj.type ) {
		case 0: // char
			sunpackf(ptr, "c", &obj.data.c);
			break;
		case 1: // int
			sunpackf(ptr, "i", &obj.data.i);
			break;
		case 2: // float
			sunpackf(ptr, "f", &obj.data.f);
			break;
		case 3: // string
			sunpackf(ptr, "s", &obj.data.s);
			break;
		case 4: // buffer (binary string)
			sunpackf(ptr, "p", &obj.data.p);
			break;	
	}

	datamap[key] = obj;
	datamap_whichuser[key] = this;
	dirtyset.insert( key );
}

void User::RunLuaFile( char *data, long sz )
{

}

void User::RunLuaCommand( char *data, long sz )
{

}

void User::GetFileList( char *data, long sz )
{

}

void User::GetFile( char *data, long sz )
{

}

void User::IdentifyVar( char *data, long sz )
{
	char *name;
	char *buf;
	long size;
	Primitive obj;
	unordered_map<string,u_long>::iterator it;
	u_long key;
	char *ptr;

	ptr = sunpackf(data, "s", &name, &obj.type);
	it = varmap.find(name);
	if( it == varmap.end() ) {
		key = (u_long)it->second;
		size = spackf(&buf, "csl", (char)4, name, *it );
	} else {
		size = spackf(&buf, "csl", (char)4, name, top_var_id );
		varmap[name] = top_var_id;
		key = (u_long)top_var_id;
		top_var_id++;
	}
	this->SendMessage( 0, size, buf );

	switch( obj.type ) {
		case 0: // char
			sunpackf(ptr, "c", &obj.data.c);
			break;
		case 1: // int
			sunpackf(ptr, "i", &obj.data.i);
			break;
		case 2: // float
			sunpackf(ptr, "f", &obj.data.f);
			break;
		case 3: // string
			sunpackf(ptr, "s", &obj.data.s);
			break;
		case 4: // buffer (binary string)
			sunpackf(ptr, "p", &obj.data.p);
			break;	
	}

	datamap[key] = obj;
	datamap_whichuser[key] = this;
	dirtyset.insert( key );	
}
