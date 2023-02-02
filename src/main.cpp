#include "main.h"

void mainloop(void);

unordered_map<string,u_long> varmap;
u_long top_var_id = 1;
unordered_map<u_long,Primitive> datamap;
unordered_map<u_long,User*> datamap_whichuser;
unordered_set<u_long> dirtyset;
bool reading_files = false;

int main(int ac, char *av[])
{
	init_pools();
	init_commands();
	init_lua();

	//! Process arguments

	// Initialize
	setlog("server.log");
	InitSocket(2038);

	// Main loop
	mainloop();

	// End
	ExitSocket();

	return 0;
}

vector<User*> userL;
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

void mainloop()
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
	char *tmpbuf, *buf, *buf2, *buf3, *packed;
	const char *cstr;
	Primitive prim;

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
		if( reading_files || dirtyset.size() > 0 ) {
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

		for( itset = dirtyset.begin(); itset != dirtyset.end(); itset++ ) {
			key = *itset;
			prim = datamap[key];
			uTarget = datamap_whichuser[key];

			tmpsize = spackf( &tmpbuf, &packsz, "lc", key, prim.type );
			switch( prim.type ) {
				case 0: // char
					size2 = spackf(&buf2, &packsz2, "c", &prim.data.c);
					break;
				case 1: // int
					size2 = spackf(&buf2, &packsz2,  "i", &prim.data.i);
					break;
				case 2: // float
					size2 = spackf(&buf2, &packsz2, "f", &prim.data.f);
					break;
				case 3: // string
					size2 = spackf(&buf2, &packsz2, "s", &prim.data.s);
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

			for( ituser = userL.begin(); ituser != userL.end(); ituser++ )
			{
				user = *ituser;
				if( user != uTarget )
					user->SendMessage( 0, tmpsize, tmpbuf );
			}
			
			datamap_whichuser.erase(key);
		}
		dirtyset.clear();

		// Process output
		zerotime.tv_usec = zerotime.tv_sec = 0;
		select(iHigh, NULL, &fdO, NULL, &zerotime);
		if( reading_files ) {
			buf = strmem->Alloc( 1024 );
		}
		bool found_file = false;
		for( ituser = userL.begin(); ituser != userL.end(); )
		{
			user = *ituser;
			if( FD_ISSET(user->fSock, &fdO) ) {
				if( reading_files && user->fReading ) {
					int status = fread( buf, 1, 1024, user->fReading );
					if( status == 0 ) {
						// File is empty, send EOF
						fclose( user->fReading );
						user->fReading = NULL;
						user->SendMessage( 4, 0, NULL );
						return;
					}
					found_file = true;
					user->SendMessage( 3, status, buf );
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
		if( reading_files ) {
			strmem->Free( buf, 1024 );
			if( !found_file )
				reading_files = false;
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


