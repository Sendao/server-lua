#include "main.h"

Object::Object()
{
	x=y=z=0;
	r1=r2=r3=0;
	r0=1;
	last_update=0;
}

Object::~Object()
{
	strmem->Free(name, strlen(name)+1);
}
