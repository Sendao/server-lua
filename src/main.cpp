#include "main.h"

void mainloop(void);

Game *game;

int main(int ac, char *av[])
{
	init_pools();
	
	game = (Game*)halloc(sizeof(Game));
	new(game) Game();

	init_commands();
	init_lua();

	lua["game"] = game;

	GetFileList();

	//! Process arguments

	// Initialize
	setlog("server.log");
	InitSocket(2038);

	// Main loop
	game->mainloop();

	// End
	ExitSocket();

	return 0;
}

struct timeval now;
time_t currentTime;

/* We convert from microseconds to milliseconds */
#if defined(_MSC_VER) || defined(__MINGW32__)
int smalltimeofday(struct timeval* tp, void* tzp) {
	uint64_t ms = std::chrono::system_clock::now().time_since_epoch() / std::chrono::milliseconds(1);
    tp->tv_sec = ms / (uint64_t)1000;
    tp->tv_usec = ms % (uint64_t)1000;
    /* 0 indicates that the call succeeded. */
    return 0;
}
#else
int smalltimeofday(struct timeval* tp, void* tzp ) {
	gettimeofday(tp, tzp);
	tp->tv_usec = tp->tv_usec / 1000;
	return 0;
}
#endif

inline long compare_usec( struct timeval *high, struct timeval *low ) {
	return (((high->tv_sec-low->tv_sec)*1000)+(high->tv_usec-low->tv_usec));
}

