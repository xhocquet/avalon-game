extends "res://Scripts/FX/ChargeCircleEffect.gd"

signal fade_finished

@export var cancel_fade_seconds: float = 0.25
@export var cancel_tint: Color = Color(1.0, 0.08, 0.04, 1.0)

var _fade_elapsed: float = 0.0
var _failing: bool = false
var _tints: Dictionary[ShaderMaterial, Color] = {}


func _ready() -> void:
	super._ready()
	for mesh: MeshInstance3D in _visual.find_children("*", "MeshInstance3D"):
		var material := mesh.material_override as ShaderMaterial
		if material != null and not _tints.has(material):
			_tints[material] = material.get_shader_parameter("tint")


func configure(radius: float) -> void:
	set_process(false)
	_failing = false
	set_fade_progress(0.0)
	super.configure(radius)


func fade_out() -> void:
	_fade_elapsed = 0.0
	_failing = true
	set_fade_progress(0.0)
	_animation.pause()
	set_process(true)


func _process(delta: float) -> void:
	_fade_elapsed += delta
	var progress := clampf(_fade_elapsed / maxf(cancel_fade_seconds, 0.01), 0.0, 1.0)
	set_fade_progress(progress)
	if progress >= 1.0:
		stop()
		fade_finished.emit()


func set_fade_progress(progress: float) -> void:
	var opacity := 1.0 - smoothstep(0.0, 1.0, progress)
	for material: ShaderMaterial in _tints:
		var tint := _tints[material]
		if _failing:
			tint = Color(cancel_tint.r, cancel_tint.g, cancel_tint.b, tint.a * cancel_tint.a)
		tint.a *= opacity
		material.set_shader_parameter("tint", tint)


func stop() -> void:
	super.stop()
	set_process(false)
