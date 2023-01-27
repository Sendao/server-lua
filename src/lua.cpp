#include "main.h"


void init_lua(void)
{
	sol::state lua;

	lua.open_libraries(sol::lib::base);

}
