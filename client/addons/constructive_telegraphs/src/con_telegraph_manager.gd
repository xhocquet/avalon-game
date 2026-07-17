@tool
extends Node
class_name ConTelegraphManager

signal data_transfer_texture_updated(data_transfer_texture: ImageTexture)

const TELEGRAPH_GROUP := "con_telegraph"

const DATA_HEIGHT := 64
const DATA_WIDTH := 64

@onready var surface_overlay: ShaderMaterial = preload("../resources/con_telegraph_next_pass.tres")

@onready var data_image: Image
@onready var data_transfer_texture: ImageTexture = preload("../resources/con_telegraph_data_transfer_texture.tres")
@onready var writer := ConTelegraphWriter.new()


func _ready() -> void:
	init_data_transfer_texture()


func _process(_delta: float) -> void:
	var instances := gather_visible_telegraph_instances()
	writer.write_data(instances, data_image)

	update_data_transfer_texture()
	update_surface_overlay_material()
	data_transfer_texture_updated.emit(data_transfer_texture)


func gather_visible_telegraph_instances() -> Array[ConTelegraphInstance3D]:
	var instances: Array[ConTelegraphInstance3D]
	instances.assign(get_tree().get_nodes_in_group(TELEGRAPH_GROUP) \
		.filter(func(node): return node is ConTelegraphInstance3D and node.visible))
	return instances


func init_data_transfer_texture() -> void:
	data_image = Image.create_empty(DATA_WIDTH, DATA_HEIGHT, false, Image.FORMAT_RGBF)
	data_transfer_texture = ImageTexture.create_from_image(data_image)


func update_data_transfer_texture() -> void:
	data_transfer_texture.update(data_image)


func update_surface_overlay_material() -> void:
	surface_overlay.set_shader_parameter("data_transfer_texture", data_transfer_texture)
