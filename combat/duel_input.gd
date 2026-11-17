class_name DuelInput
extends Node
## Ввод дуэли: удержание зоны (W/A/D/S), атака и блок (кнопки мыши и/или жесты), контратака (Space).
## Сам ничего не решает — только переводит ввод в сигналы, которые слушает PlayerDuelist.
## Работает, только пока active == true (включает DuelArena).

signal zone_changed(zone: CombatTypes.Zone)
signal attack_requested(zone: CombatTypes.Zone)
signal block_requested(zone: CombatTypes.Zone)
signal counter_requested

@export_group("Actions")
@export var zone_head_action: StringName = &"duel_zone_head"
@export var zone_arm_action: StringName = &"duel_zone_arm"
@export var zone_body_action: StringName = &"duel_zone_body"
@export var zone_legs_action: StringName = &"duel_zone_legs"
@export var attack_action: StringName = &"duel_attack"
@export var block_action: StringName = &"duel_block"
@export var counter_action: StringName = &"duel_counter"

@export_group("Zones")
## Зона, если ни одна клавиша зоны не зажата.
@export var default_zone: CombatTypes.Zone = CombatTypes.Zone.BODY

@export_group("Mouse")
## Атака и блок кнопками мыши (ЛКМ / ПКМ).
@export var use_mouse_buttons: bool = true
## Атака и блок жестом: мышь от себя (вверх) — атака, к себе (вниз) — блок.
@export var use_mouse_gestures: bool = false
## Сколько пикселей нужно провести мышью, чтобы жест засчитался.
@export_range(10.0, 500.0, 1.0, "suffix:px") var gesture_distance: float = 90.0
## Жест должен уложиться в это время, иначе накопленное движение сбрасывается.
@export_range(0.05, 1.0, 0.01, "suffix:s") var gesture_time: float = 0.3
## Пауза после жеста, чтобы одно движение не дало два действия.
@export_range(0.0, 1.0, 0.01, "suffix:s") var gesture_cooldown: float = 0.2

## true — ввод обрабатывается (идёт дуэль).
var active: bool = false:
	set(value):
		active = value
		_reset_gesture()
		_held_zones.clear()
		_update_zone()

var current_zone: CombatTypes.Zone = CombatTypes.Zone.NONE

var _held_zones: Array[CombatTypes.Zone] = [] # в порядке нажатия, последняя — текущая
var _gesture_motion: Vector2 = Vector2.ZERO
var _gesture_age: float = 0.0
var _gesture_cooldown_left: float = 0.0


func _ready() -> void:
	_ensure_input_actions()


func _process(delta: float) -> void:
	if not active:
		return

	_poll_zones()
	_gesture_cooldown_left = maxf(_gesture_cooldown_left - delta, 0.0)
	if _gesture_motion != Vector2.ZERO:
		_gesture_age += delta
		if _gesture_age > gesture_time:
			_reset_gesture()


func _unhandled_input(event: InputEvent) -> void:
	if not active:
		return

	if event.is_action_pressed(counter_action):
		counter_requested.emit()
		get_viewport().set_input_as_handled()
	elif use_mouse_buttons and event.is_action_pressed(attack_action):
		attack_requested.emit(_action_zone())
		get_viewport().set_input_as_handled()
	elif use_mouse_buttons and event.is_action_pressed(block_action):
		block_requested.emit(_action_zone())
		get_viewport().set_input_as_handled()
	elif use_mouse_gestures and event is InputEventMouseMotion:
		_track_gesture((event as InputEventMouseMotion).relative)


func _action_zone() -> CombatTypes.Zone:
	return default_zone if current_zone == CombatTypes.Zone.NONE else current_zone


# --- Зоны ---

func _poll_zones() -> void:
	var actions: Dictionary[CombatTypes.Zone, StringName] = {
		CombatTypes.Zone.HEAD: zone_head_action,
		CombatTypes.Zone.ARM: zone_arm_action,
		CombatTypes.Zone.BODY: zone_body_action,
		CombatTypes.Zone.LEGS: zone_legs_action,
	}
	for zone: CombatTypes.Zone in actions:
		var pressed := Input.is_action_pressed(actions[zone])
		if pressed and not _held_zones.has(zone):
			_held_zones.append(zone)
		elif not pressed and _held_zones.has(zone):
			_held_zones.erase(zone)
	_update_zone()


func _update_zone() -> void:
	var zone: CombatTypes.Zone = CombatTypes.Zone.NONE if _held_zones.is_empty() else _held_zones.back()
	if zone == current_zone:
		return

	current_zone = zone
	zone_changed.emit(zone)


# --- Жесты ---

func _track_gesture(relative: Vector2) -> void:
	if _gesture_cooldown_left > 0.0:
		return

	_gesture_motion += relative
	if _gesture_motion.length() < gesture_distance:
		return

	if absf(_gesture_motion.y) >= absf(_gesture_motion.x):
		if _gesture_motion.y < 0.0:
			attack_requested.emit(_action_zone())
		else:
			block_requested.emit(_action_zone())
	_gesture_cooldown_left = gesture_cooldown
	_reset_gesture()


func _reset_gesture() -> void:
	_gesture_motion = Vector2.ZERO
	_gesture_age = 0.0


## Если в Input Map нет действий дуэли — создаём с раскладкой по умолчанию, чтобы всё работало «из коробки».
func _ensure_input_actions() -> void:
	var defaults: Dictionary[StringName, InputEvent] = {
		zone_head_action: _key(KEY_W),
		zone_arm_action: _key(KEY_A),
		zone_body_action: _key(KEY_D),
		zone_legs_action: _key(KEY_S),
		attack_action: _mouse(MOUSE_BUTTON_LEFT),
		block_action: _mouse(MOUSE_BUTTON_RIGHT),
		counter_action: _key(KEY_SPACE),
	}
	for action: StringName in defaults:
		if InputMap.has_action(action):
			continue
		InputMap.add_action(action)
		InputMap.action_add_event(action, defaults[action])
		push_warning("Input action '%s' не найдено — добавлено временно. Добавь его в Project Settings → Input Map." % action)


static func _key(keycode: Key) -> InputEventKey:
	var event := InputEventKey.new()
	event.physical_keycode = keycode
	return event


static func _mouse(button: MouseButton) -> InputEventMouseButton:
	var event := InputEventMouseButton.new()
	event.button_index = button
	return event
