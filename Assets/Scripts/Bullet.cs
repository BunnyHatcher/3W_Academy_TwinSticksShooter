using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    Transform _transform;
    private Rigidbody _rigidbody;
    float _bulletSpeed;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _transform = GetComponent<Transform>();

    }

    // Start is called before the first frame update
    void Start()
    {
      // _rigidbody.velocity = transform.forward * _bulletSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void FixedUpdate()
    {
       Vector3 velocity = transform.forward * _bulletSpeed;
       Vector3 movementStep = velocity * Time.fixedDeltaTime;
       Vector3 newPos = transform.position + movementStep;
        _rigidbody.MovePosition(newPos) ;
    }

    public void Shoot(float speed) 
    {
        _bulletSpeed = speed;
    }
}
