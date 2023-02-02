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
}

User::User(void)
{
	bQuitting = false;
	fReading = NULL;
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

void User::SendMessage( char cmd, unsigned int size, char *data )
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
	char code;

	lprintf("Process %d messages", (int)messages.size());

	for( it=messages.begin(); it != messages.end(); it++ ) {
		ptr = *it;
		code = *(char *)ptr;
		sz = (*(ptr+1) << 8) | (*(ptr+2)&0xFF);
		ptr += 3;
		if( commands[code] != NULL ) {
			lprintf("Found code %d", (int)code);
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
	unordered_map<string, FileInfo*>::iterator it;

	it = files.begin();
	if( it == files.end() ) {
		this->SendMessage( 2, 0, NULL );
		return;
	}
	u_long alloced, size;
	char *buf;

	while( it != files.end() ) {
		FileInfo *fi = (it->second);

		size = spackf( &buf, &alloced, "sLL", fi->name, fi->size, fi->mtime );
		this->SendMessage( 1, size, buf );

		it++;
	}

	this->SendMessage( 2, 0, NULL );
}

void User::GetFile( char *data, long sz )
{
	char *filename = strmem->Alloc( sz + 1 );
	memcpy( filename, data, sz );
	filename[sz] = '\0';
	GetFileS(filename);
	strmem->Free( filename, sz+1 );
}

void User::GetFileS( char *filename )
{	
	if( this->fReading || this->reading_ptr ) {
		this->reading_file_q.push( filename );
		return;
	}

	unordered_map<string, FileInfo*>::iterator it;
	it = files.find( filename );
	if( it == files.end() ) {
		this->SendMessage( 4, 0, NULL );
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
			this->SendMessage( 4, 0, NULL ); // eof
			fReading = NULL; // just to make sure
			return;
		}

		buf = strmem->Alloc( 1024 );
		// Open the file and request first chunk
		int status = fread( buf, 1, 1024, fReading );
		if( status == 0 ) {
			// File is empty, send EOF
			fclose( fReading );
			fReading = NULL;
			this->SendMessage( 4, 0, NULL );
			return;
		}

		this->SendMessage( 3, status, buf );
		strmem->Free( buf, 1024 );
	}
	reading_files = true;
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
	it = varmap.find(name);
	if( it != varmap.end() ) {
		key = (u_long)it->second;
		size = spackf(&buf, &alloced, "csl", (char)4, name, *it );
	} else {
		size = spackf(&buf, &alloced, "csl", (char)4, name, top_var_id );
		varmap[name] = top_var_id;
		key = (u_long)top_var_id;
		top_var_id++;
	}
	this->SendMessage( 0, size, buf );
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

	datamap[key] = obj;
	datamap_whichuser[key] = this;
	dirtyset.insert( key );	
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
	it = objmap.find(name);
	if( it != objmap.end() ) {
		key = (u_int)it->second;
		size = spackf(&buf, &alloced, "si", name, key );
	} else {
		size = spackf(&buf, &alloced, "si", name, top_var_id );
		objmap[name] = top_var_id;
		key = (u_int)top_var_id;
		top_var_id++;
	}
	this->SendMessage( 5, size, buf );
	strmem->Free( buf, alloced );
	strmem->Free( name, strlen(name)+1 );
}
