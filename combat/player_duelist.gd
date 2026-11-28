class_name PlayerDuelist
extends Node
## Боец со стороны игрока: поднимает блок, наносит удары, копит концентрацию и тратит её на контратаку.
## Удары призрака приходят в receive_strike() — здесь решается, блок это, парирование или попадание.
## Ввод берёт из DuelInput, здоровье — из Warmth (необязательно), шкалу — из Concentration.

## Атака игрока разрешена.
signal attacked(zone: CombatTypes.Zone, result: CombatTypes.AttackResult)
## Удар призрака по игроку разрешён.
signal defended(zone: CombatTypes.Zone, result: CombatTypes.DefenseResult)
## Блок поднят в зону (NONE — опущен).
signal guard_changed(zone: CombatTypes.Zone)
signal countered
## Контратака нажата, но шкала не заполнена.
signal counter_failed

@export var input: DuelInput
@export var concentration: Concentration
## Пусто — у игрока нет Тепла, пропущенные удары только снимают концентрацию.
@export var warmth: Warmth
## Противник. Обычно назначает DuelArena.
@export var opponent: Ghost

@export_group("Timing")
## Сколько секунд блок держится после нажатия.
@export_range(0.05, 2.0, 0.01, "suffix:s") var block_duration: float = 0.5
## Блок, поднятый не раньше чем за столько секунд до удара, считается парированием.
@export_range(0.0, 1.0, 0.01, "suffix:s") var parry_window: float = 0.18
## Пауза после блока, чтобы нельзя было держать его постоянно, спамя кнопку.
@export_range(0.0, 2.0, 0.01, "suffix:s") var block_cooldown: float = 0.3
@export_range(0.0, 2.0, 0.01, "suffix:s") var attack_cooldown: float = 0.45

@export_group("Concentration Gains")
@export var block_gain: float = 15.0
@export var parry_gain: float = 30.0
@export var attack_hit_gain: float = 12.0

@export_group("Concentration Losses")
@export var hit_taken_loss: float = 25.0
@export var deflected_loss: float = 10.0

## true — дуэль идёт, действия обрабатываются (включает DuelArena).
var active: bool = false:
	set(value):
		active = value
		if input != null:
			input.active = value
		if not value:
			_lower_guard()

## Зона поднятого блока (NONE — блока нет).
var guard_zone: CombatTypes.Zone = CombatTypes.Zone.NONE

var _guard_time_left: float = 0.0
var _guard_age: float = 0.0
var _block_cooldown_left: float = 0.0
var _attack_cooldown_left: float = 0.0


func _ready() -> void:
	if input == null or concentration == null:
		push_error("%s: не назначены input или concentration." % name)
		return

	input.attack_requested.connect(_on_attack_requested)
	input.block_requested.connect(_on_block_requested)
	input.counter_requested.connect(_on_counter_requested)


func _process(delta: float) -> void:
	_block_cooldown_left = maxf(_block_cooldown_left - delta, 0.0)
	_attack_cooldown_left = maxf(_attack_cooldown_left - delta, 0.0)

	if guard_zone == CombatTypes.Zone.NONE:
		return

	_guard_age += delta
	_guard_time_left -= delta
	if _guard_time_left <= 0.0:
		_lower_guard()


## Подготовить к новой дуэли: полное Тепло, пустая шкала, без блока и перезарядок.
func reset() -> void:
	_lower_guard()
	_block_cooldown_left = 0.0
	_attack_cooldown_left = 0.0
	if concentration != null:
		concentration.reset()
	if warmth != null:
		warmth.reset()


## Удар призрака в зону. Верная зона блока — блок или парирование, иначе попадание.
func receive_strike(zone: CombatTypes.Zone) -> CombatTypes.DefenseResult:
	var result := CombatTypes.DefenseResult.HIT
	if active and guard_zone == zone:
		result = CombatTypes.DefenseResult.PARRIED if _guard_age <= parry_window \
				else CombatTypes.DefenseResult.BLOCKED

	match result:
		CombatTypes.DefenseResult.PARRIED:
			concentration.add(parry_gain)
			_lower_guard()
			_block_cooldown_left = 0.0 # после парирования можно сразу ставить новый блок
		CombatTypes.DefenseResult.BLOCKED:
			concentration.add(block_gain)
		CombatTypes.DefenseResult.HIT:
			concentration.remove(hit_taken_loss)
			if warmth != null:
				warmth.take_hit()

	defended.emit(zone, result)
	return result


# --- Действия игрока ---

func _on_block_requested(zone: CombatTypes.Zone) -> void:
	if not active or _block_cooldown_left > 0.0:
		return

	guard_zone = zone
	_guard_age = 0.0
	_guard_time_left = block_duration
	_block_cooldown_left = block_duration + block_cooldown
	guard_changed.emit(zone)


func _on_attack_requested(zone: CombatTypes.Zone) -> void:
	if not active or opponent == null or _attack_cooldown_left > 0.0:
		return

	_attack_cooldown_left = attack_cooldown
	_lower_guard() # атака опускает блок
	var result := opponent.receive_attack(zone)
	match result:
		CombatTypes.AttackResult.HIT:
			concentration.add(attack_hit_gain)
		CombatTypes.AttackResult.DEFLECTED:
			concentration.remove(deflected_loss)
	attacked.emit(zone, result)


func _on_counter_requested() -> void:
	if not active or opponent == null:
		return

	if not concentration.is_full:
		counter_failed.emit()
		return

	if opponent.receive_counter():
		concentration.spend()
		countered.emit()


func _lower_guard() -> void:
	if guard_zone == CombatTypes.Zone.NONE:
		return

	guard_zone = CombatTypes.Zone.NONE
	_guard_time_left = 0.0
	guard_changed.emit(guard_zone)
