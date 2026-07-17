/**
Thanks to Inigo Quilez for the SDF (https://iquilezles.org/articles/distfunctions/)
*/

float sdAngle(vec2 sample_pos, float angle) {
	vec2 c = vec2(sin(angle), cos(angle));
	vec2 p = sample_pos;
	p.x = abs(p.x);
  p.y = -p.y;
	float m = length(p - c * max(dot(p, c), 0.));
	return m * sign(c.y * p.x - c.x * p.y);
}

// Calculates signed distance to a box centered at the origin using Pythagorean distance from corner.
float sdBox( vec3 sample_pos, vec3 half_extents )
{
  vec3 q = abs(sample_pos) - half_extents;
  return length(max(q,0.0)) + min(max(q.x,max(q.y,q.z)),0.0);
}

// Calculates signed distance to a box centered at the origin, using max orthogonal distance from edges.
float sdBoxOrthogonal( vec3 sample_pos, vec3 half_extents )
{
    vec3 q = abs(sample_pos) - half_extents;
    return max(max(q.x, q.y), q.z);
}

// Calculates signed distance to a circle centered at the origin.
float sdCircle( vec2 sample_pos, float radius )
{
    return length(sample_pos) - radius;
}

// Calculates signed distance to a sphere centered at the origin.
float sdSphere( vec3 sample_pos, float radius )
{
  return length(sample_pos) - radius;
}

// Calculates signed distance to a vertical capped cylinder centered at the origin.
float sdCappedCylinder( vec3 sample_pos, float height, float radius )
{
  vec2 d = abs(vec2(length(sample_pos.xz),sample_pos.y)) - vec2(radius,height);
  return min(max(d.x,d.y),0.0) + length(max(d,0.0));
}

float sdOpUnion( float a, float b )
{
    return min(a, b);
}

float sdOpSubtraction( float a, float b )
{
    return max(-a, b);
}

float sdOpIntersection( float a, float b )
{
    return max(a, b);
}
