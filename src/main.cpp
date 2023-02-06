#include "main.h"

void mainloop(void);

Game *game;

int main(int ac, char *av[])
{
	init_pools();
	
	game = (Game*)halloc(sizeof(Game));
	new(game) Game();

	init_commands();
	init_lua();

	GetFileList();

	//! Process arguments

	// Initialize
	setlog("server.log");
	InitSocket(2038);

	// Main loop
	game->mainloop();

	// End
	ExitSocket();

	return 0;
}

struct timeval now;
time_t currentTime;

/* We convert from microseconds to milliseconds */
#if defined(_MSC_VER) || defined(__MINGW32__)
int smalltimeofday(struct timeval* tp, void* tzp) {
	long long ms = std::chrono::system_clock::now().time_since_epoch() / std::chrono::milliseconds(1);
    tp->tv_sec = ms / (long long)1000;
    tp->tv_usec = ms % (long long)1000;
    /* 0 indicates that the call succeeded. */
    return 0;
}
#else
int smalltimeofday(struct timeval* tp, void* tzp ) {
	gettimeofday(tp, tzp);
	tp->tv_usec = tp->tv_usec / 1000;
	return 0;
}
#endif

void Game::mainloop()
{
	struct timeval per, prev_cycle, this_cycle, zerotime, *usetv;
	fd_set fdI, fdO, fdE;
	u_long key;
	unordered_set<u_long>::iterator itset;
	int iHigh, err, lsock;
	vector<User*>::iterator ituser;
	User *user, *uTarget;
	u_long packsz, packsz2;
	long tmpsize, size2;
	char *tmpbuf, *buf, *buf2, *buf3, *packed, *fname;
	const char *cstr;
	Primitive *prim;

	smalltimeofday(&prev_cycle, NULL);
	prev_cycle.tv_sec -= 10;

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
			per.tv_sec = 10-(this_cycle.tv_sec-prev_cycle.tv_sec);// wait up to 10 seconds if no users are doing anything
		}
		usetv = &per;
		FD_ZERO(&fdI);
		FD_ZERO(&fdO);
		FD_ZERO(&fdE);

		iHigh = fSock+1;
		for( ituser = userL.begin(); ituser != userL.end(); ituser++ )
		{
			user = *ituser;
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

		// Disconnect errored machines and process inputs:
		for( ituser = userL.begin(); ituser != userL.end(); )
		{
			user = *ituser;
			if( FD_ISSET(user->fSock, &fdE) )
			{
				lprintf("Socket exception: dropped connection");
				lprintf("recv() error: %s", GetSocketError(user->fSock));
				lsock = user->fSock;
				sock_close(lsock);
				FD_CLR(lsock, &fdI);
				FD_CLR(lsock, &fdO);
				ituser = userL.erase(ituser);
				hfree(user, sizeof(User));
				continue;
			}
			if( FD_ISSET(user->fSock, &fdI) )
			{
				if( InputConnection(user) < 0 )
				{	// user broke connection
					lprintf("Input<0: client dropped connection");
					lprintf("socket error: %s", GetSocketError(user->fSock));
					/* No need apparently for this:
					lsock = user->fSock;
					sock_close(lsock);*/
					FD_CLR(lsock, &fdO);
					ituser = userL.erase(ituser);
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

			tmpsize = spackf( &tmpbuf, &packsz, "lc", key, prim->type );
			switch( prim->type ) {
				case 0: // char
					size2 = spackf(&buf2, &packsz2, "c", &prim->data.c);
					break;
				case 1: // int
					size2 = spackf(&buf2, &packsz2,  "i", &prim->data.i);
					break;
				case 2: // float
					size2 = spackf(&buf2, &packsz2, "f", &prim->data.f);
					break;
				case 3: // string
					size2 = spackf(&buf2, &packsz2, "s", &prim->data.s);
					break;
			}
			buf3 = strmem->Alloc( tmpsize + size2 );
			memcpy(buf3, tmpbuf, tmpsize);
			memcpy(buf3+tmpsize, buf2, size2);
			strmem->Free( tmpbuf, packsz );
			strmem->Free( buf2, packsz2 );
			tmpsize += size2;
			tmpbuf = buf3;
			buf3 = NULL;
			game->SendMsg(CCmdVarInfo, tmpsize, tmpbuf, uTarget);			
			game->datamap_whichuser.erase(key);
		}
		game->dirtyset.clear();

		// Send clocksync
		if( this_cycle.tv_sec >= prev_cycle.tv_sec+10 ) {
			game->last_timestamp = this_cycle.tv_sec*1000 + this_cycle.tv_usec;
			tmpsize = spackf(&buf, &packsz, "L", game->last_timestamp);
			game->SendMsg(CCmdTimeSync, tmpsize, buf, NULL);
			strmem->Free(buf, packsz);
			prev_cycle = this_cycle;
		}
		

		// Process output
		zerotime.tv_usec = zerotime.tv_sec = 0;
		select(iHigh, NULL, &fdO, NULL, &zerotime);
		if( game->reading_files ) {
			buf = strmem->Alloc( 1024 );
		}
		bool found_file = false;
		for( ituser = userL.begin(); ituser != userL.end(); )
		{
			user = *ituser;
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
						return;
					}
					found_file = true;
					user->SendMsg( CCmdFileData, status, buf );
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
						lsock = user->fSock;
						sock_close(lsock);

						ituser = userL.erase(ituser);
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
			lprintf("InitConnection %u", user->fSock);
			//TransmitWorld(user);
			userL.push_back(user);
		}
	}
}

inline long compare_usec( struct timeval *high, struct timeval *low ) {
	return (((high->tv_sec-low->tv_sec)*1000000)+(high->tv_usec-low->tv_usec));
}


Game::Game()
{

}
Game::~Game()
{
}

long long Game::GetTime()
{
	struct timeval tv;
	smalltimeofday(&tv, NULL);
	return (long long)tv.tv_sec*(long long)1000 + (long long)tv.tv_usec/(long long)1000;
}

void Game::IdentifyVar( char *name, int type, User *sender )
{
	unordered_map<string,VarData*>::iterator it;
	u_long alloced;
	VarData *v;
	Object *o;
	char *buf;
	long size;
	int ts_short;
	
	it = game->varmap.find(name);
	if( it != game->varmap.end() ) {
		v = it->second;
		size = spackf(&buf, &alloced, "scl", name, type, v->objid );
		sender->SendMsg( CCmdVarInfo, size, buf );

		if( v->type == 0 ) { // it's an object
		// send obj info
			o = game->objects[v->objid];
			ts_short = o->last_update - game->last_timestamp;

			strmem->Free( buf, alloced );
			size = spackf(&buf, &alloced, "lifffffff", o->uid, ts_short, o->x, o->y, o->z, o->r0, o->r1, o->r2, o->r3 );
			sender->SendMsg( CCmdSetObjectPositionRotation, size, buf );
		}
	} else {
		size = spackf(&buf, &alloced, "scl", name, type, game->top_var_id );
		game->SendMsg( CCmdVarInfo, size, buf, NULL );

		v = (VarData*)halloc(sizeof(VarData));
		v->name = name;
		v->objid = game->top_var_id;
		v->type = type;

		if( type == 0 ) { // allocate the object
			o = (Object*)halloc(sizeof(Object));
			new(o) Object();
			o->uid = v->objid;
			o->name = str_copy(name);
			game->objects[o->uid] = o;
		}
		
		game->top_var_id++;
		game->varmap[name] = v;
		game->varmap_by_id[v->objid] = v;
	}
	strmem->Free( buf, alloced );

}

Object *Game::FindObject( u_long objid )
{
	unordered_map<u_long, Object*>::iterator itobj;
	itobj = objects.find(objid);
	if( itobj == objects.end() ) {
		return NULL;
	}
	return itobj->second;
}

void Game::SendMsg( char cmd, unsigned int size, char *data, User *exclude )
{
	User *user;
	vector<User*>::iterator ituser;
	char *buf=NULL;
	long bufsz;
	u_long alloced;

	bufsz = spackf(&buf, &alloced, "cv", cmd, size, data );
	for( ituser = userL.begin(); ituser != userL.end(); ituser++ )
	{
		user = *ituser;
		if( user != exclude )
		{
			Output(user, buf, bufsz);
		}
	}
	strmem->Free(buf, alloced);
	
}
