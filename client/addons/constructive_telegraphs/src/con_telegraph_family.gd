@tool
extends Resource
class_name ConTelegraphFamily

enum OutlineMode {
	## No outlines.
	NONE,
	## Pixel mode creates a one-pixel wide outline.
	PIXEL,
	## Spatial mode creates an outline of configurable thickness based on world space.
	SPATIAL,
}

enum OutlineSubject {
	## No outlines.
	NONE,
	## Outline around the combination of all telegraphs that share a family.
	GROUPED_TELEGRAPHS,
	## Outlines around each individual telegraph.
	INDIVIDUAL_TELEGRAPHS,
}

## Used for internal de-duplication. If family data was already written once in a frame, the data is referenced instead of duplicated.
@export var internal_name := &"Default"
## If left blank, internal name is used for the outline group. When using an outline subject of grouped telegraphs, all telegraphs with a matching value for this variable will share an outline.
@export var outline_group: StringName

@export_range(0.001, 180, 0.001, "suffix: deg") var normal_clip_angle: float = 90

@export_group("Color")
@export var base_color := Color(Color.RED, 0.1)
@export var base_outline_color := Color(Color.RED, 0.9)
@export var fill_color := Color(Color.RED, 0.5)
@export var fill_highlight_color := Color(Color.RED, 0.7)

@export_group("Outline")
@export var outline_mode := OutlineMode.PIXEL
@export var outline_subject := OutlineSubject.GROUPED_TELEGRAPHS
## Only used in spatial outlines. Thickness of the outline.
@export_range(0, 0.1, 0.001, "or_greater") var outline_width: float = 0.05
@export_range(0, 5, 0.001, "or_greater") var fill_highlight_trail: float = 1
@export_range(0, 0.5, 0.001, "or_greater") var fill_highlight_width: float = 0.02

@export_group("Border Noise 1")
@export_range(0, 1, 0.001, "or_greater") var border_noise_1_amplitude: float = 0.5
@export_range(0, 10, 0.001, "or_greater") var border_noise_1_frequency: float = 0.5
@export_range(0, 0.5, 0.001, "or_greater") var border_noise_1_speed: float = 0.3

@export_group("Border Noise 2")
@export_range(0, 0.5, 0.001, "or_greater") var border_noise_2_amplitude: float = 0.3
@export_range(0, 10, 0.001, "or_greater") var border_noise_2_frequency: float = 5.0
@export_range(0, 0.1, 0.001, "or_greater") var border_noise_2_speed: float = 0.1

@export_group("Effect Flicker")
@export_range(0, 0.25, 0.001, "or_greater") var effect_flicker_amplitude: float = 0.1
@export_range(0, 10, 0.001, "or_greater") var effect_flicker_speed: float = 5

@export_group("Pattern Noise")
@export_range(0, 3, 0.001, "or_greater") var pattern_noise_amplitude: float = 1
@export_range(0, 1, 0.001, "or_greater") var pattern_noise_frequency: float = 0.1
@export_range(0, 0.25, 0.001, "or_greater") var pattern_noise_speed: float = 0.01

@export_group("Pattern 1")
@export_range(0, 2, 0.001, "or_greater") var pattern_1_amplitude: float = 1.5
@export_range(0, 1, 0.001, "or_greater") var pattern_1_frequency: float = 0.2
@export_range(0, 0.25, 0.001, "or_greater") var pattern_1_speed: float = 0.05
