shader_type canvas_item;
render_mode unshaded;

// Adapted from https://www.shadertoy.com/view/MssfD7
uniform float opacity : hint_range(0.0, 1.0) = 0.2;
uniform float animation_speed : hint_range(0.0, 2.0) = 0.25;
uniform float tentacle_width : hint_range(0.25, 4.0) = 2.0;
uniform float signal_strength : hint_range(0.0, 1.0) = 0.0; // Replaces iChannel0.

float hair_comb(vec2 p, float subdiv, float time) {
	subdiv /= tentacle_width;
	time *= animation_speed;
	vec2 peval = p;
	float pattern = step(0.0, sin(3.14159 * p.x * subdiv));
	peval.x = floor(p.x * subdiv) / subdiv;

	float displacement = (0.1 + 0.1 * exp(3.0 * signal_strength * signal_strength))
		* exp(-0.05 * peval.x * peval.x);
	if (pattern > 0.0) {
		peval.y = 0.15 * sin(17.0 * peval.x + 10.0 * time) - p.y + displacement;
	} else {
		peval.y = 0.15 * sin(233.0 * peval.x + 7.0 * time) - p.y - 0.5 - displacement;
	}

	float f = step(0.0, peval.y);
	peval.x = p.x - peval.x - 1.0 / (2.0 * subdiv);
	vec2 circle_position = 2.0 * subdiv * peval;
	float circle = 1.0 - step(1.0, dot(circle_position, circle_position));
	if (pattern > 0.0) {
		f = max(f, circle);
	} else {
		f -= circle;
	}
	return step(1.0, f);
}

void fragment() {
	vec2 p = vec2(UV.x - 0.5, 0.5 - UV.y);
	p.x *= SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
	p *= 20.0;

	vec3 col = vec3(0.98, 0.63, 0.49);
	col = mix(col, vec3(0.97, 0.38, 0.46), hair_comb(p - vec2(0.0, 6.0), 4.0, TIME));
	col = mix(col, vec3(0.97, 0.17, 0.48), hair_comb(p - vec2(0.0, 3.0), 2.0, TIME));
	col = mix(col, vec3(0.91, 0.098, 0.48), hair_comb(p, 1.0, TIME));
	col = mix(col, vec3(0.83, 0.1, 0.47), hair_comb(p + vec2(0.0, 4.0), 0.5, TIME));
	COLOR = vec4(col, opacity);
}
