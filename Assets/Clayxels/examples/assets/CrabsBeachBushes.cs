using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrabsBeachBushes : MonoBehaviour{
	List<Vector3> bushesDefaultRot = new List<Vector3>();
	List<Vector3> bushesDefaultSize = new List<Vector3>();

    // Start is called before the first frame update
    void Start(){
        for(int i = 0; i < this.transform.childCount; ++i){
        	this.bushesDefaultSize.Add(this.transform.GetChild(i).localScale);
        	this.bushesDefaultRot.Add(this.transform.GetChild(i).localEulerAngles);
        }
    }

    // Update is called once per frame
    void FixedUpdate(){
    	Vector3 vec = new Vector3(0.0f, 0.0f, 0.0f);

        for(int i = 0; i < this.transform.childCount; ++i){
        	vec.x = 0.0f;
        	vec.y = Mathf.Sin(Time.time * 2 + i) * 0.05f;
        	vec.z = 0.0f;

        	this.transform.GetChild(i).localScale = this.bushesDefaultSize[i] + vec;

        	vec.x = Mathf.Sin(Time.time * 2 + i) * 1.0f;
        	vec.y = 0.0f;
        	vec.z = 0.0f;

        	this.transform.GetChild(i).localEulerAngles = this.bushesDefaultRot[i] + vec;
        }
    }
}
