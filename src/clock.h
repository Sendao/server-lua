#ifndef __CLOCK_H
#define __CLOCK_H

#include <ctime>

using namespace std;

class Clock
{
public:
	Clock();
	Clock(int secs);
	~Clock();

private:
	int Test;
	time_t timetime;
	tm tmx;
	float speed;
	long usecs;
	long last_secs, last_usecs;

public:
	static void InitialiseAPITable(void);

public:
	void UpdateTo(long secs, long usecs);

public: // API:	
	void SetFull(int newHour, int newMin, int newDay, int newMonth, int newYear);
	void SetClock(int newHour, int newMin);
	void SetHour(int newHour);
	void SetMin(int newMin);
	void SetCalendar(int newDay, int newMonth, int newYear);
	tuple<int, int> GetClock(void);
	tuple<int, int, int> GetCalendar(void);
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
	float GetDaySpeed(void);
	tuple<int, int, int> GetGameClockMinutesUntil();
};



#endif
