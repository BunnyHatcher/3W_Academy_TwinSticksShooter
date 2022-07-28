
#ifdef CLAYXELS_URP
    // urp shadow pass detect
    #ifdef SHADERPASS_SHADOWCASTER 
        #define CLAYXELS_SHADOWCASTER
    #else
        #define CLAYXELS_FORWARD
    #endif
#elif defined(CLAYXELS_HDRP)
    // hdrp shadow pass detect
    #ifdef SHADERPASS 
        #if SHADERPASS == SHADERPASS_SHADOWS
            #define CLAYXELS_SHADOWCASTER
        #else
            #define CLAYXELS_FORWARD
        #endif
    #endif
#endif

uniform StructuredBuffer<int> chunkIdToContainerIdGlob; 
uniform StructuredBuffer<int> chunkIdToSourceContainerIdGlob;
uniform StructuredBuffer<int> instanceToContainerIdGlob;
uniform StructuredBuffer<int> globalChunkIdToSelectionIdGlob;
uniform StructuredBuffer<int> localChunkIdGlob;
uniform StructuredBuffer<int> chunkIdOffsetGlob;
uniform StructuredBuffer<int2> pointCloudDataMip3Glob;
uniform StructuredBuffer<int> gridPointersMip2Glob;
uniform StructuredBuffer<int> gridPointersMip3Glob;
uniform StructuredBuffer<int> boundingBoxGlob;
uniform StructuredBuffer<float3> chunksCenterGlob;
uniform StructuredBuffer<float4x4> instancesObjectMatrixGlob;
uniform StructuredBuffer<float4x4> instancesObjectMatrixInvGlob;
uniform StructuredBuffer<float> chunkSizeGlob;
uniform StructuredBuffer<int> splatTexIdGlob;

uniform StructuredBuffer<float> _smoothnessGlob;
uniform StructuredBuffer<float> _metallicGlob;
uniform StructuredBuffer<float> _alphaCutoutGlob;
uniform StructuredBuffer<float> _roughPosGlob;
uniform StructuredBuffer<float> _splatSizeMultGlob;
uniform StructuredBuffer<float> _backFillDarkGlob;
uniform StructuredBuffer<float> _backFillAlphaGlob;
uniform StructuredBuffer<float4> _emissiveColorGlob;
uniform StructuredBuffer<float> _roughOrientXGlob;
uniform StructuredBuffer<float> _roughOrientYGlob;
uniform StructuredBuffer<float> _roughOrientZGlob;
uniform StructuredBuffer<float> _roughColorGlob;
uniform StructuredBuffer<float> _roughTwistGlob;
uniform StructuredBuffer<float> _roughSizeGlob;
uniform StructuredBuffer<float> _splatBillboardGlob;
uniform StructuredBuffer<float> _emissionPowerGlob;
uniform StructuredBuffer<float> _subsurfaceScatterGlob;
uniform StructuredBuffer<float4> _subsurfaceCenterGlob;

uint maxChunks;
uint numChunks;
sampler2D _MainTex;
// TEXTURE2D_ARRAY(_MainTexGlob);
// SAMPLER(sampler_MainTexGlob);
int solidHighlightId;
int containerHighlightId;
int memoryOptimized;
float bufferSizeReduceFactor;
// float _nan;
float3 renderBoundsCenter;
float3 renderBoundsCenterGlob;
int lodEnabled = 1;

// render textures
sampler2D microVoxRenderTex0;
sampler2D microVoxRenderTex1;
sampler2D microVoxRenderTex2;
sampler2D microVoxRenderTex3;
sampler2D microVoxRenderTex4;
sampler2D microVoxRenderTexDepth;
sampler2D microVoxRenderTexDepthCurr;

float microvoxelSplatsQuality;
float microvoxelRayIterations;

static const int2 neighbourBrickLookup[64][12] = {
{int2(1,12),int2(1,28),int2(1,13),int2(1,29),int2(3,3),int2(3,19),int2(3,7),int2(3,23),int2(5,48),int2(5,52),int2(5,49),int2(5,53)},
{int2(1,12),int2(1,28),int2(1,13),int2(1,29),int2(1,14),int2(1,30),int2(5,48),int2(5,52),int2(5,49),int2(5,53),int2(5,50),int2(5,54)},
{int2(1,13),int2(1,29),int2(1,14),int2(1,30),int2(1,15),int2(1,31),int2(5,49),int2(5,53),int2(5,50),int2(5,54),int2(5,51),int2(5,55)},
{int2(1,14),int2(1,30),int2(1,15),int2(1,31),int2(2,0),int2(2,16),int2(2,4),int2(2,20),int2(5,50),int2(5,54),int2(5,51),int2(5,55)},
{int2(3,3),int2(3,19),int2(3,7),int2(3,23),int2(3,11),int2(3,27),int2(5,48),int2(5,52),int2(5,56),int2(5,49),int2(5,53),int2(5,57)},
{int2(5,48),int2(5,52),int2(5,56),int2(5,49),int2(5,53),int2(5,57),int2(5,50),int2(5,54),int2(5,58),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(5,49),int2(5,53),int2(5,57),int2(5,50),int2(5,54),int2(5,58),int2(5,51),int2(5,55),int2(5,59),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(2,0),int2(2,16),int2(2,4),int2(2,20),int2(2,8),int2(2,24),int2(5,50),int2(5,54),int2(5,58),int2(5,51),int2(5,55),int2(5,59)},
{int2(3,7),int2(3,23),int2(3,11),int2(3,27),int2(3,15),int2(3,31),int2(5,52),int2(5,56),int2(5,60),int2(5,53),int2(5,57),int2(5,61)},
{int2(5,52),int2(5,56),int2(5,60),int2(5,53),int2(5,57),int2(5,61),int2(5,54),int2(5,58),int2(5,62),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(5,53),int2(5,57),int2(5,61),int2(5,54),int2(5,58),int2(5,62),int2(5,55),int2(5,59),int2(5,63),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(2,4),int2(2,20),int2(2,8),int2(2,24),int2(2,12),int2(2,28),int2(5,54),int2(5,58),int2(5,62),int2(5,55),int2(5,59),int2(5,63)},
{int2(0,0),int2(0,16),int2(0,1),int2(0,17),int2(3,11),int2(3,27),int2(3,15),int2(3,31),int2(5,56),int2(5,60),int2(5,57),int2(5,61)},
{int2(0,0),int2(0,16),int2(0,1),int2(0,17),int2(0,2),int2(0,18),int2(5,56),int2(5,60),int2(5,57),int2(5,61),int2(5,58),int2(5,62)},
{int2(0,1),int2(0,17),int2(0,2),int2(0,18),int2(0,3),int2(0,19),int2(5,57),int2(5,61),int2(5,58),int2(5,62),int2(5,59),int2(5,63)},
{int2(0,2),int2(0,18),int2(0,3),int2(0,19),int2(2,8),int2(2,24),int2(2,12),int2(2,28),int2(5,58),int2(5,62),int2(5,59),int2(5,63)},
{int2(1,12),int2(1,28),int2(1,44),int2(1,13),int2(1,29),int2(1,45),int2(3,3),int2(3,19),int2(3,35),int2(3,7),int2(3,23),int2(3,39)},
{int2(1,12),int2(1,28),int2(1,44),int2(1,13),int2(1,29),int2(1,45),int2(1,14),int2(1,30),int2(1,46),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(1,13),int2(1,29),int2(1,45),int2(1,14),int2(1,30),int2(1,46),int2(1,15),int2(1,31),int2(1,47),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(1,14),int2(1,30),int2(1,46),int2(1,15),int2(1,31),int2(1,47),int2(2,0),int2(2,16),int2(2,32),int2(2,4),int2(2,20),int2(2,36)},
{int2(3,3),int2(3,19),int2(3,35),int2(3,7),int2(3,23),int2(3,39),int2(3,11),int2(3,27),int2(3,43),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(2,0),int2(2,16),int2(2,32),int2(2,4),int2(2,20),int2(2,36),int2(2,8),int2(2,24),int2(2,40),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(3,7),int2(3,23),int2(3,39),int2(3,11),int2(3,27),int2(3,43),int2(3,15),int2(3,31),int2(3,47),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(2,4),int2(2,20),int2(2,36),int2(2,8),int2(2,24),int2(2,40),int2(2,12),int2(2,28),int2(2,44),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(0,0),int2(0,16),int2(0,32),int2(0,1),int2(0,17),int2(0,33),int2(3,11),int2(3,27),int2(3,43),int2(3,15),int2(3,31),int2(3,47)},
{int2(0,0),int2(0,16),int2(0,32),int2(0,1),int2(0,17),int2(0,33),int2(0,2),int2(0,18),int2(0,34),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(0,1),int2(0,17),int2(0,33),int2(0,2),int2(0,18),int2(0,34),int2(0,3),int2(0,19),int2(0,35),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(0,2),int2(0,18),int2(0,34),int2(0,3),int2(0,19),int2(0,35),int2(2,8),int2(2,24),int2(2,40),int2(2,12),int2(2,28),int2(2,44)},
{int2(1,28),int2(1,44),int2(1,60),int2(1,29),int2(1,45),int2(1,61),int2(3,19),int2(3,35),int2(3,51),int2(3,23),int2(3,39),int2(3,55)},
{int2(1,28),int2(1,44),int2(1,60),int2(1,29),int2(1,45),int2(1,61),int2(1,30),int2(1,46),int2(1,62),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(1,29),int2(1,45),int2(1,61),int2(1,30),int2(1,46),int2(1,62),int2(1,31),int2(1,47),int2(1,63),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(1,30),int2(1,46),int2(1,62),int2(1,31),int2(1,47),int2(1,63),int2(2,16),int2(2,32),int2(2,48),int2(2,20),int2(2,36),int2(2,52)},
{int2(3,19),int2(3,35),int2(3,51),int2(3,23),int2(3,39),int2(3,55),int2(3,27),int2(3,43),int2(3,59),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(2,16),int2(2,32),int2(2,48),int2(2,20),int2(2,36),int2(2,52),int2(2,24),int2(2,40),int2(2,56),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(3,23),int2(3,39),int2(3,55),int2(3,27),int2(3,43),int2(3,59),int2(3,31),int2(3,47),int2(3,63),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(2,20),int2(2,36),int2(2,52),int2(2,24),int2(2,40),int2(2,56),int2(2,28),int2(2,44),int2(2,60),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(0,16),int2(0,32),int2(0,48),int2(0,17),int2(0,33),int2(0,49),int2(3,27),int2(3,43),int2(3,59),int2(3,31),int2(3,47),int2(3,63)},
{int2(0,16),int2(0,32),int2(0,48),int2(0,17),int2(0,33),int2(0,49),int2(0,18),int2(0,34),int2(0,50),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(0,17),int2(0,33),int2(0,49),int2(0,18),int2(0,34),int2(0,50),int2(0,19),int2(0,35),int2(0,51),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(0,18),int2(0,34),int2(0,50),int2(0,19),int2(0,35),int2(0,51),int2(2,24),int2(2,40),int2(2,56),int2(2,28),int2(2,44),int2(2,60)},
{int2(1,44),int2(1,60),int2(1,45),int2(1,61),int2(3,35),int2(3,51),int2(3,39),int2(3,55),int2(4,0),int2(4,4),int2(4,1),int2(4,5)},
{int2(1,44),int2(1,60),int2(1,45),int2(1,61),int2(1,46),int2(1,62),int2(4,0),int2(4,4),int2(4,1),int2(4,5),int2(4,2),int2(4,6)},
{int2(1,45),int2(1,61),int2(1,46),int2(1,62),int2(1,47),int2(1,63),int2(4,1),int2(4,5),int2(4,2),int2(4,6),int2(4,3),int2(4,7)},
{int2(1,46),int2(1,62),int2(1,47),int2(1,63),int2(2,32),int2(2,48),int2(2,36),int2(2,52),int2(4,2),int2(4,6),int2(4,3),int2(4,7)},
{int2(3,35),int2(3,51),int2(3,39),int2(3,55),int2(3,43),int2(3,59),int2(4,0),int2(4,4),int2(4,8),int2(4,1),int2(4,5),int2(4,9)},
{int2(4,0),int2(4,4),int2(4,8),int2(4,1),int2(4,5),int2(4,9),int2(4,2),int2(4,6),int2(4,10),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(4,1),int2(4,5),int2(4,9),int2(4,2),int2(4,6),int2(4,10),int2(4,3),int2(4,7),int2(4,11),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(2,32),int2(2,48),int2(2,36),int2(2,52),int2(2,40),int2(2,56),int2(4,2),int2(4,6),int2(4,10),int2(4,3),int2(4,7),int2(4,11)},
{int2(3,39),int2(3,55),int2(3,43),int2(3,59),int2(3,47),int2(3,63),int2(4,4),int2(4,8),int2(4,12),int2(4,5),int2(4,9),int2(4,13)},
{int2(4,4),int2(4,8),int2(4,12),int2(4,5),int2(4,9),int2(4,13),int2(4,6),int2(4,10),int2(4,14),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(4,5),int2(4,9),int2(4,13),int2(4,6),int2(4,10),int2(4,14),int2(4,7),int2(4,11),int2(4,15),int2(-1,-1),int2(-1,-1),int2(-1,-1)},
{int2(2,36),int2(2,52),int2(2,40),int2(2,56),int2(2,44),int2(2,60),int2(4,6),int2(4,10),int2(4,14),int2(4,7),int2(4,11),int2(4,15)},
{int2(0,32),int2(0,48),int2(0,33),int2(0,49),int2(3,43),int2(3,59),int2(3,47),int2(3,63),int2(4,8),int2(4,12),int2(4,9),int2(4,13)},
{int2(0,32),int2(0,48),int2(0,33),int2(0,49),int2(0,34),int2(0,50),int2(4,8),int2(4,12),int2(4,9),int2(4,13),int2(4,10),int2(4,14)},
{int2(0,33),int2(0,49),int2(0,34),int2(0,50),int2(0,35),int2(0,51),int2(4,9),int2(4,13),int2(4,10),int2(4,14),int2(4,11),int2(4,15)},
{int2(0,34),int2(0,50),int2(0,35),int2(0,51),int2(2,40),int2(2,56),int2(2,44),int2(2,60),int2(4,10),int2(4,14),int2(4,11),int2(4,15)},
};

static const int3 mip3CellIter[] = {
    int3(0, 0, 0), int3(1, 0, 0), int3(2, 0, 0), int3(3, 0, 0), int3(0, 1, 0), int3(1, 1, 0), int3(2, 1, 0), int3(3, 1, 0), int3(0, 2, 0), int3(1, 2, 0), int3(2, 2, 0), int3(3, 2, 0), int3(0, 3, 0), int3(1, 3, 0), int3(2, 3, 0), int3(3, 3, 0), int3(0, 0, 1), int3(1, 0, 1), int3(2, 0, 1), int3(3, 0, 1), int3(0, 1, 1), int3(1, 1, 1), int3(2, 1, 1), int3(3, 1, 1), int3(0, 2, 1), int3(1, 2, 1), int3(2, 2, 1), int3(3, 2, 1), int3(0, 3, 1), int3(1, 3, 1), int3(2, 3, 1), int3(3, 3, 1), int3(0, 0, 2), int3(1, 0, 2), int3(2, 0, 2), int3(3, 0, 2), int3(0, 1, 2), int3(1, 1, 2), int3(2, 1, 2), int3(3, 1, 2), int3(0, 2, 2), int3(1, 2, 2), int3(2, 2, 2), int3(3, 2, 2), int3(0, 3, 2), int3(1, 3, 2), int3(2, 3, 2), int3(3, 3, 2), int3(0, 0, 3), int3(1, 0, 3), int3(2, 0, 3), int3(3, 0, 3), int3(0, 1, 3), int3(1, 1, 3), int3(2, 1, 3), int3(3, 1, 3), int3(0, 2, 3), int3(1, 2, 3), int3(2, 2, 3), int3(3, 2, 3), int3(0, 3, 3), int3(1, 3, 3), int3(2, 3, 3), int3(3, 3, 3)
};

static const int3 neighboursBigSplat[7] = {
    int3(0, 0, 0),
    int3(0, 1, 0),
    int3(0, -1, 0),
    int3(1, 0, 0),
    int3(-1, 0, 0),
    int3(0, 0, 1),
    int3(0, 0, -1)
};

static const int3 neighboursMip2[6] = {
    int3(0, 1, 0),
    int3(0, -1, 0),
    int3(1, 0, 0),
    int3(-1, 0, 0),
    int3(0, 0, 1),
    int3(0, 0, -1)
};

static const int3 neighbourMip3[27] = {
    int3(0, 0, 0),
    int3(0, 1, 0),
    int3(0, -1, 0),
    int3(1, 0, 0),
    int3(-1, 0, 0),
    int3(0, 0, 1),
    int3(0, 0, -1),
    int3(1, 1, 1),
    int3(0, 1, 1),
    int3(-1, 1, 1),
    int3(1, 1, -1),
    int3(0, 1, -1),
    int3(-1, 1, -1),
    int3(1, 1, 0),
    int3(-1, 1, 0),
    int3(1, -1, 1),
    int3(0, -1, 1),
    int3(-1, -1, 1),
    int3(1, -1, -1),
    int3(0, -1, -1),
    int3(-1, -1, -1),
    int3(1, -1, 0),
    int3(-1, -1, 0),
    int3(1, 0, 1),
    int3(-1, 0, 1),
    int3(1, 0, -1),
    int3(-1, 0, -1)
};

static const float2 neighbourPixels[] = {
    float2(-1, 0),
    float2(1, 0),
    float2(0, -1),
    float2(0, 1),
    float2(-1, -1),
    float2(1, -1),
    float2(1, 1),
    float2(-1, 1)
};

int3 gridCoordFromLinearId(uint linearGridId, uint gridSize){
    int3 gridCoord;
    gridCoord.x = linearGridId % gridSize;
    gridCoord.z = int(linearGridId / (gridSize*gridSize));
    gridCoord.y = int(linearGridId / gridSize) - (gridSize * gridCoord.z);

    return gridCoord;
}

float3 expandGridPoint(int3 cellCoord, float cellSize, float localChunkSize){
    float cellCornerOffset = cellSize * 0.5;
    float halfBounds = localChunkSize * 0.5;
    float3 gridPoint = float3(
        (cellSize * cellCoord.x) - halfBounds, 
        (cellSize * cellCoord.y) - halfBounds, 
        (cellSize * cellCoord.z) - halfBounds) + cellCornerOffset;

    return gridPoint;
}

float rgbToFloat(float3 rgb){
    return rgb.r +
        (rgb.g/256.0)+
        (rgb.b/(256.0*256.0));
}

float mod(float x, float y)
{
  return x - y * floor(x/y);
}

float3 floatToRgb(float v){
    float d = rcp(256.0);

    float r = v;
    float g = mod(v*256.0,1.0);
    r-= g * d;
    float b = mod(v*256.0*256.0,1.0);
    g-=b * d;
    return float3(r,g,b);
}

// real4 PackHeightmap(real height)
// {
//     uint a = (uint)(65535.0 * height);
//     return real4((a >> 0) & 0xFF, (a >> 8) & 0xFF, 0, 0) / 255.0;
// }

// real UnpackHeightmap(real4 height)
// {
//     return (height.r + height.g * 256.0) / 257.0; // (255.0 * height.r + 255.0 * 256.0 * height.g) / 65535.0
// }

float4 floatToRgba(float v){
    float4 enc = float4(1.0, 255.0, 65025.0, 16581375.0) * v;
    enc = frac(enc);
    enc -= enc.yzww * float4(1.0/255.0,1.0/255.0,1.0/255.0,0.0);
    return enc;
}

float rgbaToFloat(float4 rgba){
    return dot( rgba, float4(1.0, 1/255.0, 1/65025.0, 1/16581375.0));
}

void unpack8888(uint inVal, out uint a, out uint b, out uint c, out uint d){
    a = inVal >> 24;
    b = (0x00FF0000 & inVal) >> 16;
    c = (0x0000FF00 & inVal) >> 8;
    d = (0x000000FF & inVal);
}

float3 unpackRgb(uint inVal){
    int r = (inVal & 0x000000FF) >>  0;
    int g = (inVal & 0x0000FF00) >>  8;
    int b = (inVal & 0x00FF0000) >> 16;

    return float3((float)r * 0.00392156862, (float)g * 0.00392156862, (float)b * 0.00392156862); // div by 255
}

uint rgbToInt(float3 color){
    uint icol = uint(color.x*255) + uint(color.y*255) * 256 + uint(color.z*255) * 256 * 256;

    return icol;
}

void unpack66668(int value, out int4 unpackedData1, out int unpackedData2){
    unpackedData2 = value & 0x000000FF;
    value >>= 8;
    unpackedData1.w = value & 0x3f;
    value >>= 6;
    unpackedData1.z = value & 0x3f;
    value >>= 6;
    unpackedData1.y = value & 0x3f;
    value >>= 6;
    unpackedData1.x = value & 0x3f;
}

void unpack6663335(int value, out uint a, out uint b, out uint c, out uint d, out uint e, out uint f, out uint g){
    g = value & 0x1f;
    value >>= 5;
    f = value & 0x7;
    value >>= 3;
    e = value & 0x7;
    value >>= 3;
    d = value & 0x7;
    value >>= 3;
    c = value & 0x3f;
    value >>= 6;
    b = value & 0x3f;
    value >>= 6;
    a = value & 0x3f;
}

uint unpack6_444455(int value){
    value >>= 26;
    return value & 0x3f;
}

void unpack6444455(int value, out uint a, out uint b, out uint c, out uint d, out uint e, out uint f, out uint g){
    g = value & 0x1f;
    value >>= 5;
    f = value & 0x1f;
    value >>= 5;
    e = value & 0xf;
    value >>= 4;
    d = value & 0xf;
    value >>= 4;
    c = value & 0xf;
    value >>= 4;
    b = value & 0xf;
    value >>= 4;
    a = value & 0x3f;
}

void unpack8618(int value, out uint a, out uint b, out uint c){
    c = value & 0x3ffff;
    value >>= 18;
    b = value & 0x3f;
    value >>= 6;
    a = value & 0xff;
}

void unpack55418(int value, out uint a, out uint b, out uint c, out uint d){
    d = value & 0x3ffff;
    value >>= 18;
    c = value & 0xf;
    value >>= 4;
    b = value & 0x1f;
    value >>= 5;
    a = value & 0x1f;
}

int4 unpack66614(int value){
    int4 outval;

    outval.w = value & 0x3fff;
    value >>= 14;
    outval.z = value & 0x3f;
    value >>= 6;
    outval.y = value & 0x3f;
    value >>= 6;
    outval.x = value & 0x3f;
    
    return outval;
}

void unpack824(int value, out uint a, out uint b){
    b = value & 0xffffff;
    value >>= 24;
    a = value & 0xff;
}

float3 unpackNormal2Byte(uint value1, uint value2){
    float2 f = float2((float)value1 / 256.0, (float)value2 / 256.0);
    // float2 f = float2(value1 * 0.00392156862, value2 * 0.00392156862);

    f = f * 2.0 - 1.0;

    float3 n = float3( f.x, f.y, 1.0 - abs( f.x ) - abs( f.y ) );
    float t = saturate( -n.z );
    n.xy += n.xy >= 0.0 ? -t : t;

    return normalize( n );
}

float3 unpackNormal(float value1, float value2){
    value1 = value1 * 2.0 - 1.0;
    value2 = value2 * 2.0 - 1.0;

    float3 n = float3( value1, value2, 1.0 - abs( value1 ) - abs( value2 ) );
    float t = saturate( -n.z );
    n.xy += n.xy >= 0.0 ? -t : t;

    return normalize(n);
}

float3 unpackNormalUnnorm(float value1, float value2){
    value1 = value1 * 2.0 - 1.0;
    value2 = value2 * 2.0 - 1.0;

    float3 n = float3( value1, value2, 1.0 - abs( value1 ) - abs( value2 ) );
    float t = saturate( -n.z );
    n.xy += n.xy >= 0.0 ? -t : t;

    return n;
}

float3 projectPointOnPlane(float3 p, float3 planeOrigin, float3 planeNormal){
    float3 vec = planeOrigin - p;

    return vec - planeNormal * ( dot( vec, planeNormal ) / dot( planeNormal, planeNormal ) );
}

float3 projectRayOnPlane(float3 rayOrigin, float3 rayDirection, float3 planeOrigin, float3 planeNormal){
    float denom = abs(dot(planeNormal, rayDirection));// + 0.000001;

    // float t = 1.0;
    // if(abs(denom) > 0.0){
        float t = dot(planeOrigin - rayOrigin, planeNormal) * rcp(denom);
    // }
    
    return rayOrigin + (rayDirection * t);
}

float projectPointOnLine(float3 pnt, float3 linePnt, float3 lineDir){
    float3 v = pnt - linePnt;
    float t = dot(v, lineDir);
    
    return t;
}

float boundsIntersection(float3 ro, float3 rd, float3 rad, float3 m) {
    float3 n = m * ro;
    float3 k = abs(m) * rad;
    float3 t2 = -n + k;

    float tF = min( min( t2.x, t2.y ), t2.z );

    return tF;
}

float boxIntersection( float3 ro, float3 rd, float3 boxPos, float boxSize){
    ro -= boxPos;
    float3 m = 1.0 / rd; // can precompute if traversing a set of aligned boxes
    float3 n = m * ro;   // can precompute if traversing a set of aligned boxes
    float3 k = abs(m) * boxSize;
    float3 t1 = -n - k;
    float3 t2 = -n + k;
    float tN = max( max( t1.x, t1.y ), t1.z );
    float tF = min( min( t2.x, t2.y ), t2.z );
    if( tN>tF || tF < 0.0) return -1.0; // no intersection

    return tN;
}

float boxIntersectionFast(float3 ro, float3 m, float3 k, float3 boxPos){
    ro -= boxPos;
    // float3 m = 1.0 / rd; // can precompute if traversing a set of aligned boxes
    float3 n = m * ro;   // can precompute if traversing a set of aligned boxes
    // float3 k = abs(m) * boxSize;
    float3 t1 = -n - k;
    float3 t2 = -n + k;
    float tN = max( max( t1.x, t1.y ), t1.z );
    float tF = min( min( t2.x, t2.y ), t2.z );
    if(tN > tF) return -1.0;
    // if(tN > tF || tF < 0.0) return -1.0; // no intersection

    // return tN;
    return 1.0;
}

float boxIntersectionNormalFast(float3 ro, float3 s, float3 m, float3 k, float3 boxPos, out float3 outNormal){
    ro -= boxPos;
    // float3 m = 1.0 / rd; // can precompute if traversing a set of aligned boxes
    float3 n = -m * ro;   // can precompute if traversing a set of aligned boxes
    // float3 k = abs(m) * boxSize;
    float3 t1 = -n - k;
    float3 t2 = -n + k;
    float tN = max( max( t1.x, t1.y ), t1.z );
    float tF = min( min( t2.x, t2.y ), t2.z );
    if(tN > tF ) return -1.0;

    outNormal = -(s*step(t1.yzx,t1.xyz)*step(t1.zxy,t1.xyz));

    return 1.0;
}

float boxIntersectionNormal( float3 ro, float3 rd, float3 boxPos, float boxSize, out float3 outNormal){
    ro -= boxPos;
    float3 m = 1.0 / rd; // can precompute if traversing a set of aligned boxes
    float3 n = m * ro;   // can precompute if traversing a set of aligned boxes
    float3 k = abs(m) * boxSize;
    float3 t1 = -n - k;
    float3 t2 = -n + k;
    float tN = max( max( t1.x, t1.y ), t1.z );
    float tF = min( min( t2.x, t2.y ), t2.z );
    // if( tN > tF || tF < 0.0) return -1.0; // no intersection

    outNormal = -sign(rd)*step(t1.yzx,t1.xyz)*step(t1.zxy,t1.xyz);

    return tN;
}

// Z buffer to linear 0..1 depth (0 at eye, 1 at far plane)
inline float Linear01Depth( float z )
{
    return 1.0 / (_ZBufferParams.x * z + _ZBufferParams.y);
}
// Z buffer to linear depth
inline float LinearEyeDepth( float z )
{
    return 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w);
}

float4 computeScreenPos(float4 positionCS)
{
    float4 o = positionCS * 0.5f;
    o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
    o.zw = positionCS.zw;
    return o;
}

float3 depthToWorld(float2 uv, float depth){
    float z = depth;

    float4 clipSpacePosition = float4(uv * 2.0 - 1.0, z, 1.0);

    float4 viewSpacePosition = mul(UNITY_MATRIX_I_VP,clipSpacePosition);
    viewSpacePosition /= viewSpacePosition.w;

    float4 worldSpacePosition = viewSpacePosition / viewSpacePosition.w;

    return worldSpacePosition.xyz;
}

void drawVoxel(float3 positionWS, float3 viewDirectionWS, float3 cellCenter, float halfCellSize, int3 colorData,
    inout float depthSort, inout bool hit, inout float3 outDepthPoint, inout float3 outNormal, inout float3 outColor){

    float3 voxelNormal = 0;
    float boxHit = boxIntersectionNormal(positionWS, -viewDirectionWS, cellCenter, halfCellSize, voxelNormal);

    if(boxHit > -1.0){// && boxHit < depthSort){
        float3 projectedPoint = projectRayOnPlane(positionWS, viewDirectionWS, cellCenter + (voxelNormal * halfCellSize), voxelNormal);

        outColor = colorData / 64;

        outNormal = voxelNormal;

        outDepthPoint = projectedPoint;

        depthSort = boxHit;

        hit = true;
    }
}

/* cube topology
               5---6
             / |     |
            /  4    |
            1---2  7
            |    |  /
            0---3/

            y  z
            |/
            ---x
            */

// this mask is used to lerp a cube mesh between min and max bounds
static const float3 boundingBoxMask[8] = {
    float3(0.0, 0.0, 0.0), // 0
    float3(0.0, 1.0, 0.0), // 1
    float3(1.0, 1.0, 0.0), // 2
    float3(1.0, 0.0, 0.0), // 3
    float3(0.0, 0.0, 1.0), // 4
    float3(0.0, 1.0, 1.0), // 5
    float3(1.0, 1.0, 1.0), // 6
    float3(1.0, 0.0, 1.0), // 7
};

float insideBox(float3 v, float3 bottomLeft, float3 topRight) {
    float3 s = step(bottomLeft, v) - step(topRight, v);
    return s.x * s.y * s.z; 
}

void clayxels_microVoxels_vertGlob(float chunkSizeGlobVal, float invertFlip, uint chunkIdGlob, uint chunkId, uint vertexId, float3 positionOS, out float3 vertexPos, out float3 boundsCenter, out float3 boundsSize, out float outBoxFlipped){
    vertexPos = 0;
    boundsCenter = 0;
    boundsSize = 0;
    outBoxFlipped = 0;

    uint boundingBoxIt = ((chunkIdGlob + chunkId) * 6);
    int minX = boundingBoxGlob[boundingBoxIt];
    int minY = boundingBoxGlob[boundingBoxIt + 1];
    int minZ = boundingBoxGlob[boundingBoxIt + 2];

    int maxX = boundingBoxGlob[boundingBoxIt + 3];
    int maxY = boundingBoxGlob[boundingBoxIt + 4];
    int maxZ = boundingBoxGlob[boundingBoxIt + 5];

    float3 chunkCenter = chunksCenterGlob[chunkIdGlob + chunkId];

    int maxExt = (minX + minY + minZ) - (maxX + maxY + maxZ);
    if(maxExt == 192){
        // if bounding box covers the whole chunk, it's an empty chunk
        vertexPos = chunkCenter;

        return;
    }

    float halfChunk = chunkSizeGlobVal * 0.5;
    float cellSizeMip2 = chunkSizeGlobVal * 0.015625; // div by 64
    // float cellSizeMip3 = chunkSizeGlobVal * 0.00392156862; // div by 256

    float3 minVec = halfChunk - (float3(minX, minY, minZ) * cellSizeMip2) + (cellSizeMip2*0.5);
    float3 maxVec = -halfChunk + (float3(maxX, maxY, maxZ) * cellSizeMip2) + (cellSizeMip2 * 2.0);
    boundsCenter = (maxVec - minVec) * 0.5;

    boundsSize = float3(
        max(minVec.x + boundsCenter.x, maxVec.x - boundsCenter.x),
        max(minVec.y + boundsCenter.y, maxVec.y - boundsCenter.y),
        max(minVec.z + boundsCenter.z, maxVec.z - boundsCenter.z));

    float flip = 1;

    if(invertFlip){
        flip = 1.0 - flip;
    }
    
    float scaler = lerp(2.0, -2.0, flip);
    float3 boxCornerA = lerp(minVec, maxVec, flip);
    float3 boxCornerB = lerp(maxVec, minVec, flip);
    
    vertexPos = ((positionOS * scaler) * lerp(boxCornerA, boxCornerB, boundingBoxMask[vertexId % 8])) + chunkCenter;

    outBoxFlipped = flip;
}

int idFromGridCoord(int x, int y, int z, int gridSize){
   return x + gridSize * (y + (gridSize * z));
}

float3 advanceRay(float3 ro, float3 s, float3 m, float3 k, float3 boxPos){
    ro -= boxPos;
    // float3 m = 1.0 / rd; // can precompute if traversing a set of aligned boxes
    float3 n = m * ro;   // can precompute if traversing a set of aligned boxes
    // float3 k = abs(m) * boxSize;
    float3 t1 = -n - k;
    float3 t2 = -n + k;
    float tN = max( max( t1.x, t1.y ), t1.z );
    
    float3 advanceDir = s*step(t1.yzx,t1.xyz)*step(t1.zxy,t1.xyz);

    return advanceDir;
}
uint pack66884(uint a, uint b, uint c, uint d, uint e){
    uint packedValue = ((((a << 6 | b) << 8 | c) << 8 | d) << 4) | e;

    return packedValue;
}

inline void getDataPointers(int globalChunkId, out int containerIdSourceChunk, out int containerIdSource, out int chunkId, out int transformAccessId, out int selectionContainerId, out int chunkUniqueId){
    containerIdSourceChunk = 0;
    chunkId = globalChunkId % numChunks;
    transformAccessId = instanceToContainerIdGlob[globalChunkId];
    containerIdSource = 0;
    selectionContainerId = chunkIdToContainerIdGlob[globalChunkId];
    chunkUniqueId = (selectionContainerId * maxChunks) + chunkId;
}

float3 findFragmentStartPos2(float3 positionWS, float3 viewDirectionWS, float3 boundsCenter, float3 boundsSize, float3 chunkCenter, float3 containerOffset, float3 boxRayCastM, float cellSizeMip2, float3 localCamPos, out bool isInside){
    isInside = false;

    // flip the bounds inside-out
    float invertT = boundsIntersection(((positionWS - chunkCenter) - boundsCenter), viewDirectionWS, boundsSize, boxRayCastM);
    float3 flippedFragPos = positionWS + (viewDirectionWS * invertT);
    float3 outerBoxVoxelFragPos = flippedFragPos - chunkCenter + containerOffset;

    // if camera inside box, place fragment at camera near plane 
    float rayDir = dot(normalize(flippedFragPos - localCamPos), viewDirectionWS);
    float3 insideVolumeFragPos = projectRayOnPlane(positionWS, viewDirectionWS, localCamPos, viewDirectionWS) - chunkCenter + containerOffset;
    
    float inside = step(0.0, rayDir);
    float3 voxelFragPos = lerp(outerBoxVoxelFragPos, insideVolumeFragPos, inside);

    isInside = inside;

    return voxelFragPos;
}

inline void traverseMip3Glob(uint containerIdSourceGlob, uint containerIdGlob, float lodFactor, float sizeMult, float3 normalOrient, float roughOffset,
    float3 voxelFragPos, float3 viewDirectionWS, float cellSizeMip3, float cellSizeMip3Splat, float3 cellCenter, float mipLevel, float testerBounds, float testerBoundsMip3, int mip2Pointer, int chunkOffsetMip3, uint cellCoordId, inout float march, inout float hit, inout float3 hitDepthPoint, inout float3 hitNormal, inout float hitNormalOffset, inout float3 splatCenter, inout int hitMip3ColorData){

     // check mip3 pointer
    float shouldCheckMip3 = mipLevel * testerBoundsMip3;
    uint mip3Id = chunkOffsetMip3 + mip2Pointer;
    if(shouldCheckMip3 != 1.0) mip3Id = 0;
    int gridPointerMip2 = pointCloudDataMip3Glob[mip3Id].x;
    uint pointerMip3Id = chunkOffsetMip3 + gridPointerMip2 + cellCoordId;
    if(shouldCheckMip3 != 1.0) pointerMip3Id = 0;
    int gridPointerMip3 =  gridPointersMip3Glob[pointerMip3Id];

    float testerPointerMip3 = saturate(1.0 + sign(gridPointerMip3)) * shouldCheckMip3;

    float testerMip3 = 0;
    
    #if !defined(CLAYXELS_SHADOWCASTER)
        // compute voxel with normal
        uint mip3Iter;
        uint mip3Pointer;
        unpack824(gridPointerMip3, mip3Iter, mip3Pointer);

        uint mip3DataId = chunkOffsetMip3 + mip3Pointer + mip3Iter + 1;
        if(testerPointerMip3 != 1.0) mip3DataId = 0;
        int2 mip3Data = pointCloudDataMip3Glob[mip3DataId];

        hitMip3ColorData = mip3Data.y;
        
        uint normalX = (0x0000FF00 & mip3Data.x) >> 8;
        uint normalY = (0x000000FF & mip3Data.x);

        float3 normalMip3 = lerp(hitNormal, unpackNormal(normalX * 0.00392156862, normalY * 0.00392156862) + normalOrient, testerPointerMip3); // div by 256
        
        if(lodFactor > 0.05 && microvoxelSplatsQuality > 0.0){
            float d = lerp(dot(normalMip3, viewDirectionWS), 1.0, _splatBillboardGlob[containerIdSourceGlob]);

            testerMip3 = step(0.0, d) * testerPointerMip3 * testerBounds;

            uint normalOffsetInt = (0x00FF0000 & mip3Data.x) >> 16;

            float normalOffsetMip3 = ((((normalOffsetInt * 0.00392156862) * 2.0) - 1.0) * cellSizeMip3) * roughOffset; // div by 256
            hitNormalOffset = normalOffsetMip3;

            float3 cellPlanePosMip3 = cellCenter + (normalMip3 * normalOffsetMip3);
            
            // project ray onto plane at cell center
            float3 projectNormal = lerp(normalMip3, viewDirectionWS, _splatBillboardGlob[containerIdSourceGlob]);
            float t = dot(cellPlanePosMip3 - voxelFragPos, projectNormal) * rcp(d);
            float3 projectedPointMip3 = voxelFragPos + (viewDirectionWS * t);
            float3 splatMip3 = projectedPointMip3 - cellPlanePosMip3;
            float splatCircle = dot(splatMip3, splatMip3); // length-squared distance
            testerMip3 *= splatCircle < (cellSizeMip3Splat * sizeMult);

            splatCenter = splatMip3;

            hitDepthPoint = cellPlanePosMip3;
        }
        else{
            testerMip3 = testerPointerMip3;
            hitDepthPoint = cellCenter;
        }

        hitNormal = normalMip3;
    #else
        testerMip3 = testerPointerMip3;
        hitDepthPoint = cellCenter;
    #endif

    // mark final hit mip3 splat
    hit = testerMip3;
    
    march *= 1.0 - hit;
}

inline void intersectSplatGlob(uint containerIdSourceGlob, uint containerIdGlob, float lodFactor, float pixelRandom, float3 voxelFragPos, float3 viewDirectionWS, int3 cellCoord, uint neighbourCellCoordId, int chunkOffsetIdMip2, int chunkOffsetMip3, float testerBoundsMip2, uint cellCoordIdMip2, float3 cellCornerMip2, float cellSizeMip3, float halfCellSizeMip3, float cellSizeMip3Splat, inout float depthTest, inout float3 hitNormal, inout float hitNormalOffset, inout int hitMip3ColorData, inout float hitColorRough, inout float3 hitDepthPoint){
    float3 cellCenter = cellCornerMip2 + float3(cellSizeMip3 * cellCoord.x, cellSizeMip3 * cellCoord.y, cellSizeMip3 * cellCoord.z) + halfCellSizeMip3;
    
    float mip3Random = frac(sin(dot(float2(cellCenter.x, cellCenter.y),float2(12.9898,78.233+cellCenter.z)))*43758.5453123);
    float mip3RandomPlusMin = (mip3Random - 0.5) * 2.0;
    
    float roughPosMult = mip3RandomPlusMin * _roughPosGlob[containerIdSourceGlob];
    cellCenter += halfCellSizeMip3 * roughPosMult;
    
    float testerBounds = saturate(
        step(cellCoord.x, 3) * step(cellCoord.y, 3) * step(cellCoord.z, 3) * // check out of bounds +
        (1.0 + sign(cellCoord.x)) * (1.0 + sign(cellCoord.y)) * (1.0 + sign(cellCoord.z))) * testerBoundsMip2; // check out of bounds -

    uint mip2Id = chunkOffsetIdMip2 + cellCoordIdMip2;
    if(testerBounds != 1.0) mip2Id = 0;
    int mip2Pointer = gridPointersMip2Glob[mip2Id];
    float testerPointerMip2 = saturate(1.0 + sign(mip2Pointer));

    float3 splatPos = 0.0;
    float3 splatLocal = 0.0;
    float3 normal = 0.0;
    float splatHit = 0.0;
    float march = 1.0;
    int mip3ColorData = 0;
    float hitNormalOffsetTmp = 0;

    float mipLevel = testerPointerMip2;

    float roughScale = _splatSizeMultGlob[containerIdSourceGlob] + (mip3RandomPlusMin * _roughSizeGlob[containerIdSourceGlob]);
    float roughSize = lerp(1.0, 10.0, roughScale);
    float3 normalOffset = float3(_roughOrientXGlob[containerIdSourceGlob], _roughOrientYGlob[containerIdSourceGlob], _roughOrientZGlob[containerIdSourceGlob]) * mip3RandomPlusMin;
    float roughOffset = 1.0 - (mip3RandomPlusMin * _roughPosGlob[containerIdSourceGlob]);

    traverseMip3Glob(containerIdSourceGlob, containerIdGlob, lodFactor, roughSize, normalOffset, roughOffset,
        voxelFragPos, viewDirectionWS, cellSizeMip3, cellSizeMip3Splat, 
        cellCenter, mipLevel, testerBoundsMip2, testerBounds, mip2Pointer, chunkOffsetMip3, 
        neighbourCellCoordId, march, splatHit, splatPos, normal, hitNormalOffsetTmp, splatLocal, mip3ColorData);

    // texture the splat
    float3 uvNormal = lerp(normal, viewDirectionWS, _splatBillboardGlob[containerIdSourceGlob]);
    float3 camUpVec = UNITY_MATRIX_V._m00_m01_m02 + (_roughTwistGlob[containerIdSourceGlob] * mip3RandomPlusMin);
    float3 splatSideVec = normalize(cross(camUpVec, uvNormal));
    float3 splatUpVec = normalize(cross(splatSideVec, uvNormal));

    float roughUvSize = lerp(0.33, 0.8, roughScale);
    float3 splatTexVec = splatLocal * rcp(cellSizeMip3 * roughUvSize);

    float4 uv = float4(
        (dot(splatTexVec, splatUpVec) + 1.0) * 0.5,
        (dot(splatTexVec, splatSideVec) + 1.0) * 0.5,
        0, 0);

    uint texId = splatTexIdGlob[containerIdSourceGlob];
    uint texLod = 0;
    float alpha = 1;
    if(lodFactor > 0.0){
        // alpha = SAMPLE_TEXTURE2D_ARRAY_LOD(_MainTexGlob, sampler_MainTexGlob, uv.xy, texId, texLod).a;
        alpha = tex2Dlod(_MainTex, uv).a;
    }
    float alphaDiscard = alpha > (pixelRandom * _alphaCutoutGlob[containerIdSourceGlob]);

    splatHit *= alphaDiscard;

    // depth sort
    float3 deltaDepth = splatPos - voxelFragPos;
    float newDepth = lerp(depthTest, dot(deltaDepth, deltaDepth), splatHit);
    float depthSort = newDepth < depthTest;
    depthTest = lerp(depthTest, newDepth, depthSort);

    hitDepthPoint = lerp(hitDepthPoint, splatPos, depthSort);

    // output if this splat is in front
    hitNormal = lerp(hitNormal, normal, depthSort);
    hitMip3ColorData = lerp(hitMip3ColorData, mip3ColorData, depthSort);
    hitNormalOffset = lerp(hitNormalOffset, hitNormalOffsetTmp, depthSort);

    hitColorRough = lerp(hitColorRough, (roughPosMult*0.1) + (mip3RandomPlusMin * (_roughColorGlob[containerIdSourceGlob]*0.5)), depthSort);
}

bool clayxels_microVoxelsMip3SplatSeed_fragGlob(float objScale, float4 screenPos, float4x4 modelMatrix, float4x4 objectMatrixInv, uint globalChunkId, uint containerIdSourceGlob, uint containerIdGlob, uint chunkIdGlob, float3 positionWS, float3 viewDirectionWS, float3 boundsCenter, float3 boundsSize, float3 localCamPos, out float3 hitNormal, out float3 hitColor, out float3 hitDepthPoint, out int hitClayObjId, out float4 hitGridData){
    hitNormal = 0;
    hitColor = 0.0;
    hitDepthPoint = float3(0, 0, 0);
    hitClayObjId = 0;
    hitGridData = 0;

    float hit = 0.0;

    float chunkSizeGlobVal = chunkSizeGlob[containerIdSourceGlob];

    float cellSizeMip2 = chunkSizeGlobVal * 0.015625;// div by 64
    float halfCellSizeMip2 = cellSizeMip2 * 0.5;
    float cellSizeMip3 = chunkSizeGlobVal * 0.00392156862;// div by 256
    float halfCellSizeMip3 = cellSizeMip3 * 0.5;
    float cellSizeMip3Div = rcp(cellSizeMip3);
    float chunkSizeDiv = rcp(chunkSizeGlobVal);

    float cellSizeMip3Splat = (cellSizeMip3 * cellSizeMip3) * 0.65;

    int chunkOffsetIdMip2 = lerp(chunkIdGlob * 262144, chunkIdOffsetGlob[containerIdGlob + chunkIdGlob], memoryOptimized);// chunkId x 64 ^ 3 if not optimized
    int chunkOffsetMip3 = lerp(chunkIdGlob * (16777216 * bufferSizeReduceFactor), 0, memoryOptimized); // chunkId x 256 ^ 3 if not optimized

    float3 boxRayCastM = rcp(viewDirectionWS);
    float3 boxRayCastKMip2 = (abs(boxRayCastM) * halfCellSizeMip2);
    float3 boxRayCastKMip3 = abs(boxRayCastM) * halfCellSizeMip3;
    float3 s = -sign(viewDirectionWS);
    float3 boxRayCastK = boxRayCastKMip2;

    float containerOffset = (chunkSizeGlobVal * 0.5) - halfCellSizeMip3;
    float3 chunkCenter = chunksCenterGlob[containerIdGlob + chunkIdGlob];

    float neighboursLod = 1;
    float marchReducer = 1.0;
    float lodFactor = 0;

    bool cameraInsideBox = false;

    float3 voxelFragPos = findFragmentStartPos2(
        positionWS, viewDirectionWS, boundsCenter, boundsSize, chunkCenter, containerOffset, boxRayCastM, cellSizeMip2, localCamPos, cameraInsideBox);

    // start placing the ray adjacent to the mesh boundary
    int3 currCellCoordMip2 = voxelFragPos / cellSizeMip2;
    float3 cellCenterMip2 = float3(cellSizeMip2 * currCellCoordMip2.x, cellSizeMip2 * currCellCoordMip2.y, cellSizeMip2 * currCellCoordMip2.z) + halfCellSizeMip2;
    float3 advanceVecMip2 = advanceRay(voxelFragPos, s, boxRayCastM, boxRayCastKMip2, cellCenterMip2);
    uint cellCoordIdMip2 = idFromGridCoord(currCellCoordMip2.x, currCellCoordMip2.y, currCellCoordMip2.z, 64);
    
    // fast voxel traversal at mip2 to early out
    uint gridSizeMip2 = 64;
    uint boundsMip2 = gridSizeMip2 - 1;
    uint traverseStepsMip2 = 200 * microvoxelRayIterations;
    for(uint preTraverseMip2It = 0; preTraverseMip2It < traverseStepsMip2; ++preTraverseMip2It){
        if(currCellCoordMip2.x > 63 || currCellCoordMip2.y > 63 || currCellCoordMip2.z > 63 ||
            currCellCoordMip2.x < 0 || currCellCoordMip2.y < 0 || currCellCoordMip2.z < 0){
                
            return 0.0;
        }

        int mip2Pointer = -1;

        int mip2Id = chunkOffsetIdMip2 + cellCoordIdMip2;
        mip2Pointer = gridPointersMip2Glob[mip2Id];

        if(mip2Pointer > -1){
            break;
        }

        advanceVecMip2 = advanceRay(voxelFragPos, s, boxRayCastM, boxRayCastKMip2, cellCenterMip2);

        currCellCoordMip2 = currCellCoordMip2 + advanceVecMip2;

        cellCenterMip2 = float3(cellSizeMip2 * currCellCoordMip2.x, cellSizeMip2 * currCellCoordMip2.y, cellSizeMip2 * currCellCoordMip2.z) + halfCellSizeMip2;
        cellCoordIdMip2 = idFromGridCoord(currCellCoordMip2.x, currCellCoordMip2.y, currCellCoordMip2.z, gridSizeMip2);
    }

    // compute the bounding box depth to perform culling
    float4 hitDepthPointWS = mul(modelMatrix, float4(voxelFragPos - containerOffset + chunkCenter, 1));
    float4 hitDepthPointScreen = mul(UNITY_MATRIX_VP, hitDepthPointWS);
    float boxDepth = saturate(hitDepthPointScreen.z / hitDepthPointScreen.w);
    
    // culling
    bool culled = false;
    if(!cameraInsideBox){
        float4 bufferTexCoord = float4(screenPos.xy / screenPos.w, 0, 0);

        float4 prevDepthRGB = tex2Dlod(microVoxRenderTexDepth, bufferTexCoord);  
        float prevDepth = rgbToFloat(prevDepthRGB.xyz);
        if(boxDepth < prevDepth){
            culled = true;
        }
    }

    if(!culled){
        // compute LOD to adjust voxel quality
        float3 cellSizeWS = hitDepthPointWS.xyz + (UNITY_MATRIX_V._m00_m10_m20 * (cellSizeMip3 * objScale));
        float4 cellSizeCS = mul(UNITY_MATRIX_VP,  float4(cellSizeWS, 1));
        float4 screenPos2 = computeScreenPos(cellSizeCS);
        float4 screenPos1 = computeScreenPos(hitDepthPointScreen);

        float deviceCoords1 = screenPos1.x / screenPos1.w;
        float deviceCoords2 = screenPos2.x / screenPos2.w;
        float cellSizePixels = abs(deviceCoords1 - deviceCoords2) * _ScreenParams.x;

        float lodPixelsWidth = 0.025;// 1.0 / 40.0;

        lodFactor = saturate(cellSizePixels * lodPixelsWidth);

        lodFactor += 1.0 - _backFillAlphaGlob[containerIdSourceGlob];
    }
    else{
        lodFactor = 0.0;
        marchReducer = 0.4;
    }

    if(cameraInsideBox){
        lodFactor = 1.0;
    }

    if(lodEnabled == 0){
        lodFactor = 1.0;
        marchReducer = 1.0;
    }

    uint cellCoordId = cellCoordIdMip2;
    float3 cellCenter = cellCenterMip2;
    float3 advanceVec = advanceVecMip2;
    int3 currCellCoord = currCellCoordMip2;
    float cellSize = cellSizeMip2;
    float halfCellSize = halfCellSizeMip2;
    uint gridSize = 64;
    float mipLevel = 0.0;
    float skipBoundsCheck = 0.0;
    float testerBoundsMip2 = 0.0;
    int3 currCellCoordMip3 = 0;
    float3 normalOffset = float3(_roughOrientXGlob[containerIdSourceGlob], _roughOrientYGlob[containerIdSourceGlob], _roughOrientZGlob[containerIdSourceGlob]) * 0.2;
    float roughOffset = 1.0 - (_roughPosGlob[containerIdSourceGlob] * 0.5);
    float3 hitNormalTmp = 0;
    int hitMip3ColorData = 0;
    float hitNormalOffset = 0.0;
    float3 splatCenterDummy = 0.0;
    
    // first pass is a voxel traversal to hit large splats that act as a solid filler/background to then splat on top
    // traverse mip2 grid 64^3
    // then discend into mip3 bricks at 4^3
    float march = 1.0;
    uint traverseSteps = lerp(150, 250, lodFactor) * marchReducer * microvoxelRayIterations;
    for(uint traverseMip2It = 0; traverseMip2It < traverseSteps; ++traverseMip2It){
        // check bounds at current mip
        int bounds = gridSize - 1;
        // float testerBounds = saturate(
        //     step(currCellCoord.x, bounds) * step(currCellCoord.y, bounds) * step(currCellCoord.z, bounds) * // check out of bounds +
        //     (1.0 + sign(currCellCoord.x)) * (1.0 + sign(currCellCoord.y)) * (1.0 + sign(currCellCoord.z))); // check out of bounds -
        
        float testerBounds = 1.0;
        if(currCellCoord.x > bounds || currCellCoord.y > bounds || currCellCoord.z > bounds ||
            currCellCoord.x < 0 || currCellCoord.y < 0 || currCellCoord.z < 0){

            testerBounds = 0.0;
        }

        // make sure mip3 bounds test can be skipped
        float testerBoundsMip3 = saturate(skipBoundsCheck + testerBounds);

        // if mip2, update mip2 bounds check
        testerBoundsMip2 = lerp(testerBoundsMip3, testerBoundsMip2, mipLevel);

        // check mip2 pointer
        int mip2Id = chunkOffsetIdMip2 + cellCoordIdMip2;
        if(testerBoundsMip2 != 1.0) mip2Id = 0;

        int mip2Pointer = gridPointersMip2Glob[mip2Id];
        float testerPointerMip2 = saturate(1.0 + sign(mip2Pointer)) * testerBoundsMip2;
        
        if(mip2Pointer > -1){
            // descend to mip3
            traverseMip3Glob(containerIdSourceGlob, containerIdGlob, lodFactor, 1.0, normalOffset, roughOffset,
            voxelFragPos, viewDirectionWS, cellSizeMip3, cellSizeMip3Splat, cellCenter, mipLevel, testerBounds, testerBoundsMip3, mip2Pointer, chunkOffsetMip3, cellCoordId, march, hit, hitDepthPoint, hitNormalTmp, hitNormalOffset, splatCenterDummy, hitMip3ColorData);
        }

        // exit if mip2 out of bounds
        march *= lerp(testerBoundsMip2, 1.0, mipLevel);

        // break here to avoid advancing the grid further
        if(march == 0.0) break;
        
        // cache current mip level to compare later
        float prevMipLevel = mipLevel;

        // switch to mip3 on valid mip2 pointer if we are at mip2
        mipLevel = lerp(testerPointerMip2, 1.0, mipLevel);

        // switch to mip2 if mip3 out of bounds and we are at mip3
        mipLevel = lerp(0.0, testerBoundsMip3, mipLevel);

        // switch mip level data
        cellSize = lerp(cellSizeMip2, cellSizeMip3, mipLevel);
        halfCellSize = cellSize * 0.5;
        boxRayCastK = lerp(boxRayCastKMip2, boxRayCastKMip3, mipLevel);
        gridSize = lerp(64, 4, mipLevel);

        // restore mip2 if we're back at that level
        currCellCoord = lerp(currCellCoordMip2, currCellCoord, mipLevel);
        cellCenter = lerp(cellCenterMip2, cellCenter, mipLevel);
        
        // advance grid at current mip
        advanceVec = advanceRay(voxelFragPos, s, boxRayCastM, boxRayCastK, cellCenter);
        currCellCoord = currCellCoord + advanceVec;

        float3 cellCornerMip2 = cellCenterMip2 - halfCellSizeMip2;
        float3 cellCorner = lerp(0.0, cellCornerMip2, mipLevel);

        cellCenter = cellCorner + float3(cellSize * currCellCoord.x, cellSize * currCellCoord.y, cellSize * currCellCoord.z) + halfCellSize;
        cellCoordId = idFromGridCoord(currCellCoord.x, currCellCoord.y, currCellCoord.z, gridSize);

        // check if we just switched mip level
        float mipLevelSwitched = prevMipLevel != mipLevel;
        float justSwitchedToMip3 = mipLevelSwitched == 1.0 && mipLevel == 1.0;

        skipBoundsCheck = justSwitchedToMip3;

        if(justSwitchedToMip3 == 1.0){
            float3 rayPosMip2 = projectRayOnPlane(voxelFragPos, viewDirectionWS, cellCenterMip2 - (advanceVecMip2 * halfCellSizeMip2), -advanceVecMip2);
            int3 startCellCoordMip3 = ((rayPosMip2 - cellCornerMip2) * cellSizeMip3Div);

            currCellCoord = lerp(currCellCoord, startCellCoordMip3, justSwitchedToMip3);
        }

        cellCenter = lerp(cellCenter, cellCorner + float3(cellSize * currCellCoord.x, cellSize * currCellCoord.y, cellSize * currCellCoord.z) + halfCellSize, justSwitchedToMip3);
        cellCoordId = lerp(cellCoordId, idFromGridCoord(currCellCoord.x, currCellCoord.y, currCellCoord.z, gridSize), justSwitchedToMip3);

        // cache some mip2 data if current mip level is 2
        currCellCoordMip2 = lerp(currCellCoord, currCellCoordMip2, mipLevel);
        cellCoordIdMip2 = lerp(cellCoordId, cellCoordIdMip2, mipLevel);
        cellCenterMip2 = lerp(cellCenter, cellCenterMip2, mipLevel);
        advanceVecMip2 = lerp(advanceVec, advanceVecMip2, mipLevel);
    }

    // second pass starts from the previously hit voxel and tries to intersect the neighbours in order to have splats bigger than the hit voxel
    float3 cellCornerMip2 = cellCenterMip2 - halfCellSizeMip2;
    cellSizeMip3Splat = cellSizeMip3 * halfCellSizeMip3;
    float depthTest = 9999.0;

    // output base color
    int4 data2Mip3 = unpack66614(hitMip3ColorData);
    float3 backColor = (data2Mip3.xyz * 0.015625) * _backFillDarkGlob[containerIdSourceGlob];// div 64

    float3 backNormal = hitNormalTmp;

    // output clayObj id for picking
    hitClayObjId = data2Mip3.w;

    cellSizeMip3Splat = (cellSizeMip3 * cellSizeMip3) * 0.1;

    float pixelRandom = frac(sin(dot(float2(positionWS.x, positionWS.y),float2(12.9898,78.233+positionWS.z)))*43758.5453123);

    float hitColorRough = 0;

    uint numNeighbours = 27 * hit * lodFactor * neighboursLod * microvoxelSplatsQuality;
    for(uint neighbourIt = 0; neighbourIt < numNeighbours; ++neighbourIt){
        int3 cellCoord = currCellCoord + neighbourMip3[neighbourIt];
        uint neighbourCellCoordId = idFromGridCoord(cellCoord.x, cellCoord.y, cellCoord.z, 4);

        intersectSplatGlob(containerIdSourceGlob, containerIdGlob, lodFactor,
            pixelRandom, voxelFragPos, viewDirectionWS, cellCoord, neighbourCellCoordId, chunkOffsetIdMip2, chunkOffsetMip3, 1.0, cellCoordIdMip2, cellCornerMip2, cellSizeMip3, halfCellSizeMip3, cellSizeMip3Splat, depthTest, hitNormalTmp, hitNormalOffset, hitMip3ColorData, hitColorRough, hitDepthPoint);
        
        backNormal += hitNormalTmp;
    }

    numNeighbours = 12 * hit * lodFactor * neighboursLod * microvoxelSplatsQuality;
    // final step, we take in the neighbours of the hit voxel that fall on neighbour bricks
    for(uint brickLookupIt = 0; brickLookupIt < numNeighbours; ++brickLookupIt){
        int2 lookupData = neighbourBrickLookup[cellCoordId][brickLookupIt];

        // lookupData.x is cardinal direction id
        // lookupData.y is mip3 id in a 4^3 grid

        if(lookupData.x == -1){
            break;
        }

        int3 neighbourCellCoordMip2 = currCellCoordMip2 + neighboursMip2[lookupData.x];

        float neighbourTesterBoundsMip2 = saturate(
            step(neighbourCellCoordMip2.x, 63) * step(neighbourCellCoordMip2.y, 63) * step(neighbourCellCoordMip2.z, 63) * // check out of bounds +
            (1.0 + sign(neighbourCellCoordMip2.x)) * (1.0 + sign(neighbourCellCoordMip2.y)) * (1.0 + sign(neighbourCellCoordMip2.z))); // check out of bounds -

        uint neighbourCellCoordIdMip2 = idFromGridCoord(neighbourCellCoordMip2.x, neighbourCellCoordMip2.y, neighbourCellCoordMip2.z, 64);
        float3 neighbourCellCenterMip2 = float3(cellSizeMip2 * neighbourCellCoordMip2.x, cellSizeMip2 * neighbourCellCoordMip2.y, cellSizeMip2 * neighbourCellCoordMip2.z) + halfCellSizeMip2;
        float3 neighbourCellCornerMip2 = neighbourCellCenterMip2 - halfCellSizeMip2;

        uint neighbourCellCoordId = lookupData.y;
        int3 cellCoord = gridCoordFromLinearId(neighbourCellCoordId, 4);

        intersectSplatGlob(containerIdSourceGlob, containerIdGlob, lodFactor,
            pixelRandom, voxelFragPos, viewDirectionWS, cellCoord, neighbourCellCoordId, chunkOffsetIdMip2, chunkOffsetMip3, neighbourTesterBoundsMip2, neighbourCellCoordIdMip2, neighbourCellCornerMip2, cellSizeMip3, halfCellSizeMip3, cellSizeMip3Splat, depthTest, hitNormalTmp, hitNormalOffset, hitMip3ColorData, hitColorRough, hitDepthPoint);
        
        backNormal += hitNormalTmp;
    }

    float splatHit = depthTest < 9999.0;
    float3 finalNormal = normalize(lerp(backNormal, hitNormalTmp, splatHit));
    
    if(splatHit == 0.0 && lodFactor > 0.0){
        float viewDot = dot(finalNormal, viewDirectionWS);
        hit *= pixelRandom < (viewDot * 2) + lerp(-2.0, 1.0, _backFillAlphaGlob[containerIdSourceGlob]);
    }

    hitNormal = (finalNormal + 1.0) * 0.5;

    data2Mip3 = unpack66614(hitMip3ColorData);
    float3 splatColor = data2Mip3.xyz * 0.015625;// div 64

    hitColor = lerp(backColor, splatColor + (hitColorRough * 0.8), splatHit);
    
    hitGridData = float4(cellCenter * chunkSizeDiv, (hitNormalOffset + 1.0) * 0.5);
    
    hitDepthPoint = hitDepthPoint - containerOffset + chunkCenter;

    return hit;
}

bool clayxels_microVoxelsMip3SplatSeed_fragGlobShadow(float objScale, float4 screenPos, float4x4 modelMatrix, float4x4 objectMatrixInv, uint globalChunkId, uint containerIdSourceGlob, uint containerIdGlob, uint chunkIdGlob, float3 positionWS, float3 viewDirectionWS, float3 boundsCenter, float3 boundsSize, float3 localCamPos, out float3 hitNormal, out float3 hitColor, out float3 hitDepthPoint, out int hitClayObjId, out float4 hitGridData){
    hitNormal = 0;
    hitColor = 0.0;
    hitDepthPoint = float3(0, 0, 0);
    hitClayObjId = 0;
    hitGridData = 0;

    float hit = 0.0;

    float chunkSizeGlobVal = chunkSizeGlob[containerIdSourceGlob];

    float cellSizeMip2 = chunkSizeGlobVal * 0.015625;// div by 64
    float halfCellSizeMip2 = cellSizeMip2 * 0.5;
    float cellSizeMip3 = chunkSizeGlobVal * 0.00392156862;// div by 256
    float halfCellSizeMip3 = cellSizeMip3 * 0.5;
    float cellSizeMip3Div = rcp(cellSizeMip3);
    float chunkSizeDiv = rcp(chunkSizeGlobVal);

    float cellSizeMip3Splat = (cellSizeMip3 * cellSizeMip3) * 0.65;

    int chunkOffsetIdMip2 = lerp(chunkIdGlob * 262144, chunkIdOffsetGlob[containerIdGlob + chunkIdGlob], memoryOptimized);// chunkId x 64 ^ 3 if not optimized
    int chunkOffsetMip3 = lerp(chunkIdGlob * (16777216 * bufferSizeReduceFactor), 0, memoryOptimized); // chunkId x 256 ^ 3 if not optimized

    float3 boxRayCastM = rcp(viewDirectionWS);
    float3 boxRayCastKMip2 = (abs(boxRayCastM) * halfCellSizeMip2);
    float3 s = -sign(viewDirectionWS);

    float containerOffset = (chunkSizeGlobVal * 0.5) - halfCellSizeMip3;
    float3 chunkCenter = chunksCenterGlob[containerIdGlob + chunkIdGlob];

    float3 voxelFragPos = positionWS - chunkCenter + containerOffset;
    
    // start placing the ray adjacent to the mesh boundary
    int3 currCellCoordMip2 = voxelFragPos / cellSizeMip2;
    float3 cellCenterMip2 = float3(cellSizeMip2 * currCellCoordMip2.x, cellSizeMip2 * currCellCoordMip2.y, cellSizeMip2 * currCellCoordMip2.z) + halfCellSizeMip2;
    float3 advanceVecMip2 = advanceRay(voxelFragPos, s, boxRayCastM, boxRayCastKMip2, cellCenterMip2);
    uint cellCoordIdMip2 = idFromGridCoord(currCellCoordMip2.x, currCellCoordMip2.y, currCellCoordMip2.z, 64);

    uint gridSizeMip2 = 64;
    uint boundsMip2 = gridSizeMip2 - 1;
    uint traverseSteps = 60;
    for(uint traverseMip2It = 0; traverseMip2It < traverseSteps; ++traverseMip2It){
        // check bounds at current mip
        float testerBoundsMip2 = saturate(
            step(currCellCoordMip2.x, boundsMip2) * step(currCellCoordMip2.y, boundsMip2) * step(currCellCoordMip2.z, boundsMip2) * // check out of bounds +
            (1.0 + sign(currCellCoordMip2.x)) * (1.0 + sign(currCellCoordMip2.y)) * (1.0 + sign(currCellCoordMip2.z))); // check out of bounds -

        // check mip2 pointer
        int mip2Id = chunkOffsetIdMip2 + cellCoordIdMip2;
        if(testerBoundsMip2 != 1.0) mip2Id = 0;

        int mip2Pointer = gridPointersMip2Glob[mip2Id];
        float testerPointerMip2 = saturate(1.0 + sign(mip2Pointer)) * testerBoundsMip2;
        
        // break here to avoid advancing the grid further
        if(testerBoundsMip2 == 0.0) break;

        if(mip2Pointer > -1){
            hitDepthPoint = cellCenterMip2;
            hit = 1.0;
            break;
        }

        advanceVecMip2 = advanceRay(voxelFragPos, s, boxRayCastM, boxRayCastKMip2, cellCenterMip2);
        currCellCoordMip2 = currCellCoordMip2 + advanceVecMip2;

        cellCenterMip2 = float3(cellSizeMip2 * currCellCoordMip2.x, cellSizeMip2 * currCellCoordMip2.y, cellSizeMip2 * currCellCoordMip2.z) + halfCellSizeMip2;
        cellCoordIdMip2 = idFromGridCoord(currCellCoordMip2.x, currCellCoordMip2.y, currCellCoordMip2.z, gridSizeMip2);
    }

    hitDepthPoint = hitDepthPoint - containerOffset + chunkCenter;
    
    return hit;
}

void clayxelMicrovoxelVertGlobal(uint inputInstanceId, uint vertexId, float3 vertexPos, out float3 outVertexPos, out float3 outBoundsCenter, out float3 outBoundsSize, out float4 outScreenPos){
    int globalChunkId = inputInstanceId;
    int containerId = 0;// id of the container plus chunk-id
    int containerIdSource = 0;// id of the container unified for all chunks
    int chunkId = 0;// local chunk id
    int transformAccessId = 0;// id to access transforms
    int selectionContainerId = 0;// unique id for each chunk of each container
    int chunkUniqueId = 0;
    getDataPointers(globalChunkId, containerId, containerIdSource, chunkId, transformAccessId, selectionContainerId, chunkUniqueId);
    
    float chunkSizeGlobVal = chunkSizeGlob[containerIdSource];

    float4x4 objectMatrixInv = instancesObjectMatrixInvGlob[transformAccessId];
    float4x4 objectMatrix = instancesObjectMatrixGlob[transformAccessId];
    
    float3 localCamPos = 0;

    float3 boundsCenter = 0;
    float3 boundsSize = 0;
    float forceBoxsFlip = 0;
    float invertFlip = 1;

    #if !defined(CLAYXELS_SHADOWCASTER)
        float3 camNearPlanePos = _WorldSpaceCameraPos + (UNITY_MATRIX_V._m20_m21_m22 * _ProjectionParams.y);
        localCamPos = mul(objectMatrixInv, float4(camNearPlanePos, 1)).xyz;
    #else
        invertFlip = 0;
    #endif
    
    int outBoxFlipped = 0;
    clayxels_microVoxels_vertGlob(chunkSizeGlobVal, invertFlip, containerId, chunkId, vertexId, vertexPos, outVertexPos, boundsCenter, boundsSize, outBoxFlipped);
    
    outVertexPos = mul(objectMatrix, float4(outVertexPos,1)).xyz - renderBoundsCenterGlob;
    
    outBoundsCenter = boundsCenter;
    outBoundsSize = boundsSize;

    float4 vcs = mul(UNITY_MATRIX_VP, mul(UNITY_MATRIX_M, float4(outVertexPos, 1)));
    outScreenPos = computeScreenPos(vcs);
}

void clayxelMicrovoxelVert(uint instanceId, uint vertexId, float3 vertexPos, out float3 outVertexPos, out float3 outBoundsCenter, out float3 outBoundsSize, out float4 outScreenPos){
    outVertexPos = 0;
    outBoundsCenter = 0;
    outBoundsSize = 0;
    outScreenPos = 0;

    clayxelMicrovoxelVertGlobal(instanceId, vertexId, vertexPos, outVertexPos, outBoundsCenter, outBoundsSize, outScreenPos);
}

inline void clayxelMicrovoxelFragForwardGlob(int inputInstanceId, float3 vertexPos, uint vertexId, float4 screenPos, float3 boundsCenter, float3 viewDir, inout float3 outSplatPos, inout float3 outColor, inout float3 outNormal, inout float3 outEmission, inout float outDepth, inout float outAlpha, inout float outSmoothness, inout float outMetallic){
    int globalChunkId = inputInstanceId;
    int containerId = 0;// id of the container plus chunk-id
    int containerIdSource = 0;// id of the container unified for all chunks
    int chunkId = 0;// local chunk id
    int transformAccessId = 0;// id to access transforms
    int selectionContainerId = 0;// unique id for each chunk of each container
    int chunkUniqueId = 0;
    getDataPointers(globalChunkId, containerId, containerIdSource, chunkId, transformAccessId, selectionContainerId, chunkUniqueId);

    float chunkSizeGlobVal = chunkSizeGlob[containerIdSource];

    float4 bufferTexCoord = float4(screenPos.xy / screenPos.w, 0, 0);

    float4 buffer4 = tex2Dlod(microVoxRenderTex4, bufferTexCoord);
    uint containerIdAtPixel = rgbToInt(buffer4.xyz);

    outAlpha = 1;

    uint containerIdCheck = chunkUniqueId + 1;
    if(containerIdAtPixel != containerIdCheck){
        outAlpha = 0;
        discard;
    }

    float4x4 objectMatrix = instancesObjectMatrixGlob[transformAccessId];
    float4x4 objectMatrixInv = instancesObjectMatrixInvGlob[transformAccessId];

    float4 buffer1 = tex2Dlod(microVoxRenderTex1, bufferTexCoord);
    float3 splatNormal = buffer1.xyz * 2.0 - 1.0;
    float3 splatNormalOriented = mul((float3x3)objectMatrix, splatNormal);

    float chunkSize = chunkSizeGlob[containerIdSource];

    float cellSizeMip3 = chunkSize * 0.00390625;// div by 256
    float halfCellSizeMip3 = cellSizeMip3 * 0.5;

    float3 chunkCenter = chunksCenterGlob[containerId];
    float containerOffset = (chunkSize * 0.5) - halfCellSizeMip3;

    float4 buffer2 = tex2Dlod(microVoxRenderTex2, bufferTexCoord);

    float3 splatColor = buffer2.xyz;

    float4 depthRGB = tex2Dlod(microVoxRenderTexDepth, bufferTexCoord);
    float depth = rgbToFloat(depthRGB.xyz);
    
    #ifdef CLAYXELS_URP
        outDepth = depth;
        
        #if defined(CLAYXELS_UNITY_2020_2)
            outSplatPos = ComputeWorldSpacePosition(bufferTexCoord.xy, depth, UNITY_MATRIX_I_VP);    
        #else
            // ComputeWorldSpacePosition doesn't work so we need to spin our own
            outSplatPos = depthToWorld(bufferTexCoord.xy, depth);
        #endif
    #elif defined(CLAYXELS_HDRP)
        // this would be faster and should be the official way to get HDRP's depth offset, but it produces the wrong result
        // vertexPos = vertexPos + (renderBoundsCenter - objectMatrix._m03_m13_m23);
        // float worldPosLinearDepth = LinearEyeDepth(depth, _ZBufferParams);
        // outDepth = worldPosLinearDepth - TransformObjectToHClip(float4(vertexPos,1)).w;

        vertexPos = vertexPos - objectMatrix._m03_m13_m23;
        float3 splatPos = ComputeWorldSpacePosition(bufferTexCoord.xy, depth, UNITY_MATRIX_I_VP);    
        splatPos = GetAbsolutePositionWS(splatPos) - objectMatrix._m03_m13_m23 - renderBoundsCenterGlob;
        outDepth = -length(vertexPos - splatPos);

        outSplatPos = 0;
    #endif

    outColor = splatColor;
    outNormal = splatNormalOriented;

    float4 buffer0 = tex2Dlod(microVoxRenderTex0, bufferTexCoord);
    int selectionIdContainer = rgbToInt(buffer0.xyz);
    if(containerHighlightId+1 == selectionIdContainer){
        int clayObjIdAtPixel = rgbToInt(float3(buffer0.w, buffer1.w, buffer2.w));
        if(solidHighlightId == -2 || clayObjIdAtPixel == solidHighlightId + 1){
            outColor *= 1.5;
        }
    }

    // emission
    float emissionMult = 0.1;
    #ifdef CLAYXELS_HDRP
        emissionMult = 100.0;
    #endif

    float emission = emissionMult * _emissionPowerGlob[containerIdSource];
    float subsurfaceScatter = lerp(1.0, buffer4.w, saturate(_subsurfaceScatterGlob[containerIdSource]));
    outEmission = splatColor * _emissiveColorGlob[containerIdSource].xyz * (emission * subsurfaceScatter);

    outSmoothness = _smoothnessGlob[containerIdSource];
    outMetallic = _metallicGlob[containerIdSource];
}

#if defined(CLAYXELS_SHADOWCASTER)

inline void clayxelMicrovoxelFragShadow(int instanceId, float3 viewDir, float3 boundsCenter, float3 boundsSize, uint vertexId, float3 vertexPos, float4 screenPos, inout float outDepth, inout float outAlpha){
    int globalChunkId = instanceId;
    int containerId = 0;// id of the container plus chunk-id
    int containerIdSource = 0;// id of the container unified for all chunks
    int chunkId = 0;// local chunk id
    int transformAccessId = 0;// id to access transforms
    int selectionContainerId = 0;// unique id for each chunk of each container
    int chunkUniqueId = 0;
    getDataPointers(globalChunkId, containerId, containerIdSource, chunkId, transformAccessId, selectionContainerId, chunkUniqueId);

    float chunkSize = chunkSizeGlob[containerIdSource];

    float4x4 objectMatrixInv = instancesObjectMatrixInvGlob[transformAccessId];
    float4x4 objectMatrix = instancesObjectMatrixGlob[transformAccessId];
    
    vertexPos = vertexPos + (renderBoundsCenterGlob - objectMatrix._m03_m13_m23);
    float3 positionWS = mul((float3x3)objectMatrixInv, vertexPos);

    #ifdef CLAYXELS_URP
        float3 viewDirectionWS = mul((float3x3)objectMatrixInv, _MainLightPosition.xyz);
    #elif defined(CLAYXELS_HDRP)
        float3 viewDirectionWS = mul((float3x3)objectMatrixInv, viewDir);
    #endif
    
    float3 hitDepthPoint = 0;

    float3 localCamPos = 0;
    float3 hitNormal = 0;
    float3 hitColor = 0;
    int hitClayObjId = 0;
    float4 hitGridData = 0;

    float objScale = 1.0;

    outAlpha = 1;
    bool hit = clayxels_microVoxelsMip3SplatSeed_fragGlobShadow(objScale, screenPos, objectMatrix, objectMatrixInv, globalChunkId, containerIdSource, containerId, chunkId, positionWS, viewDirectionWS, boundsCenter, boundsSize, localCamPos, hitNormal, hitColor, hitDepthPoint, hitClayObjId, hitGridData);

    if(!hit){
        outAlpha = 0;
        discard;
    }

    float scale = length(objectMatrix._m00_m10_m20);
    float mip2CellSize = 0.015625;// 1 / 64
    float biasOffset = ((chunkSize * mip2CellSize) * 2.0);

    #ifdef CLAYXELS_URP
        hitDepthPoint = mul(objectMatrix, float4(hitDepthPoint, 1)).xyz;
        float3 shadowBias = _MainLightPosition.xyz * ((_ShadowBias.xxx - biasOffset) * scale) + hitDepthPoint;
        float4 hitDepthPointScreen = mul(UNITY_MATRIX_VP, float4(shadowBias, 1));
        outDepth = saturate(hitDepthPointScreen.z / hitDepthPointScreen.w) * 0.99;
    #elif defined(CLAYXELS_HDRP)
        outDepth = length(vertexPos - mul((float3x3)objectMatrix, hitDepthPoint)) + (biasOffset * scale);
    #endif
}

#endif

void clayxelMicrovoxelFrag(float3 viewDir, float instanceId, float3 boundsCenter, float3 boundsSize, int vertexId, float3 vertexPos, float4 screenPos, out float3 outSplatPos, out float3 outColor, out float3 outNormal, out float3 outEmission, out float outDepth, out float outAlpha, out float outSmoothness, out float outMetallic){
    outSplatPos = 0;
    outColor = 0;
    outNormal = 0;
    outEmission = 0;
    outDepth = 0;
    outAlpha = 0;
    outSmoothness = 0;
    outMetallic = 0;

    #if defined(CLAYXELS_FORWARD)
        clayxelMicrovoxelFragForwardGlob(round(instanceId), vertexPos, vertexId, screenPos, boundsCenter, viewDir, outSplatPos, outColor, outNormal, outEmission, outDepth, outAlpha, outSmoothness, outMetallic);
    #elif defined(CLAYXELS_SHADOWCASTER)
        clayxelMicrovoxelFragShadow(round(instanceId), viewDir, boundsCenter, boundsSize, vertexId, vertexPos, screenPos, outDepth, outAlpha);
    #endif
}

struct MVPassVertIn
{
    float4 positionOS   : POSITION;
    uint vertexID: SV_VertexID;
    uint instanceID : SV_InstanceID;
};

struct MVPassVertOut
{
    float3 instanceData: TEXCOORD0;
    float3 voxelSpacePos: TEXCOORD1;
    float3 voxelSpaceViewDir: TEXCOORD2;
    float3 voxelSpaceViewDirOrtho: TEXCOORD3;
    float3 boundsCenter: TEXCOORD4;
    float3 boundsSize: TEXCOORD5;
    float3 localCamPos: TEXCOORD6;
    float4x4 modelMatrix: TEXCOORD7;
    float4 worldSpacePos: TEXCOORD11;
    float4 screenPos: TEXCOORD12;
    float4 positionCS: SV_POSITION;
};

struct MVPassFragOut{
    float4 buffer0: SV_TARGET0;
    float4 buffer1: SV_TARGET1;
    float4 buffer2: SV_TARGET2;
    float4 buffer3: SV_TARGET3;
    float4 buffer4: SV_TARGET4;
};

MVPassVertOut MicroVoxelPassVertGlobal(MVPassVertIn input){
    int globalChunkId = input.instanceID;
    int containerId = 0;// id of the container plus chunk-id
    int containerIdSource = 0;// id of the container unified for all chunks
    int chunkId = 0;// local chunk id
    int transformAccessId = 0;// id to access transforms
    int selectionContainerId = 0;// unique id for each chunk of each container
    int chunkUniqueId = 0;
    getDataPointers(globalChunkId, containerId, containerIdSource, chunkId, transformAccessId, selectionContainerId, chunkUniqueId);

    float chunkSizeGlobVal = chunkSizeGlob[containerIdSource];

    MVPassVertOut output = (MVPassVertOut)0;

    float4x4 objectMatrixInv = instancesObjectMatrixInvGlob[transformAccessId];
    float4x4 objectMatrix = instancesObjectMatrixGlob[transformAccessId];
    
    float3 camNearPlanePos = _WorldSpaceCameraPos + (UNITY_MATRIX_V._m20_m21_m22 * _ProjectionParams.y);
    output.localCamPos = mul(objectMatrixInv, float4(camNearPlanePos, 1)).xyz;

    float3 vertexPos = 0;
    float3 boundsCenter = 0;
    float3 boundsSize = 0;
    
    float invertFlip = 0;
    float outBoxFlipped = 0;
    clayxels_microVoxels_vertGlob(chunkSizeGlobVal, invertFlip, containerId, chunkId, input.vertexID, input.positionOS.xyz, vertexPos, boundsCenter, boundsSize, outBoxFlipped);
    
    output.boundsCenter = boundsCenter;
    output.boundsSize = boundsSize;

    output.voxelSpacePos = vertexPos;

    output.voxelSpaceViewDir = mul(objectMatrixInv, float4(_WorldSpaceCameraPos.xyz, 1)).xyz;

    output.modelMatrix = mul(UNITY_MATRIX_M, objectMatrix);// hdrp needs UNITY_MATRIX_M, lets pre-multiply it here with the real objectMatrix
    
    float4 modelPos = mul(output.modelMatrix, float4(vertexPos, 1.0));

    output.worldSpacePos = modelPos;

    float4 positionCS = mul(UNITY_MATRIX_VP, modelPos);
    output.positionCS = positionCS;

    output.voxelSpaceViewDirOrtho = mul(objectMatrixInv, float4(UNITY_MATRIX_VP[2].xyz + objectMatrix._m03_m13_m23,1)).xyz;

    float objScale = length(output.modelMatrix._m00_m10_m20);
    output.instanceData = float3(globalChunkId, objScale, 0);

    #if defined(CLAYXELS_URP)
        float3 v = mul(mul(unity_WorldToObject, objectMatrix), float4(vertexPos,1)).xyz;
    #else
        float3 v = mul(objectMatrix, float4(vertexPos, 1)).xyz;
    #endif 

    float4 vcs = mul(UNITY_MATRIX_VP, mul(UNITY_MATRIX_M, float4(v, 1)));    
    output.screenPos = computeScreenPos(vcs);

    return output;
}

inline float distSquared(float3 A, float3 B){
    float3 C = A - B;

    return dot(C, C);

}

MVPassFragOut MicroVoxelPassFragGlobal(MVPassVertOut input, 
#ifdef CLAYXEL_EARLY_Z_OPTIMIZE_ON
    out float outDepth : SV_DepthLessEqual // this will break going with the camera inside the container box
#else
    out float outDepth : SV_Depth// use this while sculpting or if camera needs to go inside the box
#endif
){
    int globalChunkId = round(input.instanceData.x);
    int containerId = 0;// id of the container plus chunk-id
    int containerIdSource = 0;// id of the container unified for all chunks
    int chunkId = 0;// local chunk id
    int transformAccessId = 0;// id to access transforms
    int selectionContainerId = 0;// unique id for each chunk of each container
    int chunkUniqueId = 0;
    getDataPointers(globalChunkId, containerId, containerIdSource, chunkId, transformAccessId, selectionContainerId, chunkUniqueId);

    float chunkSizeGlobVal = chunkSizeGlob[containerIdSource];

    float3 voxelPos = input.voxelSpacePos;

    float perspIso = unity_OrthoParams.w;// pespective == 0.0 , ortho == 1.0

    float3 viewDirectionWSPersp = input.voxelSpaceViewDir - voxelPos;
    float3 viewDirectionWSOrtho = input.voxelSpaceViewDirOrtho;
    float3 voxelSpaceViewDir = normalize(lerp(viewDirectionWSPersp, viewDirectionWSOrtho, perspIso * 0.9999999));// 0.9999 to avoid zero divs in some ortho camera rotations

    float objScale = input.instanceData.y;

    float3 hitNormal = 0;
    float3 hitColor = 0;
    float3 hitDepthPoint = 0;
    int hitClayObjId = 0;
    float4 hitGridData = 0;

    float4x4 objMatrixInv = instancesObjectMatrixInvGlob[transformAccessId];
    
    bool hit = false;
    hit = clayxels_microVoxelsMip3SplatSeed_fragGlob(objScale, input.screenPos, input.modelMatrix, objMatrixInv, globalChunkId, containerIdSource, containerId, chunkId, voxelPos, voxelSpaceViewDir, input.boundsCenter, input.boundsSize, input.localCamPos, hitNormal, hitColor, hitDepthPoint, hitClayObjId, hitGridData);
    
    if(!hit){
        discard;
    }

    MVPassFragOut outBuffers = (MVPassFragOut)0;

    float4 hitDepthPointWS = mul(input.modelMatrix, float4(hitDepthPoint, 1));
    float4 hitDepthPointScreen = mul(UNITY_MATRIX_VP, hitDepthPointWS);
    outDepth = saturate(hitDepthPointScreen.z / hitDepthPointScreen.w);

    float3 selectionIdContainerRGB = unpackRgb(selectionContainerId + 1);
    float3 selectionClayObjIdRGB = unpackRgb(hitClayObjId);
    float3 depthRGB = floatToRgb(outDepth);

    float3 globalChunkIdRGB = unpackRgb(chunkUniqueId + 1);

    float approxDist = distSquared(_subsurfaceCenterGlob[containerIdSource].xyz, hitDepthPoint);
    float subsurfRadius = lerp(0.0, chunkSizeGlobVal * 10.0, saturate(_subsurfaceScatterGlob[containerIdSource] - 1.0));
    float subsurfaceScatter = 1.0 - saturate(approxDist / subsurfRadius);
    
    outBuffers.buffer0 = float4(selectionIdContainerRGB, selectionClayObjIdRGB.x);
    outBuffers.buffer1 = float4(hitNormal, selectionClayObjIdRGB.y);
    outBuffers.buffer2 = float4(hitColor, selectionClayObjIdRGB.z);
    outBuffers.buffer3 = float4(depthRGB, 1.0);
    outBuffers.buffer4 = float4(globalChunkIdRGB, subsurfaceScatter);
    
    return outBuffers;
}
