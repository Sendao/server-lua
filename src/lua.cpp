#include "main.h"

sol::state lua;

void init_lua(void)
{
	lua.open_libraries(sol::lib::base);
}

void run_lua_file(char *fn)
{

}

void run_lua_command(char *cmd)
{

}