class_name Water
extends MeshInstance3D
## Animated ocean. The same wave function runs on the GPU (vertex shader) and the CPU
## (height_at) so floating objects ride the waves you see.

const LEVEL := 0.0
const DIR1 := Vector2(0.857, 0.514)
const DIR2 := Vector2(-0.371, 0.928)
const FREQ1 := 0.35
const FREQ2 := 0.8
const SPEED1 := 1.1
const SPEED2 := 1.7
const AMP1 := 0.22
const AMP2 := 0.08
const GRID_STEP := 2.0  # mesh vertex spacing; follow-snapping uses it to avoid swimming vertices

static var time := 0.0

var follow: Node3D
var _material: ShaderMaterial

const SHADER := """
shader_type spatial;
render_mode cull_disabled, specular_schlick_ggx;

uniform float wave_time;
uniform vec2 dir1; uniform vec2 dir2;
uniform float freq1; uniform float freq2;
uniform float speed1; uniform float speed2;
uniform float amp1; uniform float amp2;
uniform vec4 deep_color : source_color = vec4(0.03, 0.22, 0.38, 1.0);
uniform vec4 shallow_color : source_color = vec4(0.12, 0.62, 0.68, 1.0);
uniform vec4 foam_color : source_color = vec4(0.9, 0.97, 1.0, 1.0);

varying float wave_h;

void vertex() {
	vec2 p = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
	float a1 = dot(dir1, p) * freq1 + wave_time * speed1;
	float a2 = dot(dir2, p) * freq2 + wave_time * speed2;
	float h = amp1 * sin(a1) + amp2 * sin(a2);
	VERTEX.y += h;
	wave_h = h;
	vec2 grad = dir1 * (amp1 * freq1 * cos(a1)) + dir2 * (amp2 * freq2 * cos(a2));
	NORMAL = normalize(vec3(-grad.x, 1.0, -grad.y));
}

void fragment() {
	float t = clamp((wave_h + amp1 + amp2) / (2.0 * (amp1 + amp2)), 0.0, 1.0);
	vec3 col = mix(deep_color.rgb, shallow_color.rgb, t);
	float crest = smoothstep(0.82, 0.98, t);
	ALBEDO = mix(col, foam_color.rgb, crest * 0.55);
	ROUGHNESS = 0.08;
	METALLIC = 0.0;
	SPECULAR = 0.6;
}
"""


static func height_at(x: float, z: float) -> float:
	var p := Vector2(x, z)
	return LEVEL + AMP1 * sin(DIR1.dot(p) * FREQ1 + time * SPEED1) + AMP2 * sin(DIR2.dot(p) * FREQ2 + time * SPEED2)


func _ready() -> void:
	var plane := PlaneMesh.new()
	plane.size = Vector2(360, 360)
	plane.subdivide_width = int(360 / GRID_STEP)
	plane.subdivide_depth = int(360 / GRID_STEP)
	mesh = plane
	var shader := Shader.new()
	shader.code = SHADER
	_material = ShaderMaterial.new()
	_material.shader = shader
	_material.set_shader_parameter("dir1", DIR1)
	_material.set_shader_parameter("dir2", DIR2)
	_material.set_shader_parameter("freq1", FREQ1)
	_material.set_shader_parameter("freq2", FREQ2)
	_material.set_shader_parameter("speed1", SPEED1)
	_material.set_shader_parameter("speed2", SPEED2)
	_material.set_shader_parameter("amp1", AMP1)
	_material.set_shader_parameter("amp2", AMP2)
	material_override = _material
	cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	position.y = LEVEL


func _physics_process(delta: float) -> void:
	time += delta
	_material.set_shader_parameter("wave_time", time)
	if follow:
		position.x = snappedf(follow.global_position.x, GRID_STEP)
		position.z = snappedf(follow.global_position.z, GRID_STEP)
