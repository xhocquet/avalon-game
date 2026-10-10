extends Node3D

func _ready() -> void:
	for effect in [$Starlight, $SnowCircle]:
		var player: AnimationPlayer = effect.get_node("AnimationPlayer")
		player.get_animation("Appearance").loop_mode = Animation.LOOP_LINEAR
		player.play("Appearance")
