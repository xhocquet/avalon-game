@tool
class_name ConTelegraphWriter

# --- Family flags ---

const FLAG_F_OUTLINE_PIXEL := 1 << 0
const FLAG_F_OUTLINE_SPATIAL := 1 << 1
const FLAG_F_OUTLINE_GROUPED := 1 << 2
const FLAG_F_OUTLINE_INDIVIDUAL := 1 << 3

# --- Instance flags ---

const FLAG_I_BORDER_NOISE := 1 << 4

const FLAG_I_EFFECT_FLICKER := 1 << 5

const FLAG_I_PATTERN_NOISE := 1 << 7
const FLAG_I_PATTERN_1 := 1 << 8

# --- Area flags ---

const FLAG_A_OP_UNION := 1 << 12
const FLAG_A_OP_INTERSECTION := 1 << 13
const FLAG_A_OP_SUBTRACTION := 1 << 14

const FLAG_A_SHAPE_SPHERE := 1 << 15
const FLAG_A_SHAPE_BOX := 1 << 16
const FLAG_A_SHAPE_CYLINDER := 1 << 17

const FLAG_A_FILL_IN_TO_OUT := 1 << 18
const FLAG_A_FILL_FORWARD := 1 << 19
const FLAG_A_FILL_INVERTED := 1 << 20

# --- Family data coordinates ---

const F_BASE_RGB := 1
const F_BASE_A := 2
const F_BASE_OUTLINE_RGB := 3
const F_BASE_OUTLINE_A := 4
const F_BASE_OUTLINE_1 := 5
const F_BORDER_NOISE_1 := 6
const F_BORDER_NOISE_2 := 7
const F_EFFECT_FLICKER := 8
const F_FILL_RGB := 10
const F_FILL_A := 11
const F_FILL_HIGHLIGHT_RGB := 12
const F_FILL_HIGHLIGHT_A := 13
const F_FILL_HIGHLIGHT_1 := 14
const F_PATTERN_NOISE_1 := 15
const F_PATTERN_1 := 16
const F_1 := 18

# --- Instance data coordinates ---

const I_ANIM_1 := 20
const I_ANIM_2 := 21
const I_BASIS_X := 22
const I_BASIS_Y := 23
const I_BASIS_Z := 24
const I_ORIGIN := 25
const I_1 := 26

# --- Area data coordinates ---

const A_BASIS_X := 29
const A_BASIS_Y := 30
const A_BASIS_Z := 31
const A_ORIGIN := 32
const A_1 := 33
const A_EXTENTS := 35
const A_SPHERE := 36


func write_data(instances: Array[ConTelegraphInstance3D], to: Image) -> void:
	var outlines: Dictionary[StringName, int] = {}
	var family_names: Dictionary[StringName, int] = {}
	var outline_idx = 0
	var family_idx = 0
	var instance_idx = 0
	var area_idx = 0

	for instance in instances:
		if instance.family == null:
			continue
		for area in instance.get_areas():
			if !area.visible:
				continue
			set_area_data(to, area_idx, area, instance)
			area_idx += 1
		
		var family_name = instance.family.internal_name
		if !family_names.has(family_name):
			var outline = instance.family.outline_group
			if !outlines.has(outline):
				outlines[outline] = outline_idx
				outline_idx += 1
			set_family_data(to, family_idx, instance.family, outline_idx)
			family_names[family_name] = family_idx
			family_idx += 1
		
		set_instance_data(to, instance_idx, instance, area_idx, family_names[family_name])
		instance_idx += 1
	
	set_scene_data(to, family_idx, instance_idx, area_idx, outline_idx)


func set_scene_data(to: Image, family_count: int, instance_count: int, area_count: int, outline_count: int) -> void:
	set_pixel_f(to, 0, 0, family_count, instance_count, area_count)
	set_pixel_f(to, 1, 0, outline_count, 0, 0)


func set_family_data(to: Image, family_idx: int, family: ConTelegraphFamily, outline_idx: int) -> void:
	set_pixel_c(to, family_idx, F_BASE_RGB, family.base_color)
	set_pixel_f(to, family_idx, F_BASE_A, family.base_color.a, 0, 0)
	set_pixel_c(to, family_idx, F_BASE_OUTLINE_RGB, family.base_outline_color)
	set_pixel_f(to, family_idx, F_BASE_OUTLINE_A, family.base_outline_color.a, 0, 0)
	set_pixel_f(to, family_idx, F_BASE_OUTLINE_1, family.outline_width, 0, 0)
	set_pixel_f(to, family_idx, F_BORDER_NOISE_1, family.border_noise_1_amplitude, family.border_noise_1_frequency, family.border_noise_1_speed)
	set_pixel_f(to, family_idx, F_BORDER_NOISE_2, family.border_noise_2_amplitude, family.border_noise_2_frequency, family.border_noise_2_speed)
	set_pixel_f(to, family_idx, F_EFFECT_FLICKER, family.effect_flicker_amplitude, 0, family.effect_flicker_speed)
	set_pixel_c(to, family_idx, F_FILL_RGB, family.fill_color)
	set_pixel_f(to, family_idx, F_FILL_A, family.fill_color.a, 0, 0)
	set_pixel_c(to, family_idx, F_FILL_HIGHLIGHT_RGB, family.fill_highlight_color)
	set_pixel_f(to, family_idx, F_FILL_HIGHLIGHT_A, family.fill_highlight_color.a, 0, 0)
	set_pixel_f(to, family_idx, F_FILL_HIGHLIGHT_1, family.fill_highlight_trail, family.fill_highlight_width, 0)
	set_pixel_f(to, family_idx, F_PATTERN_NOISE_1, family.pattern_noise_amplitude, family.pattern_noise_frequency, family.pattern_noise_speed)
	set_pixel_f(to, family_idx, F_PATTERN_1, family.pattern_1_amplitude, family.pattern_1_frequency, family.pattern_1_speed)
	set_pixel_f(to, family_idx, F_1, compose_family_flags(family), family.normal_clip_angle, outline_idx)


func set_instance_data(to: Image, instance_idx: int, instance: ConTelegraphInstance3D, last_area_idx: int, family_idx: int) -> void:
	set_pixel_f(to, instance_idx, I_ANIM_1, instance.fill_progress, animated_instance_alpha_multiplier(instance), 0)
	set_pixel_v(to, instance_idx, I_BASIS_X, instance.global_basis.x)
	set_pixel_v(to, instance_idx, I_BASIS_Y, instance.global_basis.y)
	set_pixel_v(to, instance_idx, I_BASIS_Z, instance.global_basis.z)
	set_pixel_v(to, instance_idx, I_ORIGIN, instance.global_position)
	set_pixel_f(to, instance_idx, I_1, compose_instance_flags(instance), last_area_idx, family_idx)


func set_area_data(to: Image, area_idx: int, area: ConTelegraphArea3D, instance: ConTelegraphInstance3D) -> void:
	var area_basis := animated_area_basis(area, instance)
	set_pixel_v(to, area_idx, A_BASIS_X, area_basis.x)
	set_pixel_v(to, area_idx, A_BASIS_Y, area_basis.y)
	set_pixel_v(to, area_idx, A_BASIS_Z, area_basis.z)
	set_pixel_v(to, area_idx, A_ORIGIN, area.global_position)
	set_pixel_f(to, area_idx, A_1, compose_area_flags(area), animated_area_fill(area, instance), 0)
	set_pixel_v(to, area_idx, A_EXTENTS, area.extents)
	set_pixel_f(to, area_idx, A_SPHERE,
		area.radius,
		animated_area_angle(area, instance),
		animated_area_min_radius(area, instance),
	)


func compose_family_flags(family: ConTelegraphFamily) -> int:
	var flags: int = 0
	match family.outline_mode:
		ConTelegraphFamily.OutlineMode.PIXEL:
			flags |= FLAG_F_OUTLINE_PIXEL
		ConTelegraphFamily.OutlineMode.SPATIAL:
			flags |= FLAG_F_OUTLINE_SPATIAL
	match family.outline_subject:
		ConTelegraphFamily.OutlineSubject.GROUPED_TELEGRAPHS:
			flags |= FLAG_F_OUTLINE_GROUPED
		ConTelegraphFamily.OutlineSubject.INDIVIDUAL_TELEGRAPHS:
			flags |= FLAG_F_OUTLINE_INDIVIDUAL
	return flags


func compose_instance_flags(instance: ConTelegraphInstance3D) -> int:
	var flags: int = 0
	if instance.has_border_noise:
		flags |= FLAG_I_BORDER_NOISE
	if instance.has_effect_flicker:
		flags |= FLAG_I_EFFECT_FLICKER
	if instance.has_pattern_noise:
		flags |= FLAG_I_PATTERN_NOISE
	if instance.has_pattern_1:
		flags |= FLAG_I_PATTERN_1
	return flags


func compose_area_flags(area: ConTelegraphArea3D) -> int:
	var flags: int = 0
	match area.fill_mode:
		ConTelegraphArea3D.FillMode.IN_TO_OUT:
			flags |= FLAG_A_FILL_IN_TO_OUT
		ConTelegraphArea3D.FillMode.OUT_TO_IN:
			flags |= FLAG_A_FILL_IN_TO_OUT
			flags |= FLAG_A_FILL_INVERTED
		ConTelegraphArea3D.FillMode.FORWARD:
			flags |= FLAG_A_FILL_FORWARD
		ConTelegraphArea3D.FillMode.BACKWARD:
			flags |= FLAG_A_FILL_FORWARD
			flags |= FLAG_A_FILL_INVERTED
	match area.operation:
		ConTelegraphArea3D.BooleanOperation.UNION:
			flags |= FLAG_A_OP_UNION
		ConTelegraphArea3D.BooleanOperation.INTERSECTION:
			flags |= FLAG_A_OP_INTERSECTION
		ConTelegraphArea3D.BooleanOperation.SUBTRACTION:
			flags |= FLAG_A_OP_SUBTRACTION
	match area.shape_primitive:
		ConTelegraphArea3D.ShapePrimitive.SPHERE:
			flags |= FLAG_A_SHAPE_SPHERE
		ConTelegraphArea3D.ShapePrimitive.BOX:
			flags |= FLAG_A_SHAPE_BOX
		ConTelegraphArea3D.ShapePrimitive.CYLINDER:
			flags |= FLAG_A_SHAPE_CYLINDER
	return flags


func animated_instance_alpha_multiplier(instance: ConTelegraphInstance3D) -> float:
	var alpha_multiplier: float = 1
	if instance.ramp_out_progress() > 0:
		alpha_multiplier = 1  + max(0, (abs(instance.ramp_out_progress())) / 2)
	if instance.fade_out_progress() > 0:
		alpha_multiplier = max(0, (1 - instance.fade_out_progress()))
		return alpha_multiplier
	if instance.has_effect_flicker:
		alpha_multiplier *= 1 + (sin(Time.get_ticks_msec() * 0.001 * instance.family.effect_flicker_speed) + 1) * instance.family.effect_flicker_amplitude
	return alpha_multiplier


func animated_area_angle(area: ConTelegraphArea3D, instance: ConTelegraphInstance3D) -> float:
	if instance.ramp_in_progress() > 1:
		return area.angle
	var scale := sin(instance.ramp_in_progress() * PI)
	var divisor: float = 1 + (scale * (area.bounce_multiplier * area.bounce_multiplier - 1))
	return area.angle / divisor


func animated_area_basis(area: ConTelegraphArea3D, instance: ConTelegraphInstance3D) -> Basis:
	if instance.ramp_in_progress() > 1:
		return area.global_basis
	var scale := sin(instance.ramp_in_progress() * PI)
	return area.global_basis * (1 + scale * (area.bounce_multiplier - 1))


func animated_area_fill(area: ConTelegraphArea3D, instance: ConTelegraphInstance3D) -> float:
	var fill_min = area.fill_min
	var fill_max = area.fill_max
	if area.shape_primitive != ConTelegraphArea3D.ShapePrimitive.BOX:
		if area.fill_mode == ConTelegraphArea3D.FillMode.IN_TO_OUT:
			fill_min += clampf(area.min_radius / area.radius, 0, 1)
		elif area.fill_mode == ConTelegraphArea3D.FillMode.OUT_TO_IN:
			fill_max -= clampf(area.min_radius / area.radius, 0, 1)
	return remap(instance.fill_progress, 0, 1, fill_min, fill_max)


func animated_area_min_radius(area: ConTelegraphArea3D, instance: ConTelegraphInstance3D) -> float:
	if instance.ramp_in_progress() > 1:
		return area.min_radius
	var scale := sin(instance.ramp_in_progress() * PI)
	return area.min_radius * (1 + scale * ((1 / area.bounce_multiplier) - 1))


func set_pixel_c(to: Image, x: int, y: int, color: Color) -> void:
	to.set_pixel(x, y, color)


func set_pixel_f(to: Image, x: int, y: int, r: float, g: float, b: float) -> void:
	set_pixel_c(to, x, y, Color(r, g, b))


func set_pixel_v(to: Image, x: int, y: int, vec: Vector3) -> void:
	set_pixel_f(to, x, y, vec.x, vec.y, vec.z)
