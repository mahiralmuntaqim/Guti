extends Node3D
## Makes a decorative object (e.g. the ferry) bob and sway on the waves.

var _base_y := 0.0


func _ready() -> void:
	_base_y = position.y


func _process(_delta: float) -> void:
	var p := global_position
	position.y = _base_y + Water.height_at(p.x, p.z) * 0.8
	rotation.x = (Water.height_at(p.x, p.z + 2.0) - Water.height_at(p.x, p.z - 2.0)) * 0.15
	rotation.z = (Water.height_at(p.x + 1.5, p.z) - Water.height_at(p.x - 1.5, p.z)) * 0.15
