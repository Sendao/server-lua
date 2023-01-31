#include "main.h"

void mainloop(void);

unordered_map<string,ulong> varmap;
ulong top_var_id = 1;
unordered_map<ulong,Primitive> datamap;
unordered_map<ulong,User*> datamap_whichuser;
unordered_set<ulong> dirtyset;

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
	ulong key;
	unordered_set<string>::iterator itset;
	int iHigh, err, lsock;
	vector<User*>::iterator ituser;
	User *user, *uTarget;
	long tmpsize, size2;
	char *tmpbuf, *buf2, *buf3;
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
		if( dirtyset.size() > 0 ) {
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

		// Disconnect errored machines:
		for( ituser = userL.begin(); ituser != userL.end(); ituser++ )
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
				userL.erase(ituser);
				hfree(user, sizeof(User));
				continue;

			}
			if( FD_ISSET(user->fSock, &fdI) )
			{
				if( InputConnection(user) < 0 )
				{	// user broke connection
					lprintf("Input<0: client dropped connection");
					lprintf("socket error: %s", GetSocketError(user->fSock));
					lsock = user->fSock;
					sock_close(lsock);
					FD_CLR(lsock, &fdO);
					userL.erase(ituser);
					hfree(user, sizeof(User));
					continue;
				} else if( user->messages.size() > 0 ) {
					user->ProcessMessages();
				}
			}
		}

		// Send keyvals

		for( itset = dirtyset.begin(); itset != dirtyset.end(); itset++ ) {
			key = *itset;
			prim = datamap[key];
			uTarget = datamap_whichuser[key];

			tmpsize = spackf( &tmpbuf, "lc", key, prim.type );
			switch( prim.type ) {
				case 0: // char
					size2 = spackf(&buf2, "c", &prim.data.c);
					break;
				case 1: // int
					size2 = spackf(&buf2, "i", &prim.data.i);
					break;
				case 2: // float
					size2 = spackf(&buf2, "f", &prim.data.f);
					break;
				case 3: // string
					size2 = spackf(&buf2, "s", &prim.data.s);
					break;
				case 4: // buffer (binary string)
					size2 = spackf(&buf2, "p", &prim.data.p);
					break;
			}
			buf3 = strmem->Alloc( tmpsize + size2 );
			memcpy(buf3, tmpbuf, tmpsize);
			memcpy(buf3+tmpsize, buf2, size2);
			strmem->Free( tmpbuf, tmpsize );
			strmem->Free( buf2, size2 );
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
			datamap.erase(key);
		}

		// Process output
		zerotime.tv_usec = zerotime.tv_sec = 0;
		select(iHigh, NULL, &fdO, NULL, &zerotime);
		for( ituser = userL.begin(); ituser != userL.end(); ituser++ )
		{
			user = *ituser;
			if( FD_ISSET(user->fSock, &fdO) ) {
				if( user->outbufsz > 0 )
				{
					if( OutputConnection(user) < 0 || user->bQuitting )
					{
						lprintf("connection close: quitting");
						lsock = user->fSock;
						sock_close(lsock);

						userL.erase(ituser);
						hfree(user, sizeof(User));
					}
				}
			}
		}

		if( FD_ISSET(fSock, &fdI) ) // New Connection Available
		{
			lprintf("InitConnection.");
			user = InitConnection();
			//TransmitWorld(user);
			userL.push_back(user);
		}
	}
}

inline long compare_usec( struct timeval *high, struct timeval *low ) {
	return (((high->tv_sec-low->tv_sec)*1000000)+(high->tv_usec-low->tv_usec));
}


