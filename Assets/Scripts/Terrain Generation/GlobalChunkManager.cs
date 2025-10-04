using System.Collections.Generic;
using UnityEngine;

public class GlobalChunkManager : MonoBehaviour
{
    private Dictionary<Vector2, GameObject> chunkmap = new();
    private Dictionary<Vector2, GameObject> loaded = new();

    [Tooltip("Transform around which chunks are loaded")]
    [SerializeField] private Transform player;
    [SerializeField] private bool resetPlayerHeightOnStart;
    private Vector2Int currentChunk;
    [SerializeField] private GameObject chunkPrefab;

    [System.Serializable]
    private struct RandomObject
    {
        public GameObject[] prefabs;
        public float spawnChance;
        public Vector3 lockDirection;
        public float leeway;
        public bool allowOverlap;
    }
    [SerializeField] private RandomObject[] spawnObjects;
    [SerializeField] private int startClearRadius;

    [Header("Mesh Variables")]
    [SerializeField] private int renderDistance = 5;
    [Tooltip("The number of vertices on each side of a chunk mesh")]
    [SerializeField] private int standardResolution = 10;
    [Tooltip("The desired world space size of each chunk, in Unity units")]
    [SerializeField] private float chunkSize = 10;
    private int seed = 0;
    private System.Random rand = new();

    private AstarPath asp;

    [Header("Noise generation variables")]
    [SerializeField] private float coarseAmplitude = 1;
    [SerializeField] private float coarseFrequency = 1;
    [SerializeField] private int octaves = 1;
    [SerializeField] private float amplitude = 1;
    [SerializeField] private float amplitudeFactor = 1;
    [SerializeField] private float frequency = 1;
    [SerializeField] private float frequencyFactor = 1;

    [Header("Debugging")]
    [SerializeField] private bool drawGizmos = false;
    [SerializeField] private float gizmoDrawDistance = 10f;

    void Awake()
    {
        seed = (int)(rand.NextDouble() * 10000);
        rand = new System.Random(seed);
        asp = FindAnyObjectByType<AstarPath>();
    }

    void Start()
    {
        // Generate chunks in the startClearRadius without objects
        for (int i = -startClearRadius; i <= startClearRadius; i++)
            for (int j = -startClearRadius; j <= startClearRadius; j++)
            {
                Vector2 coordinates = new(i, j);
                chunkmap.Add(coordinates, CreateChunk(coordinates));
            }
        LoadChunks();

        if (resetPlayerHeightOnStart)
            player.transform.position += new Vector3(
                0,
                1 + GetHeight(player.transform.position),
                0
            );

        asp.Scan();
    }

    void Update()
    {
        LoadChunks();
    }

    private void LoadChunks()
    {
        // Get chunk position of player
        currentChunk = new Vector2Int(
            (int)(player.position.x / chunkSize),
            (int)(player.position.z / chunkSize)
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
                    GameObject c = CreateChunk(pos);
                    chunkmap.Add(pos, c);
                    GenerateObjects(c.GetComponent<Chunk>());
                }

                if (!loaded.TryGetValue(pos, out _))
                {
                    loaded.Add(pos, chunkmap[pos]);
                    chunkmap[pos].SetActive(true);
                }
            }

        asp.ScanAsync();
    }

    private void GenerateObjects(Chunk c)
    {
        foreach (RandomObject spawnObject in spawnObjects)
        {
            // Select random prefab from options
            GameObject prefab = spawnObject.prefabs[Random.Range(0, spawnObject.prefabs.Length)];
            float chance = spawnObject.spawnChance;

            for (int i = 0; i < c.vertices.Length; i++)
                if (rand.NextDouble() < chance && Vector3.Angle(spawnObject.lockDirection, c.gameObject.GetComponent<MeshFilter>().mesh.normals[i]) < spawnObject.leeway)
                    if (!(c.objects.TryGetValue(c.vertices[i], out _) && spawnObject.allowOverlap))
                        c.objects.Add(c.vertices[i], prefab);
        }

        c.CreateAllObjects();
    }

    float maxHeight;
    private float[] GenerateHeightmap(Chunk c)
    {
        float[] heightmap = new float[c.vertices.Length];

        maxHeight = amplitude * (1 - Mathf.Pow(amplitudeFactor, octaves)) / (1 - amplitudeFactor);
        if (float.IsNaN(maxHeight) || float.IsInfinity(maxHeight)) maxHeight = 1;

        float height;
        Vector3 coordinates;
        // For each vertex
        for (int i = 0; i < heightmap.Length; i++)
        {
            // Get world coordinates to use as an input for perlin noise
            coordinates = c.transform.position + c.vertices[i];

            height = GetHeight(coordinates);

            // Save to corresponding heightmap index
            heightmap[i] = height;
        }

        return heightmap;
    }

    public float GetHeight(Vector3 coordinates)
    {
        float height = 0;
        coordinates += Vector3.one * (seed + 0.1f);

        float a_local = amplitude;
        float f_local = frequency;

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

        return height;
    }

    private GameObject CreateChunk(Vector2 index)
    {
        GameObject obj = Instantiate(
            chunkPrefab,
            new Vector3(index.x, 0, index.y) * chunkSize,
            Quaternion.identity
        );

        Chunk c = obj.GetComponent<Chunk>();
        c.chunkResolution = standardResolution;
        c.worldSize = chunkSize;
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
    
    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.black;

        foreach (GameObject obj in new List<GameObject>(loaded.Values))
        {
            if ((player.transform.position - obj.transform.position).magnitude < gizmoDrawDistance)
            {
                Chunk c = obj.GetComponent<Chunk>();
                if (c.vertices == null) continue;

                foreach (Vector3 vertex in c.vertices)
                    Gizmos.DrawSphere(c.transform.position + vertex, 0.1f);
            }
        }
    }
}
