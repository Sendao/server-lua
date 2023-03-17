#include "main.h"

Npc::Npc()
{
	x=y=z=0;
	r0=r1=0;
	r2=-1;
	scalex=scaley=scalez=1;
	recipe = NULL;
	hasplatform = false;
	platid = 0;
	name = NULL;
}

Npc::~Npc()
{
	if( recipe ) {
		strmem->Free( recipe, strlen(recipe)+1 );
	}
}

