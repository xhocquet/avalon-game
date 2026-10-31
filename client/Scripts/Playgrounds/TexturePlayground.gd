extends Node3D

const GROUND_DIRS := ["res://Assets/Props/Ground", "res://Assets/Props/GroundTests"]
const SKY_DIR := "res://Assets/Props/SkyTests"
const CHANNELS := ["r", "g", "b"]

var ground_choices: Array[Dictionary] = [{"name": "World original"}]
var sky_choices: Array[String] = [""]
var ground_indices := [0, 0, 0, 0]
var repeats := [10.0, 10.0, 5.0, 10.0]
var target := 0
var sky_index := 0
var ground_label: Label
var sky_label: Label
var scale_input: SpinBox
var original_ground: ShaderMaterial
var blended_ground: ShaderMaterial
var environment: Environment
var original_sky: Sky
var panorama := PanoramaSkyMaterial.new()
var preview_sky := Sky.new()
var yaw := 0.0
var pitch := -25.0

@onready var ground: MeshInstance3D = $World/NavigationRegion3D/GroundMesh
@onready var camera: Camera3D = $Camera3D


func _ready() -> void:
	for directory in GROUND_DIRS:
		_scan_ground(directory)
	for path in _image_files(SKY_DIR):
		sky_choices.append(path)
	original_ground = ground.get_active_material(0)
	blended_ground = original_ground.duplicate()
	var world_environment: WorldEnvironment = $World/WorldEnvironment
	environment = world_environment.environment.duplicate()
	world_environment.environment = environment
	original_sky = environment.sky
	preview_sky.sky_material = panorama
	_build_ui()
	_reset_camera()
	_refresh_ground()
	_refresh_sky()


func _image_files(directory: String) -> Array[String]:
	var result: Array[String] = []
	for file in ResourceLoader.list_directory(directory):
		var path := directory.path_join(file)
		if file.ends_with("/"):
			result.append_array(_image_files(path.trim_suffix("/")))
		elif file.get_extension().to_lower() in ["png", "jpg", "jpeg", "webp", "hdr", "exr"]:
			result.append(path)
	result.sort()
	return result


func _scan_ground(directory: String) -> void:
	var files := _image_files(directory)
	for path in files:
		var file := path.get_file().to_lower()
		if directory.ends_with("GroundTests") or "color" in file:
			var entry := {"name": path.get_file().get_basename(), "albedo": path}
			if not directory.ends_with("GroundTests"):
				for companion in files:
					if companion.get_base_dir() != path.get_base_dir():
						continue
					var name_lower := companion.get_file().to_lower()
					if "normalgl" in name_lower or "normal_opengl" in name_lower:
						entry["normal"] = companion
					elif "roughness" in name_lower:
						entry["roughness"] = companion
					elif "_ao_" in name_lower or "ambient" in name_lower:
						entry["ao"] = companion
					elif "height" in name_lower or "displacement" in name_lower:
						entry["height"] = companion
			ground_choices.append(entry)


func _build_ui() -> void:
	var canvas := CanvasLayer.new()
	add_child(canvas)
	var panel := PanelContainer.new()
	panel.position = Vector2(16, 16)
	panel.custom_minimum_size.x = 410
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.055, 0.075, 0.095, 0.96)
	style.set_corner_radius_all(6)
	panel.add_theme_stylebox_override("panel", style)
	canvas.add_child(panel)
	var margin := MarginContainer.new()
	for side in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, 12)
	panel.add_child(margin)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 8)
	margin.add_child(box)
	_label(box, "GROUND & SKY", 19)
	var targets := OptionButton.new()
	for title in ["Whole ground", "Splatmap: paths (red)", "Splatmap: grass (green)", "Splatmap: stone (blue)"]:
		targets.add_item(title)
	targets.item_selected.connect(func(index: int):
		target = index
		scale_input.set_value_no_signal(repeats[target])
		_refresh_ground())
	box.add_child(targets)
	ground_label = _selector(box, func(): _cycle_ground(-1), func(): _cycle_ground(1))
	scale_input = _number(box, "Tiles across 100 m", 0.25, 400.0, 0.25, 10.0)
	scale_input.value_changed.connect(func(value: float):
		repeats[target] = value
		_refresh_ground())
	_label(box, "More tiles = smaller texture features", 12)
	box.add_child(HSeparator.new())
	_label(box, "SKY", 16)
	sky_label = _selector(box, func(): _cycle_sky(-1), func(): _cycle_sky(1))
	var rotation_input := _number(box, "Sky rotation", -180, 180, 1, 0)
	rotation_input.value_changed.connect(func(value: float): environment.sky_rotation.y = deg_to_rad(value))
	var energy_input := _number(box, "Sky energy", 0.05, 4, 0.05, 1)
	energy_input.value_changed.connect(func(value: float): environment.background_energy_multiplier = value)
	box.add_child(HSeparator.new())
	_label(box, "RMB drag: look  |  WASD: move\nQ / E: down / up  |  Shift: faster", 13)
	var reset := Button.new()
	reset.text = "Reset camera"
	reset.focus_mode = Control.FOCUS_NONE
	reset.pressed.connect(_reset_camera)
	box.add_child(reset)


func _label(parent: Node, text: String, font_size: int) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", font_size)
	parent.add_child(label)
	return label


func _selector(parent: Node, previous: Callable, next: Callable) -> Label:
	var row := HBoxContainer.new()
	parent.add_child(row)
	var back := Button.new()
	back.text = "<"
	back.focus_mode = Control.FOCUS_NONE
	back.custom_minimum_size = Vector2(34, 34)
	back.pressed.connect(previous)
	row.add_child(back)
	var label := _label(row, "", 14)
	label.custom_minimum_size.x = 300
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	var forward := Button.new()
	forward.text = ">"
	forward.focus_mode = Control.FOCUS_NONE
	forward.custom_minimum_size = Vector2(34, 34)
	forward.pressed.connect(next)
	row.add_child(forward)
	return label


func _number(parent: Node, title: String, minimum: float, maximum: float, step: float, value: float) -> SpinBox:
	var row := HBoxContainer.new()
	parent.add_child(row)
	var label := _label(row, title, 14)
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var input := SpinBox.new()
	input.min_value = minimum
	input.max_value = maximum
	input.step = step
	input.value = value
	input.custom_minimum_size.x = 120
	row.add_child(input)
	return input


func _cycle_ground(direction: int) -> void:
	ground_indices[target] = posmod(ground_indices[target] + direction, ground_choices.size())
	_refresh_ground()


func _refresh_ground() -> void:
	var index: int = ground_indices[target]
	var entry := ground_choices[index]
	ground_label.text = "%d / %d  %s" % [index + 1, ground_choices.size(), entry.name]
	ground_label.tooltip_text = entry.get("albedo", "Original World material")
	if target == 0:
		if index == 0:
			var original_preview: ShaderMaterial = original_ground.duplicate()
			for channel in CHANNELS:
				var parameter: String = "resolution_" + channel
				original_preview.set_shader_parameter(parameter, float(original_ground.get_shader_parameter(parameter)) * repeats[0] / 10.0)
			ground.material_override = original_preview
			return
		var material := StandardMaterial3D.new()
		material.albedo_texture = load(entry.albedo)
		material.uv1_scale = Vector3.ONE * repeats[0]
		material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
		if entry.has("normal"):
			material.normal_enabled = true
			material.normal_texture = load(entry.normal)
		if entry.has("roughness"):
			material.roughness_texture = load(entry.roughness)
			material.roughness_texture_channel = BaseMaterial3D.TEXTURE_CHANNEL_RED
		if entry.has("ao"):
			material.ao_enabled = true
			material.ao_texture = load(entry.ao)
		ground.material_override = material
	else:
		var channel: String = CHANNELS[target - 1]
		for map in ["albedo", "normal", "roughness", "ao", "height"]:
			var parameter: String = map + "_" + channel
			var texture: Texture2D
			if index == 0:
				texture = original_ground.get_shader_parameter(parameter)
			elif entry.has(map):
				texture = load(entry[map])
			else:
				var color := Color(0.5, 0.5, 1) if map == "normal" else Color.WHITE
				var image := Image.create(1, 1, false, Image.FORMAT_RGB8)
				image.fill(color)
				texture = ImageTexture.create_from_image(image)
			blended_ground.set_shader_parameter(parameter, texture)
		blended_ground.set_shader_parameter("resolution_" + channel, repeats[target])
		ground.material_override = blended_ground


func _cycle_sky(direction: int) -> void:
	sky_index = posmod(sky_index + direction, sky_choices.size())
	_refresh_sky()


func _refresh_sky() -> void:
	var path := sky_choices[sky_index]
	sky_label.text = "%d / %d  %s" % [sky_index + 1, sky_choices.size(), "World original" if path.is_empty() else path.get_file().get_basename()]
	sky_label.tooltip_text = path
	if path.is_empty():
		environment.sky = original_sky
	else:
		panorama.panorama = load(path)
		environment.sky = preview_sky


func _reset_camera() -> void:
	camera.position = Vector3(0, 13, 30)
	yaw = 0
	pitch = -25
	camera.rotation_degrees = Vector3(pitch, yaw, 0)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_RIGHT:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED if event.pressed else Input.MOUSE_MODE_VISIBLE
	elif event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		yaw -= event.relative.x * 0.15
		pitch = clampf(pitch - event.relative.y * 0.15, -89, 89)
		camera.rotation_degrees = Vector3(pitch, yaw, 0)
	elif event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE


func _process(delta: float) -> void:
	if get_viewport().gui_get_focus_owner() is LineEdit:
		return
	var direction := Vector3(
		float(Input.is_physical_key_pressed(KEY_D)) - float(Input.is_physical_key_pressed(KEY_A)),
		float(Input.is_physical_key_pressed(KEY_E)) - float(Input.is_physical_key_pressed(KEY_Q)),
		float(Input.is_physical_key_pressed(KEY_S)) - float(Input.is_physical_key_pressed(KEY_W)))
	var speed := 35.0 if Input.is_physical_key_pressed(KEY_SHIFT) else 12.0
	var movement := camera.basis * Vector3(direction.x, 0, direction.z) + Vector3.UP * direction.y
	camera.position += movement.normalized() * speed * delta
