## Uses raymarching to display the full 3D space telegraph areas occupy.
@tool
extends Node3D
class_name CTDebugDisplay


@onready var shader_material := ShaderMaterial.new()


func _ready() -> void:
	init_full_screen_quad()
	get_parent().connect("data_transfer_texture_updated", _update_draw_material)


func init_full_screen_quad() -> void:
	for child in get_children():
		remove_child(child)
		child.queue_free()
	
	shader_material.shader = preload("res://addons/constructive_telegraphs/shaders/con_telegraph_debug_display.gdshader")
	var mesh_instance := MeshInstance3D.new()
	mesh_instance.mesh = QuadMesh.new()
	mesh_instance.mesh.flip_faces = true
	mesh_instance.mesh.size = Vector2(2, 2)
	mesh_instance.material_override = shader_material
	add_child(mesh_instance)


func _update_draw_material(data_transfer_texture: ImageTexture) -> void:
	shader_material.set_shader_parameter("data_transfer_texture", data_transfer_texture)
