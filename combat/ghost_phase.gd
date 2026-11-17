class_name GhostPhase
extends Resource
## Настройки одной фазы дуэли с призраком (темп атак, защита, сколько контратак нужно, чтобы её сломать).
## Создавать как .tres (ПКМ в FileSystem → New Resource → GhostPhase) и класть в Ghost.phases по порядку.

## Ключ локализации названия фазы для HUD. Пусто — «Фаза N».
@export var title: String = ""

@export_group("Attack")
## Пауза между атаками — случайное значение из диапазона.
@export_range(0.2, 10.0, 0.05, "suffix:s") var idle_time_min: float = 1.2
@export_range(0.2, 10.0, 0.05, "suffix:s") var idle_time_max: float = 2.2
## Замах: сколько секунд зона удара видна игроку до самого удара.
@export_range(0.1, 5.0, 0.05, "suffix:s") var windup_time: float = 1.0
## После удара призрак открыт: в это время любая атака игрока попадает.
@export_range(0.0, 5.0, 0.05, "suffix:s") var recover_time: float = 0.6

@export_group("Guard")
## Как часто призрак меняет закрытую зону.
@export_range(0.2, 10.0, 0.05, "suffix:s") var guard_change_interval: float = 2.0

@export_group("Break")
## Сколько успешных контратак нужно, чтобы сломать фазу.
@export_range(1, 10) var counters_to_break: int = 1
## Оглушение после контратаки.
@export_range(0.1, 5.0, 0.05, "suffix:s") var stagger_time: float = 1.5
