#ifndef __SYSTEM_H
#define __SYSTEM_H

typedef struct _FileInfo FileInfo;
#include <time.h>

struct _FileInfo
{
	char *name;
	unsigned long long size;
	unsigned long long mtime;
	char *contents;
};

#endif