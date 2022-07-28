using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrabsBeachCrabs : MonoBehaviour{
	List<Vector3> crabsDefaultPos = new List<Vector3>();
	List<Vector3> crabsDefaultRot = new List<Vector3>();

    // Start is called before the first frame update
    void Start(){
        for(int i = 0; i < this.transform.childCount; ++i){
        	this.crabsDefaultPos.Add(this.transform.GetChild(i).localPosition);
        	this.crabsDefaultRot.Add(this.transform.GetChild(i).localEulerAngles);
        }
    }

    // Update is called once per frame
    void FixedUpdate(){
    	Vector3 vec = new Vector3(0.0f, 0.0f, 0.0f);
    	
     	for(int i = 0; i < this.transform.childCount; ++i){
     		if(UnityEngine.Random.Range(0, 100) > 80){
     			Vector3 p = this.crabsDefaultPos[i];
     			p.x += UnityEngine.Random.Range(-0.0001f, 0.0001f);
     			p.z += UnityEngine.Random.Range(-0.0001f, 0.0001f);
     			this.crabsDefaultPos[i] = p;

     			vec.x = 0.0f;
     			vec.y = Mathf.Sin(Time.time * 50 + i) * 0.5f;
     			vec.z = 0.0f;
        		this.transform.GetChild(i).localPosition = this.crabsDefaultPos[i] + vec;
        	}
			
			if(UnityEngine.Random.Range(0, 100) > 70){
				Vector3 r = this.crabsDefaultRot[i];
				r.y += UnityEngine.Random.Range(-10.5f, 10.5f);
				this.crabsDefaultRot[i] = r;

				vec.y = 0.0f;
	        	vec.x = Mathf.Sin(Time.time * 20 + i) * 5.0f;
	        	vec.z = 0.0f;
	        	this.transform.GetChild(i).localEulerAngles = this.crabsDefaultRot[i] + vec;
	        }
        }  
    }
}
