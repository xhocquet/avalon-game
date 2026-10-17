extends Node3D

const HAIR = preload("res://Scenes/FX/Skills/HairLock.tscn")
const VARIANTS = [
	HAIR,
	preload("res://Scenes/FX/Skills/HairLockMessy.tscn"),
	preload("res://Scenes/FX/Skills/HairLockWild.tscn"),
]
const WIZARD = preload("res://Assets/Heroes/AllHairWizard/allhairwiz.glb")

var _strangle: Node3D
var _locks: Array[Node3D] = []
var _range := 9.0
var _radius := 5.5
var _range_scale := 1.0
var _info: Label
var _camera: Camera3D


func _ready() -> void:
	get_viewport().msaa_3d = Viewport.MSAA_4X
	var skills = JSON.parse_string(FileAccess.get_file_as_string("res://Sim/Data/Assets/heroes/hairy-wizard.json"))
	for skill in skills:
		if skill.get("AssetId") == 501:
			_range = float(skill["ProjectileRange"])
		elif skill.get("AssetId") == 503:
			_radius = float(skill["AreaRadius"])
	_strangle = HAIR.instantiate()
	_strangle.thickness = 1.4
	add_child(_strangle)
	for i in range(9):
		var lock: Node3D = HAIR.instantiate()
		lock.phase = float(i) * 1.71
		lock.thickness = 1.4
		lock.motion = 0.85
		add_child(lock)
		_locks.append(lock)
	_add_wizard(Vector3(-11, 0, 0))
	_add_wizard(Vector3(5, 0, 0))
	_add_label("STRANGLE — length test", Vector3(-6.5, 0.1, 2.2))
	_add_label("BAD HAIR DAY — radial reach test", Vector3(5, 0.1, 6.8))
	var ground := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(70, 50)
	ground.mesh = plane
	var material := StandardMaterial3D.new()
	material.albedo_color = Color(0.22, 0.25, 0.24)
	material.roughness = 0.95
	ground.material_override = material
	ground.position.y = -0.03
	add_child(ground)
	var light := DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-55, -30, 0)
	light.light_energy = 1.6
	light.shadow_enabled = true
	add_child(light)
	var world := WorldEnvironment.new()
	world.environment = Environment.new()
	world.environment.background_mode = Environment.BG_COLOR
	world.environment.background_color = Color(0.12, 0.15, 0.17)
	world.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	world.environment.ambient_light_color = Color(0.8, 0.85, 1.0)
	world.environment.ambient_light_energy = 0.65
	world.environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	add_child(world)
	_camera = Camera3D.new()
	_camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	_camera.size = 25
	_camera.position = Vector3(0, 19, 22)
	add_child(_camera)
	_camera.look_at(Vector3(0, 0, 0))
	_camera.current = true
	var canvas := CanvasLayer.new()
	add_child(canvas)
	var panel := VBoxContainer.new()
	panel.position = Vector2(24, 20)
	panel.custom_minimum_size.x = 430
	canvas.add_child(panel)
	_info = Label.new()
	panel.add_child(_info)
	var variants := OptionButton.new()
	for title in ["Original", "Messy", "Wild"]:
		variants.add_item(title)
	variants.selected = 1
	variants.item_selected.connect(_set_variant)
	panel.add_child(variants)
	var slider := HSlider.new()
	slider.min_value = 0.25
	slider.max_value = 2.0
	slider.step = 0.01
	slider.value = 1.0
	slider.value_changed.connect(_resize)
	panel.add_child(slider)
	var help := Label.new()
	help.text = "Drag to change reach · Wheel to zoom · 1 overview · 2 close-up"
	panel.add_child(help)
	_set_variant(1)
	_resize(1.0)


func _set_variant(index: int) -> void:
	var preset = VARIANTS[index].instantiate()
	for lock in [_strangle] + _locks:
		lock.motion = preset.motion
		lock.messiness = preset.messiness
		lock.motion_speed = preset.motion_speed
		if index == 0 and lock != _strangle:
			lock.motion = 0.85
	preset.free()


func _resize(value: float) -> void:
	_range_scale = value
	_strangle.configure_between(Vector3(-10.5, 1.4, 0), Vector3(-10.5 + _range * value, 1.4, 0))
	for i in range(_locks.size()):
		var angle := float(i) * TAU / _locks.size()
		var root := Vector3(5, 1.4, 0)
		var tip := root + Vector3(cos(angle), -0.12, sin(angle)) * _radius * value
		_locks[i].configure_between(root, tip)
	_info.text = "Hair lock prototype\nStrangle: %.2f m    Ult radius: %.2f m" % [_range * value, _radius * value]


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			_camera.size = maxf(2, _camera.size * 0.9)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			_camera.size = minf(60, _camera.size / 0.9)
	if event is InputEventKey and event.pressed:
		if event.keycode == KEY_1:
			_camera.position = Vector3(0, 19, 22)
			_camera.look_at(Vector3.ZERO)
			_camera.size = 25
		elif event.keycode == KEY_2:
			var center := Vector3(-10.5 + _range * _range_scale * 0.5, 1.4, 0)
			_camera.position = center + Vector3(0, 3, 6)
			_camera.look_at(center)
			_camera.size = 7.5


func _add_wizard(at: Vector3) -> void:
	var wizard: Node3D = WIZARD.instantiate()
	wizard.position = at
	add_child(wizard)
	var rig := wizard.get_child(0) as Node3D
	rig.scale = Vector3.ONE * 2.4
	var animations := wizard.get_node_or_null("AnimationPlayer") as AnimationPlayer
	if animations != null and animations.has_animation("AllHairWizard_Idle"):
		animations.play("AllHairWizard_Idle")


func _add_label(text: String, at: Vector3) -> void:
	var label := Label3D.new()
	label.text = text
	label.position = at
	label.font_size = 40
	label.pixel_size = 0.009
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	add_child(label)
