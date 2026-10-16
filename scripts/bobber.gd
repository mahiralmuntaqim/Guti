class_name Bobber
extends RigidBody3D
## The float at the end of the line. Floats on the CPU wave function and reports
## whether it is sitting in open water.

const RADIUS := 0.11

var in_water := false
var on_land := false


func _init() -> void:
	mass = 0.2
	continuous_cd = true
	contact_monitor = true
	max_contacts_reported = 4
	can_sleep = false


func _ready() -> void:
	var col := CollisionShape3D.new()
	var shape := SphereShape3D.new()
	shape.radius = RADIUS
	col.shape = shape
	add_child(col)

	var top := MeshInstance3D.new()
	var top_mesh := SphereMesh.new()
	top_mesh.radius = RADIUS
	top_mesh.height = RADIUS * 2.0
	top.mesh = top_mesh
	var red := StandardMaterial3D.new()
	red.albedo_color = Color(0.9, 0.15, 0.1)
	top.material_override = red
	add_child(top)

	var band := MeshInstance3D.new()
	var band_mesh := CylinderMesh.new()
	band_mesh.top_radius = RADIUS * 1.05
	band_mesh.bottom_radius = RADIUS * 1.05
	band_mesh.height = RADIUS * 0.6
	band.mesh = band_mesh
	var white := StandardMaterial3D.new()
	white.albedo_color = Color(0.95, 0.95, 0.95)
	band.material_override = white
	add_child(band)


func _physics_process(_delta: float) -> void:
	if freeze:
		return
	on_land = get_colliding_bodies().size() > 0
	var depth := Water.height_at(global_position.x, global_position.z) - global_position.y + RADIUS * 0.4
	in_water = depth > 0.0 and not on_land
	if depth > 0.0:
		# buoyancy grows with depth; heavy damping makes it settle like a real float
		apply_central_force(Vector3.UP * mass * 9.8 * (1.0 + minf(depth, 0.5) * 14.0))
		linear_damp = 5.0
		angular_damp = 5.0
	else:
		linear_damp = 0.05
		angular_damp = 0.5


func tug(strength: float = 0.5) -> void:
	apply_central_impulse(Vector3.DOWN * strength)


func reel_toward(target: Vector3, speed: float) -> void:
	var to := target - global_position
	to.y = 0.0
	if to.length() < 0.01:
		return
	var v := to.normalized() * speed
	linear_velocity = Vector3(v.x, linear_velocity.y, v.z)
