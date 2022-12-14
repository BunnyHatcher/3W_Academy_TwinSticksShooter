using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] IntVariable _playerStartHP;
    [SerializeField] IntVariable _playerCurrentHP;

    [SerializeField] ParticleSystem hitEffect;
    [SerializeField] ParticleSystem deathEffect;

    [SerializeField] GameObject dragon;

    private void Awake()
    {
        _playerCurrentHP.Value = _playerStartHP.Value;
        Debug.Log(_playerCurrentHP.Value);
    }

    public void GameOver()
    {
        //Destroy(gameObject);
        deathEffect.Play();
        dragon.SetActive(false);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlayerHit()
    {
        _playerCurrentHP.Value -= 1;
        hitEffect.Play();
        Debug.Log(_playerCurrentHP.Value);
        if (_playerCurrentHP.Value <= 0)
        {
            Debug.Log("Player Death");
            GameOver();
        }
    }

    public void PlayerHealed()
    {
        _playerCurrentHP.Value += 1;
    }
}
