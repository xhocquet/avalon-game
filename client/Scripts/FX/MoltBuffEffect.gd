extends Node3D

@export var duration_seconds: float = 1.5

@onready var _animation: AnimationPlayer = $Visual/AnimationPlayer


func _ready() -> void:
	stop()


func configure(radius: float) -> void:
	scale = Vector3.ONE * radius
	_animation.play("Appearance")
	_animation.pause()


func set_elapsed(seconds: float) -> void:
	var progress := clampf(seconds / maxf(duration_seconds, 0.01), 0.0, 1.0)
	_animation.seek(progress * _animation.current_animation_length, true)
	visible = seconds >= 0.0 and progress < 1.0


func stop() -> void:
	visible = false
	_animation.stop()
