@tool
extends Node3D

const HAIR_SHADER = preload("res://Assets/FX/HairLock/HairLock.gdshader")
const SEGMENTS = 64
const SIDES = 6

@export_range(0.01, 100.0, 0.01, "or_greater") var length: float = 5.5:
	set(value):
		length = maxf(value, 0.01)
		_update_material()
@export_range(0.05, 5.0, 0.01, "or_greater") var thickness: float = 1.0:
	set(value):
		thickness = maxf(value, 0.05)
		_update_material()
@export_range(0.0, 2.0, 0.01) var motion: float = 0.65:
	set(value):
		motion = value
		_update_material()
@export var phase: float = 0.0:
	set(value):
		phase = value
		_update_material()
@export var hair_color: Color = Color(0.16, 0.085, 0.035):
	set(value):
		hair_color = value
		_update_material()

var _visual: MeshInstance3D
var _material: ShaderMaterial
static var _mesh: ArrayMesh


func _ready() -> void:
	_visual = MeshInstance3D.new()
	_visual.name = "Strands"
	if _mesh == null:
		_mesh = _build_mesh()
	_visual.mesh = _mesh
	_material = ShaderMaterial.new()
	_material.shader = HAIR_SHADER
	_visual.material_override = _material
	add_child(_visual)
	_update_material()


## Fits the lock from root to tip in world space. Thickness is independent of length.
func configure_between(root: Vector3, tip: Vector3) -> void:
	var parent_3d := get_parent_node_3d()
	var local_root := parent_3d.to_local(root) if parent_3d != null else root
	var local_tip := parent_3d.to_local(tip) if parent_3d != null else tip
	var direction := local_tip - local_root
	visible = direction.length_squared() > 0.000001
	if not visible:
		return
	length = direction.length()
	var up := Vector3.RIGHT if absf(direction.normalized().dot(Vector3.UP)) > 0.99 else Vector3.UP
	transform = Transform3D(Basis.looking_at(direction, up, true), local_root)


func _update_material() -> void:
	if _material == null:
		return
	_material.set_shader_parameter("lock_length", length)
	_material.set_shader_parameter("lock_width", thickness)
	_material.set_shader_parameter("motion", motion)
	_material.set_shader_parameter("phase", phase)
	_material.set_shader_parameter("hair_color", hair_color)
	var margin := thickness * 0.5 + length * 0.13
	_visual.custom_aabb = AABB(Vector3(-margin, -margin, -0.05), Vector3(margin * 2.0, margin * 2.0, length + 0.1))


static func _build_mesh() -> ArrayMesh:
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	for strand in range(18):
		var seed := float(strand) * 2.399963
		var flyaway := strand >= 11
		var radius := 0.012 if flyaway else 0.048 + 0.012 * sin(seed * 3.1)
		var spread := 0.19 if flyaway else 0.11
		var end := 0.87 + 0.13 * (0.5 + 0.5 * sin(seed * 1.7)) if flyaway else 1.0
		for segment in range(SEGMENTS + 1):
			var t := float(segment) / SEGMENTS
			var z := t * end
			var angle := seed + z * (1.3 + sin(seed))
			var separation := spread * (0.65 + sin(z * PI) * 0.8) * (1.0 - pow(z, 5.0))
			var center := Vector3(cos(angle) * separation, sin(angle) * separation, z)
			center.x += sin(z * 13.0 + seed) * 0.018 * sin(z * PI)
			center.y += cos(z * 10.0 + seed) * 0.018 * sin(z * PI)
			var taper := maxf(pow(1.0 - t, 0.65), 0.008)
			for side in range(SIDES + 1):
				var around := float(side) / SIDES
				var normal := Vector3(cos(around * TAU), sin(around * TAU), 0.0)
				surface.set_normal(normal)
				surface.set_uv(Vector2(around, z))
				surface.set_color(Color(float(strand) / 18.0, 0.0, 0.0))
				surface.add_vertex(center + normal * radius * taper)
		var offset := strand * (SEGMENTS + 1) * (SIDES + 1)
		for segment in range(SEGMENTS):
			for side in range(SIDES):
				var a := offset + segment * (SIDES + 1) + side
				var b := a + SIDES + 1
				for index in [a, b, a + 1, a + 1, b, b + 1]:
					surface.add_index(index)
	return surface.commit()
