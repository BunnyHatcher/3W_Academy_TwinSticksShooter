using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyWalker : MonoBehaviour
{
    [SerializeField] Transform _playerTransform;
    private Rigidbody _rigidbody;
    public float movementSpeed = 10f;
    public float rotationSpeed = 50f;

    // Start is called before the first frame update

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    void Start()
    {
        
    }

    private void FixedUpdate()
    {
        Vector3 directionToPlayer = _playerTransform.position - transform.position;

        Quaternion rotationToPlayer = Quaternion.LookRotation(directionToPlayer);

        //Make enemy rotate not instantaneously to make rotation more natural
        Quaternion rotation = Quaternion.RotateTowards(transform.rotation, rotationToPlayer, rotationSpeed * Time.fixedDeltaTime);


        _rigidbody.MoveRotation(rotation);
        _rigidbody.velocity = transform.forward * movementSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
