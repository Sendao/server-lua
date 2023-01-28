#include "main.h"

sol::state lua;

void init_lua(void)
{
	lua.open_libraries(sol::lib::base);

}
