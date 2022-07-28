using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrabsBeachWater : MonoBehaviour{
	public List<Transform> waves = new List<Transform>();
	public List<Transform> water = new List<Transform>();

	Vector3 containerDefaultPos = new Vector3();
	List<Vector3> wavesDefaultPos = new List<Vector3>();
	List<Vector3> wavesDefaultSize = new List<Vector3>();
	List<Vector3> waterDefaultPos = new List<Vector3>();

    // Start is called before the first frame update
    void Start(){
    	this.containerDefaultPos = this.transform.localPosition;

        for(int i = 0; i < this.waves.Count; ++i){
        	this.wavesDefaultPos.Add(this.waves[i].localPosition);
        	this.wavesDefaultSize.Add(this.waves[i].localScale);
        }

        for(int i = 0; i < this.water.Count; ++i){
        	this.waterDefaultPos.Add(this.water[i].localPosition);
        }
    }

    // Update is called once per frame
    void FixedUpdate(){
    	Vector3 vec = new Vector3(0.0f, 0.0f, 0.0f);
    	vec.y = Mathf.Sin(Time.time) * 0.5f;
    	this.transform.localPosition = this.containerDefaultPos + vec;

        for(int i = 0; i < this.waves.Count; ++i){
        	vec.y =  Mathf.Sin(Time.time + i) * 0.2f;

        	this.waves[i].localPosition = this.wavesDefaultPos[i] + vec;

        	this.waves[i].localScale = this.wavesDefaultSize[i] + vec;
    	}

    	for(int i = 0; i < this.water.Count; ++i){
    		vec.y =  Mathf.Sin(Time.time + i) * 0.1f;

    		this.water[i].localPosition = this.waterDefaultPos[i] + vec;
    	}
    }
}
