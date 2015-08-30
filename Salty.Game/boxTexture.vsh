#version 330

uniform mat4 uni_projection;
uniform mat4 uni_view;
uniform mat4 uni_model;

in vec2 position;

void main ()
{
	vec4 p = (uni_projection * uni_view * uni_model) * vec4 (position, 0, 1);
	gl_Position = vec4 (p.x, p.y, p.z, 1);
}