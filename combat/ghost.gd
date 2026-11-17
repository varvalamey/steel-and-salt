class_name Ghost
extends Node3D
## Призрак-дуэлянт. Убить его нельзя: контратаки игрока ломают фазы (GhostPhase),
## после последней фазы призрак сломлен и дуэль выиграна.
## ИИ: держит защиту в одной зоне, затем замахивается (зона удара подсвечивается) и бьёт.
## Удар разрешает цель — PlayerDuelist.receive_strike().

## Состояние ИИ.
enum State {
	INACTIVE,  # дуэль не идёт
	IDLE,      # держит защиту, ждёт момента для атаки
	WINDUP,    # замах — зона удара видна игроку
	RECOVER,   # после удара — открыт
	STAGGERED, # оглушён контратакой — открыт
	DEFEATED,  # сломлен
}

signal state_changed(state: State)
## Призрак закрыл другую зону (NONE — открыт).
signal guard_changed(zone: CombatTypes.Zone)
## Начат замах в зону; duration — сколько секунд до удара.
signal windup_started(zone: CombatTypes.Zone, duration: float)
## Удар разрешён: игрок заблокировал, парировал или пропустил.
signal strike_resolved(zone: CombatTypes.Zone, result: CombatTypes.DefenseResult)
signal attacked(zone: CombatTypes.Zone, result: CombatTypes.AttackResult)
signal countered
## Началась фаза index (с нуля) из total.
signal phase_changed(index: int, total: int)
signal defeated

## Кого бьёт призрак. Обычно назначает DuelArena.
@export var target: PlayerDuelist
## Фазы дуэли по порядку.
@export var phases: Array[GhostPhase] = []

@export_group("Visuals")
@export var body_mesh: MeshInstance3D
@export var head_marker: MeshInstance3D
@export var arm_marker: MeshInstance3D
@export var body_marker: MeshInstance3D
@export var legs_marker: MeshInstance3D
@export var body_color: Color = Color(0.7, 0.85, 1.0, 0.45)
@export var marker_idle_color: Color = Color(0.8, 0.9, 1.0, 0.12)
@export var guard_color: Color = Color(0.35, 0.6, 1.0, 0.8)
@export var windup_color: Color = Color(1.0, 0.25, 0.15, 0.95)
@export var stagger_color: Color = Color(1.0, 0.95, 0.6, 0.7)

var state: State = State.INACTIVE
var guard_zone: CombatTypes.Zone = CombatTypes.Zone.NONE
## Зона текущего замаха (NONE — замаха нет).
var strike_zone: CombatTypes.Zone = CombatTypes.Zone.NONE
var phase_index: int = 0
var phase: GhostPhase:
	get:
		return phases[phase_index] if phase_index < phases.size() else null

var _state_time_left: float = 0.0
var _guard_time_left: float = 0.0
var _counters_in_phase: int = 0
var _markers: Dictionary[CombatTypes.Zone, MeshInstance3D] = {}
var _body_material: StandardMaterial3D


func _ready() -> void:
	if phases.is_empty():
		push_warning("%s: не заданы phases — используется одна фаза по умолчанию." % name)
		phases.append(GhostPhase.new())

	_markers = {
		CombatTypes.Zone.HEAD: head_marker,
		CombatTypes.Zone.ARM: arm_marker,
		CombatTypes.Zone.BODY: body_marker,
		CombatTypes.Zone.LEGS: legs_marker,
	}
	for zone: CombatTypes.Zone in _markers:
		if _markers[zone] != null:
			_markers[zone].material_override = _make_material(marker_idle_color)
	if body_mesh != null:
		_body_material = _make_material(body_color)
		body_mesh.material_override = _body_material
	_refresh_visuals()


func _process(delta: float) -> void:
	if state in [State.INACTIVE, State.DEFEATED]:
		return

	_state_time_left -= delta
	match state:
		State.IDLE:
			_update_guard(delta)
			if _state_time_left <= 0.0:
				_begin_windup()
		State.WINDUP:
			if _state_time_left <= 0.0:
				_strike()
		State.RECOVER:
			if _state_time_left <= 0.0:
				_enter_idle()
		State.STAGGERED:
			if _state_time_left <= 0.0:
				_enter_idle()


# --- Управление дуэлью ---

## Начать дуэль с первой фазы.
func begin_duel() -> void:
	phase_index = 0
	_counters_in_phase = 0
	phase_changed.emit(phase_index, phases.size())
	_enter_idle()


## Остановить ИИ (дуэль прервана или закончена поражением игрока).
func stop_duel() -> void:
	strike_zone = CombatTypes.Zone.NONE
	_set_guard(CombatTypes.Zone.NONE)
	_set_state(State.INACTIVE)


# --- Реакция на действия игрока ---

## Атака игрока в зону. Попадает, если зона открыта; в RECOVER и STAGGERED попадает всегда.
func receive_attack(zone: CombatTypes.Zone) -> CombatTypes.AttackResult:
	var result := CombatTypes.AttackResult.HIT
	if state in [State.INACTIVE, State.DEFEATED]:
		result = CombatTypes.AttackResult.IGNORED
	elif state in [State.IDLE, State.WINDUP] and zone == guard_zone:
		result = CombatTypes.AttackResult.DEFLECTED

	attacked.emit(zone, result)
	return result


## Контратака игрока (полная концентрация). Прерывает замах и оглушает;
## counters_to_break контратак ломают фазу. false — контратака сейчас невозможна.
func receive_counter() -> bool:
	if state in [State.INACTIVE, State.DEFEATED]:
		return false

	_counters_in_phase += 1
	countered.emit()

	if _counters_in_phase < phase.counters_to_break:
		_enter_stagger()
		return true

	_counters_in_phase = 0
	if phase_index + 1 >= phases.size():
		_enter_defeated()
		return true

	_enter_stagger()
	phase_index += 1
	phase_changed.emit(phase_index, phases.size())
	return true


# --- Состояния ---

func _enter_idle() -> void:
	strike_zone = CombatTypes.Zone.NONE
	_state_time_left = randf_range(phase.idle_time_min, maxf(phase.idle_time_min, phase.idle_time_max))
	_guard_time_left = 0.0
	_set_state(State.IDLE)
	_update_guard(0.0)


func _begin_windup() -> void:
	strike_zone = CombatTypes.random_zone(guard_zone)
	_state_time_left = phase.windup_time
	_set_state(State.WINDUP)
	windup_started.emit(strike_zone, phase.windup_time)


func _strike() -> void:
	var zone := strike_zone
	var result := CombatTypes.DefenseResult.HIT
	if target != null:
		result = target.receive_strike(zone)


	strike_zone = CombatTypes.Zone.NONE
	_set_guard(CombatTypes.Zone.NONE)
	_state_time_left = phase.recover_time
	_set_state(State.RECOVER)
	strike_resolved.emit(zone, result)


func _enter_stagger() -> void:
	strike_zone = CombatTypes.Zone.NONE
	_set_guard(CombatTypes.Zone.NONE)
	_state_time_left = phase.stagger_time
	_set_state(State.STAGGERED)


func _enter_defeated() -> void:
	strike_zone = CombatTypes.Zone.NONE
	_set_guard(CombatTypes.Zone.NONE)
	_set_state(State.DEFEATED)
	defeated.emit()


func _set_state(new_state: State) -> void:
	state = new_state
	_refresh_visuals()
	state_changed.emit(state)


func _update_guard(delta: float) -> void:
	_guard_time_left -= delta
	if _guard_time_left > 0.0:
		return

	_guard_time_left = phase.guard_change_interval
	_set_guard(CombatTypes.random_zone(guard_zone))


func _set_guard(zone: CombatTypes.Zone) -> void:
	if zone == guard_zone:
		return

	guard_zone = zone
	_refresh_visuals()
	guard_changed.emit(zone)


# --- Визуал ---

func _refresh_visuals() -> void:
	for zone: CombatTypes.Zone in _markers:
		var marker := _markers[zone]
		if marker == null:
			continue
		var color := marker_idle_color
		if zone == strike_zone:
			color = windup_color
		elif zone == guard_zone:
			color = guard_color
		(marker.material_override as StandardMaterial3D).albedo_color = color

	if _body_material != null:
		match state:
			State.STAGGERED:
				_body_material.albedo_color = stagger_color
			State.DEFEATED:
				_body_material.albedo_color = Color(body_color, 0.08)
			_:
				_body_material.albedo_color = body_color


static func _make_material(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	return material
