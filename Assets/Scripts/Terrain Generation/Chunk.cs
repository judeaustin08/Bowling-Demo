using UnityEngine;

// Generate a custom plane mesh with a variable number of vertices
[RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    [Tooltip("The number of vertices per side of a chunk")]
    [SerializeField] private int chunkResolution;
    [SerializeField] private float worldSize;

    private float vertexSpacing;

    private Vector3[] vertices;
    private Mesh mesh;

    void Awake()
    {
        RegenerateMesh();
    }

    // Cleanly regenerate the vertex and tri information for this object
    private void RegenerateMesh()
    {
        vertexSpacing = worldSize / (chunkResolution - 1);

        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        GetComponent<MeshCollider>().sharedMesh = mesh;

        vertices = new Vector3[chunkResolution * chunkResolution];
        float x, y;
        // Loop through all vertices and set them to the correct positions
        for (int i = 0; i < vertices.Length; i++)
        {
            x = i % chunkResolution * vertexSpacing;
            y = i / chunkResolution * vertexSpacing;
            vertices[i] = new Vector3(x, 0, y);
        }

        mesh.vertices = vertices;

        int triangleSize = chunkResolution - 1;
        int[] triangles = new int[triangleSize * triangleSize * 6];
        // Loop through all triangles that need to be created and assign the correct vertices in the correct order
        for (int ti = 0, vi = 0, i = 0; i < triangleSize; i++, vi++)
            for (int j = 0; j < triangleSize; j++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                triangles[ti + 4] = triangles[ti + 1] = vi + triangleSize + 1;
                triangles[ti + 5] = vi + triangleSize + 2;
            }

        mesh.triangles = triangles;

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    // Takes in a float array of the same length as the vertex array and sets the heights of
    // the vertices equal to the corresponding heightmap element
    public void SetHeights(float[] heightmap, bool regenerate = true)
    {
        if (heightmap.Length != vertices.Length)
        {
            Debug.LogError("Heightmap length does not match vertex array length!");
            return;
        }

        for (int i = 0; i < vertices.Length; i++)
            vertices[i].y = heightmap[i];

        if (regenerate)
            SetVertices();
    }

    // Helper function to set vertices and recalculate mesh data
    private void SetVertices()
    {
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    private void OnDrawGizmos()
    {
        if (vertices == null) return;

        Gizmos.color = Color.black;

        foreach (Vector3 vertex in vertices)
            Gizmos.DrawSphere(vertex, 0.1f);
    }
}
