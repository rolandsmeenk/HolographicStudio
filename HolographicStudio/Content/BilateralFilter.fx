// SpriteBatch expects that default texture parameter will have name 'Texture'
Texture2D<float> Texture : register(t0);

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

cbuffer constants : register(b0)
{
	float spatialSigma; // pixels, 1/sigma
	float intensitySigma; // m, 1/sigma
}

#define PI 3.14159265358979323846

float Gaussian(float x, float sigma)
{
	float y = x * sigma; // 1/sigma
	return sigma / (sqrt(2 * PI)) * exp(-0.5*y*y); // lift exp(-0.5)?
}

#define halfWidth 4

float PS(PSInput input, float4 screenSpace : SV_Position) : SV_Target0
{
	float sum = 0;
	float sumWeights = 0;

	// SV_Position semantic is screenspace coords with 0.5 offset, truncating to int3 will remove this effect
	float x = screenSpace.x;
	float y = screenSpace.y;

	float value0 = Texture.Load(int3(x, y, 0));

	for (float dy = -halfWidth; dy <= halfWidth; dy++)
	{
		for (float dx = -halfWidth; dx <= halfWidth; dx++)
		{
			float value = Texture.Load(int3(x + dx, y + dy, 0));
			// TODO: gaussians can be combined to avoid a call to exp (lift dy first?): exp(x)*exp(y) = exp(x+y)
			float weight = Gaussian(dx, spatialSigma) * Gaussian(dy, spatialSigma) * Gaussian(value - value0, intensitySigma);
			sum += weight * value;
			sumWeights += weight;
		}
	}
	sum /= sumWeights;

	return sum;
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