using System.Collections.Generic;
using UnityEngine;

// Generate a custom plane mesh with a variable number of vertices
[RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    [Tooltip("The number of vertices per side of a chunk")]
    public int chunkResolution;
    [HideInInspector] public float worldSize;

    private float vertexSpacing;

    [HideInInspector] public Vector3[] vertices;
    private int[] triangles;
    // The normal vector calculation takes into account the faces adjacent to the face whose normal
    // is being calculated. These border faces are necessary to eliminate lighting seams between
    // adjacent chunks due to the fact that they do not have access to other chunks' vertex data.
    private int[] borderTris;
    private Mesh mesh;
    private Dictionary<Vector3, GameObject> objects = new();

    // Cleanly regenerate the vertex and tri information for this object
    public void RegenerateMesh()
    {
        vertexSpacing = worldSize / (chunkResolution - 1);

        GetComponent<MeshFilter>().mesh = mesh = new Mesh();

        int borderSize = chunkResolution + 2;
        vertices = new Vector3[borderSize * borderSize];
        float x, y;
        // Loop through all vertices and set them to the correct positions
        for (int i = 0; i < vertices.Length; i++)
        {
            x = (i % borderSize - 1) * vertexSpacing;
            y = (i / borderSize - 1) * vertexSpacing;
            vertices[i] = new Vector3(x, 0, y);
        }

        mesh.vertices = vertices;

        int triangleSize = chunkResolution - 1;
        // Array containing all rendered tris
        triangles = new int[triangleSize * triangleSize * 6];
        int rowSize = borderSize - 1;
        // Array containing all tris, including border tris
        borderTris = new int[rowSize * rowSize * 6];
        // Loop through all triangles that need to be created and assign the correct vertices in the correct order
        for (int ti = 0, bti = 0, vi = 0, i = 0; i < rowSize; i++, vi++)
        {
            for (int j = 0; j < rowSize; j++, bti += 6, vi++)
            {
                // Add triangle to borderTris array
                borderTris[bti] = vi;
                borderTris[bti + 3] = borderTris[bti + 2] = vi + 1;
                borderTris[bti + 4] = borderTris[bti + 1] = vi + rowSize + 1;
                borderTris[bti + 5] = vi + rowSize + 2;

                // If tri doesn't contain border vertices
                if (vi > borderSize && vi < borderSize * (triangleSize + 1) && vi % borderSize > 0 && vi % borderSize < rowSize - 1)
                {
                    // Add triangle to rendered triangles array
                    triangles[ti] = vi;
                    triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                    triangles[ti + 4] = triangles[ti + 1] = vi + rowSize + 1;
                    triangles[ti + 5] = vi + rowSize + 2;

                    ti += 6;
                }

                mesh.triangles = triangles;
            }
        }

        mesh.triangles = triangles;

        mesh.normals = RecalculateNormals();
        mesh.RecalculateBounds();
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
        mesh.normals = RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    // Custom method to recalculate the lighting normals for a mesh. This is necessary because
    // the built-in Mesh.RecalculateNormals() method will not take into account the border triangles
    // and create lighting seams.
    private Vector3[] RecalculateNormals()
    {
        Vector3[] normals = new Vector3[vertices.Length];
        for (int i = 0; i < borderTris.Length - 3; i += 3)
        {
            int a = borderTris[i];
            int b = borderTris[i + 1];
            int c = borderTris[i + 2];

            Vector3 normal = SurfaceNormalFromIndices(a, b, c);
            normals[a] += normal;
            normals[b] += normal;
            normals[c] += normal;
        }

        for (int i = 0; i < normals.Length; i++)
            normals[i].Normalize();

        return normals;
    }

    private Vector3 SurfaceNormalFromIndices(int idx_a, int idx_b, int idx_c)
    {
        Vector3 a = vertices[idx_a];
        Vector3 b = vertices[idx_b];
        Vector3 c = vertices[idx_c];

        Vector3 ab = b - a;
        Vector3 ac = c - a;

        return Vector3.Cross(ab, ac).normalized;
    }
}
