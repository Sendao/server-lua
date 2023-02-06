#include "main.h"

cmdcall commands[256];
using namespace std::placeholders; 

void init_commands( void )
{
	User *p=NULL;
	int i;
	for( i=0; i<256; i++ )
		commands[i] = NULL;

	commands[SCmdSetKeyValue] = &User::SetKeyValue;
	commands[SCmdRunLuaFile] = &User::RunLuaFile;
	commands[SCmdRunLuaCommand] = &User::RunLuaCommand;
	commands[SCmdGetFileList] = &User::GetFileList;
	commands[SCmdGetFile] = &User::GetFile;
	commands[SCmdIdentifyVar] = &User::IdentifyVar;
	commands[SCmdSetVar] = &User::SetVar;
	commands[SCmdClockSync] = &User::ClockSync;
	commands[SCmdCreateObject] = &User::CreateObject;
	commands[SCmdSetObjectPositionRotation] = &User::SetObjectPositionRotation;
	commands[SCmdRegister]= &User::Register;
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
	if( outbuf_memory )
		strmem->Free( outbuf_memory, outbufalloc );
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
	Primitive *obj;
	unordered_map<u_long,Primitive*>::iterator it;

	ptr = sunpackf(data, "l", &key);
	it = game->datamap.find(key);
	if( it != game->datamap.end() ) {
		lprintf("SetKeyValue: Not found!");
		return;
	}
	obj = it->second;
	switch( obj->type ) {
		case 0: // char
			sunpackf(ptr, "c", &obj->data.c);
			break;
		case 1: // int
			sunpackf(ptr, "i", &obj->data.i);
			break;
		case 2: // float
			sunpackf(ptr, "f", &obj->data.f);
			break;
		case 3: // string
			sunpackf(ptr, "s", &obj->data.s);
			break;
		case 4: // buffer (binary string)
			sunpackf(ptr, "p", &obj->data.p);
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
		this->SendMsg( CCmdEndOfFileList, 0, NULL );
		return;
	}
	u_long alloced, size;
	char *buf;

	while( it != game->files.end() ) {
		FileInfo *fi = (it->second);

		size = spackf( &buf, &alloced, "sLL", fi->name, fi->size, fi->mtime );
		this->SendMsg( CCmdFileInfo, size, buf );
		strmem->Free( buf, alloced );

		it++;
	}

	this->SendMsg( CCmdEndOfFileList, 0, NULL );
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
		this->SendMsg( CCmdNextFile, 0, NULL );
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
		this->SendMsg( CCmdNextFile, 0, NULL );
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
			this->SendMsg( CCmdNextFile, 0, NULL ); // eof
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
			this->SendMsg( CCmdNextFile, 0, NULL );
			return;
		}

		this->SendMsg( CCmdFileData, status, buf );
		strmem->Free( buf, 1024 );
	}
	game->reading_files = true;
}



void User::ClockSync( char *data, long sz )
{
	long long time, userclock;

	sunpackf(data, "L", &userclock);
	time = game->GetTime();

	this->last_update = time;
	this->clocksync = time - userclock; // measured in hours
	lprintf("Set clocksync for user to %llu", this->clocksync);
}



void User::IdentifyVar( char *data, long sz )
{
	char *name;
	int type;

	sunpackf(data, "sc", &name, type);
	game->IdentifyVar( name, type, this );

	//! Send any additional data about the object?

}

void User::SetVar( char *data, long sz )
{
	char *ptr;
	u_long objid;
	VarData *v;
	unordered_map<u_long,VarData*>::iterator it;
	Primitive *p;
	unordered_map<u_long,Primitive*>::iterator it2;

	ptr = sunpackf(data, "l", &objid);
	it = game->varmap_by_id.find(objid);
	if( it == game->varmap_by_id.end() ) { // not found
		lprintf("SetVar:: Not Found!");
		return;
	}

	v = it->second;

	it2 = game->datamap.find(objid);
	if( it2 == game->datamap.end() ) { // not found
		p = (Primitive*)halloc(sizeof(Primitive));
		p->type = v->type;
		game->datamap[objid] = p;
	} else {
		p = it2->second;
	}

	switch( p->type ) {
		case 0: // char
			sunpackf(ptr, "c", &p->data.c);
			break;
		case 1: // int
			sunpackf(ptr, "i", &p->data.i);
			break;
		case 2: // float
			sunpackf(ptr, "f", &p->data.f);
			break;
		case 3: // string
			sunpackf(ptr, "s", &p->data.s);
			break;
			/*
		case 4: // buffer (binary string) -- needs length
			sunpackf(ptr, "p", /obj.data.plen/, &obj.data.p);
			break;
			*/
	}

	game->datamap_whichuser[objid] = this;
	game->dirtyset.insert( objid );
}


void User::CreateObject( char *data, long sz )
{
	u_long objid;
	char *name;
	float x,y,z;
	float r0,r1,r2;
	int timestamp_short;
	Object *obj;
	VarData *v;

	sunpackf(data, "isffffff", &timestamp_short, &name, &x, &y, &z, &r0, &r1, &r2);

	// make sure the name does not exist.
	unordered_map<string,VarData*>::iterator it;
	it = game->varmap.find( name );

	if( it != game->varmap.end() ) {
		lprintf("CreateObject:: Name already exists!");
		return;
	}

	v = (VarData*)halloc(sizeof(VarData));
	v->type = 0;
	v->objid = game->top_var_id;
	game->varmap[name] = v;
	game->varmap_by_id[game->top_var_id] = v;
	game->top_var_id++;

	obj = (Object*)halloc(sizeof(Object));
	obj->uid = v->objid;
	obj->name = name;
	obj->x = x;
	obj->y = y;
	obj->z = z;
	obj->r0 = r0;
	obj->r1 = r1;
	obj->r2 = r2;
	obj->last_update = this->last_update + (long long)timestamp_short;
	game->objects[obj->uid] = obj;

	lprintf("Created %llu", objid);

	char *buf;
	u_long alloced;
	long size;

	timestamp_short = (int)(obj->last_update - game->last_timestamp);

	size = spackf(&buf, &alloced, "lifffffff", objid, timestamp_short, x, y, z, r0, r1, r2);
	game->SendMsg( CCmdSetObjectPositionRotation, size, buf );
	strmem->Free( buf, alloced );
}


void User::SetObjectPositionRotation( char *data, long sz )
{
	u_long objid;
	float x,y,z;
	float r0,r1,r2;
	int timestamp_short;
	Object *obj;
	VarData *v;
	unordered_map<u_long,Object*>::iterator it;

	sunpackf(data, "liffffff", &objid, &timestamp_short, &x, &y, &z, &r0, &r1, &r2);

	it = game->objects.find( objid );
	if( it == game->objects.end() ) {
		lprintf("SetObjectPositionRotation:: Not Found!");
		return;
	}

	obj = it->second;
	obj->x = x;
	obj->y = y;
	obj->z = z;
	obj->r0 = r0;
	obj->r1 = r1;
	obj->r2 = r2;
	obj->last_update = this->last_update + (long long)timestamp_short;

	lprintf("Update %llu", objid);

	char *buf;
	u_long alloced;
	long size;

	timestamp_short = (int)(obj->last_update - game->last_timestamp);

	size = spackf(&buf, &alloced, "liffffff", objid, timestamp_short, x, y, z, r0, r1, r2);
	game->SendMsg( CCmdSetObjectPositionRotation, size, buf );
	strmem->Free( buf, alloced );
}

void User::Register( char *data, long sz )
{
	char *buf;
	long size;
	u_long alloced;

	size = spackf(&buf, &alloced, "c", this->authority?1:0);
	SendMsg( CCmdRegisterUser, size, buf );
	strmem->Free( buf, alloced );
}
