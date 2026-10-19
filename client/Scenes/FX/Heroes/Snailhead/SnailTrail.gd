@tool
extends Node3D

@export_range(0.05, 10.0) var radius: float = 1.0:
    set(value):
        radius = value
        _update_visual()
@export_range(0.05, 30.0) var lifetime_seconds: float = 4.5
@export var variation_seed: float = 0.0:
    set(value):
        variation_seed = value
        _update_visual()

var _age: float = 0.0
var _playing: bool = false

func _ready() -> void:
    _update_visual()
    set_process(false)

func _update_visual() -> void:
    var decal := get_node_or_null("Decal") as MeshInstance3D
    if decal == null:
        return
    decal.scale = Vector3(radius * 2.0, 0.6, radius * 2.0)
    var material := decal.material_override as ShaderMaterial
    material.set_shader_parameter("seed", variation_seed)
    var bubbles := get_node_or_null("Bubbles")
    if bubbles != null:
        bubbles.emission_radius = radius * 0.75

func configure_segment(width: float, duration_seconds: float, seed_value: float = 0.0, autoplay: bool = true) -> void:
    radius = maxf(width, 0.05)
    lifetime_seconds = maxf(duration_seconds, 0.05)
    variation_seed = seed_value
    _start(not visible)
    if not autoplay:
        _playing = false
        set_process(false)

func set_progress(progress: float) -> void:
    _set_erosion(smoothstep(0.6, 1.0, clampf(progress, 0.0, 1.0)))

func play() -> void:
    _start(true)

func _start(restart_bubbles: bool) -> void:
    _age = 0.0
    _playing = true
    visible = true
    if restart_bubbles:
        $Bubbles.play()
    _set_erosion(0.0)
    set_process(true)

func stop() -> void:
    _playing = false
    visible = false
    set_process(false)
    $Bubbles.stop(true)

func _process(delta: float) -> void:
    if Engine.is_editor_hint() or not _playing:
        return
    _age += delta
    _set_erosion(smoothstep(0.6, 1.0, _age / lifetime_seconds))
    if _age >= lifetime_seconds:
        stop()

func _set_erosion(value: float) -> void:
    var material := $Decal.material_override as ShaderMaterial
    material.set_shader_parameter("erosion", value)
    $Bubbles.set_opacity(1.0 - value)
