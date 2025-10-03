using UnityEngine;
using UnityEngine.Rendering;

public class ProceduralGrass : MonoBehaviour
{
    [Header("Rendering Properties")]

    [Tooltip("Compute shader for generating transformation matrices.")]
    public ComputeShader computeShader;

    private Mesh terrainMesh;
    [Tooltip("Mesh for individual grass blades.")]
    public Mesh grassMesh;
    [Tooltip("Material for rendering each grass blade.")]
    public Material material;

    [Space(10)]

    [Header("Lighting and Shadows")]

    [Tooltip("Should the procedural grass cast shadows?")]
    public ShadowCastingMode castShadows = ShadowCastingMode.On;
    [Tooltip("Should the procedural grass receive shadows from other objects?")]
    public bool receiveShadows = true;

    [Space(10)]

    [Header("Grass Blade Properties")]

    [Range(0.0f, 1.0f)]
    [Tooltip("Base size of grass blades in all three axes.")]
    public float scale = 0.1f;
    [Range(0.0f, 5.0f)]
    [Tooltip("Minimum height multiplier.")]
    public float minBladeHeight = 0.5f;
    [Range(0.0f, 5.0f)]
    [Tooltip("Maximum height multiplier.")]
    public float maxBladeHeight = 1.5f;

    [Range(-1.0f, 1.0f)]
    [Tooltip("Minimum random offset in the x- and z-directions.")]
    public float minOffset = -0.1f;
    [Range(-1.0f, 1.0f)]
    [Tooltip("Maximum random offset in the x- and z-directions.")]
    public float maxOffset = 0.1f;

    private GraphicsBuffer terrainTriangleBuffer;
    private GraphicsBuffer terrainVertexBuffer;

    private GraphicsBuffer transformMatrixBuffer;

    private GraphicsBuffer grassTriangleBuffer;
    private GraphicsBuffer grassVertexBuffer;
    private GraphicsBuffer grassUVBuffer;
    // Grass position is used to generate instance-wide pseudo-random data
    private GraphicsBuffer grassNoiseBuffer;

    private Bounds bounds;
    private MaterialPropertyBlock properties;

    private int kernel;
    private uint threadGroupSize;
    private int terrainTriangleCount = 0;

    private bool initialized = false;
    private int sideLength;

    private Vector3[] RemoveBorderVertices(Vector3[] borderVertices)
    {
        int borderSize = Mathf.CeilToInt(Mathf.Sqrt(borderVertices.Length));
        sideLength = borderSize - 2;
        Vector3[] vertices = new Vector3[sideLength * sideLength];

        for (int i = 1, vi = 0; i < borderSize - 1; i++)
        {
            for (int j = 1; j < borderSize - 1; j++, vi++)
            {
                vertices[vi] = borderVertices[i * borderSize + j];
            }
        }

        return vertices;
    }

    private int[] ConstructTriangles()
    {
        int triangleSize = sideLength - 1;
        int[] tris = new int[triangleSize * triangleSize * 6];

        for (int i = 0, vi = 0, ti = 0; i < triangleSize; i++, vi++)
            for (int j = 0; j < triangleSize; j++, vi++, ti += 6)
            {
                tris[ti] = vi;
                tris[ti + 3] = tris[ti + 2] = vi + 1;
                tris[ti + 4] = tris[ti + 1] = vi + sideLength;
                tris[ti + 5] = vi + sideLength + 1;
            }

        return tris;
    }

    public void Initialize(Mesh mesh)
    {
        if (initialized) Dispose();

        kernel = computeShader.FindKernel("CalculateBladePositions");

        terrainMesh = mesh;

        // Terrain data for the compute shader.
        Vector3[] terrainVertices = RemoveBorderVertices(terrainMesh.vertices);
        terrainVertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, terrainVertices.Length, sizeof(float) * 3);
        terrainVertexBuffer.SetData(terrainVertices);

        int[] terrainTriangles = ConstructTriangles();
        terrainTriangleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, terrainTriangles.Length, sizeof(int));
        terrainTriangleBuffer.SetData(terrainTriangles);

        terrainTriangleCount = terrainTriangles.Length / 3;

        computeShader.SetBuffer(kernel, "_TerrainPositions", terrainVertexBuffer);
        computeShader.SetBuffer(kernel, "_TerrainTriangles", terrainTriangleBuffer);

        // Grass data for RenderPrimitives.
        Vector3[] grassVertices = grassMesh.vertices;
        grassVertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, grassVertices.Length, sizeof(float) * 3);
        grassVertexBuffer.SetData(grassVertices);

        int[] grassTriangles = grassMesh.triangles;
        grassTriangleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, grassTriangles.Length, sizeof(int));
        grassTriangleBuffer.SetData(grassTriangles);

        Vector2[] grassUVs = grassMesh.uv;
        grassUVBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, grassUVs.Length, sizeof(float) * 2);
        grassUVBuffer.SetData(grassUVs);

        // Set up buffer for the grass blade transformation matrices.
        transformMatrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, terrainTriangleCount, sizeof(float) * 16);
        computeShader.SetBuffer(kernel, "_TransformMatrices", transformMatrixBuffer);

        // Set up a buffer for the grass noise.
        grassNoiseBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, terrainTriangleCount, sizeof(float) * 3);
        computeShader.SetBuffer(kernel, "_GrassNoise", grassNoiseBuffer);

        // Set bounds.
        bounds = terrainMesh.bounds;
        bounds.center += transform.position;
        bounds.Expand(maxBladeHeight);

        // Bind buffers to a MaterialPropertyBlock which will get used for the draw call.
        properties = new MaterialPropertyBlock();
        properties.SetBuffer("_TransformMatrices", transformMatrixBuffer);
        properties.SetBuffer("_Positions", grassVertexBuffer);
        properties.SetBuffer("_UVs", grassUVBuffer);
        properties.SetBuffer("_GrassNoise", grassNoiseBuffer);

        RunComputeShader();

        initialized = true;
    }

    private void RunComputeShader()
    {
        // Bind variables to the compute shader.
        computeShader.SetMatrix("_TerrainObjectToWorld", transform.localToWorldMatrix);
        computeShader.SetInt("_TerrainTriangleCount", terrainTriangleCount);
        computeShader.SetFloat("_MinBladeHeight", minBladeHeight);
        computeShader.SetFloat("_MaxBladeHeight", maxBladeHeight);
        computeShader.SetFloat("_MinOffset", minOffset);
        computeShader.SetFloat("_MaxOffset", maxOffset);
        computeShader.SetFloat("_Scale", scale);

        // Run the compute shader's kernel function.
        computeShader.GetKernelThreadGroupSizes(kernel, out threadGroupSize, out _, out _);
        int threadGroups = Mathf.CeilToInt(terrainTriangleCount / threadGroupSize);
        computeShader.Dispatch(kernel, threadGroups, 1, 1);
    }

    // Run a single draw call to render all the grass blade meshes each frame.
    private void Update()
    {
        if (!initialized) return;
        /*
        RenderParams rp = new RenderParams(material);
        rp.worldBounds = bounds;
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer("_TransformMatrices", transformMatrixBuffer);
        rp.matProps.SetBuffer("_Positions", grassVertexBuffer);
        rp.matProps.SetBuffer("_UVs", grassUVBuffer);
        */

        //Graphics.RenderPrimitivesIndexed(rp, MeshTopology.Triangles, grassTriangleBuffer, grassTriangleBuffer.count, instanceCount: terrainTriangleCount);
        if (grassTriangleBuffer != null)
            Graphics.DrawProcedural(
                material, bounds, MeshTopology.Triangles, grassTriangleBuffer, grassTriangleBuffer.count,
                instanceCount: terrainTriangleCount,
                properties: properties,
                castShadows: castShadows,
                receiveShadows: receiveShadows
            );
    }

    public void Dispose()
    {
        terrainTriangleBuffer?.Dispose();
        terrainVertexBuffer?.Dispose();
        transformMatrixBuffer?.Dispose();

        grassTriangleBuffer?.Dispose();
        grassVertexBuffer?.Dispose();
        grassUVBuffer?.Dispose();

        initialized = false;
    }

    private void OnDisable()
    {
        Dispose();
    }
}
