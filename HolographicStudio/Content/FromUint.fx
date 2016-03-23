// SpriteBatch expects that default texture parameter will have name 'Texture'
Texture2D<uint> Texture : register(t0);

// SpriteBatch expects that default texture sampler parameter will have name 'TextureSampler'
sampler TextureSampler : register(s0);

// SpriteBatch expects that default vertex transform parameter will have name 'MatrixTransform'
row_major float4x4 MatrixTransform;

struct VS_IN
{
	float4 Position : SV_Position;
	float2 TexCoord : TEXCOORD0;
};

struct PS_IN
{
	float2 tex : TEXCOORD0;
	float4 pos : SV_POSITION;
};

PS_IN VS(VS_IN input)
{
	PS_IN output;

	output.tex = input.TexCoord.xy;
	output.pos = mul(input.Position, MatrixTransform);

	return output;
}

struct PSInput
{
	float4 pos : SV_Position;
	float2 tex : TEXCOORD;
};

float PS(PSInput input, float4 screenSpace : SV_Position) : SV_Target0
{
	float value = Texture.Load(int3(screenSpace.x, screenSpace.y, 0));
	return value;
}

technique
{
	pass
	{
		Profile = 11;
		VertexShader = VS;
		PixelShader = PS;
	}
}