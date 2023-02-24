#include "main.h"

const char *LOGFILE="log.txt";

#define MSL 1024

void setlog(const char *p)
{
	LOGFILE=p;
}

void lprintf(const char *fmt, ...)
{
    char buf[2*MSL];
    FILE *fp;
    va_list args;
    char timebuf[MSL];
    std::time_t timeData = std::time(NULL);
	strftime(timebuf, MSL, "%M:%S", std::localtime(&timeData));

    va_start(args, fmt);
    vsprintf(buf, fmt, args);
    va_end(args);

    strcat(buf, "\n");

    fp = fopen(LOGFILE, "a");

	fprintf(stdout, "%s %s", timebuf, buf);
	fflush(stdout);
	if( !fp ) {
		return;
	}
	fprintf(fp, "%s %s", timebuf, buf);
    fclose(fp);
}

uint16_t dbg_flags=0;//DBG_MAIN;

void debuglogflags( int16_t fla )
{
	if( fla < 0 ) {
		dbg_flags = dbg_flags&(~(uint16_t)(-fla));
	} else {
		dbg_flags = dbg_flags|fla;
	}
}

void lprintfx(uint16_t flx, const char *fmt, ...)
{
    char buf[2*MSL];
    FILE *fp;

    if( (dbg_flags&flx)==0 ) return;

    va_list args;
    va_start(args, fmt);
    vsprintf(buf, fmt, args);
    va_end(args);

    strcat(buf, "\n");

	char timebuf[MSL];
	std::time_t timeData = std::time(NULL);
	strftime(timebuf, MSL, "%M:%S", std::localtime(&timeData));

    fp = fopen(LOGFILE, "a");

	fprintf(stdout, "%s %s", timebuf, buf);
	fflush(stdout);
	if( !fp ) {
		return;
	}
	fprintf(fp, "%s %s", timebuf, buf);
    fclose(fp);
}


char *str_copy( const char *src )
{
	char *sv = strmem->Alloc( strlen(src) + 1 );
	strcpy(sv, src);
	return sv;
}
char *strdupsafe( const char *p )
{
	return str_copy(p);
}
char *strndupsafe( const char *p, int n )
{
	char *np = (char*)strmem->Alloc( n+1 );
	if( *p )
		strncpy( np, p, n );
	else
		np[0]='\0';
	np[n]='\0';
	return np;
}
char *str_toupper( const char *src )
{
	char *psrc, *ptr = strmem->Alloc(strlen(src)+1);
	psrc=ptr;
	while( *src ) {
		*ptr = toupper(*src);
		ptr++;
		src++;
	}
	*ptr = '\0';
	char *pCopy = str_copy(psrc);
	strmem->Free( psrc, strlen(psrc)+1 );
	return pCopy;
}
char *str_tolower( const char *src )
{
	char *psrc, *ptr = strmem->Alloc(strlen(src)+1);
	psrc=ptr;
	while( *src ) {
		*ptr = tolower(*src);
		ptr++;
		src++;
	}
	*ptr = '\0';
	char *pCopy = str_copy(psrc);
	strmem->Free( psrc, strlen(psrc)+1 );
	return pCopy;
}
bool isalphastr( const char *str )
{
	if(!str) return false;
	while( *str ) {
		if( !isalpha(*str) ) return false;
		str++;
	}
	return true;
}
char *substr( const char *src, int start, int end)
{
	if( end-start <= 0 ) return strdupsafe("");

	char *tgt = (char*)strmem->Alloc( (end-start) + 1 );
	strncpy( tgt, src+start, end-start);
	tgt[end-start]=0;
	return tgt;
}

char *str_replace( const char *needle, const char *newform, const char *haystack )
{
	char *newbuf=NULL, *tmpbuf;
	const char *op=haystack, *ptr;
	int needlelen=strlen(needle);

	while( (ptr = strstr( op, needle )) != NULL )
	{
		if( op != ptr ) {
			tmpbuf = strndupsafe( op, (int)(ptr-op) );
			newbuf = strmem->ReallocStr( newbuf, strlen(newbuf), strlen(tmpbuf)+strlen(newbuf) );
			strcat( newbuf, tmpbuf );
			strmem->Free(tmpbuf, strlen(tmpbuf));
		}
		newbuf = strmem->ReallocStr( newbuf, strlen(newbuf), strlen(newbuf)+strlen(newform) );
		strcat(newbuf, newform);
		op = ptr + needlelen;
	}

	if( *op ) {
		newbuf = strmem->ReallocStr( newbuf, strlen(newbuf), strlen(newbuf)+strlen(op) );
		strcat(newbuf, op);
	}

	return newbuf;
}

const char *findfirst( const char *pat, const char *tests[], int cnt, const char **resptr )
{
	const char *pats, *src;
	const char **p;// [cnt];
	int c;

	p = (const char**)strmem->Alloc(sizeof(char*) * cnt);

	for( c=0; c<cnt; c++ ) {
		p[c] = tests[c];
	}

	while( *pat )
	{
		for( c = 0; c < cnt; c++ )
		{
			if( *(p[c]) == *pat ) {
				pats=pat;
				src = p[c];
				if( resptr ) *resptr = src;
				while( *pats == *src ) {
					pats++;
					src++;
				}
				if (!*src) {
					free(p);
					return pat;
				}
			}
		}
		pat++;
	}
	if( resptr ) *resptr=NULL;
	free(p);
	return NULL;
}
const char *findfrom( const char *pat, const char *tests )
{
	const char *src;

	while( *pat )
	{
		for( src = tests; *src; src++ ) {
			if( *pat == *src )
				return pat;
		}
		pat++;
	}

	return NULL;
}
int mystrpos( const char *haystack, const char *needle, int start_offset )
{
	const char *hs, *nd, *hsr;
	int i=0;

	for( hs = haystack+start_offset, nd=needle; *hs; hs++,i++ ) {
		if( *hs == *needle ) {
			for( hsr=hs, nd=needle; *hsr; hsr++, nd++ ) {
				if( !*nd ) {
					return i+start_offset;
				} else if( *hsr != *nd ) {
					break;
				}
			}
		}
	}
	return -1;
}

void mystrim( char **pbuf )
{
	char *buf = *pbuf;
	char *pb;
	char *tb;
	bool changed=false;

	while( isspace(*buf) ) {
		changed=true;
		buf++;
	}
	pb = buf + strlen(buf);
	while( !*pb ) pb--;
	while( isspace(*pb) ) {
		*pb = '\0';
		pb--;
	}
	if( changed ) {
		tb = str_copy(buf);
		free(*pbuf);
		*pbuf = tb;
	}
}

char *sunpackf( char *buffer, const char *fmt, ... )
{
	va_list args;
	unsigned long *len, mylen;
	unsigned int *ilen;
	long *lptr;
	long long *llptr;
	int *iptr, ival;
	uint16_t *uiptr;
	int16_t i16val;
	unsigned int uilen;
	uint32_t *ulptr;
	uint64_t *ullptr;
	unsigned char *smol;
	float *pf, maxbase;
	char *c, **p, **s;
	union FloatChar {
		float f;
    	char  c[sizeof(float)];
	};

	va_start(args, fmt);
	while( *fmt )
	{
		switch( *fmt++ )
		{
			case 'b':
				smol = va_arg(args, unsigned char*);
				*smol = *buffer;
				buffer++;
				continue;
			case 'c':
				c = (char*)va_arg(args, char*);
				*c = *buffer;
				buffer++;
				continue;
			case 'i':
				iptr = va_arg(args, int*);
				*iptr = (int16_t)(*buffer << 8 | (int16_t)*(buffer+1) & 0xFF);
				buffer += 2;
				continue;
			case 'u':
				uiptr = (uint16_t*)va_arg(args, uint16_t*);
				*uiptr = (uint16_t)(*buffer << 8) | (uint16_t)*(buffer+1) & 0xFF;
				buffer += 2;
				continue;
			case 'l':
				lptr = va_arg(args, long*);
				*lptr = (int32_t)*buffer << 24 | (int32_t)*(buffer+1) << 16 | (int32_t)*(buffer+2) << 8 | (int32_t)*(buffer+3) & 0xFF;
				buffer += 4;
				continue;
			case 'm':
				ulptr = va_arg(args, uint32_t*);
				*ulptr = (uint32_t)*buffer << 24 | (uint32_t)*(buffer+1) << 16 | (uint32_t)*(buffer+2) << 8 | (uint32_t)*(buffer+3) & 0xFF;
				buffer += 4;
				continue;
			case 'L':
				llptr = va_arg(args, long long*);
				// note we have to convert first 4 to long long. others don't have to be.
				*ullptr = (int64_t)(((int64_t)*(buffer)&0xff) << 56) |
						  (int64_t)(((int64_t)*(buffer+1)&0xff) << 48) |
						  (int64_t)(((int64_t)*(buffer+2)&0xff) << 40) |
						  (int64_t)(((int64_t)*(buffer+3)&0xff) << 32) |
						  (int64_t)(((int64_t)*(buffer+4)&0xff) << 24) |
						  (int64_t)(((int64_t)*(buffer+5)&0xff) << 16) |
						  (int64_t)(((int64_t)*(buffer+6)&0xff) << 8) |
						  (int64_t)(((int64_t)*(buffer+7)&0xff) );
				buffer += 8;
				continue;
			case 'M':
				ullptr = va_arg(args, uint64_t*);
				*ullptr = (uint64_t)(((uint64_t)*(buffer)&0xff) << 56) |
						  (uint64_t)(((uint64_t)*(buffer+1)&0xff) << 48) |
						  (uint64_t)(((uint64_t)*(buffer+2)&0xff) << 40) |
						  (uint64_t)(((uint64_t)*(buffer+3)&0xff) << 32) |
						  (uint64_t)(((uint64_t)*(buffer+4)&0xff) << 24) |
						  (uint64_t)(((uint64_t)*(buffer+5)&0xff) << 16) |
						  (uint64_t)(((uint64_t)*(buffer+6)&0xff) << 8) |
						  (uint64_t)(((uint64_t)*(buffer+7)&0xff) );
				buffer += 8;
				continue;
			case 'F': // short
				i16val = (int16_t)(*buffer << 8 | *(buffer+1) & 0xFF);
				maxbase = va_arg(args, double);
				pf = (float*)va_arg(args, float*);
				*pf = (float)i16val / ((float)32767 / maxbase);
				buffer += 2;
				continue;
			case 'f':
				FloatChar x;
				x.c[0] = *buffer;
				x.c[1] = *(buffer+1);
				x.c[2] = *(buffer+2);
				x.c[3] = *(buffer+3);
				pf = (float*)va_arg(args, float*);
				*pf = x.f;
				buffer += 4;
				continue;
			case 's':
				mylen = *buffer << 8 | *(buffer+1);
				buffer += 2;/*
				lprintf("str at %s, mylen: %d", buffer, mylen);
				for( int j = 0; j < mylen; j++ ) {
					lprintf("pt %c %d", *(buffer+j), (int)*(buffer+j));
				}
				lprintf("mylen now: %d", mylen);*/
				s = (char**)va_arg(args, char**);
				*s = strmem->Alloc(mylen+1);
				if( mylen != 0 ) {
					strncpy(*s, buffer, mylen);
					buffer += mylen;
				}
				(*s)[mylen] = '\0';
				continue;
			case 'V':
				len = va_arg(args, unsigned long*);
				*len = (uint64_t)(*buffer << 24 | *(buffer+1) << 16 | *(buffer+2) << 8 | *(buffer+3) & 0xFF);
				buffer += 4;
				p = (char**)va_arg(args, char**);
				if( *len != 0 ) {
					*p = strmem->Alloc(*len+1);
					memcpy(*p, buffer, *len);
					buffer += *len;
				} else {
					*p = NULL;
				}
				continue;
			case 'v': // use an int for length
				ilen = va_arg(args, unsigned int*);
				*ilen = (uint16_t)( *buffer<<8 | *(buffer+1) & 0xFF );
				buffer += 2;
				p = (char**)va_arg(args, char**);
				if( *ilen != 0 ) {
					*p = strmem->Alloc(*ilen+1);
					memcpy(*p, buffer, *ilen);
					buffer += *ilen;
				} else {
					*p = NULL;
				}
				continue;
			case 'p': // use unsigned char for length
				smol = (unsigned char*)va_arg(args, unsigned char*);
				*smol = *(unsigned char*)buffer;
				buffer ++;
				p = (char**)va_arg(args, char**);
				if( *smol != 0 ) {
					*p = strmem->Alloc(*smol);
					memcpy(*p, buffer, *smol);
					buffer += *smol;
				} else {
					*p = NULL;
				}
				continue;
			case 'x': // use external length
				uilen = va_arg(args, int);
				p = (char**)va_arg(args, char**);
				if( uilen != 0 ) {
					*p = strmem->Alloc(uilen);
					memcpy(*p, buffer, uilen);
					buffer += uilen;
				} else {
					*p = NULL;
				}
		}
	}
	va_end(args);
	return buffer;
}
long spackf( char **target, unsigned long *alloced, const char *fmt, ... )
{
	*alloced = 32;
	char *buf = strmem->Alloc(*alloced);
	char *buffer = buf;
	*target = buf;
	long bufsz=0;
	union FloatChar {
		float f;
    	char  c[sizeof(float)];
	};

	va_list args;
	long l;
	long long ll;
	int16_t i16;
	int i;
	uint16_t u;
	uint32_t ul;
	uint64_t ull;
	unsigned long len;
	unsigned int ilen;
	unsigned char smol;
	float maxbase, fv;
	char c, *p, *s;

	va_start(args, fmt);
	while( *fmt )
	{
		switch( *fmt++ )
		{
			case 'c':
				c = (char)va_arg(args, int);
				while( bufsz+1 >= *alloced ) {
					*target =buf = strmem->Realloc(buf, *alloced, *alloced*2);
					*alloced *= 2;
					buffer = buf + bufsz;
				}
				*buffer = c;
				buffer++;
				bufsz++;
				continue;
			case 'f':
				FloatChar x;
				x.f = (float)va_arg(args, double);
				while( bufsz+sizeof(float) >= *alloced ) {
					*target = buf = strmem->Realloc(buf, *alloced, *alloced*2);
					*alloced *= 2;
					buffer = buf + bufsz;
				}
				*(buffer) = x.c[0];
				*(buffer+1) = x.c[1];
				*(buffer+2) = x.c[2];
				*(buffer+3) = x.c[3];
				buffer += sizeof(float);
				bufsz += sizeof(float);
				continue;
			case 'F':
				maxbase = (float)va_arg(args, double);
				fv = (float)va_arg(args, double);
				i16 = (int16_t)roundf( fv * ( 32767.0/maxbase ) );
				if( i16 == 0 && fv != 0.0 ) {
					lprintf("i16==0, fv=%f", fv);
				}
				*(buffer) = (i16 >> 8);
				*(buffer+1) = (i16 & 0xFF);
				buffer += 2;
				bufsz += 2;
				continue;
			case 'i':
				i = (int)va_arg(args, int);
				while( bufsz+2 >= *alloced ) {
					*target = buf = strmem->Realloc(buf, *alloced, *alloced*2);
					*alloced *= 2;
					buffer = buf + bufsz;
				}
				*buffer = (i >> 8) & 0xFF;
				*(buffer+1) = (i) & 0xFF;
				buffer += 2;
				bufsz += 2;
				continue;
			case 'u':
				u = (uint16_t)va_arg(args, unsigned int);
				while( bufsz+2 >= *alloced ) {
					*target = buf = strmem->Realloc(buf, *alloced, *alloced*2);
					*alloced *= 2;
					buffer = buf + bufsz;
				}
				*buffer = (u >> 8) & 0xFF;
				*(buffer+1) = (u) & 0xFF;
				buffer += 2;
				bufsz += 2;
				continue;
			case 'l':
				l = va_arg(args, long);
				while( bufsz+sizeof(long) >= *alloced ) {
					*target = buf = strmem->Realloc(buf, *alloced, *alloced*2);
					*alloced *= 2;
					buffer = buf + bufsz;
				}
				*(buffer) = (l >> 24) & 0xFF;
				*(buffer+1) = (l >> 16) & 0xFF;
				*(buffer+2) = (l >> 8) & 0xFF;
				*(buffer+3) = (l) & 0xFF;
				buffer += 4;
				bufsz += 4;
				continue;
			case 'm':
				ul = (uint32_t)va_arg(args, unsigned long);
				while( bufsz+4 >= *alloced ) {
					*target = buf = strmem->Realloc(buf, *alloced, *alloced*2);
					*alloced *= 2;
					buffer = buf + bufsz;
				}
				*(buffer) = (ul >> 24) & 0xFF;
				*(buffer+1) = (ul >> 16) & 0xFF;
				*(buffer+2) = (ul >> 8) & 0xFF;
				*(buffer+3) = (ul) & 0xFF;
				buffer += 4;
				bufsz += 4;
				continue;
			case 'L':
				ll = va_arg(args, long long);
				while( bufsz+8 >= *alloced ) {
					*target = buf = strmem->Realloc(buf, *alloced, *alloced*2);
					*alloced *= 2;
					buffer = buf + bufsz;
				}
				*(buffer) = (ll >> 56) & 0xFF;
				*(buffer+1) = (ll >> 48) & 0xFF;
				*(buffer+2) = (ll >> 40) & 0xFF;
				*(buffer+3) = (ll >> 32) & 0xFF;
				*(buffer+4) = (ll >> 24) & 0xFF;
				*(buffer+5) = (ll >> 16) & 0xFF;
				*(buffer+6) = (ll >> 8) & 0xFF;
				*(buffer+7) = (ll) & 0xFF;
				buffer += 8;
				bufsz += 8;
				continue;
			case 'M':
				ull = (uint64_t)va_arg(args, unsigned long long);
				while( bufsz+8 >= *alloced ) {
					*target = buf = strmem->Realloc(buf, *alloced, *alloced*2);
					*alloced *= 2;
					buffer = buf + bufsz;
				}
				*(buffer) = (ull >> 56) & 0xFF;
				*(buffer+1) = (ull >> 48) & 0xFF;
				*(buffer+2) = (ull >> 40) & 0xFF;
				*(buffer+3) = (ull >> 32) & 0xFF;
				*(buffer+4) = (ull >> 24) & 0xFF;
				*(buffer+5) = (ull >> 16) & 0xFF;
				*(buffer+6) = (ull >> 8) & 0xFF;
				*(buffer+7) = (ull) & 0xFF;
				buffer += 8;
				bufsz += 8;
				continue;
			case 's':
				s = va_arg(args, char*);
				if( !s || !*s )
					ilen = 0;
				else
					ilen = (unsigned int)strlen(s);
				while( bufsz+2+ilen >= *alloced ) {
					*target = buf = strmem->Realloc(buf, *alloced, *alloced*2);
					*alloced *= 2;
					buffer = buf + bufsz;
				}
				*buffer = (ilen >> 8) & 0xFF;
				*(buffer+1) = (ilen) & 0xFF;
				buffer += 2;
				bufsz += 2;
				if( ilen != 0 ) {
					strncpy(buffer, s, ilen);
					buffer += ilen;
					bufsz += ilen;
				}
				continue;
			case 'V':
				len = va_arg(args, unsigned long);
				p = va_arg(args, char*);
				while( bufsz+4+len >= *alloced ) {
					*target = buf = strmem->Realloc(buf, *alloced, *alloced*2);
					*alloced *= 2;
					buffer = buf + bufsz;
				}
				*(buffer) = (len >> 24) & 0xFF;
				*(buffer+1) = (len >> 16) & 0xFF;
				*(buffer+2) = (len >> 8) & 0xFF;
				*(buffer+3) = (len) & 0xFF;
				buffer += 4;
				bufsz += 4;
				if( len != 0 ) {
					memcpy(buffer, p, len);
					buffer += len;
					bufsz += len;
				}
				continue;
			case 'v': // use an int instead
				ilen = va_arg(args, unsigned int);
				p = va_arg(args, char*);
				while( bufsz+2+ilen >= *alloced ) {
					*target = buf = strmem->Realloc(buf, *alloced, *alloced*2);
					*alloced *= 2;
					buffer = buf + bufsz;
				}
				*buffer = (ilen >> 8) & 0xFF;
				*(buffer+1) = (ilen) & 0xFF;
				//lprintf("Length bytes: %d %d for %d", (int)*buffer, (int)*(buffer+1), ilen);

				buffer += 2;
				bufsz += 2;
				if( ilen != 0 ) {
					memcpy(buffer, p, ilen);
					buffer += ilen;
					bufsz += ilen;
				}
				continue;
			case 'p': // use an unsigned char for size
				smol = (unsigned char)va_arg(args, int); // note: va_arg requires this to be int, not unsinged char
				p = va_arg(args, char*);
				while( bufsz+sizeof(unsigned char)+smol >= *alloced ) {
					*target = buf = strmem->Realloc(buf, *alloced, *alloced*2);
					*alloced *= 2;
					buffer = buf + bufsz;
				}
				*(unsigned char*)buffer = smol;
				buffer ++;
				bufsz ++;
				if( smol != 0 ) {
					memcpy(buffer, p, smol);
					buffer += smol;
					bufsz += smol;
				}
				continue;
			case 'x': // external length. do not add length to stream.
				ilen = va_arg(args, unsigned int);
				memcpy(buffer, p, ilen);
				buffer += ilen;
				bufsz += ilen;
				continue;
		}
	}
	va_end(args);

	return bufsz;
}

long myatoi( const char *src, long *offset )
{
	long val=0;
	*offset=0;

	while( isdigit(*src) )
	{
		val *= 10;
		switch(*src){
			case '1':val++;break;
			case '2':val+=2;break;
			case '3':val+=3;break;
			case '4':val+=4;break;
			case '5':val+=5;break;
			case '6':val+=6;break;
			case '7':val+=7;break;
			case '8':val+=8;break;
			case '9':val+=9;break;
		}
		(*offset)++;
		src++;
	}
	return val;
}

long stringhash( const char *ptr )
{
	long key=0;

	while(*ptr) {
		key += (int)*ptr;
		ptr++;
	}
	return key;
}

unsigned int crc32( const char *ptr, int len )
{
	int i;
	unsigned int crc=0;

	for( i=0; i<len; i++ ) {
		crc = (crc + (unsigned char)*(ptr+i)) & 0xFFFFFFFF;
	}
	return crc;
}
