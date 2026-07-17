## Thanks to Inigo Quilez for the SDF (https://iquilezles.org/articles/distfunctions/)
class_name DistFunctions

static func sdAngle(sample_pos: Vector2, angle: float) -> float:
	var c := Vector2(sin(angle), cos(angle))
	var p := sample_pos
	p.x = abs(p.x)
	p.y = -p.y
	var m: float = (p - c * max(p.dot(c), 0)).length()
	return m * sign(c.y * p.x - c.x * p.y)


static func sdBox(sample_pos: Vector3, half_extents: Vector3) -> float:
	var q: Vector3 = abs(sample_pos) - half_extents
	return (max(q, 0.0)).length() + min(max(q.x, max(q.y, q.z)), 0.0)


static func sdBoxOrthogonal(sample_pos: Vector3, half_extents: Vector3) -> float:
	var q: Vector3 = abs(sample_pos) - half_extents
	return max(max(q.x, q.y), q.z)


static func sdCircle(sample_pos: Vector2, radius: float) -> float:
	return sample_pos.length() - radius


static func sdSphere(sample_pos: Vector3, radius: float) -> float:
	return sample_pos.length() - radius


static func sdCappedCylinder(sample_pos: Vector3, height: float, radius: float) -> float:
	var d: Vector2 = abs(Vector2(Vector2(sample_pos.x, sample_pos.z).length(), sample_pos.y)) - Vector2(radius, height)
	return min(max(d.x, d.y), 0) + d.max(Vector2(0, 0)).length()


static func sdOpUnion(left: float, right: float) -> float:
	return min(left, right)


static func sdOpSubtraction(left: float, right: float) -> float:
	return max(-left, right)


static func sdOpIntersection(left: float, right: float) -> float:
	return max(left, right)
