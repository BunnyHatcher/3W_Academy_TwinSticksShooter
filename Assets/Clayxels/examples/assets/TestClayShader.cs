using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Clayxels;

namespace ClayxelsExamples{
	public class TestClayShader : MonoBehaviour{
		int counter = 0;
		List<Vector3> startPos = new List<Vector3>();
		float waveAnimValue = 0.0f;

		void Start(){
			// when the script starts we store the initial positions of some clayObjects to animate them later with a local offset
			for(int i = 0; i < this.transform.childCount; ++i){
				this.startPos.Add(this.transform.GetChild(i).localPosition);
			}
		}

	    void Update()
	    {
	    	if(this.counter == 0){	
	    		// here we update the shader parameter that randomizes the finger prints
		    	ClayContainer clayContainer = this.GetComponent<ClayContainer>();
				
		        // from here we just move clayObjects around to make some fun anims
		        int randomClayObj = UnityEngine.Random.Range(0, this.startPos.Count);
		        this.transform.GetChild(randomClayObj).transform.localPosition = this.startPos[randomClayObj] + 
		        	new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), 
		        		UnityEngine.Random.Range(-0.1f, 0.1f), 
		        		UnityEngine.Random.Range(-0.1f, 0.1f));

		        GameObject.Find("hairAnim").transform.localEulerAngles = 
		        	new Vector3(UnityEngine.Random.Range(0.0f, 360.0f), 2.2f, -7.2f);

		         GameObject.Find("clayBoard").transform.localEulerAngles = 
		        	new Vector3(UnityEngine.Random.Range(-10.0f, 10.0f), 0.0f, 0.0f);

		        GameObject.Find("waveAnim").transform.localEulerAngles = new Vector3(this.waveAnimValue, 2.2f, 2.3f);
		        this.waveAnimValue -= 30.0f;
		    }

		    this.counter = (this.counter + 1) % 60;
	    }
	}
}
