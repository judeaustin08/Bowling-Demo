using System.Collections.Generic;
using UnityEngine;

public class EntitySpawner : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private GameObject entityPrefab;
    [SerializeField] private float spawnChance;
    [SerializeField] private float simulationDistance;
    Dictionary<int, GameObject> spawnedEntities = new();
    [SerializeField] private int cap = 10;
    private int count = 0;

    private GlobalChunkManager _gcm;
    private System.Random rand = new();

    private void Awake()
    {
        _gcm = GetComponent<GlobalChunkManager>();
    }

    private void Update()
    {
        if (rand.NextDouble() < spawnChance)
        {
            Vector3 position = new(
                player.position.x + Random.Range(-simulationDistance, simulationDistance),
                0,
                player.position.z + Random.Range(-simulationDistance, simulationDistance)
            );
            position.y = _gcm.GetHeight(position);
            SpawnEntity(position);
        }
    }

    private void SpawnEntity(Vector3 position)
    {
        GameObject obj = Instantiate(entityPrefab, position, Quaternion.identity);

        count++;

        spawnedEntities.Add(obj.GetInstanceID(), obj);
    }

    private void RemoveOutsideSimulationDistance()
    {
        List<int> ids = new(spawnedEntities.Keys);

        for (int i = count - 1; i >= 0; i--)
        {
            GameObject obj = spawnedEntities[ids[i]];
            Vector3 delta = obj.transform.position - player.position;
            if (delta.x < -simulationDistance || delta.x > simulationDistance || delta.y < -simulationDistance || delta.y > simulationDistance)
            {
                Destroy(obj);
                spawnedEntities.Remove(ids[i]);
                count--;
            }
        }
    }
}
