#include "main.h"
#include <dirent.h>
#include <sys/stat.h>

void GetFileList( void )
{
	struct dirent *ent;
	DIR *dirp;
	FILE *f;
	FileInfo *fi;
	struct stat statbuf;
	char fn[256];

	dirp = opendir("../server");
	if( !dirp ) {
		lprintf("Couldn't find server directory: no custom files.");
		return;
	}

	while( ent=readdir(dirp) ) {
		if( strcmp(ent->d_name, ".") == 0 || strcmp(ent->d_name, "..") == 0 )
			continue;
		sprintf(fn, "../server/%s", ent->d_name);
		if( stat(fn, &statbuf) == -1 ) continue;

		fi = (FileInfo*)halloc(sizeof(FileInfo));
		fi->name = str_copy( ent->d_name );
		fi->mtime = statbuf.st_mtime;
		fi->size = statbuf.st_size;

		if( fi->size < 256*1024*1024 ) {
			fi->contents = (char*)halloc(fi->size);
			if( strstr(ent->d_name, ".lua") ) {
				lprintf("Loading lua file %s", ent->d_name);
				lua.script_file(fn);
				f = fopen( fn, "r" );
			} else {
				f = fopen( fn, "rb" );
			}
			if( f ) {
				fread( fi->contents, 1, fi->size, f );
				fclose( f );
				f=NULL;
			} else {
				lprintf("Error reading file %s(%s)", fi->name, fn);
				memset( fi->contents, 0, fi->size);
			}
		} else {
			fi->contents = NULL;
		}
		game->files[ent->d_name] = fi;
		lprintf("Record file %s", ent->d_name);
	}

}
