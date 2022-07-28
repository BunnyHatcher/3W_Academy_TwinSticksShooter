using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*!\brief A simple spring used for auto-rigging.
*/
[DefaultExecutionOrder(-999)]
public class ClaySpring : MonoBehaviour{
    /*!\brief Use this to change the local position of the object.*/
    public Vector3 inputPos;

    /*!\brief Use this to change the local euler angles of the object.*/
    public Quaternion inputRot;

    /*!\brief Use values from 0.0 to 1.0 to selectively nullify the effect of the spring on some position axis.*/
    public Vector3 multiplierPos = Vector3.one;

    /*!\brief Use values from 0.0 to 1.0 to selectively nullify the effect of the spring on some rotation axis.*/
    public float multiplierRot = 1.0f;

    public float mass = 500.0f;
    public float damping = 10.0f;
    
    Vector3 targetPos;
    Vector3 targetRot;
    Vector3 currentVelocityPos;
    Vector3 currentVelocityRot;
    Vector3 currentPos;
    Quaternion currentRotLocal;
    Vector3 accelerationPos;
    Vector3 accelerationRot;
    Vector3 currentPosLocal;
    Quaternion offsetRot;
    Quaternion parentRestRot;
    Vector3 aimDir;
    Quaternion newAimRot;
    Quaternion newOffsetRot;
    Quaternion newRotLocal;
    
    void Start(){
        this.currentPos = this.transform.position;
        this.inputPos = this.transform.localPosition;

        this.inputRot = this.transform.localRotation;
        this.parentRestRot = this.transform.parent.rotation;
        Vector3 restAimDir = this.transform.position - (this.transform.parent.position + this.transform.parent.up);
        Quaternion aimRot = Quaternion.LookRotation(restAimDir, this.transform.parent.forward);
        this.offsetRot = Quaternion.Inverse(aimRot);
    }

    void Update(){
        // compute springy position
        float dampingFactor = Mathf.Max(0, 1 - this.damping * Time.deltaTime);

    	this.targetPos = this.transform.parent.localToWorldMatrix.MultiplyPoint(this.inputPos);
    	this.accelerationPos = (this.targetPos - this.currentPos) * this.mass * Time.deltaTime;
        this.currentVelocityPos = (this.currentVelocityPos * dampingFactor) + this.accelerationPos;
        this.currentPos += this.currentVelocityPos * Time.deltaTime;

        this.currentPosLocal = this.transform.parent.worldToLocalMatrix.MultiplyPoint(this.currentPos);
        
        this.currentPosLocal.x = Mathf.Lerp(this.inputPos.x, this.currentPosLocal.x, this.multiplierPos.x);
        this.currentPosLocal.y = Mathf.Lerp(this.inputPos.y, this.currentPosLocal.y, this.multiplierPos.y);
        this.currentPosLocal.z = Mathf.Lerp(this.inputPos.z, this.currentPosLocal.z, this.multiplierPos.z);

        this.transform.localPosition = this.currentPosLocal;

        // compute springy rotation using position aiming
    	this.aimDir = this.currentPos - (this.transform.parent.position + this.transform.parent.up);
    	this.newAimRot = Quaternion.LookRotation(this.aimDir, this.transform.parent.forward);
        
        this.newOffsetRot = this.offsetRot * (this.parentRestRot * this.inputRot);
        this.newRotLocal = Quaternion.Inverse(this.transform.parent.rotation) * (this.newAimRot * this.newOffsetRot);

        this.currentRotLocal = Quaternion.Slerp(this.inputRot, this.newRotLocal, this.multiplierRot);
        this.transform.localRotation = this.currentRotLocal;
    }
}
