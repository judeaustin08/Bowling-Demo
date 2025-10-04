using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.Universal;

public class SplatCollectible : Collectible
{
    [SerializeField] private AudioClip[] sounds;
    [SerializeField] private AudioMixerGroup group;

    [SerializeField] private GameObject model;
    private Collider _col;
    private CharacterController _cc;
    private WanderingAI _ai;
    private DecalProjector _dp;
    private AudioSource _as;

    private void Awake()
    {
        _col = GetComponent<Collider>();
        _cc = GetComponent<CharacterController>();
        _ai = GetComponent<WanderingAI>();
        _dp = GetComponentInChildren<DecalProjector>();
        _as = GetComponent<AudioSource>();
    }

    private void Start()
    {
        _dp.enabled = false;
    }

    public override void OnGet()
    {
        model.SetActive(false);
        _col.enabled = false;
        _cc.enabled = false;
        _ai.enabled = false;

        _dp.enabled = true;

        AudioClip sound = sounds[Random.Range(0, sounds.Length)];
        _as.outputAudioMixerGroup = group;
        _as.PlayOneShot(sound);
    }
}