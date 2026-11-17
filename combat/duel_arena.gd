class_name DuelArena
extends Node3D
## Солевой круг. Игрок входит в Trigger — начинается дуэль: управление Норой отключается,
## включается камера дуэли, игрок ставится на PlayerMark лицом к призраку.
## Победа — призрак сломлен (все фазы), поражение — кончилось Тепло.
## Повторно дуэль начинается только после того, как игрок выйдет из круга и войдёт снова.

signal duel_started
signal duel_ended(won: bool)

@export var ghost: Ghost
@export var duelist: PlayerDuelist
@export var duel_camera: Camera3D
## Куда ставится игрок на время дуэли.
@export var player_mark: Marker3D
## Зона входа в круг. Пусто — дуэль запускается только вызовом start_duel().
@export var trigger: Area3D

@export_group("Rules")
@export var start_on_enter: bool = true
## Пауза между концом дуэли и возвратом управления (успеть прочитать итог).
@export_range(0.0, 5.0, 0.1, "suffix:s") var end_delay: float = 1.5
## Высота точки на призраке, куда смотрит камера дуэли.
@export var look_height: float = 1.2

var is_active: bool = false
var player: Player

var _previous_camera: Camera3D
var _wait_for_exit: bool = false # после дуэли не начинать новую, пока игрок не выйдет из круга


func _ready() -> void:
	if ghost == null or duelist == null:
		push_error("%s: не назначены ghost или duelist." % name)
		return

	ghost.target = duelist
	duelist.opponent = ghost
	ghost.defeated.connect(_on_ghost_defeated)
	if duelist.warmth != null:
		duelist.warmth.depleted.connect(_on_warmth_depleted)
	if player_mark != null:
		ghost.look_at(Vector3(player_mark.global_position.x, ghost.global_position.y, player_mark.global_position.z))

	if trigger != null:
		trigger.body_entered.connect(_on_body_entered)
		trigger.body_exited.connect(_on_body_exited)


## Начать дуэль с игроком.
func start_duel(new_player: Player) -> void:
	if is_active or new_player == null:
		return

	is_active = true
	player = new_player
	player.controls_enabled = false
	player.velocity = Vector3.ZERO
	if player_mark != null:
		player.global_position = player_mark.global_position
	player.face_towards(ghost.global_position)

	if duel_camera != null:
		_previous_camera = get_viewport().get_camera_3d()
		duel_camera.look_at(ghost.global_position + Vector3.UP * look_height)
		duel_camera.make_current()

	duelist.reset()
	duelist.active = true
	ghost.begin_duel()
	duel_started.emit()


## Закончить дуэль. won — призрак сломлен.
func end_duel(won: bool) -> void:
	if not is_active:
		return

	is_active = false
	_wait_for_exit = true
	duelist.active = false
	if not won:
		ghost.stop_duel()
	duel_ended.emit(won)

	if end_delay > 0.0:
		await get_tree().create_timer(end_delay).timeout
	_release_player()


func _release_player() -> void:
	if player != null and is_instance_valid(player):
		player.controls_enabled = true
	if _previous_camera != null and is_instance_valid(_previous_camera):
		_previous_camera.make_current()
	_previous_camera = null


# --- Сигналы ---

func _on_body_entered(body: Node3D) -> void:
	if start_on_enter and not _wait_for_exit and body is Player:
		start_duel.call_deferred(body as Player)


func _on_body_exited(body: Node3D) -> void:
	if body is Player and not is_active:
		_wait_for_exit = false


func _on_ghost_defeated() -> void:
	end_duel(true)


func _on_warmth_depleted() -> void:
	end_duel(false)
