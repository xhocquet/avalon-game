extends "res://Scripts/FX/ChargeCircleEffect.gd"

signal finished

@export var duration_seconds: float = 0.5

var _elapsed: float = 0.0
var _tints: Dictionary[ShaderMaterial, Color] = {}


func _ready() -> void:
	super._ready()
	for mesh: MeshInstance3D in _visual.find_children("*", "MeshInstance3D"):
		var material := mesh.material_override as ShaderMaterial
		if material != null and not _tints.has(material):
			_tints[material] = material.get_shader_parameter("tint")


func play(radius: float) -> void:
	configure(radius)
	_elapsed = 0.0
	set_burst_progress(0.0)
	set_process(true)


func _process(delta: float) -> void:
	_elapsed += delta
	var progress := clampf(_elapsed / maxf(duration_seconds, 0.01), 0.0, 1.0)
	set_burst_progress(progress)
	if progress >= 1.0:
		stop()
		finished.emit()


func set_burst_progress(progress: float) -> void:
	super.set_progress(progress * 0.25)
	var expansion := 1.0 - pow(1.0 - clampf(progress / 0.65, 0.0, 1.0), 3.0)
	scale = Vector3.ONE * lerpf(0.15, 1.0, expansion)
	var opacity := 1.0 - smoothstep(0.15, 1.0, progress)
	for material: ShaderMaterial in _tints:
		var tint := _tints[material]
		tint.a *= opacity
		material.set_shader_parameter("tint", tint)
	visible = progress < 1.0


func stop() -> void:
	super.stop()
	set_process(false)
