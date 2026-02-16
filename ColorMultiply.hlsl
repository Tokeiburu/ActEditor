sampler2D input : register(s0);

float4 Color : register(c0);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 pixel = tex2D(input, uv);
    pixel.rgb *= Color.rgb;
	pixel.r = 1;
	pixel.g = 1;
	pixel.b = 1;
	return pixel;
}