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

#if defined(_MSC_VER) || defined(__MINGW32__)
int gettimeofday(struct timeval* tp, void* tzp) {
    DWORD t = timeGetTime();
    tp->tv_sec = t / 1000;
    tp->tv_usec = t % 1000;
    /* 0 indicates that the call succeeded. */
    return 0;
}
#endif

void Game::mainloop()
{
	struct timeval per, next_cycle, zerotime, *usetv;
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

	gettimeofday(&next_cycle, NULL);

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
			per.tv_sec = 5; // wait up to 5 seconds if no users are doing anything
			per.tv_usec = 0;
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
			game->SendMsg(0, tmpsize, tmpbuf, uTarget);			
			game->datamap_whichuser.erase(key);
		}
		game->dirtyset.clear();

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
						user->SendMsg( 4, 0, NULL );
						if( user->reading_file_q.size() > 0 ) {
							fname = user->reading_file_q.front();
							user->GetFileS(fname);
							strmem->Free(fname, strlen(fname)+1);
							user->reading_file_q.pop();
						}
						return;
					}
					found_file = true;
					user->SendMsg( 3, status, buf );
				} else if( game->reading_files && user->reading_ptr && user->reading_sz > 0 ) {
					int status = user->reading_sz > user->inbufmax ? user->inbufmax : user->reading_sz;
					user->SendMsg( 3, status, user->reading_ptr );
					user->reading_ptr += status;
					user->reading_sz -= status;
					if( user->reading_sz == 0 ) {
						user->reading_ptr = NULL;
						user->SendMsg( 4, 0, NULL );
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
	time_t ms = time(NULL);
	return (long long)ms;
}

void Game::IdentifyVar( char *name, int type, User *sender )
{
	unordered_map<string,VarData*>::iterator it;
	u_long alloced;
	VarData *v;
	char *buf;
	long size;
	
	it = game->varmap.find(name);
	if( it != game->varmap.end() ) {
		v = it->second;
		size = spackf(&buf, &alloced, "scl", name, 0, v->objid );
		sender->SendMsg( 0, size, buf );
	} else {
		size = spackf(&buf, &alloced, "scl", name, 0, game->top_var_id );
		game->SendMsg( 0, size, buf, NULL );
		
		v = (VarData*)halloc(sizeof(VarData));
		v->name = name;
		v->objid = game->top_var_id;
		v->type = type;

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
