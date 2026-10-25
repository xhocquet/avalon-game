extends SceneTree

var elapsed := 0.0
var previous_usec := 0
var last_click := -1
var rows := PackedStringArray(["seconds,phase,frame_ms,wall_ms,process_ms,physics_ms,draw_calls"])
var output := ""
var duration := 60.0

func _initialize() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--profile-output="):
			output = arg.trim_prefix("--profile-output=")
		if arg.begins_with("--profile-seconds="):
			duration = arg.trim_prefix("--profile-seconds=").to_float()
	call_deferred("change_scene_to_file", "res://Scenes/Lobby.tscn")

func _process(delta: float) -> bool:
	if current_scene == null or current_scene.name != "Multiplayer":
		return false
	var now := Time.get_ticks_usec()
	var wall_ms := delta * 1000.0 if previous_usec == 0 else (now - previous_usec) / 1000.0
	previous_usec = now
	elapsed += delta
	var phase := "idle" if elapsed < 12.0 else "movement"
	rows.append("%.6f,%s,%.3f,%.3f,%.3f,%.3f,%d" % [elapsed, phase, delta * 1000.0, wall_ms,
		Performance.get_monitor(Performance.TIME_PROCESS) * 1000.0,
		Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS) * 1000.0,
		Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)])
	if elapsed >= 12.0:
		var step := int((elapsed - 12.0) / 3.0)
		if step != last_click:
			last_click = step
			var offsets := [Vector2(180, 70), Vector2(-180, -70), Vector2(160, -60), Vector2(-160, 60)]
			var point: Vector2 = root.get_visible_rect().size * 0.5 + offsets[step % offsets.size()]
			for pressed in [true, false]:
				var event := InputEventMouseButton.new()
				event.button_index = MOUSE_BUTTON_RIGHT
				event.position = point
				event.global_position = point
				event.pressed = pressed
				Input.parse_input_event(event)
			print("[MovementProfile] seconds=%.3f click=%d screen=%s" % [elapsed, step, point])
	if elapsed >= duration:
		var file := FileAccess.open(output, FileAccess.WRITE)
		if file == null:
			push_error("Cannot write movement profile: " + output)
			quit(1)
		else:
			file.store_string("\n".join(rows) + "\n")
			file.close()
			quit()
	return false
