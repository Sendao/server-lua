#include "main.h"

const char *LOGFILE="log.txt";

#define MSL 1024

void qexpand( char **buf, const char *add );

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
			qexpand( &newbuf, tmpbuf );
			free(tmpbuf);
		}
		qexpand( &newbuf, newform );
		op = ptr + needlelen;
	}

	if( *op )
		qexpand( &newbuf, op );

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


void qexpand( char **buf, const char *add )
{
	char *tptr;

	if( !buf )
		return;

	if( !*buf ) {
		tptr = strmem->Alloc( strlen(add) + 1 );
		strcpy(tptr, add);
	} else if( !**buf ) {
		strmem->Free( *buf, strlen(*buf)+1 );
		tptr = strmem->Alloc( strlen(add) + 1 );
		strcpy(tptr, add);
	} else {
		tptr = strmem->Alloc( strlen(*buf) + strlen(add) + 1 );
		strcpy(tptr, *buf);
		strcat(tptr, add);
		strmem->Free( *buf, strlen(*buf)+1 );
	}

	*buf = tptr;

	return;
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

void funpackf( FILE *fp, const char *fmt, ... )
{
	va_list args;
	long *len, mylen;
	char *c, **p, **s;

	va_start(args, fmt);
	while( *fmt )
		switch( *fmt++ )
		{
			case 'c':
				c = (char*)va_arg(args, int*);
				fread( c, 1, 1, fp );
				continue;
			case 'l':
				len = (long*)va_arg(args, long*);
				fread( len, sizeof(long), 1, fp );
				continue;
			case 's':
				fread( &mylen, sizeof(long), 1, fp );
				s = (char**)va_arg(args, char**);
				*s = strmem->Alloc(mylen+1);
				if( mylen != 0 )
					fread( *s, sizeof(char), mylen, fp );
				s[mylen] = 0;
				continue;
			case 'p': case 'v':
				len = (long*)va_arg(args, long*);
				fread( len, sizeof(long), 1, fp );
				p = (char**)va_arg(args, char**);
				*p = strmem->Alloc(*len+1);
				if( *len != 0 )
					fread( *p, sizeof(char), *len, fp );
				p[*len] = 0;
				continue;
		}
	va_end(args);
}
void funpackd( int fd, const char *fmt, ... )
{
	va_list args;
	long *len, mylen;
	char *c, **p, **s;

	va_start(args, fmt);
	while( *fmt )
		switch( *fmt++ )
		{
			case 'c':
				c = (char*)va_arg(args, int*);
				read( fd, c, 1 );
				continue;
			case 'l':
				len = (long*)va_arg(args, long*);
				read( fd, len, sizeof(long));
				continue;
			case 's':
				s = (char**)va_arg(args, char**);
				read( fd, &mylen, sizeof(long) );
				*s = strmem->Alloc(mylen+1);
				if( mylen != 0 )
					read( fd, *s, mylen );
				s[mylen] = 0;
				continue;
			case 'p': case 'v':
				len = (long*)va_arg(args, long*);
				read( fd, len, sizeof(long) );
				p = (char**)va_arg(args, char**);
				*p = strmem->Alloc(*len+1);
				if( *len != 0 )
					read( fd, *p, *len );
				p[*len] = 0;
				continue;
		}
	va_end(args);
}
void fpackf( FILE *fp, const char *fmt, ... )
{
	va_list args;
	long len;
	char c, *p, *s;

	va_start(args, fmt);

	while( *fmt )
	{
		switch( *fmt++ )
		{
			case 'c':
				c = (char)va_arg(args, int);
				fwrite( &c, 1, 1, fp );
				continue;
			case 'l':
				len = va_arg(args, long);
				fwrite( &len, sizeof(long), 1, fp );
				continue;
			case 's':
				s = va_arg(args, char*);
				if( !s || !*s ) {
					len=0;
					fwrite( &len, sizeof(long), 1, fp );
				} else {
					len = (unsigned long)strlen(s);
					fwrite( &len, sizeof(long), 1, fp );
					fwrite( s, sizeof(char), len, fp );
				}
				continue;
			case 'v': case 'p':
				len = va_arg(args, long);
				p = va_arg(args, char*);
				fwrite( &len, sizeof(long), 1, fp );
				if( len != 0 )
					fwrite( p, sizeof(char), len, fp );
				continue;
		}
	}
	va_end(args);
}
void fpackd( int fd, const char *fmt, ... )
{
	va_list args;
	long len;
	char c, *p, *s;

	va_start(args, fmt);

	while( *fmt )
	{
		switch( *fmt++ )
		{
			case 'c':
				c = (char)va_arg(args, int);
				write( fd, &c, 1 );
				continue;
			case 'l':
				len = va_arg(args, long);
				write( fd, &len, sizeof(long) );
				continue;
			case 's':
				s = va_arg(args, char*);
				if( !s || !*s ) {
					len=0;
					write( fd, &len, sizeof(long) );
				} else {
					len = (unsigned long)strlen(s);
					write( fd, &len, sizeof(long) );
					write( fd, s, len );
				}
				continue;
			case 'v': case 'p':
				len = va_arg(args, long);
				p = va_arg(args, char*);
				write( fd, &len, sizeof(long) );
				if( len != 0 )
					write( fd, p, len );
				continue;
		}
	}
	va_end(args);
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
