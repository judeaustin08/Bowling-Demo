using System.Collections.Generic;
using UnityEngine;

public class GlobalChunkManager : MonoBehaviour
{
    private Dictionary<Vector2, Chunk> chunkmap = new();

    [SerializeField] private int renderDistance = 5;
    [SerializeField] private GameObject chunkPrefab;

    void Update()
    {
        
    }
}
