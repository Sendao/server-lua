#include "main.h"

Animation::Animation()
{
	x = z = 0;
	pitch = yaw = 0;
	speed = 0;
	height = 0;
	moving = aiming = false;
	moveSetID = 0;
	abilityIndex = 0;
	abilityInt = 0;
	abilityFloat = 0;
}

Animation::~Animation()
{
	params.clear();
}
