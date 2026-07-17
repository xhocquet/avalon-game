@tool
@icon("res://addons/constructive_telegraphs/icons/con_telegraph_area_3d_icon.png")
extends Node3D
class_name ConTelegraphArea3D

enum BooleanOperation {
	UNION,
	INTERSECTION,
	SUBTRACTION,
}

enum FillMode {
	IN_TO_OUT,
	OUT_TO_IN,
	FORWARD,
	BACKWARD,
	NONE,
}

enum ShapePrimitive {
	BOX,
	CYLINDER,
	SPHERE,
}

@export var operation: BooleanOperation

@export_category("Shape")
@export var shape_primitive: ShapePrimitive
@export var extents := Vector3(1, 1, 1)
@export_range(0, 360) var angle: float = -1
@export var radius: float = 1
## Creates a 'donut hole' effect. This is a convenient way to achieve the visual result of subtracting a cylinder of infinite height and reciprocal bounce multiplier while adjusting the min fill property to compensate.
@export var min_radius: float = -1

@export_category("Fill")
@export var fill_mode: FillMode
@export_range(0, 1) var fill_min: float = 0
@export_range(0, 1) var fill_max: float = 1

@export_category("Animation")
@export_range(0, 2) var bounce_multiplier: float = 1.1
