#version 330 core

uniform sampler2D uni_texture;

// Interpolated values from the vertex shaders
in vec2 uv;

out vec4 out_color;

void main()
{
	out_color = texture(uni_texture, uv);
}