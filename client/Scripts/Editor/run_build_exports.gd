@tool
extends SceneTree

const WORLD_SCENE := "res://Scenes/World/World.tscn"

var _scene: Node

func _initialize() -> void:
	var packed := load(WORLD_SCENE) as PackedScene
	if packed == null:
		push_error("[AvalonBuildExportRunner] Scene not found: %s" % WORLD_SCENE)
		quit(1)
		return

	_scene = packed.instantiate()
	root.add_child(_scene)
	call_deferred("_run_exports")

func _run_exports() -> void:
	var runner := AvalonBuildExportRunner.new()
	var ok := runner.RunLoaded(_scene, WORLD_SCENE)
	_scene.free()
	quit(0 if ok else 1)
