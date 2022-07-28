using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestClayxelMeshCollider : MonoBehaviour
{
	Clayxels.ClayContainer clayxel;
    int frameSkip = 0;

    void Start(){
        // Here we initialize the clay container and we add the relevant components

        this.clayxel = this.GetComponent<Clayxels.ClayContainer>();
        this.clayxel.init();

        this.gameObject.AddComponent<MeshFilter>();
        this.gameObject.AddComponent<MeshRenderer>();
        this.gameObject.AddComponent<MeshCollider>();

        this.gameObject.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Clayxels/ClayxelBuiltInMeshShader"));

        // we'll disable the container to only render the mesh and we'll update the clay manually
        this.clayxel.enabled = false;
    }

    void OnDestroy(){
        // lets make sure we delete the mesh buffers allocated
        Clayxels.ClayContainer.clearFrozenMeshBuffers();
    }

    void Update(){
        if(this.frameSkip == 0){
            // here we just randomize the rotation of a few clay objects, badly
            GameObject[] pivots = GameObject.FindGameObjectsWithTag("Player");
            for(int i = 0; i < pivots.Length; ++i){
                if(UnityEngine.Random.Range(0, 100) > 50){
                    continue;
                }

                Transform trn = pivots[i].transform;
                Vector3 newRot = new Vector3(0.0f, 0.0f, Mathf.Lerp(trn.localEulerAngles.z, UnityEngine.Random.Range(-10.0f, 10.0f), 0.1f));
                if(newRot.z < -30.0f){
                    newRot.z = -30.0f;
                }
                else if(newRot.z > 30.0f){
                    newRot.z = 30.0f;
                }

                trn.localEulerAngles = newRot;
            }
            
            // inform the container we updated all clay objects and recompute the clay
            this.clayxel.forceUpdateAllSolids();
            this.clayxel.computeClay();

            // we generate a mesh at a low level of detail, fast and good for collisions
            int levelOfDetail = 0;
            bool vertexColors = false;
            bool normals = false;
            bool freeMemory = false;// we don't want to reallocate mesh buffers on each frame
            float smoothNormalAngle = 0.0f;
            Mesh mesh = this.clayxel.generateMesh(levelOfDetail, vertexColors, normals, smoothNormalAngle, freeMemory);
            
            // finally we visualize the mesh and set the collider
            if(this.gameObject.GetComponent<MeshFilter>().sharedMesh != null){
                DestroyImmediate(this.gameObject.GetComponent<MeshFilter>().sharedMesh);
                DestroyImmediate(this.gameObject.GetComponent<MeshCollider>().sharedMesh);
            }

            this.gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            this.gameObject.GetComponent<MeshCollider>().sharedMesh = mesh;
        }

        this.frameSkip = (this.frameSkip + 1) % 10;
    }
}
