class_name CombatTypes
extends RefCounted
## Общие типы дуэли: зоны удара/защиты и исходы обмена ударами.
## Только данные — экземпляры не создаются, обращаться как CombatTypes.Zone.HEAD.

## Зона удара/защиты. Во время дуэли выбирается удержанием W/A/D/S.
enum Zone {
	NONE = -1, # зона не выбрана / призрак ничего не закрывает
	HEAD,      # голова — W
	ARM,       # рука — A
	BODY,      # корпус — D
	LEGS,      # ноги — S
}

## Чем закончился удар призрака по игроку.
enum DefenseResult {
	HIT,     # блока нет или не та зона — минус Тепло, минус концентрация
	BLOCKED, # зона верная, блок поднят заранее
	PARRIED, # зона верная, блок поднят в последний момент — больше концентрации
}

## Чем закончилась атака игрока.
enum AttackResult {
	HIT,       # зона открыта — плюс концентрация
	DEFLECTED, # призрак закрыл эту зону — минус концентрация
	IGNORED,   # призрак сломлен или дуэль не идёт
}

const ZONE_KEYS: Dictionary[Zone, String] = {
	Zone.NONE: "COMBAT_ZONE_NONE",
	Zone.HEAD: "COMBAT_ZONE_HEAD",
	Zone.ARM: "COMBAT_ZONE_ARM",
	Zone.BODY: "COMBAT_ZONE_BODY",
	Zone.LEGS: "COMBAT_ZONE_LEGS",
}

## Все настоящие зоны (без NONE) — для случайного выбора и обхода в UI.
const ALL_ZONES: Array[Zone] = [Zone.HEAD, Zone.ARM, Zone.BODY, Zone.LEGS]


## Локализованное название зоны для UI.
static func zone_name(zone: Zone) -> String:
	return TranslationServer.translate(ZONE_KEYS.get(zone, ZONE_KEYS[Zone.NONE]))


static func random_zone(exclude: Zone = Zone.NONE) -> Zone:
	var options: Array[Zone] = []
	for zone: Zone in ALL_ZONES:
		if zone != exclude:
			options.append(zone)
	return options.pick_random()
