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
	commands[6] = &User::IdentifyObj;
	commands[7] = &User::ClockSync;
	commands[8] = &User::SetPosition;
}

User::User(void)
{
	bQuitting = false;
	fReading = NULL;
	outbuf = outbuf_memory = NULL;
	outbufsz = 0;
	outbufalloc = 0;
	outbufmax = 1500; // MTU
	compbuf = compbuf_memory = NULL;
	compbufsz = 0;
	compbufalloc = 0;
	inbufmax = 1500;
	inbuf_memory = inbuf = strmem->Alloc(inbufmax);
	inbufsz = 0;
	sHost = NULL;
	fSock = -1;
	state = 0;
	reading_ptr = NULL;
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

void User::SendMsg( char cmd, unsigned int size, char *data )
{
	char *buf=NULL;
	long bufsz;
	u_long alloced;

	bufsz = spackf(&buf, &alloced, "cv", cmd, size, data );
	Output( this, buf, bufsz );
	strmem->Free( buf, alloced );
	
}

void User::ProcessMessages(void)
{
	vector<char*>::iterator it;
	char *ptr;
	int sz;
	char code, *data;
	string buf;
	int x;

	lprintf("Process %d messages", (int)messages.size());

	for( it=messages.begin(); it != messages.end(); it++ ) {
		ptr = *it;
		code = *(char *)ptr;
		sz = (*(ptr+1) << 8) | (*(ptr+2)&0xFF);
		ptr += 3;
		if( commands[code] != NULL ) {
			lprintf("Found code %d length %lld", (int)code, sz);
			std::bind( commands[code], this, _1, _2 )( ptr, sz );
		}
		strmem->Free( *it, sz+3 );
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

	game->datamap[key] = obj;
	game->datamap_whichuser[key] = this;
	game->dirtyset.insert( key );
}

void User::RunLuaFile( char *data, long sz )
{

}

void User::RunLuaCommand( char *data, long sz )
{

}

void User::GetFileList( char *data, long sz )
{
	unordered_map<string, FileInfo*>::iterator it;

	it = game->files.begin();
	if( it == game->files.end() ) {
		this->SendMsg( 2, 0, NULL );
		return;
	}
	u_long alloced, size;
	char *buf;

	while( it != game->files.end() ) {
		FileInfo *fi = (it->second);

		size = spackf( &buf, &alloced, "sLL", fi->name, fi->size, fi->mtime );
		this->SendMsg( 1, size, buf );
		strmem->Free( buf, alloced );

		it++;
	}

	this->SendMsg( 2, 0, NULL );
}

void User::GetFile( char *data, long sz )
{
	char *filename = strmem->Alloc( sz + 1 );
	memcpy( filename, data, sz );
	filename[sz] = '\0';
	lprintf("Request file %lld %s", sz, filename);

	if( strstr(filename, "..") != NULL ) {
		lprintf("Found ..");
		strmem->Free( filename, sz+1 );
		this->SendMsg( 4, 0, NULL );
		return;
	}
	GetFileS(filename);
	strmem->Free( filename, sz+1 );
}

void User::GetFileS( char *filename )
{	
	if( this->fReading || this->reading_ptr ) {
//		lprintf("Already reading file: %s", this->fReading ? "fp" : "mem");
		char *bufcopy = str_copy(filename);
		this->reading_file_q.push( bufcopy );
		return;
	}

	unordered_map<string, FileInfo*>::iterator it;
	it = game->files.find( filename );
	if( it == game->files.end() ) {
		lprintf("Not found %s", filename);
		this->SendMsg( 4, 0, NULL );
		return;
	}
	FileInfo *fi = it->second;

	if( fi->contents ) {
		this->reading_ptr = fi->contents;
		this->reading_sz = fi->size;
 	} else {
		char *buf;

		this->fReading = fopen( filename, "rb" );
		if( !fReading ) {
			this->SendMsg( 4, 0, NULL ); // eof
			fReading = NULL; // just to make sure
			return;
		}

		buf = strmem->Alloc( 1024 );
		// Open the file and request first chunk
		int status = fread( buf, 1, 1024, fReading );
		if( status == 0 ) {
			// File is empty, send EOF
			strmem->Free( buf, 1024 );
			fclose( fReading );
			fReading = NULL;
			this->SendMsg( 4, 0, NULL );
			return;
		}

		this->SendMsg( 3, status, buf );
		strmem->Free( buf, 1024 );
	}
	game->reading_files = true;
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
	u_long alloced;

	ptr = sunpackf(data, "s", &name, &obj.type);
	it = game->varmap.find(name);
	if( it != game->varmap.end() ) {
		key = (u_long)it->second;
		size = spackf(&buf, &alloced, "csl", (char)4, name, key );
	} else {
		size = spackf(&buf, &alloced, "csl", (char)4, name, game->top_var_id );
		game->varmap[name] = game->top_var_id;
		key = (u_long)game->top_var_id;
		game->top_var_id++;
	}
	this->SendMsg( 0, size, buf );
	strmem->Free( buf, alloced );

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
			/*
		case 4: // buffer (binary string) -- needs length
			sunpackf(ptr, "p", /obj.data.plen/, &obj.data.p);
			break;
			*/
	}

	game->datamap[key] = obj;
	game->datamap_whichuser[key] = this;
	game->dirtyset.insert( key );
}

void User::IdentifyObj( char *data, long sz )
{
	char *name;
	char *buf;
	long size;
	unordered_map<string,u_int>::iterator it;
	u_int key;
	char *ptr;
	u_long alloced;

	ptr = sunpackf(data, "s", &name);
	it = game->objmap.find(name);
	if( it != game->objmap.end() ) {
		key = (u_int)it->second;
		size = spackf(&buf, &alloced, "si", name, key );
	} else {
		size = spackf(&buf, &alloced, "si", name, game->top_var_id );
		game->objmap[name] = game->top_var_id;
		key = (u_int)game->top_var_id;
		game->top_var_id++;
	}
	this->SendMsg( 5, size, buf );
	strmem->Free( buf, alloced );
	strmem->Free( name, strlen(name)+1 );
}

void User::ClockSync( char *data, long sz )
{
	long long time, userclock;

	sunpackf(data, "L", &userclock);
	time = game->GetTime();

	this->clocksync = time - userclock;
	lprintf("Set clocksync for user to %llu", this->clocksync);
}

void User::SetPosition( char *data, long sz )
{
	u_long objid;
	float x, y, z;
	float r0, r1, r2, r3;

	sunpackf(data, "lfffffff", &objid, &x, &y, &z, &r0, &r1, &r2, &r3);
	game->SetPosition( objid, x, y, z, r0, r1, r2, r3 );
	lprintf("Position updated");

}

void User::CreateObject( char *data, long sz )
{
	u_long objid;
	char *name;

	sunpackf(data, "ls", &objid, &name);
	game->CreateObject( objid, name );
	lprintf("Created %llu", objid);
	
}

