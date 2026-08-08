extends Node3D
## Lane telegraph, the runtime-built shape of demo_spell_7: N parallel bars running down local -Z.
## Every number comes from the caster's SkillAsset row via configure(), so the geometry can't drift
## from what the sim fires. Colour/effects come from the family resource the caller picks.

const AreaScript := preload("res://addons/constructive_telegraphs/src/con_telegraph_area_3d.gd")

## lane_spacing/half_width/length/start_offset are world units measured along the node's own axes;
## lane_height is the box's vertical half-extent, which only has to clear the terrain it decals onto.
func configure(family: ConTelegraphFamily, lane_count: int, lane_spacing: float,
		lane_half_width: float, lane_length: float, start_offset: float,
		fill_seconds: float, lane_height: float) -> void:
	# get_node rather than @onready: the caller places and configures the node in one go, and @onready
	# would leave this null whenever that happens before _ready.
	var instance: ConTelegraphInstance3D = get_node("ConTelegraphInstance3D")
	instance.family = family
	instance.fill_duration = maxf(fill_seconds, 0.01)

	var first_offset := -lane_spacing * (lane_count - 1) * 0.5
	for i in lane_count:
		var area: ConTelegraphArea3D = AreaScript.new()
		area.name = "Lane%d" % i
		area.shape_primitive = ConTelegraphArea3D.ShapePrimitive.BOX
		area.fill_mode = ConTelegraphArea3D.FillMode.FORWARD
		area.extents = Vector3(lane_half_width, lane_height, lane_length * 0.5)
		# Negated x: local +X is the caster's left, the mirror of SkillProjectiles' `right`.
		area.position = Vector3(
			-(first_offset + lane_spacing * i),
			0,
			-(start_offset + lane_length * 0.5))
		instance.add_child(area)

	instance.fill_progress = 0.0
	instance.in_progress = true


func _on_fade_out_completed() -> void:
	queue_free()
