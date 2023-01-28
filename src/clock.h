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
	float speed;

public:
	void InitialiseAPITable(void);
	
	void SetFull(int newHour, int newMin, int newDay, int newMonth, int newYear);
	void SetClock(int newHour, int newMin);
	void SetHour(int newHour);
	void SetMin(int newMin);
	void SetCalendar(int newDay, int newMonth, int newYear);
	std::tuple<int, int> GetClock(void);
	std::tuple<int, int, int> GetCalendar(void);
	int GetHour(void);
	int GetMin(void);
	int GetDay(void);
	int GetWeek(void);
	int GetWeekDay(void);
	int GetMonth(void);
	int GetYear(void);
	void PassDays(int days);
	void SetTotalDaySec(int secs);
	void SetDaySpeed(float speed);
	std::tuple<int, int, int> GetGameClockMinutesUntil();
};



#endif
