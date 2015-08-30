#version 330 core

uniform sampler2D uni_texture;

// Interpolated values from the vertex shaders
in vec2 UV;

out vec4 out_color;

void main()
{
	out_color = vec4(1.0,0,0,1);//texture(uni_texture, UV).rgb;
}