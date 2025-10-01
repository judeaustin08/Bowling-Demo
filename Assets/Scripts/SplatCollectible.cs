using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SplatCollectible : Collectible
{
    private MeshRenderer _mr;
    private Collider _col;
    private CharacterController _cc;
    private WanderingAI _ai;
    private DecalProjector _dp;

    private void Awake()
    {
        _mr = GetComponent<MeshRenderer>();
        _col = GetComponent<Collider>();
        _cc = GetComponent<CharacterController>();
        _ai = GetComponent<WanderingAI>();
        _dp = GetComponentInChildren<DecalProjector>();
    }

    private void Start()
    {
        _dp.enabled = false;
    }

    public override void OnGet()
    {
        _mr.enabled = false;
        _col.enabled = false;
        _cc.enabled = false;
        _ai.enabled = false;
        
        _dp.enabled = true;
    }
}