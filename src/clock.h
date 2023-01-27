#ifndef __CLOCK_H
#define __CLOCK_H

#include <ctime>

class Clock
{
public:
	Clock();
	Clock(int secs);
	~Clock();

private:
	int Test;
	time_t time;
	tm tmx;

public:
	void InitialiseAPITable(void);
	
	void SetFull(int newHour, int newMin, int newDay, int newMonth, int newYear);
	void SetClock(int newHour, int newMin);
	void SetHour(int newHour);
	void SetMin(int newMin);
	void SetCalendar(int newDay, int newMonth, int newYear);
	std::tuple<int, int> Clock::GetClock(void);
	std::tuple<int, int, int> Clock::GetCalendar(void);
	int GetDay(void);
	int GetWeek(void);
	int GetWeekDay(void);
	int GetMonth(void);
	int GetYear(void);
	void PassDays(int days);
	void SetTotalDaySec(int secs);
	void SetDaySpeed(float speed);
	int GetGameClockMinutesUntil();

}



#endif
