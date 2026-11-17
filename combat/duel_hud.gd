class_name DuelHud
extends CanvasLayer
## HUD дуэли: Тепло, шкала концентрации, крест зон (выбранная зона, блок, защита и замах призрака),
## фаза призрака и короткие сообщения об исходе ударов.
## Только читает состояние и слушает сигналы DuelArena / PlayerDuelist / Ghost, сам ничего не меняет.

@export var arena: DuelArena

@export_group("Nodes")
## Корневой Control — показывается только во время дуэли.
@export var root: Control
@export var phase_label: Label
@export var warmth_label: Label
@export var concentration_bar: ProgressBar
## «Space — контратака», виден при полной шкале.
@export var counter_hint: Control
@export var message_label: Label
@export var head_label: Label
@export var arm_label: Label
@export var body_label: Label
@export var legs_label: Label

@export_group("Colors")
@export var zone_normal_color: Color = Color(0.85, 0.85, 0.85, 0.45)
## Зона, которую игрок держит клавишей.
@export var zone_selected_color: Color = Color(1.0, 1.0, 1.0, 1.0)
## Поднятый блок игрока.
@export var zone_block_color: Color = Color(0.4, 1.0, 0.5, 1.0)
## Зону закрывает призрак — туда бить бесполезно.
@export var zone_ghost_guard_color: Color = Color(0.35, 0.6, 1.0, 1.0)
## Замах призрака — сюда нужно ставить блок.
@export var zone_windup_color: Color = Color(1.0, 0.3, 0.2, 1.0)

@export_group("Messages")
@export_range(0.1, 5.0, 0.1, "suffix:s") var message_time: float = 1.2
@export var warmth_full_symbol: String = "●"
@export var warmth_empty_symbol: String = "○"

var _duelist: PlayerDuelist
var _ghost: Ghost
var _zone_labels: Dictionary[CombatTypes.Zone, Label] = {}
var _message_tween: Tween


func _ready() -> void:
	if root != null:
		root.visible = false
	if arena == null:
		push_error("%s: не назначена arena." % name)
		return

	_duelist = arena.duelist
	_ghost = arena.ghost
	_zone_labels = {
		CombatTypes.Zone.HEAD: head_label,
		CombatTypes.Zone.ARM: arm_label,
		CombatTypes.Zone.BODY: body_label,
		CombatTypes.Zone.LEGS: legs_label,
	}
	for zone: CombatTypes.Zone in _zone_labels:
		if _zone_labels[zone] != null:
			_zone_labels[zone].text = CombatTypes.zone_name(zone)

	arena.duel_started.connect(_on_duel_started)
	arena.duel_ended.connect(_on_duel_ended)
	_duelist.attacked.connect(_on_attacked)
	_duelist.defended.connect(_on_defended)
	_duelist.countered.connect(_show_message.bind("COMBAT_MESSAGE_COUNTER"))
	_duelist.counter_failed.connect(_show_message.bind("COMBAT_MESSAGE_COUNTER_NOT_READY"))
	_duelist.concentration.changed.connect(_on_concentration_changed)
	if _duelist.warmth != null:
		_duelist.warmth.changed.connect(_on_warmth_changed)
	elif warmth_label != null:
		warmth_label.visible = false
	_ghost.phase_changed.connect(_on_phase_changed)


func _process(_delta: float) -> void:
	if root == null or not root.visible:
		return

	for zone: CombatTypes.Zone in _zone_labels:
		var label := _zone_labels[zone]
		if label != null:
			label.modulate = _zone_color(zone)


func _zone_color(zone: CombatTypes.Zone) -> Color:
	if _ghost.strike_zone == zone:
		return zone_windup_color
	if _duelist.guard_zone == zone:
		return zone_block_color
	if _duelist.input.current_zone == zone:
		return zone_selected_color
	if _ghost.guard_zone == zone:
		return zone_ghost_guard_color
	return zone_normal_color


# --- Реакция на дуэль ---

func _on_duel_started() -> void:
	root.visible = true
	if message_label != null:
		message_label.text = ""
	_on_concentration_changed(_duelist.concentration.value, _duelist.concentration.max_value)
	if _duelist.warmth != null:
		_on_warmth_changed(_duelist.warmth.current, _duelist.warmth.max_warmth)


func _on_duel_ended(won: bool) -> void:
	_show_message("COMBAT_MESSAGE_WIN" if won else "COMBAT_MESSAGE_LOSE")
	await get_tree().create_timer(arena.end_delay).timeout
	if not arena.is_active:
		root.visible = false


func _on_attacked(_zone: CombatTypes.Zone, result: CombatTypes.AttackResult) -> void:
	match result:
		CombatTypes.AttackResult.HIT:
			_show_message("COMBAT_MESSAGE_HIT")
		CombatTypes.AttackResult.DEFLECTED:
			_show_message("COMBAT_MESSAGE_DEFLECTED")


func _on_defended(_zone: CombatTypes.Zone, result: CombatTypes.DefenseResult) -> void:
	match result:
		CombatTypes.DefenseResult.PARRIED:
			_show_message("COMBAT_MESSAGE_PARRY")
		CombatTypes.DefenseResult.BLOCKED:
			_show_message("COMBAT_MESSAGE_BLOCK")
		CombatTypes.DefenseResult.HIT:
			_show_message("COMBAT_MESSAGE_HIT_TAKEN")


func _on_concentration_changed(value: float, maximum: float) -> void:
	if concentration_bar != null:
		concentration_bar.max_value = maximum
		concentration_bar.value = value
	if counter_hint != null:
		counter_hint.visible = value >= maximum


func _on_warmth_changed(current: int, maximum: int) -> void:
	if warmth_label != null:
		warmth_label.text = "%s  %s%s" % [tr("COMBAT_WARMTH"),
				warmth_full_symbol.repeat(current), warmth_empty_symbol.repeat(maximum - current)]


func _on_phase_changed(index: int, total: int) -> void:
	var phase := _ghost.phases[index]
	var title := tr(phase.title) if not phase.title.is_empty() else tr("COMBAT_PHASE") % (index + 1)
	if phase_label != null:
		phase_label.text = "%s  (%d / %d)" % [title, index + 1, total]
	if index > 0:
		_show_message("COMBAT_MESSAGE_PHASE_BROKEN")


func _show_message(key: String) -> void:
	if message_label == null:
		return

	message_label.text = tr(key)
	message_label.modulate.a = 1.0
	if _message_tween != null:
		_message_tween.kill()
	_message_tween = create_tween()
	_message_tween.tween_interval(message_time)
	_message_tween.tween_property(message_label, "modulate:a", 0.0, 0.3)
