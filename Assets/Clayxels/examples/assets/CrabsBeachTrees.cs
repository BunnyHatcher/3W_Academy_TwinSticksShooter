using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrabsBeachTrees : MonoBehaviour{
	List<Transform> leafs = new List<Transform>();
	List<Vector3> leafsDefaultRot = new List<Vector3>();
	List<Transform> trees = new List<Transform>();
	List<Vector3> treesDefaultRot = new List<Vector3>();

    // Start is called before the first frame update
    void Start(){
        Clayxels.ClayContainer[] containers = this.GetComponentsInChildren<Clayxels.ClayContainer>();
        for(int i = 0; i < containers.Length; ++i){
        	Transform trn = containers[i].transform;
        	this.leafs.Add(trn);
        	this.leafsDefaultRot.Add(trn.localEulerAngles);
        }

        for(int i = 0; i < this.transform.childCount; ++i){
        	this.trees.Add(this.transform.GetChild(i));
        	this.treesDefaultRot.Add(this.transform.GetChild(i).localEulerAngles);
        }
    }

    // Update is called once per frame
    void Update(){
    	Vector3 vec = new Vector3(0.0f, 0.0f, 0.0f);

        for(int i = 0; i < this.leafs.Count; ++i){
        	vec.x = Mathf.Sin(Time.time * 5 + i) * 3.5f;

        	this.leafs[i].localEulerAngles = this.leafsDefaultRot[i] + vec;
        }

        for(int i = 0; i < this.trees.Count; ++i){
        	vec.x = Mathf.Sin(Time.time * 5 + i) * 1.5f;

        	this.trees[i].localEulerAngles = this.treesDefaultRot[i] + vec;
        }
    }
}
