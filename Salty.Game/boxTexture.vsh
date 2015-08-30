#version 330

uniform mat4 uni_projection;
uniform mat4 uni_view;
uniform mat4 uni_model;

in vec2 position;
in vec2 in_uv;

out vec2 uv;

void main ()
{
	vec4 p = (uni_projection * uni_view * uni_model) * vec4 (position, 0, 1);
	gl_Position = vec4 (p.x, p.y, p.z, 1);
	uv = in_uv;
}