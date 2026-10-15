extends Node3D

@onready var _visual: Node3D = $Visual
@onready var _animation: AnimationPlayer = $Visual/AnimationPlayer


func _ready() -> void:
	stop()


func configure(radius: float) -> void:
	var ring: MeshInstance3D = $Visual/EnergyRing/Mesh
	var bounds := ring.get_aabb()
	var authored_radius := maxf(bounds.size.x, bounds.size.y) * ring.scale.x * 0.5
	_visual.scale = Vector3.ONE * radius / authored_radius
	_animation.play("Loop")
	_animation.pause()
	set_progress(0.0)


func set_progress(progress: float) -> void:
	_animation.seek(clampf(progress, 0.0, 1.0) * _animation.current_animation_length, true)
	visible = progress < 1.0


func stop() -> void:
	visible = false
	_animation.stop()
