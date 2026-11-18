extends SceneTree

# Run with --path client --script ../scripts/bake-minimap.gd.
func _initialize() -> void:
	call_deferred("bake")

func bake() -> void:
	var source: Node3D = load("res://Scenes/World/World.tscn").instantiate()
	var guides: Node3D = source.get_node("NavigationRegion3D/GroundMesh/MinimapGuides")
	var ground: MeshInstance3D = guides.Ground.duplicate()
	var output: String = guides.Texture.resource_path
	var bounds := ground.get_aabb()
	ground.transform = Transform3D.IDENTITY
	ground.scale = Vector3(1.0 / bounds.size.x, 1, 1.0 / bounds.size.z)
	ground.position = Vector3(-bounds.get_center().x / bounds.size.x, 0, -bounds.get_center().z / bounds.size.z)
	for child in ground.get_children():
		child.free()
	source.free()
	var material: ShaderMaterial = ground.mesh.surface_get_material(0).duplicate()
	var shader := Shader.new()
	shader.code = material.shader.code.replace("shader_type spatial;", "shader_type spatial;\nrender_mode unshaded;")
	material.shader = shader
	material.next_pass = null
	ground.material_override = material
	var viewport := SubViewport.new()
	viewport.size = Vector2i(1024, 1024)
	viewport.own_world_3d = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	viewport.add_child(ground)
	var camera := Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 1.0
	camera.position = Vector3(0, 60, 0)
	camera.rotation_degrees = Vector3(-90, 0, 0)
	viewport.add_child(camera)
	await process_frame
	await RenderingServer.frame_post_draw
	await process_frame
	await RenderingServer.frame_post_draw
	var image := viewport.get_texture().get_image()
	var error := image.save_png(output)
	print("MINIMAP_BAKED result=", error, " size=", image.get_size())
	viewport.queue_free()
	await process_frame
	quit.call_deferred(error)

