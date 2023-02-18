#include "main.h"

cmdcall commands[256];
unsigned int sizes[256];
using namespace std::placeholders; 

void init_commands( void )
{
	int i;
	for( i=0; i<256; i++ ) {
		commands[i] = NULL;
		sizes[i] = 0;
	}

	commands[SCmdSetKeyValue] = &User::SetKeyValue;
	sizes[SCmdSetKeyValue] = 0;
	commands[SCmdRunLuaFile] = &User::RunLuaFile;
	sizes[SCmdRunLuaFile] = 0;
	commands[SCmdRunLuaCommand] = &User::RunLuaCommand;
	sizes[SCmdRunLuaCommand] = 0;
	commands[SCmdGetFileList] = &User::GetFileList;
	sizes[SCmdGetFileList] = 0;
	commands[SCmdGetFile] = &User::GetFile;
	sizes[SCmdGetFile] = 0;
	commands[SCmdIdentifyVar] = &User::IdentifyVar;
	sizes[SCmdIdentifyVar] = 0;
	commands[SCmdSetVar] = &User::SetVar;
	sizes[SCmdSetVar] = 0;
	commands[SCmdClockSync] = &User::ClockSync;
	sizes[SCmdClockSync] = 0;
	commands[SCmdSetObjectPositionRotation] = &User::SetObjectPositionRotation;
	sizes[SCmdSetObjectPositionRotation] = 0;
	commands[SCmdRegister]= &User::Register;
	sizes[SCmdRegister] = 0;
	commands[SCmdDynPacket] = &User::DynPacket;
	sizes[SCmdDynPacket] = 0;
	commands[SCmdPacket] = &User::Packet;
	sizes[SCmdPacket] = 0;
	commands[SCmdQuit] = &User::Quit;
	sizes[SCmdQuit] = 0;
}

int top_uid = 0;

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
	x = y = z = 0;
	r0 = r1 = r2 = r3 = 0;
	scalex = scaley = scalez = 1;
	reading_ptr = NULL;
	uid = top_uid;
	top_uid++;

	snapAnimator = false;
	stopAllAbilities = false;

	anim = (Animation*)halloc(sizeof(Animation));
	new(anim) Animation();

	look = (LookSource*)halloc(sizeof(LookSource));
	new(look) LookSource();
	look->x = look->y = look->z = 0;
	look->dirx = 0;
	look->diry = 0;
	look->dirz = -1;
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

void User::Close( void )
{
	if( authority ) {
		game->PickNewAuthority();
	}
	if( fSock != -1 )
		close( fSock );
}

void User::Quit( char *data, uint16_t sz )
{
	bQuitting = true;
}
void User::SendQuit()
{
	char *buf=NULL;
	long bufsz;
	u_long alloced = 0;

	bufsz = spackf(&buf, &alloced, "i", uid);
	game->SendMsg( CCmdUserQuit, bufsz, buf, this );
	strmem->Free( buf, alloced );
}

void User::SendMsg( char cmd, unsigned int size, char *data )
{
	char *buf=NULL;
	long bufsz;
	u_long alloced = 0;

	if( sizes[cmd] == 0 ) {
		bufsz = spackf(&buf, &alloced, "cv", cmd, size, data );
	} else {
		bufsz = spackf(&buf, &alloced, "cx", cmd, size, data ); // do not include size
	}
	Output( this, buf, bufsz );
	strmem->Free( buf, alloced );
}

void User::ProcessMessages(void)
{
	vector<char*>::iterator it;
	char *ptr, *pread;
	int sz;
	unsigned char code;
	string buf;
	uint16_t x;

	//lprintf("Process %d messages", (int)messages.size());

	for( it=messages.begin(); it != messages.end(); it++ ) {
		ptr = *it;
		code = *(unsigned char *)ptr;
		if( sizes[code] == 0 ) {
			sz = (*(ptr+1) << 8) | (*(ptr+2)&0xFF);
			//lprintf("Message %d size: %d", code, sz);
		} else {
			sz = sizes[code];
			lprintf("Size set for code %d", code);
		}
		ptr += 3;
		if( commands[code] != NULL ) {
			/*
			if( code == SCmdPacket ) {
				for( pread = ptr; pread != ptr+sz; pread++ ) {
					x = *pread;
					lprintf("Packet: %d", x);
				}
			}
			*/
			std::bind( commands[code], this, _1, _2 )( ptr, sz );
		} else {
			lprintf("Unknown command code %d", (int)code);
		}
		strmem->Free( *it, sz+3 );
	}
	messages.clear();
}

void User::SetKeyValue( char *data, uint16_t sz )
{
	uint16_t key;
	char *ptr;
	Primitive *obj;
	unordered_map<uint16_t,Primitive*>::iterator it;

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

void User::RunLuaFile( char *data, uint16_t sz )
{

}

void User::RunLuaCommand( char *data, uint16_t sz )
{

}

void User::GetFileList( char *data, uint16_t sz )
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

void User::GetFile( char *data, uint16_t sz )
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



void User::ClockSync( char *data, uint16_t sz )
{
	uint64_t time, userclock;

	sunpackf(data, "L", &userclock);
	time = game->GetTime();

	this->last_update = time;
	this->clocksync = time - userclock; // measured in hours
	lprintf("Set clocksync for user %u to %llu", uid, this->clocksync);
}


// Identifies a var by type and name
void User::IdentifyVar( char *data, uint16_t sz )
{
	char *name;
	char type;

	sunpackf(data, "sc", &name, &type);
	lprintf("IdentifyVar(%s data size: %ld)", name, sz);
	game->IdentifyVar( name, (int)type, this );
	strmem->Free(name, strlen(name)+1);
}

// For setting primitives.
void User::SetVar( char *data, uint16_t sz )
{
	char *ptr;
	uint16_t objid;
	VarData *v;
	unordered_map<uint16_t,VarData*>::iterator it;
	Primitive *p;
	unordered_map<uint16_t,Primitive*>::iterator it2;

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
	}

	game->datamap_whichuser[objid] = this;
	game->dirtyset.insert( objid );
}

void User::SetObjectPositionRotation( char *data, uint16_t sz )
{
	uint16_t objid;
	float x,y,z;
	float r0,r1,r2,r3;
	int timestamp_short;
	Object *obj;
	VarData *v;
	unordered_map<uint16_t,Object*>::iterator it;

	sunpackf(data, "iifffffff", &objid, &timestamp_short, &x, &y, &z, &r0, &r1, &r2, &r3);

	it = game->objects.find( objid );
	if( it == game->objects.end() ) {
		lprintf("SetObjectPositionRotation:: Not Found %d!", objid);
		return;
	}

	obj = it->second;
	obj->x = x;
	obj->y = y;
	obj->z = z;
	obj->r0 = r0;
	obj->r1 = r1;
	obj->r2 = r2;
	obj->r3 = r3;
	obj->last_update = this->last_update + (uint64_t)timestamp_short;

	char *buf;
	u_long alloced = 0;
	long size;

	timestamp_short = (int)(obj->last_update - game->last_timestamp);

	size = spackf(&buf, &alloced, "lifffffff", objid, timestamp_short, x, y, z, r0, r1, r2, r3);

	game->SendMsg( CCmdSetObjectPositionRotation, size, buf, this );
	strmem->Free( buf, alloced );
	//lprintf("Updated %llu: %f %f %f rotation set to %f %f %f %f", objid, x, y, z, r0, r1, r2, r3);
}

//Register: Tells this user where all other users are.
// Also registers the target user with all other users.
// All user data should be loaded before this is called.
void User::Register( char *data, uint16_t sz )
{
	char *buf;
	long size;
	u_long alloced = 0;
	User *otheruser;
	unordered_map<uint16_t,User*>::iterator ituser;

	sunpackf(data, "ffffff", &this->x, &this->y, &this->z, &this->r0, &this->r1, &this->r2);
	

	size = spackf(&buf, &alloced, "ci", this->authority?1:0, this->uid);
	SendMsg( CCmdRegisterUser, size, buf );
	strmem->Free( buf, alloced );
	lprintf("Register: Found user %u at %f %f %f", this->uid, this->x, this->y, this->z);

	ituser = game->usermap.begin();
	while( ituser != game->usermap.end() ) {
		otheruser = ituser->second;
		if( otheruser != this )
			otheruser->SendTo(this);
		ituser++;
	}

	this->SendTo(NULL); // sends to all
}

void User::SendTo( User *otheruser )
{
	// generate all the needed packets and send to otheruser/all.
	char *buf, *buf2, **buffers;
	u_long alloced = 0, *alloceds;
	long size, *sizes, totalsize;
	uint16_t timestamp_short;
	AnimParam ap, &apptr=ap;

	lprintf("Send user %u to %u", this->uid, otheruser?otheruser->uid:0);
	timestamp_short = (int)(this->last_update - game->last_timestamp); // maybe we should use the current time instead. This is the time since the last update.

// newuser packet
	size = spackf(&buf, &alloced, "iffffff", uid, px, py, pz, r0, r1, r2);
	if( !otheruser ) {
		game->SendMsg( CCmdUser, size, buf, this );
	} else {
		otheruser->SendMsg( CCmdUser, size, buf );
	}
	strmem->Free( buf, alloced );
	buf = NULL; alloced = 0;

// position + rotation
	size = spackf(&buf, &alloced, "iiiffffffcc", CNetSetPositionAndRotation, uid, timestamp_short,
			x, y, z, r0, r1, r2, true, true);
	if( !otheruser )
		game->SendMsg( CCmdPacket, size, buf, this );
	else
		otheruser->SendMsg( CCmdPacket, size, buf );
	strmem->Free( buf, alloced );
	buf=NULL; alloced = 0;

//! inventory


// transform
	char dirtyfull = 255;
	char dirtypart = TransformPosition|TransformRotation|TransformScale;
	size = spackf(&buf, &alloced, "iiiicFFFFFFFFFFFF", CNetTransform, uid, timestamp_short, 25, dirtypart,
		1000.0, px, 1000.0, py, 1000.0, pz,
		1000.0, vx, 1000.0, vy, 1000.0, vz,
		1000.0, pr0, 1000.0, pr1, 1000.0, pr2,
		1000.0, scalex, 1000.0, scaley, 1000.0, scalez);
	if( !otheruser )
		game->SendMsg( CCmdDynPacket, size, buf, this );
	else
		otheruser->SendMsg( CCmdDynPacket, size, buf );
	strmem->Free( buf, alloced );
	buf=NULL; alloced = 0;

// look source
	size = spackf(&buf, &alloced, "iiiicFFFFFFFF", CNetPlayerLook, uid, timestamp_short, 17, dirtyfull,
		1000.0, look->distance, 1000.0, look->pitch,
		1000.0, look->x, 1000.0, look->y, 1000.0, look->z,
		1000.0, look->dirx, 1000.0, look->diry, 1000.0, look->dirz);
	if( !otheruser )
		game->SendMsg( CCmdDynPacket, size, buf, this );
	else
		otheruser->SendMsg( CCmdDynPacket, size, buf );
	strmem->Free( buf, alloced );
	buf=NULL; alloced = 0;

/* animation
	size = spackf(&buf, &alloced, "iiiiFFFFFicciiiF", CNetInitAnimation, uid, timestamp_short, 22,
			1000.0, anim->x, 1000.0, anim->z, 1000.0, anim->pitch, 1000.0, anim->yaw, 1000.0, anim->speed,
			anim->height, anim->moving, anim->aiming,
			anim->moveSetID, anim->abilityIndex, anim->abilityInt,
			1000.0, anim->abilityFloat);
	if( !otheruser )
		game->SendMsg( CCmdDynPacket, size, buf, this );
	else
		otheruser->SendMsg( CCmdDynPacket, size, buf );
	strmem->Free( buf, alloced );
	buf=NULL; alloced = 0;
*/

/* animation parameters
	for( int i=0; i<anim->params.size(); i++ ) {
		apptr = anim->params[i];
		size = spackf(&buf, &alloced, "iiiiiii", CNetInitItemAnimation, uid, timestamp_short, i, apptr.itemid, apptr.stateindex, apptr.substateindex);
		if( !otheruser )
			game->SendMsg( CCmdPacket, size, buf, this );
		else
			otheruser->SendMsg( CCmdPacket, size, buf );
		strmem->Free( buf, alloced );
		buf=NULL; alloced = 0;
	} */
}

void User::DynPacket( char *data, uint16_t sz )
{
	char *buf, *ptr, *ptr1;
	long size;
	u_long alloced=0;
	int timestamp_short;
	uint16_t cmd, objtgt;
	uint16_t dirtyflags;
	uint64_t this_update;
	float x,y,z;
	float rx,ry,rz,rw;
	int i;
	int dynlen;

	ptr1 = ptr = sunpackf(data, "iiii", &cmd, &objtgt, &timestamp_short, &dynlen);
	//lprintf("Got dyn packet: %u %u %u %d", cmd, objtgt, timestamp_short, dynlen);

	this_update = this->last_update + (uint64_t)timestamp_short;
	timestamp_short = (int)(this_update - game->last_timestamp);

	// animation:
	float pitch, yaw, speed;
	int height;
	char moving, aiming;
	int moveSetID, abilityIndex, abilityInt;
	float abilityFloat;
	uint16_t itemflags;
	unsigned char dirtybyte;
	int itemid, stateindex, substateindex;
	Animation *anim;
	AnimParam animparam, &apptr=animparam;

	// look source:
	switch( cmd ) {
		/* do not save animations I guess
		case CNetAnimation:
			ptr = sunpackf(ptr, "i", &dirtyflags);
			if( (dirtyflags&ParamX) != 0 ) {
				ptr = sunpackf(ptr, "F", 1000.0, &anim->x);
			}
			if( (dirtyflags&ParamZ) != 0 ) {
				ptr = sunpackf(ptr, "F", 1000.0, &anim->z);
			}
			if( (dirtyflags&ParamPitch) != 0) {
				ptr = sunpackf(ptr, "F", 1000.0, &anim->pitch);
			}
			if( (dirtyflags&ParamYaw) != 0) {
				ptr = sunpackf(ptr, "F", 1000.0, &anim->yaw);
			}
			if( (dirtyflags&ParamSpeed) != 0) {
				ptr = sunpackf(ptr, "F", 1000.0, &anim->speed);
			}
			if( (dirtyflags&ParamHeight) != 0) {
				ptr = sunpackf(ptr, "i", &anim->height);
			}
			if( (dirtyflags&ParamMoving) != 0) {
				ptr = sunpackf(ptr, "c", &anim->moving);
			}
			if( (dirtyflags&ParamAiming) != 0) {
				ptr = sunpackf(ptr, "c", &anim->aiming);
			}
			if( (dirtyflags&ParamMoveSet) != 0) {
				ptr = sunpackf(ptr, "i", &anim->moveSetID);
			}
			if( (dirtyflags&ParamAbility) != 0) {
				ptr = sunpackf(ptr, "i", &anim->abilityIndex);
			}
			if( (dirtyflags&ParamAbilityInt) != 0) {
				ptr = sunpackf(ptr, "i", &anim->abilityInt);
			}
			if( (dirtyflags&ParamAbilityFloat) != 0) {
				ptr = sunpackf(ptr, "F", 1000.0, &anim->abilityFloat);
			}
			ptr = sunpackf(ptr, "i", &itemflags);
			for( i=0; i<16; i++ ){
				if( itemflags&(1<<i) == 0 ) {
					continue;
				}
				animparam.itemid = animparam.stateindex = animparam.substateindex = 0;
				while( i >= anim->params.size() ) {
					anim->params.push_back( animparam );
				}

				apptr = anim->params[ i ];
				ptr = sunpackf(ptr, "iii", &apptr.itemid, &apptr.stateindex, &apptr.substateindex);
			}
			break;*/
		case CNetPlayerLook:
			ptr = sunpackf(ptr, "c", &dirtybyte);
			if( (dirtybyte&LookDistance) != 0 ) {
				ptr = sunpackf(ptr, "F", 1000.0, &look->distance);
			}

			if( (dirtybyte&LookPitch) != 0 ) {
				ptr = sunpackf(ptr, "F", 1000.0, &look->pitch);
			}

			if( (dirtybyte&LookPosition) != 0 ) {
				ptr = sunpackf(ptr, "FFF", 1000.0, &look->x, 1000.0, &look->y, 1000.0, &look->z);
			}

			if( (dirtybyte&LookDirection) != 0 ) {
				ptr = sunpackf(ptr, "FFF", 1000.0, &look->dirx, 1000.0, &look->diry, 1000.0, &look->dirz);
				lprintf("New look dir: %f %f %f", look->dirx, look->diry, look->dirz);
			}
			break;
		case CNetTransform:
			ptr = sunpackf(ptr, "c", &dirtybyte);
			if( (dirtybyte&TransformPlatform) != 0 ) {
				hasplatform = true;
				ptr = sunpackf(ptr, "i", &platid);
				if( (dirtybyte&TransformPosition) != 0 ) {
					ptr = sunpackf(ptr, "FFF", 1000.0, &px, 1000.0, &py, 1000.0, &pz);
					vx = vy = vz = 0;
				}
				if( (dirtybyte&TransformRotation) != 0 ) {
					ptr = sunpackf(ptr, "FFF", 1000.0, &pr0, 1000.0, &pr1, 1000.0, &pr2);
				}
			} else {
				hasplatform = false;
				platid = 0;
				if( (dirtybyte&TransformPosition) != 0 ) {
					ptr = sunpackf(ptr, "FFFFFF", 1000.0, &px, 1000.0, &py, 1000.0, &pz, 1000.0, &vx, 1000.0, &vy, 1000.0, &vz);
				}
				if( (dirtybyte&TransformRotation) != 0 ) {
					ptr = sunpackf(ptr, "FFF", 1000.0, &pr0, 1000.0, &pr1, 1000.0, &pr2);
				}
			}
			if( (dirtybyte&TransformScale) != 0 ) {
				ptr = sunpackf(ptr, "FFF", 1000.0, &scalex, 1000.0, &scaley, 1000.0, &scalez);
			}
			break;
	}

	size = spackf(&buf, &alloced, "iiii", cmd, objtgt, timestamp_short, dynlen);
	char *buf2 = strmem->Alloc( sz );
	memcpy( buf2, buf, size );
	memcpy( buf2+size, ptr1, sz-size );

	game->SendMsg( CCmdDynPacket, sz, buf2, this );
	strmem->Free( buf2, sz );
	strmem->Free( buf, alloced );
}

void User::Packet( char *data, uint16_t sz )
{
	char *buf, *ptr;
	long size;
	u_long alloced=0;
	uint16_t timestamp_short;
	uint16_t cmd, objtgt;
	uint64_t this_update;
	User *otheruser;
	float x,y,z;
	float r0,r1,r2;

	ptr = sunpackf(data, "iii", &cmd, &objtgt, &timestamp_short);
	//lprintf("Got packet: %u %u %u", cmd, objtgt, timestamp_short);

	this_update = this->last_update + (uint64_t)timestamp_short;

	timestamp_short = (int)(this_update - game->last_timestamp);

	size = spackf(&buf, &alloced, "iii", cmd, objtgt, timestamp_short);
	char *buf2 = strmem->Alloc( sz );
	memcpy( buf2, buf, size );
	memcpy( buf2+size, ptr, sz-size );

	game->SendMsg( CCmdPacket, sz, buf2, this );
	strmem->Free( buf2, sz );
	strmem->Free( buf, alloced );

	switch( cmd ) {
		case CNetSetPositionAndRotation:
			lprintf("read CNetSetPositionAndRotation");
			otheruser = game->usermap[objtgt];
			if( otheruser ) {
				sunpackf(ptr, "ffffffcc", &x, &y, &z, &r0, &r1, &r2, &otheruser->snapAnimator, &otheruser->stopAllAbilities);
				otheruser->x = x;
				otheruser->y = y;
				otheruser->z = z;
				otheruser->r0 = r0;
				otheruser->r1 = r1;
				otheruser->r2 = r2;
				lprintf("Set user %d position and rotation to %f %f %f %f %f %f", objtgt, x, y, z, r0, r1, r2);
				otheruser->last_update = this_update;
			}
			break;
		case CNetSetPosition:
			lprintf("read CNetSetPosition");
			otheruser = game->usermap[objtgt];
			if( otheruser ) {
				sunpackf(ptr, "fff", &x, &y, &z);
				otheruser->x = x;
				otheruser->y = y;
				otheruser->z = z;
				otheruser->last_update = this_update;
			}
			break;
		case CNetSetRotation:
			lprintf("read CNetSetRotation");
			otheruser = game->usermap[objtgt];
			if( otheruser ) {
				sunpackf(ptr, "fff", &r0, &r1, &r2);
				otheruser->r0 = r0;
				otheruser->r1 = r1;
				otheruser->r2 = r2;
			}
			break;
	}
}
