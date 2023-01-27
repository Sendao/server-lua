#include "main.h"
#include "clock.h"


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

	time = mktime(&tmx);
}

void Clock::SetClock(int newHour, int newMin)
{
	tmx.tm_sec = 0;
	tmx.tm_min = newMin;
	tmx.tm_hour = newHour;

	time = mktime(&tmx);
}

void Clock::SetHour(int newHour)
{
	tmx.tm_hour = newHour;

	time = mktime(&tmx);
}

void Clock::SetMin(int newMin)
{
	tmx.tm_sec = 0;
	tmx.tm_min = newMin;

	time = mktime(&tmx);
}

void Clock::SetCalendar(int newDay, int newMonth, int newYear)
{
	tmx.tm_mday = newDay;
	tmx.tm_mon = newMonth-1;
	tmx.tm_year = newYear - 1900;

	time = mktime(&tmx);
}

std::tuple<int, int> Clock::GetClock(void)
{
	return std::make_tuple<int, int>( tmx.tm_hour, tmx.tm_min );
}

std::tuple<int, int, int> Clock::GetCalendar(void)
{
	return std::make_tuple<int, int, int>( tmx.tm_mday, tmx.tm_mon+1, tmx.tm_year + 1900 );
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

	time = mktime(&tmx);
}
void Clock::SetTotalDaySec(int secs)
{
	int hour = floor(secs/3600);
	int min = floor((secs-hours*3600)/60);
	int sec = (secs - hours*3600) - (mins*60);

	tmx.tm_hour = hour;
	tmx.tm_min = min;
	tmx.tm_sec = sec;

	time = mktime(&tmx);
}
void Clock::SetDaySpeed(float speed)
{
	this->speed = speed;
}
int Clock::GetGameClockMinutesUntil()
{
	return std::make_tuple<int, int, int>( 0, 0, 0 );
}
