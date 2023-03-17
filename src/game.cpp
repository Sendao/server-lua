#include "main.h"


Game::Game()
{
	top_var_id = 100;
}
Game::~Game()
{
}

void Game::InitialiseAPITable(void)
{
	sol::usertype<Game> game_type = lua.new_usertype<Game>("Game", sol::constructors<Game()>());
	
	game_type["clock"] = &Game::clock;
	game_type["usermap"] = &Game::usermap;
	game_type["objects"] = &Game::objects;
}

void Game::mainloop()
{
	struct timeval per, prev_cycle, this_cycle, zerotime, *usetv;
	fd_set fdI, fdO, fdE;
	uint16_t key;
	unordered_set<uint16_t>::iterator itset;
	int iHigh, err, lsock;
	unordered_map<uint16_t, User*>::iterator ituser;
	User *user, *uTarget;
	u_long packsz, packsz2;
	long tmpsize, size2;
	char *tmpbuf, *buf, *buf2, *buf3, *packed, *fname;
	const char *cstr;
	Primitive *prim;

	smalltimeofday(&prev_cycle, NULL);
	prev_cycle.tv_sec -= 10;
	this_cycle.tv_sec = 0;

	while(1)
	{
		/*
		tl_timestamp ts;
		double timeleft;

		if( tl_eta(&timeleft) ) {
			lprintf("Running pending events");
			tl_step();
			per.tv_sec = (long)timeleft;
			per.tv_usec = (long)( (timeleft-per.tv_sec)*1000 );
			usetv = &per;
		} else {
			usetv = NULL;
			lprintf("No events - waiting for connection");
		}
		*/
		if( game->reading_files || game->dirtyset.size() > 0 ) {
			per.tv_sec = 0; // run immediately if there is data to process
			per.tv_usec = 0;
		} else {
			per.tv_usec = 0;
			if( this_cycle.tv_sec-prev_cycle.tv_sec > 10 ) {
				per.tv_sec = 0;
			} else {
				per.tv_sec = 10-(this_cycle.tv_sec-prev_cycle.tv_sec);// wait up to 10 seconds if no users are doing anything
			}
			ituser = usermap.begin();
			while( ituser != usermap.end() )
			{
				user = ituser->second;
				if( user->outbufsz > 0 )
				{
					per.tv_sec = 0;
					break;
				}
				ituser++;
			}
		}
		usetv = &per;
		FD_ZERO(&fdI);
		FD_ZERO(&fdO);
		FD_ZERO(&fdE);

		iHigh = fSock+1;
		for( ituser = usermap.begin(); ituser != usermap.end(); ituser++ )
		{
			user = ituser->second;
			if( user->fSock+1 > iHigh )
				iHigh = user->fSock+1;
			FD_SET((user->fSock), (&fdI));
			FD_SET((user->fSock), (&fdO));
			FD_SET((user->fSock), (&fdE));
		}
		if( fSock != -1 ) {
			FD_SET(fSock, &fdI);
			FD_SET(fSock, &fdE);
		}

		err=select((int)iHigh, &fdI, NULL, &fdE, usetv);
		if( err == -1 ) {
			perror("select()");
			tmpbuf = GetSocketError(fSock);
			lprintf("select() error: %s", tmpbuf);
			strmem->Free( tmpbuf, strlen(tmpbuf)+1 );
			abort();
		}

		smalltimeofday(&this_cycle, NULL);
		clock.UpdateTo(this_cycle.tv_sec, this_cycle.tv_usec);
		game->last_update = (uint64_t)this_cycle.tv_sec*(uint64_t)1000 + (uint64_t)this_cycle.tv_usec;
		//lprintf("Set game time to %llu", game->last_update);

		// Disconnect errored machines and process inputs:
		ituser = usermap.begin();
		while( ituser != usermap.end() ) 
		{
			user = ituser->second;
			if( FD_ISSET(user->fSock, &fdE) )
			{
				lprintf("Socket exception: dropped connection");
				lprintf("recv() error: %s", GetSocketError(user->fSock));
				FD_CLR(user->fSock, &fdI);
				FD_CLR(user->fSock, &fdO);
				user->SendQuit();
				user->Close();
				ituser = usermap.erase(ituser);
				hfree(user, sizeof(User));
				continue;
			}
			if( FD_ISSET(user->fSock, &fdI) )
			{
				if( InputConnection(user) < 0 )
				{	// user broke connection
					lprintf("Input<0: client dropped connection");
					lprintf("socket error: %s", GetSocketError(user->fSock));
					FD_CLR(user->fSock, &fdO);
					user->SendQuit();
					user->Close();
					ituser = usermap.erase(ituser);
					hfree(user, sizeof(User));
					continue;
				} else if( user->messages.size() > 0 ) {
					user->ProcessMessages();
				}
			}
			ituser++;
		}

		// Send keyvals
		for( itset = game->dirtyset.begin(); itset != game->dirtyset.end(); itset++ ) {
			key = *itset;
			prim = game->datamap[key];
			uTarget = game->datamap_whichuser[key];

			tmpbuf=NULL;
			packsz = 0;
			tmpsize = spackf( &tmpbuf, &packsz, "lc", key, prim->type );
			packsz2 = 0;
			buf2 = NULL;
			switch( prim->type ) {
				case 100: // char
					size2 = spackf(&buf2, &packsz2, "c", &prim->data.c);
					break;
				case 101: // int
					size2 = spackf(&buf2, &packsz2,  "i", &prim->data.i);
					break;
				case 102: // float
					size2 = spackf(&buf2, &packsz2, "f", &prim->data.f);
					break;
				case 103: // string
					size2 = spackf(&buf2, &packsz2, "s", &prim->data.s);
					break;
			}
			buf3 = strmem->Alloc( tmpsize + size2 );
			memcpy(buf3, tmpbuf, tmpsize);
			if( buf2 )
				memcpy(buf3+tmpsize, buf2, size2);
			strmem->Free( tmpbuf, packsz );
			if( buf2 ) {
				strmem->Free( buf2, packsz2 );
				tmpsize += size2;
			}
			tmpbuf = buf3;
			buf3 = NULL;
			game->SendMsg(CCmdVarInfo, tmpsize, tmpbuf, uTarget);			
			game->datamap_whichuser.erase(key);
		}
		game->dirtyset.clear();

		// Send clocksync
		if( this_cycle.tv_sec >= prev_cycle.tv_sec+3 ) {
			game->last_timestamp = (uint64_t)this_cycle.tv_sec*(uint64_t)1000 + (uint64_t)this_cycle.tv_usec;
			buf = NULL; packsz=0;
			tmpsize = spackf(&buf, &packsz, "M", game->last_timestamp);
			game->SendMsg(CCmdTimeSync, tmpsize, buf, NULL);
			strmem->Free(buf, packsz); buf = NULL; packsz=0;
			prev_cycle = this_cycle;
		}
		
		// Process output
		zerotime.tv_usec = zerotime.tv_sec = 0;
		select(iHigh, NULL, &fdO, NULL, &zerotime);
		if( game->reading_files ) {
			buf = strmem->Alloc( 1024 );
		}
		bool found_file = false;
		ituser = usermap.begin();
		while( ituser != usermap.end() )
		{
			user = ituser->second;
			if( FD_ISSET(user->fSock, &fdO) ) {
				if( game->reading_files && user->fReading ) {
					int status = fread( buf, 1, 1024, user->fReading );
					if( status == 0 ) {
						// File is empty, send EOF
						fclose( user->fReading );
						user->fReading = NULL;
						user->SendMsg( CCmdNextFile, 0, NULL );
						if( user->reading_file_q.size() > 0 ) {
							fname = user->reading_file_q.front();
							user->GetFileS(fname);
							strmem->Free(fname, strlen(fname)+1);
							user->reading_file_q.pop();
						}
					} else {
						found_file = true;
						user->SendMsg( CCmdFileData, status, buf );
					}
				} else if( game->reading_files && user->reading_ptr && user->reading_sz > 0 ) {
					int status = user->reading_sz > user->inbufmax ? user->inbufmax : user->reading_sz;
					user->SendMsg( CCmdFileData, status, user->reading_ptr );
					user->reading_ptr += status;
					user->reading_sz -= status;
					if( user->reading_sz == 0 ) {
						user->reading_ptr = NULL;
						user->SendMsg( CCmdNextFile, 0, NULL );
						if( user->reading_file_q.size() > 0 ) {
							fname = user->reading_file_q.front();
							user->GetFileS(fname);
							strmem->Free(fname, strlen(fname)+1);
							user->reading_file_q.pop();
						}
					}
					found_file = true;
				}

				if( user->outbufsz > 0 )
				{
					if( OutputConnection(user) < 0 || user->bQuitting )
					{
						lprintf("connection close: quitting");
						ituser = usermap.erase(ituser);
						user->SendQuit();
						user->Close();
						hfree(user, sizeof(User));
						continue;
					}
				}
			}
			ituser++;
		}
		if( game->reading_files ) {
			strmem->Free( buf, 1024 );
			if( !found_file )
				game->reading_files = false;
		}

		if( FD_ISSET(fSock, &fdI) ) // New Connection Available
		{
			user = InitConnection();
			if( user ) {
				lprintf("InitConnection %u", user->fSock);
				game->usermap[user->uid] = user;
			}
		}
	}
}

uint64_t Game::GetTime()
{
	struct timeval tv;
	smalltimeofday(&tv, NULL);
	return (uint64_t)tv.tv_sec*(uint64_t)1000 + (uint64_t)tv.tv_usec;
}


void Game::IdentifyVar( char *name, int type, User *sender )
{
	unordered_map<string,VarData*>::iterator it;
	u_long alloced = 0;
	VarData *v;
	Object *o;
	char *buf=NULL;
	long size;
	uint16_t ts_short;
	
	it = game->varmap.find(name);
	if( it != game->varmap.end() ) {
		v = it->second;
		size = spackf(&buf, &alloced, "scu", name, type, v->objid );
		sender->SendMsg( CCmdVarInfo, size, buf );

		switch( v->type ) {
			case 0: // object (rigidbody)
				o = game->objects[v->objid];
				if( o->last_update != 0 && !sender->authority ) {
					ts_short = o->last_update - game->last_timestamp;

					lprintf("First time update obj %s: %f %f %f rotation %f %f %f", v->name, o->x, o->y, o->z, o->r0, o->r1, o->r2);

					strmem->Free( buf, alloced );
					buf = NULL; alloced = 0;

					size = spackf(&buf, &alloced, "uuffffff", o->uid, ts_short, o->x, o->y, o->z, o->r0, o->r1, o->r2 );
					sender->SendMsg( CCmdSetObjectPositionRotation, size, buf );
				}
				break;
			case 1: // collider?
				break;
			case 2: // player-obj
				break;
		}
	} else {
		size = spackf(&buf, &alloced, "scu", name, type, game->top_var_id );
		game->SendMsg( CCmdVarInfo, size, buf, NULL );

		v = (VarData*)halloc(sizeof(VarData));
		v->name = str_copy(name);
		v->objid = game->top_var_id;
		v->type = type;

		if( type == 0 ) { // allocate the object
			o = (Object*)halloc(sizeof(Object));
			new(o) Object();
			o->uid = v->objid;
			o->name = str_copy(name);
			lprintf("New object %s: %u", name, o->uid);
			game->objects[o->uid] = o;
		}
		
		game->top_var_id++;
		game->varmap[name] = v;
		game->varmap_by_id[v->objid] = v;
	}
	strmem->Free( buf, alloced );

}

Object *Game::FindObject( uint16_t objid )
{
	unordered_map<uint16_t, Object*>::iterator itobj;
	itobj = objects.find(objid);
	if( itobj == objects.end() ) {
		return NULL;
	}
	return itobj->second;
}

void Game::SendMsg( char cmd, unsigned int size, char *data, User *exclude )
{
	User *user;
	unordered_map<uint16_t, User*>::iterator ituser;
	char *buf=NULL;
	long bufsz;
	u_long alloced = 0;

	bufsz = spackf(&buf, &alloced, "cv", cmd, size, data );
	ituser = usermap.begin();
	while( ituser != usermap.end() )
	{
		user = ituser->second;
		if( user != exclude )
		{
			Output(user, buf, bufsz);
		}
		ituser++;
	}
	strmem->Free(buf, alloced);
	
}
void Game::PickNewAuthority( User *exclude )
{
	unordered_map<uint16_t, User*>::iterator ituser;

	ituser = usermap.begin();
	if( ituser != usermap.end() && ituser->second == exclude )
		ituser++;
	if( ituser == usermap.end() ) {
		lprintf("No users, waiting for host.");
		firstUser = true;
		game->top_var_id = 100; // reset
		// no users found		
		return;
	}
	User *user = ituser->second;
	lprintf("Changing host to %u.", user->uid);
	char *buf=NULL;
	u_long alloced = 0;
	long size;
	user->authority = true;
	size = spackf(&buf, &alloced, "c", 1 );
	user->SendMsg( CCmdChangeUserRegistration, size, buf );
	strmem->Free( buf, alloced );

	lua["hostid"] = user->uid;	
	vector<sol::function> &funcs = LuaEvent( ServerEvent::ChangeHost );
	for( vector<sol::function>::iterator it = funcs.begin(); it != funcs.end(); it++ ) {
		sol::function &f = *it;
		f( this );
	}
}
