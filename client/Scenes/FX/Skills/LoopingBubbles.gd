@tool
extends Node3D

@export_range(0.05, 10.0) var emission_radius: float = 0.8:
    set(value):
        emission_radius = value
        _apply_settings()
@export var bubble_color: Color = Color(0.62, 0.88, 1.0, 0.8):
    set(value):
        bubble_color = value
        _apply_settings()
@export var emitting: bool = true:
    set(value):
        emitting = value
        _apply_settings()

func _ready() -> void:
    _apply_settings()

func _apply_settings() -> void:
    var particles := get_node_or_null("Particles") as GPUParticles3D
    if particles == null:
        return
    var process := particles.process_material as ParticleProcessMaterial
    process.emission_ring_radius = emission_radius
    particles.material_override.set_shader_parameter("bubble_color", bubble_color)
    particles.emitting = emitting
    particles.visibility_aabb = AABB(Vector3(-emission_radius - 0.5, -0.5, -emission_radius - 0.5),
        Vector3(emission_radius * 2.0 + 1.0, 4.0, emission_radius * 2.0 + 1.0))

func configure(radius: float, color: Color) -> void:
    emission_radius = maxf(radius, 0.05)
    bubble_color = color

func play() -> void:
    visible = true
    emitting = true
    $Particles.restart()

func set_opacity(value: float) -> void:
    $Particles.material_override.set_shader_parameter("opacity", clampf(value, 0.0, 1.0))

func stop(clear: bool = false) -> void:
    emitting = false
    if clear:
        visible = false
