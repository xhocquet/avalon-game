@tool
extends "res://Scripts/FX/HairLock.gd"


func _update_material() -> void:
	super._update_material()
	var upper := get_node_or_null("UpperLayer") as Node3D
	if upper != null:
		upper.length = length
