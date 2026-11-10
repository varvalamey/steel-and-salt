class_name Player
extends CharacterBody3D

@export_group("Movement")
@export var walk_speed: float = 3.0
@export var fast_walk_speed: float = 6.0
@export var acceleration: float = 10.0
@export var turn_speed: float = 10.0
@export var step_height: float = 0.35

@export_group("Camera")
@export var mouse_sensitivity: float = 0.003

@onready var _model: Node3D = $Model
@onready var _camera_pivot: Node3D = $CameraPivot

var _gravity: float = ProjectSettings.get_setting("physics/3d/default_gravity")


func _ready() -> void:
	for arm: SpringArm3D in [$CameraPivot/PitchPivot/ShoulderArm,
			$CameraPivot/PitchPivot/ShoulderArm/BackArm]:
		arm.add_excluded_object(get_rid())
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	elif event is InputEventMouseButton and event.pressed \
			and Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	elif event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		_camera_pivot.rotate_y(-event.relative.x * mouse_sensitivity)


func _physics_process(delta: float) -> void:
	if not is_on_floor():
		velocity.y -= _gravity * delta

	var input := Input.get_vector("move_left", "move_right", "move_forward", "move_back")
	var direction := _camera_pivot.global_basis * Vector3(input.x, 0.0, input.y)
	direction.y = 0.0
	direction = direction.normalized() * input.length()

	var speed := fast_walk_speed if Input.is_action_pressed("fast_walk") else walk_speed
	var horizontal := Vector3(velocity.x, 0.0, velocity.z)
	horizontal = horizontal.lerp(direction * speed, clampf(acceleration * delta, 0.0, 1.0))
	velocity.x = horizontal.x
	velocity.z = horizontal.z

	if direction.length_squared() > 0.001:
		var target_yaw := atan2(-direction.x, -direction.z)
		_model.rotation.y = lerp_angle(_model.rotation.y, target_yaw, turn_speed * delta)

	if not _try_step_up(horizontal * delta):
		move_and_slide()


func _try_step_up(motion: Vector3) -> bool:
	if not is_on_floor() or motion.length_squared() < 1e-8:
		return false
	var from := global_transform
	if not test_move(from, motion):
		return false
	var up := Vector3.UP * step_height
	if test_move(from, up):
		return false
	var raised := from.translated(up)
	if test_move(raised, motion):
		return false
	var ahead := raised.translated(motion)
	var hit := KinematicCollision3D.new()
	if not test_move(ahead, -up, hit):
		return false
	var probe := hit.get_position() + motion.normalized() * 0.02
	var query := PhysicsRayQueryParameters3D.create(
		probe + Vector3.UP * 0.05, probe - Vector3.UP * 0.05, collision_mask, [get_rid()])
	var ground := get_world_3d().direct_space_state.intersect_ray(query)
	if ground.is_empty() or ground.normal.angle_to(Vector3.UP) > floor_max_angle:
		return false
	global_position = ahead.origin + hit.get_travel()
	velocity.y = 0.0
	return true
