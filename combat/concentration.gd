class_name Concentration
extends Node
## Шкала концентрации. Копится за блоки, парирования и точные удары, теряется за ошибки.
## Полная шкала открывает контратаку (spend). Без действий шкала медленно спадает.

signal changed(value: float, maximum: float)
## Шкала заполнилась — контратака доступна.
signal filled
## Концентрация потрачена на контратаку.
signal spent

@export var max_value: float = 100.0

@export_group("Decay")
## Через сколько секунд без прироста шкала начинает спадать.
@export_range(0.0, 10.0, 0.1, "suffix:s") var decay_delay: float = 2.5
@export var decay_per_second: float = 4.0
## Полная шкала не спадает — игрок может выбрать момент для контратаки.
@export var hold_when_full: bool = true

var value: float = 0.0
var is_full: bool:
	get:
		return value >= max_value

var _decay_wait: float = 0.0


func _process(delta: float) -> void:
	if value <= 0.0 or (hold_when_full and is_full):
		return

	if _decay_wait > 0.0:
		_decay_wait -= delta
		return

	_set_value(value - decay_per_second * delta)


func add(amount: float) -> void:
	if amount <= 0.0:
		return

	var was_full := is_full
	_decay_wait = decay_delay
	_set_value(value + amount)
	if is_full and not was_full:
		filled.emit()


func remove(amount: float) -> void:
	if amount > 0.0:
		_set_value(value - amount)


## Потратить полную шкалу. false — шкала ещё не заполнена, контратака невозможна.
func spend() -> bool:
	if not is_full:
		return false

	_set_value(0.0)
	spent.emit()
	return true


func reset() -> void:
	_decay_wait = 0.0
	_set_value(0.0)


func _set_value(new_value: float) -> void:
	new_value = clampf(new_value, 0.0, max_value)
	if is_equal_approx(new_value, value):
		return

	value = new_value
	changed.emit(value, max_value)
