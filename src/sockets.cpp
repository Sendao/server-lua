#include "main.h"

#include <zlib.h>

int fSock;

void InitSocket(int port)
{
	struct sockaddr_in saConn;
	int yes=1;
	int fSockTest;

#ifdef WIN32
	WSADATA w;
	int error = WSAStartup( 0x0202, &w );

	if( error ) {
		lprintf("Error starting socket library.");
		exit(-1);
	}
#endif

	// Get a file descriptor
	if( (fSockTest = socket(AF_INET, SOCK_STREAM, 0)) < 0 ) {
		lprintf("Error allocating AF_INET SOCK_STREAM socket: %s", strerror(errno));
		exit(-1);
	}
	fSock = (unsigned int)fSockTest;

#ifdef LINUX
	fcntl(fSock, F_SETFL, O_NONBLOCK);
#endif
	setsockopt( fSock, SOL_SOCKET, SO_REUSEADDR, (char*)&yes, sizeof(yes) );

	// Bind the socket
	saConn.sin_family = AF_INET;
	saConn.sin_port = htons(port);
	saConn.sin_addr.s_addr = htonl( INADDR_ANY );
	if( bind(fSock, (struct sockaddr *)&saConn, sizeof(struct sockaddr)) < 0 )
	{
		lprintf("Error binding socket %d to port %d: %s", fSock, port, strerror(errno));
		exit(-1);
	}
	if( listen(fSock, 5) < 0 )
	{
		lprintf("Error listening for socket %d connections: %s", fSock, strerror(errno));
		exit(-1);
	}
	lprintf("Listening.");
}
char *GetSocketError(int fSocket)
{
	char *errbuf=NULL;

#ifdef WIN32
	int iErr = WSAGetLastError();
	LPSTR errString = NULL;
	/*int size = */FormatMessage( FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM,
					 0, iErr, 0, (LPSTR)&errString, 0, 0 );
	lprintf("recv() error %d: %s", iErr, errString);
	errbuf = str_copy(errString);
	LocalFree( errString );
#else
	int error = 0;
	socklen_t len = sizeof (error);
	int retval = getsockopt(fSocket, SOL_SOCKET, SO_ERROR, (void*)&error, &len);
	if ( error != 0 ) {
		errbuf = str_copy(strerror(retval));
	} else {
		errbuf = str_copy("no error");
	}
#endif

	return errbuf;
}
void ExitSocket(void)
{
	close(fSock);
#ifdef WIN32
	WSACleanup();
#endif
}
void sock_close(int lsock)
{
	lprintf("Close socket %d: Result %d.", lsock, close(lsock));
}

User *InitConnection(void)
{
	int fUserTest, iTmp;
	unsigned int fUser, sockLen;
	struct sockaddr_in saConn;
	User *user;

	iTmp = sizeof(struct sockaddr_in);
#ifdef WIN32
	if( (fUserTest = accept(fSock, (struct sockaddr *)&saConn, &iTmp)) < 0 ) {
#else
	if( (fUserTest = accept(fSock, (struct sockaddr *)&saConn, (socklen_t*)&sockLen)) < 0 ) {
//		iTmp = sockLen;
#endif
		lprintf("accept(): connection failed '%s'(%d)", strerror(errno), errno);
		return NULL;
	}
	lprintf("[initConnection]");
    fUser = (unsigned int)fUserTest;

	user = (User*)halloc( sizeof(User) );
	new(user) User();
	
	user->fSock = fUser;
	char *ntoa = inet_ntoa(saConn.sin_addr);
	user->sHost = strmem->Alloc( strlen(ntoa)+1 );
	strcpy(user->sHost, ntoa);

	sscanf(user->sHost, "%d.%d.%d.%d", &user->iHost[0], &user->iHost[1], &user->iHost[2], &user->iHost[3]);
	lprintf("Got connection from %d.%d.%d.%d", user->iHost[0], user->iHost[1], user->iHost[2], user->iHost[3]);

	return user;
}

int OutputConnection(User *user)
{
	if( !user || user->outbufsz <= 0 )
		return 0;
	int outbufoffset = user->outbuf - user->outbuf_memory;
	int sendsize = user->outbufsz - outbufoffset;
	
	// Throttle:
	sendsize = sendsize > user->outbufmax ? user->outbufmax : sendsize;

	int iSent = send(user->fSock, user->outbuf, sendsize, 0);

	if(iSent>0)
	{
		//lprintf("Output %d", iSent);
		if( iSent+outbufoffset >= user->outbufsz )
		{
			strmem->Free( user->outbuf_memory, user->outbufalloc );
			user->outbuf_memory = NULL;
			user->outbuf = NULL;
			user->outbufsz = 0;
			user->outbufalloc = 0;
		} else {
			user->outbuf += iSent;
		}
	} else if(iSent<0) {
		lprintf("Error xmitting: %s", strerror(errno));
	} else {
		lprintf("Nothing sent of %d.", sendsize);
	}

	if(user->bQuitting && user->outbufsz == 0 ) // Output sent, now quit.
		return -1;

	return iSent<0?-1:0;
}

int InputConnection(User *user)
{
	uintptr_t iSize;
	char *p, *tmpbuf;
	z_stream strm = {0};
	unsigned char in[1024];
	unsigned char out[1024];

	int status;

	if( !user ) return 0;

	if( user->inbufsz >= user->inbufmax ) return 0;

	iSize = recv(user->fSock, user->inbuf, user->inbufmax - user->inbufsz, 0);

	if( iSize <= 0 ) {
		lprintf("Read <=0 from recv()");
		return -1;
	}

	user->inbufsz += iSize;
	user->inbuf += iSize;

	// Parse into messages
	int sz;
	unsigned char smallsz;
	char *msgbuf;
	char *endpt;
	unsigned char ctl;

	char *subbuf;
	unsigned char *subptr, *leftover=NULL;
	unsigned char *subend, *subend2;
	int leftover_sz=0;
	bool failed=false;

	p = user->inbuf_memory;
	while( p+sizeof(int) < user->inbuf ) {
		ctl = *(unsigned char*)p;
		if( ctl == 255 ) {
			p++;
			sz = *(int*)p;
			if( p+sizeof(int)+sz >= user->inbuf ) {
				p--;
				break;
			}
			p += sizeof(int);

			strm.zalloc = Z_NULL;
			strm.zfree = Z_NULL;
			strm.opaque = Z_NULL;
			strm.avail_in = 0;
			strm.next_in = in;
			status = inflateInit(&strm, 15|ENABLE_ZLIB_GZIP);
			strm.avail_in = sz;
			strm.next_in = (unsigned char*)p;
			do {
				strm.avail_out = 1024;
				strm.next_out = out;
				status = inflate(&strm, Z_NO_FLUSH);
				switch( status ) {
				case Z_OK: case Z_STREAM_END: case Z_BUF_ERROR:
					break;
				default:
					inflateEnd(&strm);
					lprintf("Error decompressing: %d", status);
					failed=true;
					break;
				}
				if( failed ) break;
				sz = 1024 - strm.avail_out; // how much is actually in 'out'

				subptr = leftover;
				subend = leftover + leftover_sz;
				subend2 = out + sz;
				do {

					if( subptr == subend ) {
						subptr = out;
						subend = NULL;
					}

					smallsz = *(unsigned char*)subptr;
					if( subend != NULL && subptr+1+smallsz > subend ) {
						int remainder = subend - (subptr+1+smallsz);

						if( sz < remainder ) {
							msgbuf = strmem->Alloc( subend-subptr + sz );
							memcpy( msgbuf, subptr, subend-subptr );
							memcpy( msgbuf+(subend-subptr), out, sz );
							if( leftover )
								strmem->Free( leftover, leftover_sz );
							leftover = msgbuf;
							leftover_sz = (subend-subptr)+sz;
							break;
						} else {
							msgbuf = strmem->Alloc( subend-subptr + remainder );
							memcpy( msgbuf, subptr, subend-subptr );
							memcpy( msgbuf+(subend-subptr), out, remainder );
							user->messages.push_back( msgbuf );
							leftover = NULL;
							leftover_sz = 0;
							subptr = out + remainder;
							subend = NULL;
						}
						continue;
					}
					if( ( subend != NULL && subptr+1+smallsz > subend ) ||
						( subend == NULL && subptr+1+smallsz > subend2 ) ) {
						if( leftover )
							strmem->Free( leftover, leftover_sz );
						leftover = strmem->Alloc( subend-subptr );
						memcpy( leftover, subptr, subend-subptr );
						leftover_sz = subend - subptr;
						break;
					} else {
						subptr++;
						msgbuf = strmem->Alloc( smallsz );
						memcpy( msgbuf, subptr, smallsz );
						subptr += smallsz;
						user->messages.push_back( msgbuf );
					}
				} while( subptr != subend2 );

			} while( strm.avail_out == 0 );

			inflateEnd(&strm);

			msgbuf = strmem->Alloc( strm.total_out );
			memcpy( msgbuf, out, strm.total_out );
			p += sz;

		} else if( p+ctl >= user->inbuf ) {
			break;
		}
		endpt = p+ctl;
		while( p < endpt ) {
			smallsz = *(unsigned char*)p;
			msgbuf = strmem->Alloc(1+smallsz); // 1+ is for the size of the char smallsz
			memcpy( msgbuf, p, 1+smallsz );
			p += 1+smallsz;
			user->messages.push_back( msgbuf );
		}
	}

	if( p != user->inbuf_memory ) { // we have read some data so we need to trim the input buffer
		sz = p - user->inbuf_memory;
		user->inbufsz -= sz;
		tmpbuf = strmem->Alloc( user->inbufsz );
		//! Todo: test whether one memcpy is sufficient
		memcpy( tmpbuf, p, user->inbufsz );
		memcpy( user->inbuf_memory, tmpbuf, user->inbufsz );
		strmem->Free( tmpbuf, user->inbufsz );
		user->inbuf = user->inbuf_memory + user->inbufsz;
	}

	return 0;
}

void Output(User *user, const char *str, uint16_t len)
{
	char *np;

//https://www.lemoda.net/c/zlib-open-write/index.html

	if( user->outbufsz + len > user->outbufalloc ) {
		user->outbufalloc = user->outbufsz + len + 1024;
		long outbufoffset = user->outbuf - user->outbuf_memory;
		user->outbuf_memory = strmem->Realloc( user->outbuf, user->outbufsz, user->outbufalloc );
		user->outbuf = user->outbuf_memory + outbufoffset;
	}
	memcpy( user->outbuf_memory+user->outbufsz, str, len );
	user->outbufsz += len;
}
