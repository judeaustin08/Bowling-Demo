using System.Collections.Generic;
using UnityEngine;

public class GlobalChunkManager : MonoBehaviour
{
    private Dictionary<Vector2, GameObject> chunkmap = new();
    private Dictionary<Vector2, GameObject> loaded = new();

    [Tooltip("Transform around which chunks are loaded")]
    [SerializeField] private Transform player;
    private Vector2Int currentChunk;
    [SerializeField] private GameObject chunkPrefab;

    [SerializeField] private int renderDistance = 5;
    [Tooltip("The desired world space size of each chunk, in Unity units")]
    [SerializeField] private float worldSize = 10;
    private int seed = 0;
    private System.Random rand = new();

    [Header("Noise generation variables")]
    [SerializeField] private float coarseAmplitude;
    [SerializeField] private float coarseFrequency;
    [SerializeField] private int octaves;
    [SerializeField] private float amplitude;
    [SerializeField] private float amplitudeFactor;
    [SerializeField] private float frequency;
    [SerializeField] private float frequencyFactor;

    void Awake()
    {
        seed = (int)(rand.NextDouble() * 10000);
    }

    void Update()
    {
        // Get chunk position of player
        currentChunk = new Vector2Int(
            (int)(player.position.x / worldSize),
            (int)(player.position.z / worldSize)
        );

        // Loop through all loaded chunks to see if there are any chunks that should be unloaded
        List<Vector2> loadedChunks = new(loaded.Keys);
        foreach (Vector2 pos in loadedChunks)
        {
            if (
                pos.x < currentChunk.x - renderDistance ||
                pos.x > currentChunk.x + renderDistance ||
                pos.y < currentChunk.y - renderDistance ||
                pos.y > currentChunk.y + renderDistance
            )
            {
                loaded.Remove(pos);
                chunkmap[pos].SetActive(false);
            }
        }

        // Loop through every chunk that should be loaded and ensure that it is
        for (int dx = -renderDistance; dx < renderDistance; dx++)
            for (int dy = -renderDistance; dy < renderDistance; dy++)
            {
                Vector2Int pos = currentChunk + new Vector2Int(dx, dy);

                if (!chunkmap.TryGetValue(pos, out _))
                {
                    chunkmap.Add(pos, CreateChunk(pos));
                }

                if (!loaded.TryGetValue(pos, out _))
                {
                    loaded.Add(pos, chunkmap[pos]);
                    chunkmap[pos].SetActive(true);
                }
            }
    }

    float maxHeight;
    private float[] GenerateHeightmap(Chunk c)
    {
        float[] heightmap = new float[c.vertices.Length];

        float a_local, f_local;

        maxHeight = amplitude * (1 - Mathf.Pow(amplitudeFactor, octaves)) / (1 - amplitudeFactor);

        float height;
        Vector3 coordinates;
        // For each vertex
        for (int i = 0; i < heightmap.Length; i++)
        {
            a_local = amplitude;
            f_local = frequency;

            height = 0;
            // Get world coordinates to use as an input for perlin noise
            coordinates = c.transform.position + c.vertices[i] + Vector3.one * (seed + 0.1f);

            // Detailing noise
            for (int octave = 0; octave < octaves; octave++)
            {
                // Calculate and transform perlin noise output
                height += Mathf.PerlinNoise(coordinates.x / f_local, coordinates.z / f_local) * a_local;

                a_local *= amplitudeFactor;
                f_local *= frequencyFactor;
            }

            // Normalize detailing height
            height *= amplitude / maxHeight;

            // Coarse noise to add large-scale height variation
            height += Mathf.PerlinNoise(coordinates.x / coarseFrequency, coordinates.z / coarseFrequency) * coarseAmplitude;

            height = Mathf.Floor(height);

            // Save to corresponding heightmap index
            heightmap[i] = height;
        }

        return heightmap;
    }

    private GameObject CreateChunk(Vector2 index)
    {
        GameObject obj = Instantiate(
            chunkPrefab,
            new Vector3(index.x, 0, index.y) * worldSize,
            Quaternion.identity
        );

        Chunk c = obj.GetComponent<Chunk>();
        c.worldSize = worldSize;
        c.RegenerateMesh();
        float[] heightmap = GenerateHeightmap(c);
        c.SetHeights(heightmap);

        return obj;
    }

    public void RegenerateAllChunks()
    {
        List<GameObject> chunks = new List<GameObject>(chunkmap.Values);
        for (int i = 0; i < chunks.Count; i++)
        {
            Chunk c = chunks[i].GetComponent<Chunk>();
            c.RegenerateMesh();
            float[] heightmap = GenerateHeightmap(c);
            c.SetHeights(heightmap);
        }
    }
}
