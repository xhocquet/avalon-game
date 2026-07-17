@tool
@icon("res://addons/constructive_telegraphs/icons/con_telegraph_instance_3d_icon.png")
extends Node3D
class_name ConTelegraphInstance3D

@export var family: ConTelegraphFamily: set = _set_family

@export_category("Duration")
@export_range(0, 10) var fill_duration: float = 2.0
@export_range(0, 1) var ramp_in_duration: float = 0.1
@export_range(0, 1) var ramp_out_duration: float = 0.2
@export_range(0, 1) var fade_out_duration: float = 0.3

@export_category("Effects")
@export var has_border_noise: bool
@export var has_effect_flicker: bool
@export var has_pattern_noise: bool
@export var has_pattern_1: bool

@export_category("Animation")
@export var in_progress: bool = false
@export var autoloop: bool = false
@export_range(0, 10, 0.001) var loop_delay: float = 1
@export_tool_button("Play once")
var play_once_button = play_once
@export_range(0, 1, 0.001, "or_greater") var fill_progress: float

signal fill_completed
signal fade_out_completed

var fill_completed_already := false
var fade_out_completed_already := false

func _ready() -> void:
	add_to_group(ConTelegraphManager.TELEGRAPH_GROUP)


func _process(delta: float) -> void:
	if in_progress:
		update_fill_progress(delta)
		emit_signals()
		if autoloop:
			var delay_progress := (fill_progress - 1) * fill_duration / loop_delay
			if delay_progress >= 1:
				fill_progress = 0


func update_fill_progress(delta: float) -> void:
	fill_progress = fill_progress + delta / fill_duration


func ramp_in_progress() -> float:
	if ramp_in_duration <= 0:
		return 2.0 if fill_progress > 0.0 else 0.0
	return fill_progress * fill_duration / ramp_in_duration


func ramp_out_progress() -> float:
	if ramp_out_duration <= 0:
		return 2.0 if fill_progress >= 1.0 else 0.0
	return (fill_progress * fill_duration + ramp_out_duration - fill_duration) / ramp_out_duration


func fade_out_progress() -> float:
	if fade_out_duration <= 0.0:
		return 2.0 if fill_progress > 1.0 else 0.0
	return (fill_progress - 1) * fill_duration / fade_out_duration


func emit_signals() -> void:
	if fill_progress >= 1:
		if not fill_completed_already:
			fill_completed.emit()
			fill_completed_already = true
		if fade_out_progress() >= 1:
			if not fade_out_completed_already:
				fade_out_completed.emit()
				fade_out_completed_already = true
		else:
			fade_out_completed_already = false
	else:
		fill_completed_already = false


func play_once() -> void:
	var editor_interface = Engine.get_singleton("EditorInterface")
	if !editor_interface:
		return
	var undo_redo = editor_interface.get_editor_undo_redo()

	undo_redo.create_action("Play once")
	undo_redo.add_do_property(self, &"fill_progress", 0)
	undo_redo.add_do_property(self, &"in_progress", true)
	undo_redo.add_do_property(self, &"autoloop", false)
	undo_redo.add_undo_property(self, &"fill_progress", fill_progress)
	undo_redo.add_undo_property(self, &"in_progress", in_progress)
	undo_redo.add_undo_property(self, &"autoloop", autoloop)
	undo_redo.commit_action()


func get_areas() -> Array[ConTelegraphArea3D]:
	var areas: Array[ConTelegraphArea3D]
	areas.assign(get_children().filter(func(node): return node is ConTelegraphArea3D))
	return areas


func _get_configuration_warnings() -> PackedStringArray:
	var warnings = []
	if family == null:
		warnings.append("This node has no telegraph family, so it can't be stylized.")
	if get_areas().size() == 0:
		warnings.append("This node contains no areas. \nConsider adding one or more ConTelegraphArea3D as a child.")
	return warnings


func _set_family(value: ConTelegraphFamily) -> void:
	update_configuration_warnings()
	family = value
