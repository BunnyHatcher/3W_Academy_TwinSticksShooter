using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Clayxels;

namespace ClayxelsExamples{
    public class ProceduralCharacterMotion : MonoBehaviour{
        public Transform rigHip;

        public Transform rigLeftLeg;
        public Transform rigLeftLegOffset;
        public Transform rigLeftFoot;
        public Transform rigLeftFootHome;
        
        public Transform rigRightLeg;
        public Transform rigRightLegOffset;
        public Transform rigRightFoot;
        public Transform rigRightFootHome;

        public Transform followWho = null;
        
        public float stepDistance;
        public float stepDuration;
        public float stepOvershoot;

        public List<Transform> rigBodyParts = new List<Transform>();
        public List<Transform> clayBodyParts = new List<Transform>();

        bool isMoving;
        int limbMoving = 0;
        int frame = 0;
        Vector3 targetPos;
        List<Transform> instances = new List<Transform>();

        void Start(){
            ClayContainer.setUpdateFrameSkip(5);
            this.initRig();
        }

        void Update(){
            // first we move our target rig from the hip
            this.moveRig();

            // then we update some rig objects to do leg movements
            // in this case we do procedural motion,
            // you could have a full skeleton with animation just as well
            this.updateRig();

            // then we simply match clay parts onto rig body parts
            this.updateClay();
        }

        void initRig(){
            // first we match the rig parts onto the clay parts
            for(int i = 0; i < this.rigBodyParts.Count; ++i){
                this.rigBodyParts[i].position = this.clayBodyParts[i].position;
                this.rigBodyParts[i].rotation = this.clayBodyParts[i].rotation;
                this.rigBodyParts[i].localScale = this.clayBodyParts[i].lossyScale;
            }

            // footHome needs to be where the foot is at the first frame
            this.rigLeftFootHome.position = this.rigLeftFoot.position;
            this.rigLeftFootHome.rotation = this.rigLeftFoot.rotation;

            this.rigRightFootHome.position = this.rigRightFoot.position;
            this.rigRightFootHome.rotation = this.rigRightFoot.rotation;

            this.targetPos = this.rigHip.position;

            // here we find all the instances pointing at this rig
            ClayContainer[] instancedClayContainers = GameObject.FindObjectsOfType<ClayContainer>();
            for(int i = 0; i < instancedClayContainers.Length; ++i){
                if(this.clayBodyParts[0].GetComponent<ClayContainer>() == instancedClayContainers[i].getInstanceOf()){
                    this.instances.Add(instancedClayContainers[i].transform);
                }
            }
        }

        void moveRig(){
            // here we move the rig from the hip object, which is the parent of the whole rig
            Vector3 prevTargetPos = this.rigHip.position;

            bool shouldFollow = false;
            if(this.followWho != null){
                if(this.frame == 0){
                    if(UnityEngine.Random.Range(0, 100) > 50){
                        shouldFollow = true;
                    }
                }
            }

            if(shouldFollow == false){
                this.targetPos = this.targetPos + new Vector3(UnityEngine.Random.Range(-1.0f, 1.0f), 0.0f, UnityEngine.Random.Range(-1.0f, 1.0f));
            }
            else{
                this.targetPos = this.followWho.position;
            }

             this.frame = (this.frame + 1) % UnityEngine.Random.Range(5, 100);

            this.rigHip.position = Vector3.Lerp(this.rigHip.position, this.targetPos, 0.001f);
            
            Vector3 delta = this.rigHip.position - prevTargetPos;
            Vector3 lookAtPos = this.rigHip.position + Vector3.Lerp(this.rigHip.forward, delta.normalized, 0.01f).normalized;
            this.rigHip.LookAt(lookAtPos, Vector3.up);

            // then we move the instances along the same path
            for(int i = 0; i < this.instances.Count; ++i){
                Transform instance = this.instances[i];
                instance.position = Vector3.Lerp(instance.position, this.targetPos, 0.001f);
                instance.LookAt(lookAtPos, Vector3.up);
            }
        }

         void updateRig(){
            if(this.limbMoving == 0){
                this.startMoveLegRig(this.rigLeftFoot, this.rigLeftFootHome);
            }
            else{
                this.startMoveLegRig(this.rigRightFoot, this.rigRightFootHome);
            }

            this.aimLegRig(this.rigLeftLeg, this.rigLeftLegOffset, this.rigLeftFoot);
            this.aimLegRig(this.rigRightLeg, this.rigRightLegOffset, this.rigRightFoot);
        }

        void updateClay(){
            for(int i = 0; i < this.clayBodyParts.Count; ++i){
                this.clayBodyParts[i].position = this.rigBodyParts[i].position;
                this.clayBodyParts[i].rotation = this.rigBodyParts[i].rotation;
                this.clayBodyParts[i].localScale = this.rigBodyParts[i].lossyScale;
            }
        }

        void aimLegRig(Transform legRoot, Transform legOffset, Transform foot){
            legRoot.LookAt(foot, this.rigHip.forward);
            Vector3 delta = foot.position - legRoot.position;
            float legLen = delta.magnitude;
            legOffset.position = legRoot.position + (delta * 0.5f);
            legOffset.localPosition = legOffset.localPosition + new Vector3(0.0f, legLen * 0.1f, 0.0f);
            legOffset.localScale = new Vector3(legLen, legLen * 0.3f, 0.2f);
        }

        void startMoveLegRig(Transform foot, Transform footHome){
            if (isMoving) return;

            float distFromHome = Vector3.Distance(foot.position, footHome.position);

            if (distFromHome > stepDistance)
            {
                StartCoroutine(this.moveLeg(foot, footHome));
            }
        }

        float easeInOutCubic(float t) { 
            return t < .5 ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1; 
        }

        IEnumerator moveLeg(Transform foot, Transform footHome){
            isMoving = true;

            Vector3 startPoint = foot.position;
            Quaternion startRot = foot.rotation;

            Quaternion endRot = footHome.rotation;

            Vector3 towardHome = (footHome.position - foot.position);

            float overshootDistance = stepDistance * stepOvershoot;
            Vector3 overshootVector = towardHome * overshootDistance;

            overshootVector = Vector3.ProjectOnPlane(overshootVector, Vector3.up);

            Vector3 endPoint = footHome.position + overshootVector;

            Vector3 centerPoint = (startPoint + endPoint) / 2;

            centerPoint += footHome.up * Vector3.Distance(startPoint, endPoint) / 2f;

            float timeElapsed = 0;
            do
            {
                timeElapsed += Time.deltaTime;
                float normalizedTime = timeElapsed / stepDuration;

                normalizedTime = easeInOutCubic(normalizedTime);

                foot.position =
                    Vector3.Lerp(
                        Vector3.Lerp(startPoint, centerPoint, normalizedTime),
                        Vector3.Lerp(centerPoint, endPoint, normalizedTime),
                        normalizedTime
                    );

                foot.rotation = Quaternion.Slerp(startRot, endRot, normalizedTime);

                yield return null;
            }
            while (timeElapsed < stepDuration);

            isMoving = false;
            if(this.limbMoving == 0){
                this.limbMoving = 1;
            }
            else{
                this.limbMoving = 0;
            }
        }
    }
}
