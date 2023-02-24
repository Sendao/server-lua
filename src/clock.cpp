#include "main.h"


Clock::Clock()
{
	usecs = 0;
	speed = 1;
	time( &this->timetime );
	struct tm *tmptr = localtime( &this->timetime );
	memcpy( &tmx, tmptr, sizeof(tm) );
}
Clock::Clock( int secs )
{

}

Clock::~Clock()
{

}


void Clock::InitialiseAPITable(void)
{
	sol::usertype<Clock> clock_type = lua.new_usertype<Clock>("Clock",
		sol::constructors<Clock(), Clock(int)>());
	
	clock_type["speed"] = &Clock::speed;
	clock_type["SetFull"] = &Clock::SetFull;
	clock_type["SetClock"] = &Clock::SetClock;
	clock_type["SetHour"] = &Clock::SetHour;
	clock_type["SetMin"] = &Clock::SetMin;
	clock_type["SetCalendar"] = &Clock::SetCalendar;

	clock_type["GetClock"] = &Clock::GetClock;
	clock_type["GetCalendar"] = &Clock::GetCalendar;
	clock_type["GetHour"] = &Clock::GetHour;
	clock_type["GetMin"] = &Clock::GetMin;
	clock_type["GetDay"] = &Clock::GetDay;
	clock_type["GetWeekDay"] = &Clock::GetWeekDay;
	clock_type["GetWeek"] = &Clock::GetWeek;
	clock_type["GetMonth"] = &Clock::GetMonth;
	clock_type["GetYear"] = &Clock::GetYear;
	clock_type["GetSpeed"] = &Clock::GetDaySpeed;
	clock_type["PassDays"] = &Clock::PassDays;
	clock_type["SetTotalDaySec"] = &Clock::SetTotalDaySec;
	clock_type["SetDaySpeed"] = &Clock::SetDaySpeed;
	clock_type["GetGameClockMinutesUntil"] = &Clock::GetGameClockMinutesUntil;
	clock_type["Test"] = &Clock::Test;
}


void Clock::SetFull(int newHour, int newMin, int newDay, int newMonth, int newYear)
{
	tmx.tm_sec = 0;
	tmx.tm_min = newMin;
	tmx.tm_hour = newHour;
	tmx.tm_mday = newDay;
	tmx.tm_mon = newMonth-1;
	tmx.tm_year = newYear - 1900;

	timetime = mktime(&tmx);
}

void Clock::SetClock(int newHour, int newMin)
{
	tmx.tm_sec = 0;
	tmx.tm_min = newMin;
	tmx.tm_hour = newHour;

	timetime = mktime(&tmx);
}

void Clock::SetHour(int newHour)
{
	tmx.tm_hour = newHour;

	timetime = mktime(&tmx);

	char *buf=NULL;
	long bufsz;
	u_long alloced = 0;

	lprintf("Set clock hour to %d", newHour);

	bufsz = spackf(&buf, &alloced, "u", (uint16_t)newHour);
	game->SendMsg( CCmdClockSetHour, bufsz, buf );
	strmem->Free( buf, alloced );
}

void Clock::SetMin(int newMin)
{
	tmx.tm_sec = 0;
	tmx.tm_min = newMin;

	timetime = mktime(&tmx);
}

void Clock::SetCalendar(int newDay, int newMonth, int newYear)
{
	tmx.tm_mday = newDay;
	tmx.tm_mon = newMonth-1;
	tmx.tm_year = newYear - 1900;

	timetime = mktime(&tmx);
}

std::tuple<int, int> Clock::GetClock(void)
{
	int hours = tmx.tm_hour;
	int mins = tmx.tm_min;
	return std::make_tuple( hours, mins );
}

std::tuple<int, int, int> Clock::GetCalendar(void)
{
	int mday = tmx.tm_mday;
	int month = tmx.tm_mon+1;
	int year = tmx.tm_year + 1900;
	return std::make_tuple( mday, month, year );
}

int Clock::GetHour( void )
{
	return tmx.tm_hour;
}

int Clock::GetMin( void )
{
	return tmx.tm_min;
}

int Clock::GetDay( void )
{
	return tmx.tm_mday;
}

int Clock::GetWeek( void )
{
	if( tmx.tm_mday <= tmx.tm_wday ) return 0;

	return floor((tmx.tm_mday-tmx.tm_wday)/7)+1;
}

int Clock::GetWeekDay( void )
{
	return tmx.tm_wday;
}
int Clock::GetMonth( void )
{
	return tmx.tm_mon + 1;
}
int Clock::GetYear( void )
{
	return tmx.tm_year + 1900;
}
void Clock::PassDays(int days)
{
	tmx.tm_mday += days;

	timetime = mktime(&tmx);
}
void Clock::SetTotalDaySec(int secs)
{
	int hour = floor(secs/3600);
	int min = floor((secs-hour*3600)/60);
	int sec = (secs - hour*3600) - (min*60);

	tmx.tm_hour = hour;
	tmx.tm_min = min;
	tmx.tm_sec = sec;

	timetime = mktime(&tmx);

	char *buf=NULL;
	long bufsz;
	u_long alloced = 0;

	bufsz = spackf(&buf, &alloced, "u", (uint16_t)secs);
	game->SendMsg( CCmdClockSetTotalDaySec, bufsz, buf );
	strmem->Free( buf, alloced );
}
float Clock::GetDaySpeed()
{
	return this->speed;
}
void Clock::SetDaySpeed(float speed)
{
	this->speed = speed;

	char *buf=NULL;
	long bufsz;
	u_long alloced = 0;

	bufsz = spackf(&buf, &alloced, "f", speed);
	game->SendMsg( CCmdClockSetDaySpeed, bufsz, buf );
	strmem->Free( buf, alloced );
	lprintf("SetDaySpeed(%f)", speed);
}
std::tuple<int, int, int> Clock::GetGameClockMinutesUntil()
{
	return std::make_tuple( 0, 0, 0 );
}

void Clock::UpdateTo( long secs, long usecs )
{
	this->usecs += ((secs-this->last_secs)*1000 + (usecs-this->last_usecs)) * speed;

	if( this->usecs > 1000 ) {
		int newsecs = floor(this->usecs/1000);
		this->usecs -= newsecs*1000;
		tmx.tm_sec += newsecs;

		timetime = mktime(&tmx);
	}

	this->last_secs = secs;
	this->last_usecs = usecs;
}
