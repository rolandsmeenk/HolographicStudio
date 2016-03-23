Texture2D<float> depthTexture : register(t0);

struct VSInput
{
	float4 pos : SV_POSITION;
};

struct VSOutput
{
	float4 pos : SV_POSITION;
	float3 norm : NORMAL;
	float2 tex : TEXCOORD0;
	float depth : MYSEMANTIC;
	float4 world: TEXCOORD1;
};

static const float2 colorImageDims = float2(1920, 1080);

cbuffer cbRarelyChanges : register(b0)
{
	matrix depthToColor;
	float2 f;
	float2 c;
	float k1, k2;
	float4 clipzone;
	float clipfloor;
	float clipceiling;
	matrix WorldViewProjection;
	matrix World;
	float depthThreshold;
	bool hologramEnabled;
}

float2 Project(float4 x)
{
	float2 xp = x.xy / x.z;
	float rSq = dot(xp, xp);
	float2 xpp = xp * (1 + k1 * rSq + k2 * rSq * rSq);
	return f*xpp + c;
}

// Input vertices are setup so that xy are the entries of the table 
// returned by GetDepthFrameToCameraSpaceTable, and zw are the (integer)
// depth image coordinates.

VSOutput VS(VSInput input)
{
	VSOutput output = (VSOutput)0;

	// depth
	float depth = depthTexture.Load(int3(input.pos.zw, 0)) / 1000.0; // m

	// depth camera coords
	float4 depthCamera = float4(input.pos.xy*depth, depth, 1);

	// color camera coords
	float4 colorCamera = mul(depthToColor, depthCamera);

	// color image coords [0,1],[0,1]
	float2 colorImage = Project(colorCamera) / colorImageDims;

	// texture coords
	float2 tex = float2(colorImage.x, 1 - colorImage.y); // flip y for texture coords

														 // view volume
	float4 pos = mul(depthCamera, WorldViewProjection);

	output.pos = pos;
	output.tex = tex;
	output.depth = depth;
	output.world = mul(depthCamera, World);

	return output;
}

////////////////////////////////////////////////////////////////////////////////

struct GSInput
{
	float4 pos : SV_POSITION;
	float3 norm : NORMAL;
	float2 tex : TEXCOORD0;
	float depth : MYSEMANTIC;
	float4 world: TEXCOORD1;
};

struct GSOutput
{
	float4 pos : SV_POSITION;
	float3 norm : NORMAL;
	float2 tex : TEXCOORD0;
	float depth : MYSEMANTIC;
	float4 world: TEXCOORD1;
};

[maxvertexcount(3)]
void GS(triangle GSInput points[3], inout TriangleStream< GSOutput > output)
{
	// test the triangle; avoid dynamic branching

	// A triangle is valid if all its points are nonzero, and each point is close to each other in 
	// depth (i.e., they do not straddle a large depth discontinuity).

	float nonZero = (points[0].depth * points[1].depth * points[2].depth) > 0 ? 1 : 0;

	float near01 = abs(points[0].depth - points[1].depth) < depthThreshold ? 1 : 0;
	float near02 = abs(points[0].depth - points[2].depth) < depthThreshold ? 1 : 0;
	float near12 = abs(points[1].depth - points[2].depth) < depthThreshold ? 1 : 0;

	float valid = nonZero * near01 * near02 * near12;

	float3 faceEdgeA = (points[1].pos - points[0].pos).xyz;
	float3 faceEdgeB = (points[2].pos - points[0].pos).xyz;
	float3 faceNormal = normalize(cross(faceEdgeA, faceEdgeB));

	points[0].norm = faceNormal;
	points[1].norm = faceNormal;
	points[2].norm = faceNormal;

	// place invalid triangles at the origin
	points[0].pos *= valid;
	points[1].pos *= valid;
	points[2].pos *= valid;

	output.Append(points[0]);
	output.Append(points[1]);
	output.Append(points[2]);
}

////////////////////////////////////////////////////////////////////////////////
Texture2D<float4> colorTexture : register(t0);
SamplerState colorSampler : register(s0);

struct PSInput
{
	float4 pos : SV_POSITION;
	float3 norm : NORMAL;
	float2 tex : TEXCOORD0;
	float depth : MYSEMANTIC;
	float4 world: TEXCOORD1;
};

float4 PS(PSInput input) : SV_TARGET0
{
	if (input.world.y < clipfloor || input.world.y > clipceiling || distance(clipzone.xz, input.world.xz) > clipzone.w)
	{
		discard;
    }
	
	float4 color = colorTexture.Sample(colorSampler, input.tex);

	if (hologramEnabled)
	{
		float intensity = color.r * 0.333 + color.g * 0.333 + color.b * 0.333;
		intensity = intensity + pow(1.0 - dot(input.norm, float3(0.0, 0.0, 1.0)), 0.8);
		intensity *= (0.5 + 0.5 * sin(1000.0 * input.world.y));

		return intensity * float4(0.7, 0.8, 1.0, 1.0);
	}
	return color;
}

technique
{
	pass
	{
		Profile = 11;
		VertexShader = VS;
		GeometryShader = GS;
		PixelShader = PS;
	}
}