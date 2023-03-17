#include "main.h"

Object::Object()
{
	x=y=z=0;
	r0=r1=r2=0;
	last_update=0;
	name = NULL;
	spawned = false;
}

Object::~Object()
{
	if( name )
		strmem->Free(name, strlen(name)+1);
}

void InitialiseAPITable(void)
{
	sol::usertype<Object> object_type = lua.new_usertype<Object>("Object", sol::constructors<Object()>());
	
	object_type["x"] = &Object::x;
	object_type["y"] = &Object::y;
	object_type["z"] = &Object::z;
	object_type["r0"] = &Object::r0;
	object_type["r1"] = &Object::r1;
	object_type["r2"] = &Object::r2;
	object_type["scalex"] = &Object::scalex;
	object_type["scaley"] = &Object::scaley;
	object_type["scalez"] = &Object::scalez;
}
