#include "main.h"

Object::Object()
{
	x=y=z=0;
	r0=r1=r2=0;
	last_update=0;
}

Object::~Object()
{
	if( name )
		strmem->Free(name, strlen(name)+1);
}
