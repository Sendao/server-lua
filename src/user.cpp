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
	commands[SCmdPacketTo] = &User::PacketTo;
	sizes[SCmdPacketTo] = 0;
	commands[SCmdDynPacketTo] = &User::DynPacketTo;
	sizes[SCmdDynPacketTo] = 0;
	commands[SCmdActivateLua] = &User::ActivateLua;
	sizes[SCmdActivateLua] = 0;
	commands[SCmdEchoRTT] = &User::Echo;
	sizes[SCmdEchoRTT] = 0;
	commands[SCmdObjectTop] = &User::ObjectTop;
	sizes[SCmdObjectTop] = 0;
	commands[SCmdObjectClaim] = &User::ObjectClaim;
	sizes[SCmdObjectClaim] = 0;
	commands[SCmdSpawn] = &User::Spawn;
	sizes[SCmdSpawn] = 0;
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
	x = y = z = 0;
	r0 = r1 = r2 = r3 = 0;
	scalex = scaley = scalez = 1;
	reading_ptr = NULL;
	uid = game->top_var_id;
	game->top_var_id++;

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



void User::InitialiseAPITable(void)
{
	sol::usertype<User> user_type = lua.new_usertype<User>("User",
		sol::constructors<User()>());
	
	user_type["outbufmax"] = &User::outbufmax;
	user_type["sHost"] = &User::sHost;
	user_type["uid"] = &User::uid;
	user_type["x"] = &User::px;
	user_type["y"] = &User::py;
	user_type["z"] = &User::pz;
	user_type["vx"] = &User::vx;
	user_type["vy"] = &User::vy;
	user_type["vz"] = &User::vz;
	user_type["r0"] = &User::pr0;
	user_type["r1"] = &User::pr1;
	user_type["r2"] = &User::pr2;
	user_type["scalex"] = &User::scalex;
	user_type["scaley"] = &User::scaley;
	user_type["scalez"] = &User::scalez;

	user_type["SendQuit"] = &User::SendQuit;
	user_type["SendMsg"] = &User::SendMsg;
	user_type["SendTo"] = &User::SendTo;
}


void User::Close( void )
{
	if( authority ) {
		game->PickNewAuthority(this);
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
		lprintf("watch out using this");
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
	int64_t synctime;

	sunpackf(data, "M", &userclock);
	time = game->GetTime();

	this->last_update = userclock;
	synctime = (int64_t)time - (int64_t)userclock; // measured in hours of milliseconds, so around   86400000

	this->clocksync_readings.push_back( synctime );
	if( this->clocksync_readings.size() > 60 ) {
		this->clocksync_readings.erase( this->clocksync_readings.begin() );
	}

	// get the average from the clocksync_readings vector
	int64_t sum = 0;
	for( int i = 0; i < this->clocksync_readings.size(); i++ ) {
		sum += this->clocksync_readings.at(i);
	}
	this->clocksync = sum / this->clocksync_readings.size();

	//lprintf("Set clocksync for user %u, %llu - %llu, to %lld", uid, time, userclock, this->clocksync);
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

void User::ActivateLua( char *data, uint16_t sz )
{
	uint16_t objid;
	Object *obj;
	unordered_map<uint16_t,Object*>::iterator it;

	sunpackf(data, "u", &objid);
	it = game->objects.find( objid );
	if( it == game->objects.end() ) {
		lprintf("ActivateLua:: Not Found %u!", objid);
		return;
	}

	LuaActivate( it->second, ServerEvent::Activate );
}

void User::SetObjectPositionRotation( char *data, uint16_t sz )
{
	uint16_t objid;
	float x,y,z;
	float r0,r1,r2;
	uint16_t timestamp_short;
	Object *obj;
	VarData *v;
	unordered_map<uint16_t,Object*>::iterator it;

	sunpackf(data, "uuffffff", &objid, &timestamp_short, &x, &y, &z, &r0, &r1, &r2);

	it = game->objects.find( objid );
	if( it == game->objects.end() ) {
		obj = (Object*)halloc(sizeof(Object));
		obj->uid = objid;
		game->objects[objid] = obj;
	} else {
		obj = it->second;
	}

	obj->x = x;
	obj->y = y;
	obj->z = z;
	obj->r0 = r0;
	obj->r1 = r1;
	obj->r2 = r2;
	obj->last_update = (uint64_t)( this->last_update + timestamp_short + this->clocksync );

	char *buf;
	u_long alloced = 0;
	long size;

	timestamp_short = (int)(obj->last_update - game->last_timestamp);

	size = spackf(&buf, &alloced, "uuffffff", objid, timestamp_short, x, y, z, r0, r1, r2);

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
	unsigned char type;
	u_long alloced = 0;
	User *otheruser;
	Object *otherobj;
	Npc *othernpc;
	unordered_map<uint16_t,User*>::iterator ituser;
	unordered_map<uint16_t,Object*>::iterator itobj;
	unordered_map<uint16_t,Npc*>::iterator itnpc;

	sunpackf(data, "ffffff", &this->x, &this->y, &this->z, &this->r0, &this->r1, &this->r2);
	

	size = spackf(&buf, &alloced, "ci", this->authority?1:0, this->uid);
	SendMsg( CCmdRegisterUser, size, buf );
	strmem->Free( buf, alloced ); alloced = 0;
	lprintf("Register: Found user %u at %f %f %f", this->uid, this->x, this->y, this->z);

	ituser = game->usermap.begin();
	while( ituser != game->usermap.end() ) {
		otheruser = ituser->second;
		if( otheruser != this )
			otheruser->SendTo(this);
		ituser++;
	}

	itobj = game->objects.begin();
	type = 0;
	while( itobj != game->objects.end() ) {
		otherobj = itobj->second;
		if( otherobj->spawned ) {
			size = spackf(&buf, &alloced, "bufffffffff", type, otherobj->uid, otherobj->x, otherobj->y, otherobj->z, otherobj->r0, otherobj->r1, otherobj->r2, otherobj->scalex, otherobj->scaley, otherobj->scalez);
			SendMsg( CCmdSpawn, size, buf );
			strmem->Free( buf, alloced );
			alloced = 0;
		}
		itobj++;
	}

	itnpc = game->npcs.begin();
	type = 1;
	while( itnpc != game->npcs.end() ) {
		othernpc = itnpc->second;
		size = spackf(&buf, &alloced, "bufffffffff", type, othernpc->uid, othernpc->x, othernpc->y, othernpc->z, othernpc->r0, othernpc->r1, othernpc->r2, othernpc->scalex, othernpc->scaley, othernpc->scalez);
		SendMsg( CCmdSpawn, size, buf );
		strmem->Free( buf, alloced );
		alloced = 0;
		itnpc++;
	}

	this->SendTo(NULL); // sends me to all

	vector<sol::function> &funcs = LuaEvent( ServerEvent::Login );
	for( vector<sol::function>::iterator it = funcs.begin(); it != funcs.end(); it++ ) {
		sol::function &f = *it;
		f( this );
	}
}

void User::SendTo( User *otheruser )
{
	// generate all the needed packets and send to otheruser/all.
	char *buf, *buf2, **buffers;
	u_long alloced = 0, *alloceds;
	long size, *sizes, totalsize;
	uint16_t timestamp_short;
	AnimParam ap, &apptr=ap;

	lprintf("Send user %u to %u (%f %f %f)", this->uid, otheruser?otheruser->uid:99, x, y, z);
	timestamp_short = (int)(this->last_update - (game->last_timestamp - this->clocksync));

// newuser packet
	size = spackf(&buf, &alloced, "uffffff", uid, x, y, z, r0, r1, r2);
	if( !otheruser ) {
		game->SendMsg( CCmdUser, size, buf, this );
	} else {
		otheruser->SendMsg( CCmdUser, size, buf );
	}
	strmem->Free( buf, alloced );
	buf = NULL; alloced = 0;

// position + rotation
/*
	size = spackf(&buf, &alloced, "uuuffffffcc", CNetSetPositionAndRotation, uid, timestamp_short,
			x, y, z, r0, r1, r2, true, true);
	if( !otheruser )
		game->SendMsg( CCmdPacket, size, buf, this );
	else
		otheruser->SendMsg( CCmdPacket, size, buf );
	strmem->Free( buf, alloced );
	buf=NULL; alloced = 0;
*/
//! inventory


// transform
	char dirtyfull = 255;
	char dirtypart = TransformPosition|TransformRotation|TransformScale;
	size = spackf(&buf, &alloced, "uuuicffffffffffff", CNetTransform, uid, timestamp_short, 49, dirtypart,
		px, py, pz,
		vx, vy, vz,
		pr0, pr1, pr2,
		scalex, scaley, scalez);
	if( !otheruser )
		game->SendMsg( CCmdDynPacket, size, buf, this );
	else
		otheruser->SendMsg( CCmdDynPacket, size, buf );
	strmem->Free( buf, alloced );
	buf=NULL; alloced = 0;

// look source
	size = spackf(&buf, &alloced, "uuuicFFFFFFFF", CNetPlayerLook, uid, timestamp_short, 17, dirtyfull,
		1000.0, look->distance, 1000.0, look->pitch,
		1000.0, look->x, 1000.0, look->y, 1000.0, look->z,
		1000.0, look->dirx, 1000.0, look->diry, 1000.0, look->dirz);
	lprintf("Sending lookdir %f %f %f to user %u", look->dirx, look->diry, look->dirz, otheruser? otheruser->uid : 99);
	if( !otheruser )
		game->SendMsg( CCmdDynPacket, size, buf, this );
	else
		otheruser->SendMsg( CCmdDynPacket, size, buf );
	strmem->Free( buf, alloced );
	buf=NULL; alloced = 0;

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
	unordered_map<uint16_t, Object*>::iterator objit;
	Object *target;
	vector<sol::function> funcs;

	ptr1 = ptr = sunpackf(data, "uuui", &cmd, &objtgt, &timestamp_short, &dynlen);
	//lprintf("Got dyn packet: %u %u %u %d", cmd, objtgt, timestamp_short, dynlen);

	this_update = this->C2SL( timestamp_short );
	timestamp_short = (int)(this_update - game->last_timestamp);

	char dirtybyte;

	// look source:
	switch( cmd ) {
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
				if( look->dirx == 0 && look->diry == 0 && look->dirz == 0 ) {
					lprintf("Lookdir was all zero");
				}
			}
			break;
		case CNetTransform:
			ptr = sunpackf(ptr, "c", &dirtybyte);
			if( (dirtybyte&TransformPlatform) != 0 ) {
				hasplatform = true;
				ptr = sunpackf(ptr, "i", &platid);
		    	/*
				if( (dirtybyte&TransformPosition) != 0 ) {
					ptr = sunpackf(ptr, "fff", &px, &py, &pz);
					vx = vy = vz = 0;
				}
				if( (dirtybyte&TransformRotation) != 0 ) {
					ptr = sunpackf(ptr, "fff", &pr0, &pr1, &pr2);
				}
				*/
			} else {
				hasplatform = false;
				platid = 0;
				if( (dirtybyte&TransformPosition) != 0 ) {
					ptr = sunpackf(ptr, "ffffff", &px, &py, &pz, &vx, &vy, &vz);
				}
				if( (dirtybyte&TransformRotation) != 0 ) {
					ptr = sunpackf(ptr, "fff", &pr0, &pr1, &pr2);
				}
			}
			if( (dirtybyte&TransformScale) != 0 ) {
				ptr = sunpackf(ptr, "fff", &scalex, &scaley, &scalez);
			}
			funcs = LuaUserEvent( this, ServerEvent::Move );
			for( vector<sol::function>::iterator it = funcs.begin(); it != funcs.end(); it++ ) {
				sol::function &f = *it;
				f( this );
			}
			break;
		case CNetObjTransform:
			objit = game->objects.find(objtgt);
			if( objit == game->objects.end() ) {
				target = (Object*)halloc(sizeof(Object));
				new(target) Object();

				target->uid = objtgt;
				game->objects[objtgt] = target;
			} else {
				target = objit->second;
			}
			ptr = sunpackf(ptr, "b", &dirtybyte);
			if( (dirtybyte&TransformPosition) != 0 ) {
				ptr = sunpackf(ptr, "fff", &target->x, &target->y, &target->z);
			}
			if( (dirtybyte&TransformRotation) != 0 ) {
				ptr = sunpackf(ptr, "fff", &target->r0, &target->r1, &target->r2);
			}
			if( (dirtybyte&TransformScale) != 0 ) {
				ptr = sunpackf(ptr, "fff", &target->scalex, &target->scaley, &target->scalez);
			}
			
			funcs = LuaObjEvent( target, ServerEvent::Move );
			for( vector<sol::function>::iterator it = funcs.begin(); it != funcs.end(); it++ ) {
				sol::function &f = *it;
				f( target );
			}
			
			break;
	}

	size = spackf(&buf, &alloced, "uuui", cmd, objtgt, timestamp_short, dynlen);
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

	ptr = sunpackf(data, "uuu", &cmd, &objtgt, &timestamp_short);
	//lprintf("Got packet: %u %u %u", cmd, objtgt, timestamp_short);

	this_update = this->C2SL( timestamp_short );

	timestamp_short = (int)(this_update - game->last_timestamp);

	size = spackf(&buf, &alloced, "uuu", cmd, objtgt, timestamp_short);
	char *buf2 = strmem->Alloc( sz );
	memcpy( buf2, buf, size );
	memcpy( buf2+size, ptr, sz-size );

	game->SendMsg( CCmdPacket, sz, buf2, this );
	strmem->Free( buf2, sz );
	strmem->Free( buf, alloced );

	switch( cmd ) {
		case CNetSetPositionAndRotation:
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
			}
			break;
		case CNetSetPosition:
			otheruser = game->usermap[objtgt];
			if( otheruser ) {
				sunpackf(ptr, "fff", &x, &y, &z);
				otheruser->x = x;
				otheruser->y = y;
				otheruser->z = z;
			}
			break;
		case CNetSetRotation:
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

void User::DynPacketTo( char *data, uint16_t sz )
{
	char *buf, *ptr, *ptr1;
	long size;
	u_long alloced=0;
	int timestamp_short;
	uint16_t cmd, objtgt;
	uint16_t dirtyflags, cmdto;
	uint64_t this_update;
	float x,y,z;
	float rx,ry,rz,rw;
	int i;
	int dynlen;

	ptr1 = ptr = sunpackf(data, "uuuui", &cmd, &cmdto, &objtgt, &timestamp_short, &dynlen);
	//lprintf("Got dyn packet: %u %u %u %d", cmd, objtgt, timestamp_short, dynlen);

	this_update = this->C2SL( timestamp_short );
	timestamp_short = (int)(this_update - game->last_timestamp);

	size = spackf(&buf, &alloced, "uuui", cmd, objtgt, timestamp_short, dynlen);
	char *buf2 = strmem->Alloc( sz-2 );
	memcpy( buf2, buf, size );
	memcpy( buf2+size, ptr1, (sz-2)-size );

	unordered_map<uint16_t, User*>::iterator it = game->usermap.find(cmdto);
	if( it != game->usermap.end() ) {
		it->second->SendMsg( CCmdDynPacket, sz-2, buf2 );
	}

	strmem->Free( buf2, sz-2 );
	strmem->Free( buf, alloced );
}

uint64_t User::C2SL( uint16_t ts_short )
{
	uint64_t this_update = this->last_update + (uint64_t)ts_short + this->clocksync;
	int64_t diff1 = game->last_update - this_update;
	int32_t diff = (int32_t)abs(diff1);
	this->c2sl_readings.push_back( diff );
	if( this->c2sl_readings.size() > 10 ) {
		this->c2sl_readings.erase( this->c2sl_readings.begin() );
	}
	int32_t sum = 0;
	int count = 0;
	for( vector<int32_t>::iterator it = this->c2sl_readings.begin(); it != this->c2sl_readings.end(); it++ ) {
		sum += *it;
		if( *it != 0 ) count++;
	}
	if( count == 0 )
		this->c2sl = 0;
	else
		this->c2sl = sum / (int32_t)count;

	return this_update;
}

void User::PacketTo( char *data, uint16_t sz )
{
	char *buf, *ptr;
	long size;
	uint16_t cmdto;
	u_long alloced=0;
	uint16_t timestamp_short;
	uint16_t cmd, objtgt;
	uint64_t this_update;
	unordered_map<uint16_t, User*>::iterator it;
	User *otheruser;
	float x,y,z;
	float r0,r1,r2;

	ptr = sunpackf(data, "uuuu", &cmd, &cmdto, &objtgt, &timestamp_short);
	//lprintf("Got packet: %u %u %u", cmd, objtgt, timestamp_short);

	this_update = this->C2SL( timestamp_short );

	timestamp_short = (int)(this_update - game->last_timestamp);

	size = spackf(&buf, &alloced, "uuu", cmd, objtgt, timestamp_short);
	char *buf2 = strmem->Alloc( sz-2 );
	memcpy( buf2, buf, size );
	memcpy( buf2+size, ptr, (sz-2)-size );

	it = game->usermap.find(cmdto);
	if( it != game->usermap.end() ) {
		it->second->SendMsg( CCmdPacket, sz-2, buf2 );
	}

	strmem->Free( buf2, sz-2 );
	strmem->Free( buf, alloced );
}

void User::Echo( char *data, uint16_t sz )
{
	char *buf;
	u_long alloced=0;
	long size;
	uint64_t ts;

	sunpackf(data, "M", &ts); // put some gravy on it:
	size = spackf(&buf, &alloced, "Ml", ts, this->c2sl);

	SendMsg( CCmdRTTEcho, size, buf );
	strmem->Free( buf, alloced );
}

void User::ObjectTop( char *data, uint16_t sz )
{
	uint16_t objid;

	sunpackf(data, "u", &objid);
	lprintf("ObjectTop: %u", objid);

	if( game->top_var_id <= objid )
		game->top_var_id = objid+1;

	SendMsg( CCmdTopObject, sz, data );
}

void User::ObjectClaim( char *data, uint16_t sz )
{
	uint16_t objid;

	char *buf;
	u_long alloced=0;
	long size;

	sunpackf(data, "u", &objid);
	lprintf("ObjectClaim: %u by %u", objid, uid);

	size = spackf(&buf, &alloced, "uu", objid, uid);
	game->SendMsg( CCmdObjectClaim, size, buf );
	strmem->Free( buf, alloced );
}

void User::Spawn( char *data, uint16_t sz )
{
	char *ptr;

	char *buf;
	u_long alloced=0;
	long size;

	unsigned char type;
	uint16_t spawnid;

	ptr = sunpackf(data, "bu", &type, &spawnid);

	lprintf("Spawn: %u %u", (uint16_t)type, spawnid);

	if( type == 0 ) { // object
		Object *o = (Object*)halloc(sizeof(Object));
		new(o) Object();

		o->uid = game->top_var_id++;
		o->spawned = true;
		sunpackf(ptr, "fffffffff", &o->x, &o->y, &o->z, &o->r0, &o->r1, &o->r2, &o->scalex, &o->scaley, &o->scalez);

		size = spackf(&buf, &alloced, "bufffffffff", type, o->uid, o->x, o->y, o->z, o->r0, o->r1, o->r2, o->scalex, o->scaley, o->scalez);
		game->SendMsg( CCmdSpawn, size, buf, this );
		strmem->Free( buf, alloced );
		alloced = 0; buf = NULL;

		size = spackf(&buf, &alloced, "buu", 99, spawnid, o->uid);
		SendMsg( CCmdSpawn, size, buf );
		strmem->Free( buf, alloced );

		game->objects[ o->uid ] = o;
	} else if( type == 1 ) { // npc
		Npc *n = (Npc*)halloc(sizeof(Npc));
		new(n) Npc();

		n->uid = game->top_var_id++;
		sunpackf(ptr, "fffffffff", &n->x, &n->y, &n->z, &n->r0, &n->r1, &n->r2, &n->scalex, &n->scaley, &n->scalez);

		size = spackf(&buf, &alloced, "bufffffffff", type, n->uid, n->x, n->y, n->z, n->r0, n->r1, n->r2, n->scalex, n->scaley, n->scalez);
		game->SendMsg( CCmdSpawn, size, buf, this );
		strmem->Free( buf, alloced );
		alloced = 0; buf = NULL;

		type=99;
		size = spackf(&buf, &alloced, "buu", type, spawnid, n->uid);
		SendMsg( CCmdSpawn, size, buf );
		lprintf("sent npc spawn confirm (size %ld)", size);
		strmem->Free( buf, alloced );

		game->npcs[ n->uid ] = n;
	}
}
