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
	z_stream strm;
	int status;
	char idbyte;
	char *tgtbuf = NULL;
	long tgtsz = 0;
	char buf[1024];
	unsigned char bufsz;
	int readsize;
	int iSent;
	char smallbuf[4];
	
	// Throttle:
	sendsize = sendsize > user->outbufmax ? user->outbufmax : sendsize;

//https://www.lemoda.net/c/zlib-open-write/index.html

	if( sendsize > 128 ) {
		strm.zalloc = Z_NULL;
		strm.zfree = Z_NULL;
		strm.opaque = Z_NULL;
		strm.avail_in = sendsize;
		strm.next_in = (Bytef*)user->outbuf;
		strm.avail_out = 0;
		status = deflateInit2(&strm, Z_DEFAULT_COMPRESSION, Z_DEFLATED,
							15 | 16, 8,
							Z_DEFAULT_STRATEGY);
		if( status < 0 ) {
			lprintf("deflateInit() failed: %d", status);
			return -1;
		}

		do {
			strm.avail_out = 1024;
			strm.next_out = (Bytef*)buf;
			status = deflate(&strm, Z_FINISH);
			if( status < 0 ) {
				lprintf("deflate() failed: %d", status);
				return -1;
			}
			readsize = 1024 - strm.avail_out;
			lprintf("Compression: readsize=%d", readsize);

			if( tgtbuf ) {
				tgtbuf = (char*)strmem->Realloc( tgtbuf, tgtsz, tgtsz+readsize );
				memcpy( tgtbuf + tgtsz, buf, readsize );
				tgtsz += readsize;
			} else {
				tgtbuf = (char*)strmem->Alloc( readsize );
				memcpy( tgtbuf, buf, readsize );
				tgtsz = readsize;
			}
		} while( strm.avail_out == 0 );

		deflateEnd(&strm);

		// first send the size of the compressed data

		idbyte = (unsigned char)255;
		iSent = send(user->fSock, &idbyte, 1, 0);
		if( iSent < 0 ) {
			lprintf("send() failed sending compressed data: %s", strerror(errno));
			return -1;
		} else if( iSent == 0 ) {
			lprintf("send() failed sending compressed data: connection closed");
			return -1;
		}
		smallbuf[0] = sendsize<<24;
		smallbuf[1] = sendsize<<16;
		smallbuf[2] = sendsize<<8;
		smallbuf[3] = sendsize&0xFF;
		iSent = send(user->fSock, smallbuf, 4, 0);
		if( iSent == 4 ) iSent=1;
	} else {
		idbyte = (unsigned char)(sendsize & 0xFF);
		tgtbuf = user->outbuf;
		tgtsz = sendsize;

		lprintf("Compile and send %d+1 bytes", (int)idbyte);
		iSent = send(user->fSock, &idbyte, 1, 0);
	}

	if( iSent == 1 ) {
		iSent = send(user->fSock, tgtbuf, tgtsz, 0);
	} else if( iSent < 0 ) {
		lprintf("Error sending idbyte: %s", strerror(errno));
		return -1;
	} else {
		lprintf("Nothing sent of idbyte.");
		return -1;
	}

	if(iSent>0)
	{
		if( iSent != sendsize ) {
			lprintf("Sent %d of %d.", iSent, sendsize);
			lprintf("Aborting.");
			return -1;
		}
		lprintf("Output %d bytes", iSent);
		if( iSent+outbufoffset >= user->outbufsz )
		{
			lprintf("release outbuf");
			strmem->Free( user->outbuf_memory, user->outbufalloc );
			user->outbuf_memory = NULL;
			user->outbuf = NULL;
			user->outbufsz = 0;
			user->outbufalloc = 0;
		} else {
			lprintf("shift bytes");
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

	lprintf("Received %d bytes", iSize);

	if( iSize <= 0 ) {
		lprintf("Read <=0 from recv()");
		return -1;
	}

	user->inbufsz += iSize;
	user->inbuf += iSize;

	// Parse into messages
	long sz, compsize;
	int ilen;
	unsigned char smallsz;
	char *msgbuf;
	char *endpt;
	unsigned char ctl, cmdByte;

	char *subbuf;
	unsigned char *subptr;
	char *leftover=NULL;
	unsigned char *subend, *subend2;
	int leftover_sz=0;
	bool failed=false;

	p = user->inbuf_memory;
	while( p+2 < user->inbuf ) {
		ctl = *(unsigned char*)p;
		if( ctl == 255 ) {
			p++;
			compsize = sz = *(long*)p;
			if( p+sizeof(long)+sz >= user->inbuf ) {
				p--;
				break;
			}
			p += sizeof(long);

			strm.zalloc = Z_NULL;
			strm.zfree = Z_NULL;
			strm.opaque = Z_NULL;
			strm.avail_in = 0;
			strm.next_in = (unsigned char*)p;
			status = inflateInit2(&strm, 15|16);
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

				subptr = (unsigned char*)leftover;
				subend = (unsigned char*)leftover + leftover_sz;
				subend2 = out + sz;
				do {

					if( subptr == subend ) {
						subptr = out;
						subend = NULL;
					}

					cmdByte = *subptr;
					ilen = (int)( (*subptr+1)<<8 | (*(subptr+2)&0xFF) );
					if( subend != NULL && subptr+3+ilen > subend ) {
						int remainder = subend - (subptr+3+ilen);

						if( sz < remainder ) { // not enough data in this packet, we'll need the next one
							msgbuf = strmem->Alloc( subend-subptr + sz );
							memcpy( msgbuf, subptr, subend-subptr );
							memcpy( msgbuf+(subend-subptr), out, sz );
							if( leftover )
								strmem->Free( leftover, leftover_sz );
							leftover = msgbuf;
							leftover_sz = (subend-subptr)+sz;
							break;
						} else {
							msgbuf = strmem->Alloc( (subend-subptr) + remainder );
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
					if( ( subend != NULL && subptr+3+ilen > subend ) ||
						( subend == NULL && subptr+3+ilen > subend2 ) ) {
						if( leftover )
							strmem->Free( leftover, leftover_sz );
						leftover = strmem->Alloc( subend-subptr );
						memcpy( leftover, subptr, subend-subptr );
						leftover_sz = subend - subptr;
						break;
					} else {
						msgbuf = strmem->Alloc( ilen+3 );
						memcpy( msgbuf, subptr, ilen+3 );
						subptr += ilen + 3;
						user->messages.push_back( msgbuf );
					}
				} while( subptr != subend2 );

			} while( strm.avail_out == 0 );

			inflateEnd(&strm);
			if( leftover ) {
				strmem->Free( leftover, leftover_sz );
				leftover = NULL;
				leftover_sz = 0;
			}

			//dunno why this was here:
			//msgbuf = strmem->Alloc( strm.total_out );
			//memcpy( msgbuf, out, strm.total_out );
			p += sz;

		} else if( p+ctl >= user->inbuf ) {
			lprintf("Packet size %d is too large for buffer %d", (int)ctl, user->inbufsz);
			break;
		} else {
			p++;
		}
		lprintf("Got %d packet size", (int)ctl);
		endpt = p+ctl;
		while( p < endpt ) {
			cmdByte = *p;
			ilen = (int)((*(p+1)<<8) | (*(p+2)&0xFF));
			if( ilen == 0 ) {
				msgbuf = strmem->Alloc(3);
				memcpy( msgbuf, p, 3 );
				user->messages.push_back(msgbuf);
				p += 3;
			} else {
				msgbuf = strmem->Alloc(ilen);
				memcpy( msgbuf, p, ilen+3 );
				p += 3 + ilen;
				user->messages.push_back( msgbuf );
			}
		}
	}

	if( p != user->inbuf_memory ) { // we have read some data so we need to trim the input buffer
		sz = p - user->inbuf_memory;
		user->inbufsz -= sz;
		// Note: copying in overlapping buffers is undefined behavior. So we require two copies here.
		//! An alternative is to copy byte-by-byte
		tmpbuf = strmem->Alloc( user->inbufsz );
		memcpy( tmpbuf, p, user->inbufsz );
		memcpy( user->inbuf_memory, tmpbuf, user->inbufsz );
		strmem->Free( tmpbuf, user->inbufsz );
		user->inbuf = user->inbuf_memory + user->inbufsz;
	}

	return 0;
}

void Output(User *user, const char *str, unsigned long len)
{
	char *np;

	if( user->outbufsz + len > user->outbufalloc ) {
		user->outbufalloc = user->outbufsz + len + 1024;
		long outbufoffset = user->outbuf - user->outbuf_memory;
		user->outbuf_memory = strmem->Realloc( user->outbuf, user->outbufsz, user->outbufalloc );
		user->outbuf = user->outbuf_memory + outbufoffset;
	}
	
	memcpy( user->outbuf_memory+user->outbufsz, str, len );
	user->outbufsz += len;
}
