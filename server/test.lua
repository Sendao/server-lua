function OnLogin(player)
	Log("Player logged in")
	game.clock:SetDaySpeed(game.clock:GetSpeed())
end

function OnMidnight(obj)
	Log("Midnight")
	game.clock:SetHour(0)
end

function OnNoon(obj)
	Log("Noon")
	game.clock:SetHour(12)
end

function OnDawn(obj)
	Log("Dawn")
	game.clock:SetHour(6)
end

function OnDusk(obj)
	Log("Dusk")
	game.clock:SetHour(18)
end

function OnSlower(obj)
	Log("Slower")
	a = game.clock:GetSpeed()
	if a > 1 then
		game.clock:SetDaySpeed( a / 2 )
	end
end

function OnFaster(obj)
	Log("Faster")
	a = game.clock:GetSpeed()
	if a < 8192 then
		game.clock:SetDaySpeed( a * 2 )
	end
end

Log("Registering events " .. Event["Login"])
game.clock:SetDaySpeed(1024)
RegisterEvent( Event["Login"], OnLogin )
RegisterObjEvent( Event["Activate"], "Midnight", OnMidnight )
RegisterObjEvent( Event["Activate"], "Noon", OnNoon )
RegisterObjEvent( Event["Activate"], "Dawn", OnDawn )
RegisterObjEvent( Event["Activate"], "Dusk", OnDusk )
RegisterObjEvent( Event["Activate"], "Slower", OnSlower )
RegisterObjEvent( Event["Activate"], "Faster", OnFaster )
