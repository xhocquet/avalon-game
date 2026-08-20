@tool
extends SceneTree

# Every scene a game type can launch: each export writes its own navmesh, static colliders and
# MapLayout, named after the scene (World keeps the unsuffixed names the server loads).
const MAP_SCENES := [
	"res://Scenes/World/World.tscn",
	"res://Scenes/Playgrounds/NavPlayground.tscn",
]

var _index := 0
var _scene: Node
var _failed := false

func _initialize() -> void:
	call_deferred("_run_next")

func _run_next() -> void:
	if _index >= MAP_SCENES.size():
		quit(1 if _failed else 0)
		return

	var scene_path: String = MAP_SCENES[_index]
	_index += 1

	var packed := load(scene_path) as PackedScene
	if packed == null:
		push_error("[AvalonBuildExportRunner] Scene not found: %s" % scene_path)
		_failed = true
		call_deferred("_run_next")
		return

	_scene = packed.instantiate()
	root.add_child(_scene)
	call_deferred("_run_exports", scene_path)

func _run_exports(scene_path: String) -> void:
	var runner := AvalonBuildExportRunner.new()
	if not runner.RunLoaded(_scene, scene_path):
		_failed = true
	_scene.free()
	_scene = null
	call_deferred("_run_next")
