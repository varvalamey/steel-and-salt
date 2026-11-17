class_name Warmth
extends Node
## «Тепло» — здоровье Норы в дуэли. Каждый пропущенный удар призрака отнимает очко,
## на нуле дуэль проиграна. Если выключить enabled, урон не наносится и проиграть нельзя.

signal changed(current: int, maximum: int)
## Тепло закончилось.
signal depleted

## false — Тепло не тратится (дуэль без поражения, удобно для тестов).
@export var enabled: bool = true
@export_range(1, 20) var max_warmth: int = 5

var current: int = 0


func _ready() -> void:
	reset()


func reset() -> void:
	current = max_warmth
	changed.emit(current, max_warmth)


func take_hit(amount: int = 1) -> void:
	if not enabled or current <= 0:
		return

	current = maxi(current - amount, 0)
	changed.emit(current, max_warmth)
	if current == 0:
		depleted.emit()


func is_depleted() -> bool:
	return enabled and current <= 0
