using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrabsBeachCamera : MonoBehaviour{
    // Start is called before the first frame update
    void Start(){
        
    }

    // Update is called once per frame
    void FixedUpdate(){
        Vector3 p = this.transform.localPosition;

        float s = Mathf.Sin(Time.time * 0.1f);

        p.x += s * 0.05f;
        this.transform.localPosition = p;

        this.GetComponent<Camera>().fieldOfView -= s * 0.01f;
    }
}
