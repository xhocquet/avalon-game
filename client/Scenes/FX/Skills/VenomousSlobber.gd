@tool
extends Node3D

signal finished

@export_range(0.1, 20.0) var cone_range: float = 6.0:
    set(value):
        cone_range = value
        _update_shape()
@export_range(1.0, 180.0) var cone_angle_degrees: float = 60.0:
    set(value):
        cone_angle_degrees = value
        _update_shape()
@export_range(0.1, 5.0) var duration_seconds: float = 1.4

var _elapsed: float = 0.0

func _ready() -> void:
    _update_shape()
    set_process(false)
    for bubbles in $Bubbles.get_children():
        var particles := bubbles.get_node("Particles") as GPUParticles3D
        particles.amount = 10
        particles.lifetime = 0.8
        particles.preprocess = 0.0

func _update_shape() -> void:
    var decal := get_node_or_null("Decal") as MeshInstance3D
    if decal == null:
        return
    decal.position = Vector3(0.0, 0.12, -cone_range * 0.5)
    decal.scale = Vector3(cone_range * 2.0, 0.6, cone_range)
    decal.material_override.set_shader_parameter("cone_angle_degrees", cone_angle_degrees)
    decal.material_override.set_shader_parameter("texture_scale", cone_range)
    var group := get_node_or_null("Bubbles")
    if group == null:
        return
    var index := 0
    for bubbles in group.get_children():
        var distance := cone_range * (0.15 + float(index) * 0.17)
        bubbles.position = Vector3(0.0, 0.0, -distance)
        var radius := minf(distance * sin(deg_to_rad(cone_angle_degrees * 0.5)), cone_range - distance) * 0.7
        bubbles.emission_radius = maxf(radius, 0.05)
        index += 1

func configure_cone(range_value: float, angle_value: float) -> void:
    cone_range = maxf(range_value, 0.1)
    cone_angle_degrees = clampf(angle_value, 1.0, 180.0)

func play() -> void:
    _elapsed = 0.0
    visible = true
    for bubbles in $Bubbles.get_children():
        bubbles.set_opacity(1.0)
        bubbles.play()
    _apply_progress(0.0)
    set_process(true)

func stop() -> void:
    visible = false
    set_process(false)
    for bubbles in $Bubbles.get_children():
        bubbles.stop(true)

func _process(delta: float) -> void:
    if Engine.is_editor_hint():
        return
    _elapsed += delta
    _apply_progress(clampf(_elapsed / duration_seconds, 0.0, 1.0))
    if _elapsed >= duration_seconds:
        stop()
        finished.emit()

func _apply_progress(progress: float) -> void:
    var material := $Decal.material_override as ShaderMaterial
    var erosion := smoothstep(0.35, 1.0, progress)
    material.set_shader_parameter("reveal", smoothstep(0.0, 0.16, progress))
    material.set_shader_parameter("erosion", erosion)
    for bubbles in $Bubbles.get_children():
        bubbles.set_opacity(1.0 - erosion)
        if progress >= 0.35 and bubbles.emitting:
            bubbles.stop()
