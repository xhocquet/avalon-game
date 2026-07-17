class_name ConTelegraphUtils

static func is_node_in_telegraph(node: Node3D, telegraph: ConTelegraphInstance3D, hit_radius: float) -> bool:
	return is_position_in_telegraph(node.global_position, telegraph, hit_radius)


static func is_position_in_telegraph(position: Vector3, telegraph: ConTelegraphInstance3D, hit_radius: float) -> bool:
	return distance_to_telegraph(position, telegraph) <= hit_radius


static func distance_to_telegraph(position: Vector3, telegraph: ConTelegraphInstance3D) -> float:
	var instance_sdist: float = INF
	for area in telegraph.get_areas():
		var area_sdist := distance_to_area(position, area)

		match area.operation:
			ConTelegraphArea3D.BooleanOperation.UNION:
				instance_sdist = DistFunctions.sdOpUnion(instance_sdist, area_sdist)
			ConTelegraphArea3D.BooleanOperation.INTERSECTION:
				instance_sdist = DistFunctions.sdOpIntersection(instance_sdist, area_sdist)
			ConTelegraphArea3D.BooleanOperation.SUBTRACTION:
				instance_sdist = DistFunctions.sdOpSubtraction(instance_sdist, area_sdist)

	return instance_sdist


static func distance_to_area(position: Vector3, area: ConTelegraphArea3D) -> float:
	var area_sdist: float = INF
	var position_in_area = area.to_local(position)

	match area.shape_primitive:
		ConTelegraphArea3D.ShapePrimitive.BOX:
			area_sdist = DistFunctions.sdBoxOrthogonal(position_in_area, area.extents)
		ConTelegraphArea3D.ShapePrimitive.CYLINDER:
			area_sdist = DistFunctions.sdCappedCylinder(position_in_area, area.extents.y, area.radius)
		ConTelegraphArea3D.ShapePrimitive.SPHERE:
			area_sdist = DistFunctions.sdSphere(position_in_area, area.radius)

	if area.min_radius > -1:
		area_sdist = DistFunctions.sdOpSubtraction(DistFunctions.sdCircle(Vector2(position_in_area.x, position_in_area.z), area.min_radius), area_sdist)
	if area.angle > -1:
		var angle_sdist = DistFunctions.sdAngle(Vector2(position_in_area.x, position_in_area.z), deg_to_rad(area.angle) * 0.5)
		area_sdist = DistFunctions.sdOpIntersection(area_sdist, angle_sdist)

	return area_sdist
