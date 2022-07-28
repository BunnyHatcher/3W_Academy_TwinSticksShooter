using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using Clayxels;

namespace Clayxels{

	/*!\brief ClayObject are nested inside a ClayContainer to form sculptures.
		A ClayObject can spawn one or multiple Clayxels.Solid objects to form more complex structures 
		like splines or duplicated offsetted patterns.
	*/
	[ExecuteInEditMode, DefaultExecutionOrder(-999)]
	public class ClayObject : MonoBehaviour{
		public enum ClayObjectMode{
			single,
			offset,
			spline,
			clayGroup
		}

		/*!\brief Set this clayObject to any primitive currently defined inside claySDF.compute (box, sphere, etc.). */
		public void setPrimitiveType(int primType){
			this.primitiveType = primType;
		}

		/*!\brief Get the current primitive type with an id as defined inside claySDF.compute. */
		public int getPrimitiveType(){
			return this.primitiveType;
		}

		/*!\brief Set how this clayObject will blend with the previous one in the stack, 
			value goes from -100.0 to 100.0. 
			Negative values are for negative blend, positive values are for positive blends. */
		public void setBlend(float blend){
			this.blend = blend * 0.01f;
		}

		/*!\brief Get the current blend value, result will range from -100.0 to 100.0 depending if it's a negative or positive blend. */
		public float getBlend(){
			return this.blend;
		}

		/*!\brief Set the color of this clayObject. */
		public void setColor(Color color){
			this.color = color;
		}

		/*!\brief Get the color of this clayObject. */
		public Color getColor(){
			return this.color;
		}

		/*!\brief */
		public float getThickness(){
			return this.attrs2.w;
		}

		/*!\brief */
		public void setThickness(float value){
			this.attrs2.w = value;
		}

		/*!\brief Is this clayObject only leaving color on other primitives? */
		public bool getIsPainter(){
			int bitPos = 0;
			int bitfield = (int)this.attrs.w;

			if((bitfield & (1 << bitPos)) != 0){
				return true;
			}

			return false;
		}

		/*!\brief Set if this clayObject should only leave color on other primitives. */
		public void setIsPainter(bool state){
			int bitPos = 0;
			int bitfield = (int)this.attrs.w;

			if(!state){
				bitfield = bitfield &~(1 << bitPos);
			}
			else{
				bitfield |= (1 << bitPos);
			}
			
			this.attrs.w = (float)bitfield;
		}

		/*!\brief Is this clayObject being mirrored on the X axis? */
		public bool getMirror(){
			int bitPos = 1;
			int bitfield = (int)this.attrs.w;

			if((bitfield & (1 << bitPos)) != 0){
				return true;
			}

			return false;
		}

		/*!\brief Set if this clayObject should mirror on the X axis. */
		public void setMirror(bool state){
			int bitPos = 1;
			int bitfield = (int)this.attrs.w;

			if(!state){
				bitfield = bitfield &~(1 << bitPos);
			}
			else{
				bitfield |= (1 << bitPos);
			}
			
			this.attrs.w = (float)bitfield;
		}

		/*!\brief Set an attribute on this primitive type as defined inside claySDF.compute. */
		public void setAttribute(string attrName, float value){
			List<string[]> parameters = ClayContainer.getSolidsCatalogueParameters(this.primitiveType);

			for(int i = 0; i < parameters.Count; ++i){
	 			string[] parameterValues = parameters[i];
	 			string attr = parameterValues[0];
	 			string name = parameterValues[1];

	 			if(name == attrName){
	 				float valueDemultiplied = value * 0.01f;

					if(attr == "x"){
						this.attrs.x = valueDemultiplied;
					}
					else if(attr == "y"){
						this.attrs.y = valueDemultiplied;
					}
					else if(attr == "z"){
						this.attrs.z = valueDemultiplied;
					}
					else if(attr == "x2"){
						this.attrs2.x = valueDemultiplied;
					}
					else if(attr == "y2"){
						this.attrs2.y = valueDemultiplied;
					}
					else if(attr == "z2"){
						this.attrs2.z = valueDemultiplied;
					}
					
					// else if(attr.StartsWith("w")){
					// 	int bitfield = (int)this.attrs.w;

					// 	byte bitPos = 0;
					// 	if(attr == "w0"){
					// 		bitPos = 0;
					// 	}
					// 	else if(attr == "w1"){
					// 		bitPos = 1;
					// 	}
					// 	else if(attr == "w2"){
					// 		bitPos = 2;
					// 	}
					// 	else if(attr == "w3"){
					// 		bitPos = 3;
					// 	}
					// 	else if(attr == "w4"){
					// 		bitPos = 4;
					// 	}

					// 	if(value == 0.0f){
					// 		bitfield = bitfield &~(1 << bitPos);
					// 	}
					// 	else{
					// 		bitfield |= (1 << bitPos);
					// 	}
						
					// 	this.attrs.w = (float)bitfield;
					// }

					break;
				}
			}
		}

		/*!\brief Get an attribute on this primitive type as defined inside claySDF.compute. */
		public float getAttribute(string attrName){
			List<string[]> parameters = ClayContainer.getSolidsCatalogueParameters(this.primitiveType);

			float outVal = 0.0f;

			for(int i = 0; i < parameters.Count; ++i){
	 			string[] parameterValues = parameters[i];
	 			string attr = parameterValues[0];
	 			string name = parameterValues[1];

	 			if(name == attrName){
					if(attr == "x"){
						outVal = this.attrs.x;
					}
					else if(attr == "y"){
						outVal = this.attrs.y;
					}
					else if(attr == "z"){
						outVal = this.attrs.z;
					}
					else if(attr == "x2"){
						outVal = this.attrs2.x;
					}
					else if(attr == "y2"){
						outVal = this.attrs2.y;
					}
					else if(attr == "z2"){
						outVal = this.attrs2.z;
					}

					// else if(attr.StartsWith("w")){
					// 	int bitfield = (int)this.attrs.w;

					// 	byte bitPos = 0;
					// 	if(attr == "w0"){
					// 		bitPos = 0;
					// 	}
					// 	else if(attr == "w1"){
					// 		bitPos = 1;
					// 	}
					// 	else if(attr == "w2"){
					// 		bitPos = 2;
					// 	}
					// 	else if(attr == "w3"){
					// 		bitPos = 3;
					// 	}
					// 	else if(attr == "w4"){
					// 		bitPos = 4;
					// 	}

					// 	if((bitfield & (1 << bitPos)) != 0){
					// 		outVal = 1.0f;
					// 	}
					// 	else{
					// 		outVal = 0.0f;
					// 	}
					// }

					break;
				}
			}

			return outVal * 100.0f;
		}

		/*!\brief Force this clayObject to update its internal solids list and notify the parent container. */
		public void forceUpdate(){
			this.transform.hasChanged = true;

			this.updateSolids(true);
		}

		/*!\brief Get a specific solid inside this container.
			Depending if this clayObject was set to offset or spline mode, there might be multiple solids inside this clayObject. */
		public Solid getSolid(int id){
			return this.solids[id];
		}

		/*!\brief How many solids are contained in this clayObject. 
			Depending if this clayObject was set to offset or spline mode, there might be multiple solids inside this clayObject. */
		public int getNumSolids(){
			return this.solids.Count;
		}

		/*!\brief If this clayObject was set to offset mode, 
			this will get you the reference GameObject used as offset reference between solids. */
		public GameObject getOffsetter(){
			return this.offsetter;
		}

		/*!\brief If this clayObject was set to offset mode, 
			this will set how many solids are being spawned along the offset. */
		public void setOffsetNum(int num){
			this.numSolids = num;
			this.init();
			this.forceUpdate();

			this.getClayContainer().scheduleClayObjectsScan();
		}

		/*!\brief If this clayObject was set to spline mode, 
			this will get how many solids are being distributed among each section of the spline. 
			Note: this is not the total number of solids. */
		public int getSplineSubdiv(){
			return this.splineSubdiv;
		}

		/*!\brief If this clayObject was set to spline mode, 
			this will set how many solids will be distributed among each section of the spline. 
			Note: this is not the total number of solids. */
		public void setSplineSubdiv(int num){
			this.splineSubdiv = num;

			if(this.splineSubdiv < 1){
				this.splineSubdiv = 1;
			}

			this.updateSplineSubdiv();

			this.init();
			this.forceUpdate();

			this.getClayContainer().scheduleClayObjectsScan();
		}

		/*!\brief Get the list of gameObjects used to control the spline if this was set to spline mode. */
		public List<GameObject> getSplinePoints(){
			return this.splinePoints;
		}

		/*!\brief Add a spline control point at the end of the list. */
		public GameObject addSplineControlPoint(){
			GameObject prevLastPoint = this.splinePoints[this.splinePoints.Count - 3];
			GameObject lastPoint = this.splinePoints[this.splinePoints.Count - 1];

			Vector3 delta = (lastPoint.transform.position - prevLastPoint.transform.position);
			delta.Normalize();

			GameObject offsetObj = new GameObject("splinePnt" + (this.splinePoints.Count-1));
			offsetObj.transform.parent = this.transform;
			offsetObj.transform.position = lastPoint.transform.position + delta;
			offsetObj.transform.localEulerAngles = new Vector3(0.0f, 0.0f, 0.0f);
			offsetObj.transform.localScale = lastPoint.transform.localScale;

			this.splinePoints[this.splinePoints.Count - 1] = offsetObj;

			this.splinePoints.Add(offsetObj);

			this.updateSplineSubdiv();

			this.init();

			this.forceUpdate();

			this.getClayContainer().scheduleClayObjectsScan();

			return offsetObj;
		}

		/*!\brief Remove the last spline control point in the list. */
		public void removeLastSplineControlPoint(){
			if(this.splinePoints.Count > 4){
				GameObject controlPoint = this.splinePoints[this.splinePoints.Count - 1];
				DestroyImmediate(controlPoint);

				this.splinePoints.RemoveAt(this.splinePoints.Count - 1);

				this.splinePoints[this.splinePoints.Count - 1] = this.splinePoints[this.splinePoints.Count - 2];

				this.updateSplineSubdiv();

				this.init();

				this.forceUpdate();

				this.getClayContainer().scheduleClayObjectsScan();
			}
		}

		/*!\brief Which mode is currently set for this clayObject. */
		public ClayObjectMode getMode(){
			return this.mode;
		}

		/*!\brief Single mode: just one solid (box, sphere, etc.) that you can move around, scale and rotate like any other GameObject.
			Offset mode: 	multiple solids are created and offsetted one from the other using an offsetter reference object.
			Spline mode: multiple solids are created and distributed along a spline. */
		public void setMode(ClayObjectMode mode){
			if(mode == this.mode){
				return;
			}

			if(this.mode == ClayObjectMode.clayGroup){
				this.attrs.w = 0.0f;
				this.solids.Clear();
				ClayContainer container = this.getClayContainer();
				if(container != null){
					container.scheduleClayObjectsScan();
				}
			}

			this.mode = mode;

			if(this.mode == ClayObjectMode.single){
				this.numSolids = 1;
			}
			else if(this.mode == ClayObjectMode.offset){
				if(this.offsetter == null){
					GameObject offsetObj = new GameObject("offsetter");
					offsetObj.transform.parent = this.transform;
					offsetObj.transform.localPosition = new Vector3(0.0f, 0.5f, 0.0f);
					offsetObj.transform.localEulerAngles = new Vector3(0.0f, 30.0f, 0.0f);
					offsetObj.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
					
					this.offsetter = offsetObj;
				}

				this.numSolids = 3;
			}
			else if(this.mode == ClayObjectMode.spline){
				if(this.splinePoints.Count == 0){
					GameObject offsetObj = new GameObject("splinePnt1");
					offsetObj.transform.parent = this.transform;
					offsetObj.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
					offsetObj.transform.localEulerAngles = new Vector3(0.0f, 0.0f, 0.0f);
					offsetObj.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
					this.splinePoints.Add(offsetObj);
					this.splinePoints.Add(offsetObj);

					offsetObj = new GameObject("splinePnt2");
					offsetObj.transform.parent = this.transform;
					offsetObj.transform.localPosition = new Vector3(2.0f, 1.0f, 0.0f);
					offsetObj.transform.localEulerAngles = new Vector3(0.0f, 0.0f, 0.0f);
					offsetObj.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
					this.splinePoints.Add(offsetObj);

					offsetObj = new GameObject("splinePnt3");
					offsetObj.transform.parent = this.transform;
					offsetObj.transform.localPosition = new Vector3(3.0f, 0.0f, 0.0f);
					offsetObj.transform.localEulerAngles = new Vector3(0.0f, 0.0f, 0.0f);
					offsetObj.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
					this.splinePoints.Add(offsetObj);

					offsetObj = new GameObject("splinePnt4");
					offsetObj.transform.parent = this.transform;
					offsetObj.transform.localPosition = new Vector3(4.0f, 0.0f, 0.0f);
					offsetObj.transform.localEulerAngles = new Vector3(0.0f, 0.0f, 0.0f);
					offsetObj.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
					this.splinePoints.Add(offsetObj);
					this.splinePoints.Add(offsetObj);
				}

				this.updateSplineSubdiv();
			}
			else if(this.mode == ClayObjectMode.clayGroup){
				this.numSolids = 1;
			}

			this.init();

			this.forceUpdate();

			this.getClayContainer().scheduleClayObjectsScan();
		}

		/*!\brief Get the parent clayContainer for this clayObject. */
		public ClayContainer getClayContainer(){
			if(this.clayxelContainerRef == null){
				if(this.invalidated){
					return null;
				}

				this.init();

				if(this.clayxelContainerRef == null){
					return null;
				}
			}

			return (ClayContainer)this.clayxelContainerRef.Target;
		}

		/*!\brief Set the parent clayContainer for this clayObject. */
		public void setClayxelContainer(ClayContainer container){
			ClayContainer oldContainer = this.getClayContainer();
			
			this.clayxelContainerRef = new WeakReference(container);

			if(oldContainer != null){
				oldContainer.scheduleClayObjectsScan();
			}

			container.scheduleClayObjectsScan();
		}

		/*!\brief Duplicate this clayObject and mirror it on the X axis. */
		public void mirrorDuplicate(){
			ClayObject mirrorClayObj = null;
			
			ClayObject[] clayObjs = this.getClayContainer().GetComponentsInChildren<ClayObject>();
			for(int i = 0; i < clayObjs.Length; ++i){
				ClayObject clayObj = clayObjs[i];
				if(clayObj.name == "mirror_" + this.name){
					if(clayObj.blend == this.blend && clayObj.attrs == this.attrs && clayObj.attrs2 == this.attrs2){
						mirrorClayObj = clayObj;
						break;
					}
				}
			}

			if(mirrorClayObj == null){
				mirrorClayObj = Instantiate(this.gameObject, this.transform.parent).GetComponent<ClayObject>();
				mirrorClayObj.name = "mirror_" + this.name;
			}

			Transform containerTrn = this.getClayContainer().transform;
			
			Plane mirrorPlane = new Plane(containerTrn.right, containerTrn.position);
			
        	Transform[] sourceChildren = this.GetComponentsInChildren<Transform>();
        	Transform[] mirrorChildren = mirrorClayObj.GetComponentsInChildren<Transform>();

        	for(int i = 0; i < sourceChildren.Length; ++i){
        		GameObject clayObjChild = sourceChildren[i].gameObject;
        		GameObject mirrorClayObjChild = mirrorChildren[i].gameObject;

        		Vector3 pointOnPlane = mirrorPlane.ClosestPointOnPlane(clayObjChild.transform.position);
        		float distanceToPlane = mirrorPlane.GetDistanceToPoint(clayObjChild.transform.position);
        		Vector3 mirrorPos = pointOnPlane - mirrorPlane.normal * distanceToPlane;

        		Transform currentParent = mirrorClayObjChild.transform.parent;
        		mirrorClayObjChild.transform.parent = containerTrn;
        		mirrorClayObjChild.transform.position = mirrorPos;
        		mirrorClayObjChild.transform.rotation = Quaternion.LookRotation(
        			Vector3.Reflect(clayObjChild.transform.rotation * -Vector3.forward, mirrorPlane.normal), Vector3.Reflect(clayObjChild.transform.rotation * Vector3.up, mirrorPlane.normal));
        		mirrorClayObjChild.transform.localScale = clayObjChild.transform.localScale;
        		mirrorClayObjChild.transform.parent = currentParent;
        		mirrorClayObjChild.transform.localScale = clayObjChild.transform.localScale;
        	}

			this.getClayContainer().clayObjectUpdated(mirrorClayObj);
		}

		// end of public interface //

		// properties
		public float blend = 0.0f;
		public Color color = new Color(1.0f, 0.8f, 0.25f, 1.0f);
		public Vector4 attrs = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
		public Vector4 attrs2 = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
		public int primitiveType = 0;
		public ClayObjectMode mode = ClayObjectMode.single;
		public GameObject offsetter = null;
		public List<GameObject> splinePoints = new List<GameObject>();
		public int clayObjectId = 0;
		public WeakReference clayxelContainerRef = null;

		[SerializeField] int numSolids = 1;
		[SerializeField] int splineSubdiv = 3;

		List<Solid> solids = new List<Solid>();
		bool invalidated = false;
		Color gizmoColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
		int groupClayObjId = -1;

		void Awake(){
			ClayContainer container = this.getClayContainer();
			if(container != null){
				container.scheduleClayObjectsScan();
			}
		}

		void OnDisable(){
			ClayContainer container = this.getClayContainer();
			if(container != null){
				if(!container.isMemoryOptimized()){
					container.scheduleClayObjectsScan();
				}
			}
		}

		void OnEnable(){
			ClayContainer container = this.getClayContainer();
			if(container != null){
				if(!container.isMemoryOptimized()){
					container.scheduleClayObjectsScan();
				}
			}
		}

		void Update(){
			this.updateSolids(true);
			
			#if UNITY_EDITOR
				if(!Application.isPlaying){
					ClayContainer parentContainer = this.GetComponentInParent<ClayContainer>();
					
					if(parentContainer != this.getClayContainer()){
						ClayContainer oldContainer = this.getClayContainer();
						if(oldContainer != null){
							oldContainer.scheduleClayObjectsScan();
						}

						this.init();
					}
				}
			#endif
		}

		/*!\brief for internal use */
		public int _getGroupClayObjectId(){
			return this.groupClayObjId;
		}

		public Solid _getGroupEndSolid(){
			Solid solid = null;
			if(this.solids.Count == 2){
				solid = this.solids[1];
			}
			else{
				solid = new Solid();
				this.solids.Add(solid);
			}
			
			int bitfield = 0;
			byte bitPos = 3;
			bitfield |= (1 << bitPos);
			solid.attrs.w = (float)bitfield;
			
			return solid;
		}

		public void _setGroupEnd(int groupClayObjId){
			this.groupClayObjId = groupClayObjId;
		}

		public bool _isGroupEnd(){
			if(this.groupClayObjId > -1){
				return true;
			}

			return false;
		}

		public bool isValid(){
			return !this.invalidated;
		}

		/*!\brief for internal use */
		public void init(){
			this.solids.Clear();
			this.changeNumSolids(this.numSolids);
			this.transform.hasChanged = true;

			if(this.mode == ClayObjectMode.clayGroup){
				int bitfield = 0;
				byte bitPos = 2;
				bitfield |= (1 << bitPos);
				this.solids[0].attrs.w = (float)bitfield;

				this.attrs.w = (float)bitfield;

				if(this.solids.Count == 2){
					bitfield = 0;
					bitPos = 3;
					bitfield |= (1 << bitPos);
					this.solids[1].attrs.w = (float)bitfield;
				}
			}

			ClayContainer oldContainer = null;
			if(this.clayxelContainerRef != null){
				oldContainer = (ClayContainer)this.clayxelContainerRef.Target;
			}

			if(oldContainer != null){
				oldContainer.scheduleClayObjectsScan();
				oldContainer.computeClay();
			}

			this.clayxelContainerRef = null;
			if(this.transform.parent == null){
				return;
			}

			GameObject parent = this.transform.parent.gameObject;

			ClayContainer clayxel = null;
			for(int i = 0; i < 100; ++i){
				clayxel = parent.GetComponent<ClayContainer>();
				if(clayxel != null){
					break;
				}
				else{
					if(parent.transform.parent == null){
						break;
					}
					else{
						parent = parent.transform.parent.gameObject;
					}
				}
			}

			if(clayxel == null){
				return;
			}
			else{
				this.clayxelContainerRef = new WeakReference(clayxel);

				if(oldContainer != null && oldContainer != clayxel){
					clayxel.scheduleClayObjectsScan();
					clayxel.computeClay();
				}
			}

			#if UNITY_EDITOR
				Undo.undoRedoPerformed -= this.onUndoPerformed;
				Undo.undoRedoPerformed += this.onUndoPerformed;
			#endif
		}

		/*!\brief for internal use*/
		public void pullUpdate(){
			if(this.invalidated){
				return;
			}

			this.updateSolids(false);
		}

		void updateSolids(bool notifyContainer){
			if(this.clayxelContainerRef == null){
				return;
			}
			
			if(this.mode == ClayObjectMode.single){
				if(this.transform.hasChanged){
					this.transform.hasChanged = false;

					this.updateSingle();

					if(notifyContainer){
						this.getClayContainer().clayObjectUpdated(this);
					}
				}
			}
			else if(this.mode == ClayObjectMode.offset){
				if(this.transform.hasChanged || this.offsetter.transform.hasChanged){
					this.transform.hasChanged = false;
					this.offsetter.transform.hasChanged = false;

					this.updateOffset();

					if(notifyContainer){
						this.getClayContainer().clayObjectUpdated(this);
					}
				}
			}
			else if(this.mode == ClayObjectMode.spline){
				bool changed = false;

				if(this.transform.hasChanged){
					changed = true;
				}
				else{
					for(int i = 0; i < this.splinePoints.Count; ++i){
						try{
							if(this.splinePoints[i].transform.hasChanged){
								changed = true;
								break;
							}
						}
						catch{
							this.repairSplineControlPoints();

							return;
						}
					}
				}

				if(changed){
					this.transform.hasChanged = false;

					try{
						this.updateSpline();
					}
					catch{
						this.repairSplineControlPoints();

						return;
					}

					if(notifyContainer){
						this.getClayContainer().clayObjectUpdated(this);
					}
				}
			}
			else if(this.mode == ClayObjectMode.clayGroup){
				if(this.transform.hasChanged){
					this.transform.hasChanged = false;
					
					this.updateSingle();

					if(notifyContainer){
						this.getClayContainer().clayObjectUpdated(this);
					}
				}
			}
		}

		/*!\brief internal use only */
		public ClayContainer getClayContainerPtr(){
			if(this.clayxelContainerRef == null){
				if(this.invalidated){
					return null;
				}

				if(this.clayxelContainerRef == null){
					return null;
				}
			}

			return (ClayContainer)this.clayxelContainerRef.Target;
		}

		void changeNumSolids(int num){
			this.solids = new List<Solid>(num);

			for(int i = 0; i < num; ++i){
				this.solids.Add(new Solid());
			}
		}

		void repairSplineControlPoints(){
			this.splinePoints.Clear();

			for(int i = 0; i < this.transform.childCount; ++i){
				GameObject controlPnt = this.transform.GetChild(i).gameObject;
				if(controlPnt.name.StartsWith("splinePnt")){
					this.splinePoints.Add(controlPnt);

					if(this.splinePoints.Count == 1){
						this.splinePoints.Add(controlPnt);				
					}

					controlPnt.name = "splinePnt" + (this.splinePoints.Count - 1);
				}
			}

			this.splinePoints.Add(this.splinePoints[this.splinePoints.Count - 1]);

			this.updateSplineSubdiv();

			this.init();

			this.forceUpdate();

			this.getClayContainer().scheduleClayObjectsScan();
		}
		
		void OnDestroy(){
			this.invalidated = true;
			this.enabled = false;
			
			ClayContainer clayxel = this.getClayContainer();
			if(clayxel != null){
				clayxel.scheduleClayObjectsScan();

				#if UNITY_EDITOR
					if(!Application.isPlaying){
						if(!clayxel.isMemoryOptimized()){
							clayxel.computeClay();
						}

						UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
					}
				#endif
			}

			#if UNITY_EDITOR
				Undo.undoRedoPerformed -= this.onUndoPerformed;
			#endif
		}

		void updateSplineSubdiv(){
			if(this.splineSubdiv < 0){
				this.splineSubdiv = 0;
			}

			if(this.splineSubdiv > 20){
				this.splineSubdiv = 20;
			}

			this.numSolids = this.splineSubdiv * (this.splinePoints.Count - 3);
			if(this.numSolids < 0){
				this.numSolids = 0;
			}
		}

		void updateSingle(){
			if(this.solids.Count == 0){
				return;
			}

			Solid solid = this.solids[0];

			ClayContainer container = this.getClayContainer();
			Matrix4x4 invMat = container.transform.worldToLocalMatrix * this.transform.localToWorldMatrix;

			solid.position.x = invMat[0, 3];
			solid.position.y = invMat[1, 3];
			solid.position.z = invMat[2, 3];

			solid.rotation = invMat.rotation;

			solid.scale.x = Mathf.Abs((this.transform.lossyScale.x / container.transform.lossyScale.x) * 0.5f);
			solid.scale.y = Mathf.Abs((this.transform.lossyScale.y / container.transform.lossyScale.y) * 0.5f);
			solid.scale.z = Mathf.Abs((this.transform.lossyScale.z / container.transform.lossyScale.z) * 0.5f);
			
			solid.blend = this.blend;
			
			solid.color.x = this.color.r;
			solid.color.y = this.color.g;
			solid.color.z = this.color.b;

			solid.attrs = this.attrs;
			solid.attrs2 = this.attrs2;

			solid.primitiveType = this.primitiveType;
		}

		void updateOffset(){
			ClayContainer container = this.getClayContainer();
			Matrix4x4 invMat = container.transform.worldToLocalMatrix * this.transform.localToWorldMatrix;

			Vector3 offsetPos = new Vector3(invMat[0, 3], invMat[1, 3], invMat[2, 3]);
			Quaternion offsetRot = invMat.rotation;
			Vector3 offsetScale;
			offsetScale.x = (this.transform.lossyScale.x / container.transform.lossyScale.x) * 0.5f;
			offsetScale.y = (this.transform.lossyScale.y / container.transform.lossyScale.y) * 0.5f;
			offsetScale.z = (this.transform.lossyScale.z / container.transform.lossyScale.z) * 0.5f;

			Vector3 offsetterPos;
			for(int i = 0; i < this.solids.Count; ++i){
				Solid solid = this.solids[i];

				solid.position = offsetPos;

				offsetterPos = this.offsetter.transform.localPosition;
				offsetterPos.x *= this.offsetter.transform.lossyScale.x / container.transform.lossyScale.x;
				offsetterPos.y *= this.offsetter.transform.lossyScale.y / container.transform.lossyScale.y;
				offsetterPos.z *= this.offsetter.transform.lossyScale.z / container.transform.lossyScale.z;

				offsetPos += offsetRot * offsetterPos;

				solid.rotation = offsetRot;

				offsetRot = offsetRot * this.offsetter.transform.localRotation;

				solid.scale = offsetScale;

				offsetScale.x *= this.offsetter.transform.localScale.x;
				offsetScale.y *= this.offsetter.transform.localScale.y;
				offsetScale.z *= this.offsetter.transform.localScale.z;

				solid.blend = this.blend;
				
				solid.color.x = this.color.r;
				solid.color.y = this.color.g;
				solid.color.z = this.color.b;

				solid.attrs = this.attrs;
				solid.attrs2 = this.attrs2;

				solid.primitiveType = this.primitiveType;
			}
		}

		void updateSpline(){
			if(this.splinePoints.Count > 3){
				float incrT = 1.0f / this.splineSubdiv;

				int solidIt = 0;

				ClayContainer container = this.getClayContainer();
				Matrix4x4 parentInvMat = container.transform.worldToLocalMatrix;
				
				for(int i = 0; i < this.splinePoints.Count - 3; ++i){
					GameObject splinePoint0 = this.splinePoints[i];
					GameObject splinePoint1 = this.splinePoints[i + 1];
					GameObject splinePoint2 = this.splinePoints[i + 2];
					GameObject splinePoint3 = this.splinePoints[i + 3];

					splinePoint0.transform.hasChanged = false;
					splinePoint1.transform.hasChanged = false;
					splinePoint2.transform.hasChanged = false;
					splinePoint3.transform.hasChanged = false;

					Vector3 s0;
					s0.x = splinePoint0.transform.lossyScale.x / container.transform.lossyScale.x;
					s0.y = splinePoint0.transform.lossyScale.y / container.transform.lossyScale.y;
					s0.z = splinePoint0.transform.lossyScale.z / container.transform.lossyScale.z;

					Vector3 s1;
					s1.x = splinePoint1.transform.lossyScale.x / container.transform.lossyScale.x;
					s1.y = splinePoint1.transform.lossyScale.y / container.transform.lossyScale.y;
					s1.z = splinePoint1.transform.lossyScale.z / container.transform.lossyScale.z;

					Vector3 s2;
					s2.x = splinePoint2.transform.lossyScale.x / container.transform.lossyScale.x;
					s2.y = splinePoint2.transform.lossyScale.y / container.transform.lossyScale.y;
					s2.z = splinePoint2.transform.lossyScale.z / container.transform.lossyScale.z;

					Vector3 s3;
					s3.x = splinePoint3.transform.lossyScale.x / container.transform.lossyScale.x;
					s3.y = splinePoint3.transform.lossyScale.y / container.transform.lossyScale.y;
					s3.z = splinePoint3.transform.lossyScale.z / container.transform.lossyScale.z;

					Matrix4x4 pointMat0 = parentInvMat * splinePoint0.transform.localToWorldMatrix;
					Matrix4x4 pointMat1 = parentInvMat * splinePoint1.transform.localToWorldMatrix;
					Matrix4x4 pointMat2 = parentInvMat * splinePoint2.transform.localToWorldMatrix;
					Matrix4x4 pointMat3 = parentInvMat * splinePoint3.transform.localToWorldMatrix;

					Vector3 point0 = new Vector3(pointMat0[0, 3], pointMat0[1, 3], pointMat0[2, 3]);
					Vector3 point1 = new Vector3(pointMat1[0, 3], pointMat1[1, 3], pointMat1[2, 3]);
					Vector3 point2 = new Vector3(pointMat2[0, 3], pointMat2[1, 3], pointMat2[2, 3]);
					Vector3 point3 = new Vector3(pointMat3[0, 3], pointMat3[1, 3], pointMat3[2, 3]);

					for(int j = 0; j < this.splineSubdiv; ++j){
						float t = incrT * j;

						Solid solid = this.solids[solidIt];
						solid.position = this.getCatmullRomVec3(point0, point1, point2, point3, t);
						solid.rotation = this.getCatmullRomQuat(pointMat0.rotation, pointMat1.rotation, pointMat2.rotation, pointMat3.rotation, t);
						solid.scale = this.getCatmullRomVec3(s0, s1, s2, s3, t) * 0.5f;

						solid.blend = this.blend;
						solid.attrs = this.attrs;
						solid.attrs2 = this.attrs2;
						solid.primitiveType = this.primitiveType;

						solid.color.x = this.color.r;
						solid.color.y = this.color.g;
						solid.color.z = this.color.b;

						solidIt += 1;
					}
				}
			}
		}

		Vector3 getCatmullRomVec3(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t){
			//The coefficients of the cubic polynomial (except the 0.5f * which I added later for performance)
			Vector3 a = 2f * p1;
			Vector3 b = p2 - p0;
			Vector3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
			Vector3 d = -p0 + 3f * p1 - 3f * p2 + p3;

			//The cubic polynomial: a + b * t + c * t^2 + d * t^3
			Vector3 pos = 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));

			return pos;
		}

		Quaternion getCatmullRomQuat(Quaternion p0, Quaternion p1, Quaternion p2, Quaternion p3, float t){
			Quaternion a;
			a.x = p1.x * 2.0f;
			a.y = p1.y * 2.0f;
			a.z = p1.z * 2.0f;
			a.w = p1.w * 2.0f;

			Quaternion b;
			b.x = p2.x - p0.x;
			b.y = p2.y - p0.y;
			b.z = p2.z - p0.z;
			b.w = p2.w - p0.w;

			Quaternion c;
			c.x = 2.0f * p0.x - 5.0f * p1.x + 4.0f * p2.x - p3.x;
			c.y = 2.0f * p0.y - 5.0f * p1.y + 4.0f * p2.y - p3.y;
			c.z = 2.0f * p0.z - 5.0f * p1.z + 4.0f * p2.z - p3.z;
			c.w = 2.0f * p0.w - 5.0f * p1.w + 4.0f * p2.w - p3.w;

			Quaternion d;
			d.x = -p0.x + 3.0f * p1.x - 3.0f * p2.x + p3.x;
			d.y = -p0.y + 3.0f * p1.y - 3.0f * p2.y + p3.y;
			d.z = -p0.z + 3.0f * p1.z - 3.0f * p2.z + p3.z;
			d.w = -p0.w + 3.0f * p1.w - 3.0f * p2.w + p3.w;

			Quaternion rot;
			float pow2 = t * t;
			float pow3 = t * t * t;
			rot.x = 0.5f * (a.x + (b.x * t) + (c.x * pow2) + (d.x * pow3));
			rot.y = 0.5f * (a.y + (b.y * t) + (c.y * pow2) + (d.y * pow3));
			rot.z = 0.5f * (a.z + (b.z * t) + (c.z * pow2) + (d.z * pow3));
			rot.w = 0.5f * (a.w + (b.w * t) + (c.w * pow2) + (d.w * pow3));

			return rot.normalized;
		}

		#if UNITY_EDITOR

		public void onUndoPerformed(){
			for(int i = 0; i < UnityEditor.Selection.gameObjects.Length; ++i){
				try{
					if(UnityEditor.Selection.gameObjects[i] == this.gameObject && !this.invalidated){
						this.forceUpdate();
						break;
					}
				}
				catch{}
			}
		}

		void OnDrawGizmos(){
			if(this.blend < 0.0f || // negative shape?
				(((int)this.attrs.w >> 0)&1) == 1){// painter?

				if(UnityEditor.Selection.Contains(this.gameObject)){// if selected draw wire cage
					Gizmos.color = this.gizmoColor;
					if(this.primitiveType == 0){
						Gizmos.matrix = this.transform.localToWorldMatrix;
						Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
					}
					else if(this.primitiveType == 1){
						Gizmos.matrix = this.transform.localToWorldMatrix;
						Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
					}
					else if(this.primitiveType == 2){
						this.drawCylinder();
					}
					else if(this.primitiveType == 3){
						this.drawTorus();
					}
					else if(this.primitiveType == 4){
						this.drawCurve();
					}
					else if(this.primitiveType == 5){
						Gizmos.matrix = this.transform.localToWorldMatrix;
						Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
					}
					else if(this.primitiveType == 6){
						Gizmos.matrix = this.transform.localToWorldMatrix;
						Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
					}
				}
			}
		}

		void drawCurve(){
			Handles.color = Color.white;
			
			float radius = this.attrs.z * 0.5f;
			Vector3 heightVec = (this.transform.up * (this.transform.lossyScale.y - this.attrs.z)) * 0.5f;
			Vector3 sideVec = this.transform.right * ((this.transform.lossyScale.x*0.5f) - radius);
			Vector3 startPnt = this.transform.position - sideVec - heightVec;
			Vector3 endPnt = this.transform.position + sideVec - heightVec;
			Vector3 tanOffset = this.transform.right * - (this.transform.lossyScale.x * 0.2f);
			Vector3 tanOffset2 = this.transform.up * (radius * 0.5f);
			Vector3 tanSlide = this.transform.right * ((this.attrs.x - 0.5f) * (this.transform.lossyScale.x * 0.5f));
			Vector3 startTan = this.transform.position + heightVec + tanOffset + tanOffset2 + tanSlide;
			Vector3 endTan = this.transform.position + heightVec - tanOffset + tanOffset2 + tanSlide;
			Vector3 elongVec =  this.transform.forward * ((this.transform.lossyScale.z * 0.5f) - radius);
			Vector3 elongVec2 =  this.transform.forward * ((this.transform.lossyScale.z * 0.5f) - (radius*2.0f));

			float w0 = (1.0f - this.attrs.y) * 2.0f;
			float w1 = this.attrs.y * 2.0f;

			Handles.DrawBezier(startPnt - (elongVec*w0), endPnt - (elongVec*w1), startTan - elongVec, endTan - elongVec, Color.white, null, 2.0f);
			Handles.DrawBezier(startPnt + (elongVec*w0), endPnt + (elongVec*w1), startTan + elongVec, endTan + elongVec, Color.white, null, 2.0f);

			Gizmos.DrawWireSphere(startPnt - elongVec2, radius * w0);
			Gizmos.DrawWireSphere(endPnt - elongVec2, radius * w1);

			if(this.transform.lossyScale.z > 1.0f){
				Gizmos.DrawWireSphere(startPnt + elongVec2, radius);
				Gizmos.DrawWireSphere(endPnt + elongVec2, radius);

				Handles.DrawLine(
					startPnt + elongVec2 - (this.transform.right * radius), 
					startPnt - elongVec2 - (this.transform.right * radius));

				Handles.DrawLine(
					endPnt + elongVec2 + (this.transform.right * radius), 
					endPnt - elongVec2 + (this.transform.right * radius));
			}
		}

		void drawTorus(){
			Handles.color = Color.white;

			float radius = this.attrs.x;

			Vector3 elongationVec = this.transform.forward * ((this.transform.lossyScale.z * 0.5f) - radius);
			Vector3 sideVec = this.transform.right * ((this.transform.lossyScale.x * 0.5f) - radius);
			Vector3 radiusSideOffsetVec = this.transform.right * radius;
			Vector3 heightVec = this.transform.up * ((this.transform.lossyScale.y * 0.5f) - radius);
			Vector3 radiusUpOffsetVec = this.transform.up * radius;
			Vector3 sideCrossSecVec = this.transform.right * (this.transform.lossyScale.x * 0.5f);

			float crossSecRadius = this.transform.lossyScale.x * 0.5f;
			Vector3 radiusCrossSecVec = this.transform.up * crossSecRadius;
			Vector3 heightCrossSecVec = this.transform.up * ((this.transform.lossyScale.y *0.5f) - crossSecRadius);

			float crossSecRadiusIn = (this.transform.lossyScale.x * 0.5f) - (radius*2.0f);
			Vector3 sideCrossSecVecIn = this.transform.right * ((this.transform.lossyScale.x * 0.5f) - (radius * 2.0f));

			if(this.transform.lossyScale.y >= this.transform.lossyScale.x){
				// cross out section
				Handles.DrawWireArc(this.transform.position + heightCrossSecVec, 
					this.transform.forward, this.transform.right, 180.0f, crossSecRadius);

				Handles.DrawWireArc(this.transform.position - heightCrossSecVec, 
					this.transform.forward, this.transform.right, -180.0f, crossSecRadius);

				Handles.DrawLine(
					this.transform.position + heightCrossSecVec + sideCrossSecVec, 
					this.transform.position - heightCrossSecVec + sideCrossSecVec);

				Handles.DrawLine(
					this.transform.position + heightCrossSecVec - sideCrossSecVec, 
					this.transform.position - heightCrossSecVec - sideCrossSecVec);

				// cross in section
				Handles.DrawWireArc(this.transform.position + heightCrossSecVec, 
					this.transform.forward, this.transform.right, 180.0f, crossSecRadiusIn);

				Handles.DrawWireArc(this.transform.position - heightCrossSecVec, 
					this.transform.forward, this.transform.right, -180.0f, crossSecRadiusIn);

				Handles.DrawLine(
					this.transform.position + heightCrossSecVec + sideCrossSecVecIn, 
					this.transform.position - heightCrossSecVec + sideCrossSecVecIn);

				Handles.DrawLine(
					this.transform.position + heightCrossSecVec - sideCrossSecVecIn, 
					this.transform.position - heightCrossSecVec - sideCrossSecVecIn);
			}

			if(this.transform.lossyScale.z >= radius * 2.0f){
				// top section
				Handles.DrawWireArc(this.transform.position - elongationVec + heightVec, 
					this.transform.right, this.transform.up, -180.0f, radius);

				Handles.DrawWireArc(this.transform.position + elongationVec + heightVec, 
					this.transform.right, this.transform.up, 180.0f, radius);

				Handles.DrawLine(
					this.transform.position + elongationVec + heightVec + radiusUpOffsetVec , 
					this.transform.position - elongationVec + heightVec + radiusUpOffsetVec);

				Handles.DrawLine(
					this.transform.position + elongationVec + heightVec - radiusUpOffsetVec , 
					this.transform.position - elongationVec + heightVec - radiusUpOffsetVec);

				// bottom section
				Handles.DrawWireArc(this.transform.position - elongationVec - heightVec, 
					this.transform.right, this.transform.up, -180.0f, radius);

				Handles.DrawWireArc(this.transform.position + elongationVec - heightVec, 
					this.transform.right, this.transform.up, 180.0f, radius);

				Handles.DrawLine(
					this.transform.position + elongationVec - heightVec + radiusUpOffsetVec , 
					this.transform.position - elongationVec - heightVec + radiusUpOffsetVec);

				Handles.DrawLine(
					this.transform.position + elongationVec - heightVec - radiusUpOffsetVec , 
					this.transform.position - elongationVec - heightVec - radiusUpOffsetVec);

				// left section
				Handles.DrawWireArc(this.transform.position - elongationVec - sideVec, 
					this.transform.up, this.transform.right, 180.0f, radius);

				Handles.DrawWireArc(this.transform.position + elongationVec - sideVec, 
					this.transform.up, this.transform.right, -180.0f, radius);

				Handles.DrawLine(
					this.transform.position + elongationVec - sideVec + radiusSideOffsetVec , 
					this.transform.position - elongationVec - sideVec + radiusSideOffsetVec);

				Handles.DrawLine(
					this.transform.position + elongationVec - sideVec - radiusSideOffsetVec, 
					this.transform.position - elongationVec - sideVec - radiusSideOffsetVec);

				// right section
				Handles.DrawWireArc(this.transform.position - elongationVec + sideVec, 
					this.transform.up, this.transform.right, 180.0f, radius);

				Handles.DrawWireArc(this.transform.position + elongationVec + sideVec, 
					this.transform.up, this.transform.right, -180.0f, radius);

				Handles.DrawLine(
					this.transform.position + elongationVec + sideVec + radiusSideOffsetVec , 
					this.transform.position - elongationVec + sideVec + radiusSideOffsetVec);

				Handles.DrawLine(
					this.transform.position + elongationVec + sideVec - radiusSideOffsetVec, 
					this.transform.position - elongationVec + sideVec - radiusSideOffsetVec);
			}
		}

		void drawCylinder(){
			Handles.color = Color.white;
			
			float radius = this.transform.lossyScale.x;
			if(this.transform.lossyScale.z < radius){
				radius = this.transform.lossyScale.z;
			}

			radius *= 0.5f;

			Vector3 arcDir = this.transform.right;
			Vector3 extVec = - (this.transform.forward * ((this.transform.lossyScale.z * 0.5f) - radius));
			if(this.transform.lossyScale.z < this.transform.lossyScale.x){
				arcDir = this.transform.forward;
				extVec = (this.transform.right * ((this.transform.lossyScale.x*0.5f) - radius));
			}

			Vector3 heightVec = this.transform.up * (this.transform.lossyScale.y * 0.5f);

			// draw top
			Handles.DrawWireArc(this.transform.position + extVec + heightVec, this.transform.up, arcDir, 180.0f, radius);
			Handles.DrawWireArc(this.transform.position - extVec + heightVec, this.transform.up, arcDir, -180.0f, radius);

			Handles.DrawLine(
				this.transform.position + extVec + heightVec + (arcDir*radius), 
				this.transform.position - extVec + heightVec + (arcDir*radius));

			Handles.DrawLine(
				this.transform.position + extVec + heightVec - (arcDir*radius), 
				this.transform.position - extVec + heightVec - (arcDir*radius));

			// draw bottom
			Handles.DrawWireArc(this.transform.position + extVec - heightVec, this.transform.up, arcDir, 180.0f, radius+this.attrs.z);
			Handles.DrawWireArc(this.transform.position - extVec - heightVec, this.transform.up, arcDir, -180.0f, radius+this.attrs.z);
			
			Handles.DrawLine(
				this.transform.position + extVec - heightVec - (arcDir*(radius+this.attrs.z)), 
				this.transform.position - extVec - heightVec - (arcDir*(radius+this.attrs.z)));

			Handles.DrawLine(
				this.transform.position + extVec - heightVec + (arcDir*(radius+this.attrs.z)), 
				this.transform.position - extVec - heightVec + (arcDir*(radius+this.attrs.z)));

			// draw side lines
			Handles.DrawLine(
				this.transform.position + heightVec + (arcDir*radius), 
				this.transform.position - heightVec + (arcDir*(radius+this.attrs.z)));

			Handles.DrawLine(
				this.transform.position + heightVec - (arcDir*radius), 
				this.transform.position - heightVec - (arcDir*(radius+this.attrs.z)));
		}
		#endif // end if UNITY_EDITOR 
	}
}
