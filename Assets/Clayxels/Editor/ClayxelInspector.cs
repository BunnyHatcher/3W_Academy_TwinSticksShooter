
#if UNITY_EDITOR // exclude from build

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Networking;

using Clayxels;

namespace Clayxels{
	[CustomEditor(typeof(ClayContainer))]
	public class ClayxelInspector : Editor{
		static bool extrasPanel = false;
		
		public override void OnInspectorGUI(){
			Color defaultColor = GUI.backgroundColor;

			ClayContainer clayContainer = (ClayContainer)this.target;
			
			GUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Clayxels V" + ClayContainer.version);

			if(clayContainer.getInstanceOf() == null && !clayContainer.isFrozenToMesh()){
				bool interactive = clayContainer.isInteractive();
				GUI.backgroundColor = defaultColor;
				if(interactive){
					GUI.backgroundColor = Color.yellow;
				}
				if(GUILayout.Button((new GUIContent("interactive", "Interactive containers can be animated and edited at runtime. Disabling this will prevent changes at runtime but it will optimize the container to render faster and use less memory.")))){
					clayContainer.setInteractive(!interactive);

					if(!Application.isPlaying){
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}
				}
				GUI.backgroundColor = defaultColor;
			}

			GUILayout.EndHorizontal();

			EditorGUILayout.Space();

			string userWarn = clayContainer.getUserWarning();
			if(userWarn != ""){
				GUIStyle s = new GUIStyle();
				s.wordWrap = true;
				s.normal.textColor = Color.yellow;
				EditorGUILayout.LabelField(userWarn, s);
			}

			if(clayContainer.getNumSolids() > clayContainer.getMaxSolids()){
				GUIStyle s = new GUIStyle();
				s.wordWrap = true;
				s.normal.textColor = Color.yellow;
				EditorGUILayout.LabelField("Max solid count exeeded, open Global Config to tweak settings.");
			}

			if(PrefabUtility.IsPartOfAnyPrefab(clayContainer)){
				if(GUILayout.Button((new GUIContent("link prefab instances to this", "Link all other instances of this prefab in scene to this prefab.\nThis becomes the editable master prefab.\nThis will also fix prefab links in scene if something breaks!")))){
					ClayContainer.linkAllPrefabInstances(clayContainer);
					
					if(!Application.isPlaying){
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}
				}

				EditorGUILayout.Space();
			}

			if(clayContainer.getInstanceOf() != null){
				ClayContainer newInstance = (ClayContainer)EditorGUILayout.ObjectField(new GUIContent("instance", "Set this to point at another clayContainer in scene to make this into an instance and avoid having to compute the same thing twice."), clayContainer.getInstanceOf(), typeof(ClayContainer), true);
			
				if(newInstance != clayContainer.getInstanceOf() && newInstance != clayContainer){
					clayContainer.setIsInstanceOf(newInstance);
					clayContainer.init();

					if(!Application.isPlaying){
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}
				}

				EditorGUILayout.Space();
				if(GUILayout.Button((new GUIContent("global config", "")))){
					ClayxelsPrefsWindow.Open();
				}

				return;
			}

			if(clayContainer.isFrozen()){
				if(clayContainer.isFrozenToMesh()){
					if(GUILayout.Button(new GUIContent("defrost clayxels", "Back to live clayxels."))){
						clayContainer.defrostContainersHierarchy();
					}

					EditorGUILayout.LabelField("frozen mesh options:");

					EditorGUILayout.Space();

					GUILayout.BeginHorizontal();

					float meshNormalSmooth = EditorGUILayout.Slider("normal smooth", clayContainer.getSmoothMeshNormalAngle(), 0.0f, 180.0f);
					clayContainer.setSmoothMeshNormalAngle(meshNormalSmooth);

					if(GUILayout.Button((new GUIContent("apply", "")))){
						clayContainer.smoothNormalsContainersHierarchy(meshNormalSmooth);
					}

					GUILayout.EndHorizontal();

					if(!clayContainer.isAutoRigEnabled()){
						if(GUILayout.Button((new GUIContent("auto-rig", "Generate bones and skinning.")))){
							clayContainer.enableAutoRiggedFrozenMesh(true);
						}
					}
					else{
						if(GUILayout.Button((new GUIContent("disable auto-rig", "disable bones and skinning.")))){
							clayContainer.enableAutoRiggedFrozenMesh(false);
						}
					}
					
					if(GUILayout.Button((new GUIContent("generate UV map", "Generate a simple triangles unwrap useful for lightmaps.")))){
						Mesh mesh = clayContainer.gameObject.GetComponent<MeshFilter>().sharedMesh;
						Unwrapping.GenerateSecondaryUVSet(mesh);
						mesh.uv = mesh.uv2;
					}

					#if CLAYXELS_RETOPO
						EditorGUILayout.Space();

						GUILayout.BeginHorizontal();

						clayContainer.retopoMaxVerts = EditorGUILayout.IntField(new GUIContent("retopo vertex count", "-1 will let the tool decide on the best number of vertices."), clayContainer.retopoMaxVerts);

						if(GUILayout.Button((new GUIContent("retopo", "will try to improve the mesh topology automatically (it will break uvs and rigging)")))){
							// reset mesh in case it had previous retopo applied
							clayContainer.defrostToLiveClayxels();
							bool freeMemory = true;
							clayContainer.freezeToMesh(clayContainer.getClayxelDetail(), clayContainer.getSmoothMeshNormalAngle(), freeMemory);
							clayContainer.retopoApplied = true;

							Mesh mesh = clayContainer.gameObject.GetComponent<MeshFilter>().sharedMesh;

							if(mesh != null){
								int targetVertCount = RetopoUtils.getRetopoTargetVertsCount(clayContainer.gameObject, clayContainer.retopoMaxVerts);
								if(targetVertCount == 0){
									return;
								}

								RetopoUtils.retopoMesh(mesh, targetVertCount, -1);
								MeshUtils.freezeMeshPostPass(mesh, clayContainer.getSmoothMeshNormalAngle(), clayContainer.gameObject);

								AssetDatabase.SaveAssets();
							}
						}

						GUILayout.EndHorizontal();
					#endif

					EditorGUILayout.Space();
				}

				EditorGUILayout.Space();
				if(GUILayout.Button((new GUIContent("global config", "")))){
					ClayxelsPrefsWindow.Open();
				}

				return;
			}

			EditorGUI.BeginChangeCheck();

			GUILayout.BeginHorizontal();
			
			int clayxelDetail = EditorGUILayout.IntField(new GUIContent("clayContainer detail", "How coarse or finely detailed is your sculpt. Enable Gizmos in your viewport to see the boundaries."), clayContainer.getClayxelDetail());
			
			if(clayContainer.isAutoBoundsActive()){
				GUI.backgroundColor = Color.yellow;
			}

			if(GUILayout.Button(new GUIContent("auto-bounds", "Disable auto-bounds to gain more control over the bounds for this container, small bounds will get better performance overall."))){
				clayContainer.setAutoBoundsActive(!clayContainer.isAutoBoundsActive());

				if(!Application.isPlaying){
					EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
				}
			}

			GUI.backgroundColor = defaultColor;

			GUILayout.EndHorizontal();

			if(EditorGUI.EndChangeCheck()){
				ClayContainer._inspectorUpdate();

				Undo.RecordObject(this.target, "changed clayContainer");

				clayContainer.setClayxelDetail(clayxelDetail);
				
				if(!Application.isPlaying){
					EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
				}

				return;
			}

			GUILayout.BeginHorizontal();

			GUI.backgroundColor = defaultColor;

			if(clayContainer.isAutoBoundsActive()){
				if(ClayContainer.getMaxBounds() > 1){
					EditorGUI.BeginChangeCheck();

					int boundsLimit = EditorGUILayout.IntField(new GUIContent("bounds limit", "Limits the work area available for your sculpt, a value of 1 will perform better and consume less video memory, a value of 3 will give you more detail but will require more gpu resources."), clayContainer.getAutoBoundsLimit());

					if(EditorGUI.EndChangeCheck()){
						ClayContainer._inspectorUpdate();

						clayContainer.setAutoBoundsLimit(boundsLimit);

						if(!Application.isPlaying){
							UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
							EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
						}
					}
				}
			}
			else{
				EditorGUI.BeginChangeCheck();
				Vector3Int boundsScale = EditorGUILayout.Vector3IntField(new GUIContent("bounds scale", "How much work area you have for your sculpt within this container. Enable Gizmos in your viewport to see the boundaries."), clayContainer.getBoundsScale());
				
				if(EditorGUI.EndChangeCheck()){
					ClayContainer._inspectorUpdate();

					clayContainer.setBoundsScale(boundsScale.x, boundsScale.y, boundsScale.z);

					if(!Application.isPlaying){
						UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}

					return;
				}

				if(GUILayout.Button(new GUIContent("-", ""))){
					ClayContainer._inspectorUpdate();

					Vector3Int bounds = clayContainer.getBoundsScale();
					clayContainer.setBoundsScale(bounds.x - 1, bounds.y - 1, bounds.z - 1);

					clayContainer.init();
					clayContainer.needsUpdate = true;

					if(!Application.isPlaying){
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}

					return;
				}

				if(GUILayout.Button(new GUIContent("+", ""))){
					ClayContainer._inspectorUpdate();

					Vector3Int bounds = clayContainer.getBoundsScale();
					clayContainer.setBoundsScale(bounds.x + 1, bounds.y + 1, bounds.z + 1);

					clayContainer.init();
					clayContainer.needsUpdate = true;

					if(!Application.isPlaying){
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}

					return;
				}
			}

			GUI.backgroundColor = defaultColor;

			GUILayout.EndHorizontal();

			EditorGUILayout.Space();

			if(GUILayout.Button(new GUIContent("add clay", "lets get this party started"))){
				ClayObject clayObj = ((ClayContainer)this.target).addClayObject();

				Undo.RegisterCreatedObjectUndo(clayObj.gameObject, "added clayObject");
				UnityEditor.Selection.objects = new GameObject[]{clayObj.gameObject};

				clayContainer.needsUpdate = true;

				if(!Application.isPlaying){
					EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
				}

				return;
			}

			if(!ClayContainer.directPickingEnabled()){
				if(GUILayout.Button(new GUIContent("pick clay ("+ClayContainer.pickingKey+")", "Press p on your keyboard to mouse pick ClayObjects from the viewport. Pressing Shift will add to a previous selection."))){
					ClayContainer.startScenePickingMesh();
				}
			}

			EditorGUILayout.Space();

			if(GUILayout.Button((new GUIContent("global config", "")))){
				ClayxelsPrefsWindow.Open();
			}

			ClayxelInspector.extrasPanel = EditorGUILayout.Foldout(ClayxelInspector.extrasPanel, "extras", true);

			if(ClayxelInspector.extrasPanel){
				string[] renderLabels = {"polySplat", "microVoxelSplat", "smoothMesh"};

				bool builtinRedermode = false;
				if(ClayContainer.getRenderPipe() == "builtin"){
					renderLabels = new string[]{"polySplat", "smoothMesh"};
					builtinRedermode = true;
				}
				
				int currRenderMode = clayContainer.getRenderMode();
				
				if(builtinRedermode){
					if(currRenderMode == 2){// microvoxels not supported in builtin renderer
						currRenderMode = 1;
					}
				}

 				int renderMode = EditorGUILayout.Popup("render mode", currRenderMode, renderLabels);
				
 				if(currRenderMode != renderMode){
 					if(builtinRedermode){
						if(renderMode == 1){// microvoxels not supported in builtin renderer
							renderMode = 2;
						}
					}
					
 					clayContainer.setRenderMode(renderMode);
 					
 					if(!Application.isPlaying){
 						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
 					}
 				}

				EditorGUILayout.Space();

				ClayContainer instance = (ClayContainer)EditorGUILayout.ObjectField(new GUIContent("instance", "Set this to point at another clayContainer in scene to make this into an instance and avoid having to compute the same thing twice."), clayContainer.getInstanceOf(), typeof(ClayContainer), true);
				if(instance != clayContainer.getInstanceOf() && instance != clayContainer){
					clayContainer.setIsInstanceOf(instance);

					if(!Application.isPlaying){
						UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}
				}
				
				if(clayContainer.getRenderMode() == 2){// smooth mesh
					EditorGUI.BeginChangeCheck();

					float meshNormalSmooth = EditorGUILayout.Slider("normal smooth", clayContainer.getSmoothMeshNormalAngle(), 0.0f, 180.0f);
					float meshVoxelize = EditorGUILayout.Slider("voxelize", clayContainer.getSmoothMeshVoxelize(), 0.0f, 1.0f);
					
					if(EditorGUI.EndChangeCheck()){
						clayContainer.setSmoothMeshVoxelize(meshVoxelize);
						clayContainer.setSmoothMeshNormalAngle(meshNormalSmooth);

						clayContainer.computeClay();

						if(!Application.isPlaying){
							UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
							EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
						}
					}
				}
				else if(clayContainer.getRenderMode() == 1){// microvoxel
					EditorGUI.BeginChangeCheck();

					float microvoxelSplatsQuality = EditorGUILayout.Slider(
						new GUIContent("splats quality", "Set the quality of microvoxel splats, 1.0 will get you beautiful textured splats that overlap with each other when close to camera, 0.0 will display blocky splats.Use this value to speedup the rendering of microvoxel containers."), 
						clayContainer.getMicrovoxelSplatsQuality(), 0.0f, 1.0f);

					float microvoxelRayIterations = EditorGUILayout.Slider(
						new GUIContent("ray iterations", "Set how far a ray will travel inside a microvoxel container.1.0 will traverse every voxel in the container until a hit is found.0.5 will stop the ray early and potentially miss the voxel that needs to be rendered.Use this value to speedup the rendering of microvoxel containers."), 
						clayContainer.getMicrovoxelRayIterations(), 0.5f, 1.0f);

					if(EditorGUI.EndChangeCheck()){
						clayContainer.setMicrovoxelSplatsQuality(microvoxelSplatsQuality);
						clayContainer.setMicrovoxelRayIterations(microvoxelRayIterations);
					}
				}

				EditorGUILayout.Space();

				if(clayContainer.storeAssetPath == ""){
					clayContainer.storeAssetPath = clayContainer.gameObject.name;
				}
				clayContainer.storeAssetPath = EditorGUILayout.TextField(new GUIContent("frozen asset name", "Specify an asset name to store this frozen mesh on disk. Files are saved relative to this project's Assets folder."), clayContainer.storeAssetPath);
				string[] paths = clayContainer.storeAssetPath.Split('.');
				if(paths.Length > 0){
					clayContainer.storeAssetPath = paths[0];
				}

				EditorGUILayout.Space();

				if(GUILayout.Button(new GUIContent("freeze mesh", "Switch between live clayxels and a frozen mesh."))){
					clayContainer.freezeContainersHierarchyToMesh();
				}

				EditorGUILayout.Space();

				EditorGUI.BeginChangeCheck();

				bool castShadows = EditorGUILayout.Toggle("cast shadows", clayContainer.getCastShadows());

				if(EditorGUI.EndChangeCheck()){
					ClayContainer._inspectorUpdate();

					clayContainer.setCastShadows(castShadows);

					if(!Application.isPlaying){
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}
				}

				EditorGUI.BeginChangeCheck();

				GUILayout.BeginHorizontal();

				Material customMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("customMaterial", "Custom materials need to use shaders specifically made for clayxels. Use the provided shaders and examples as reference. "), clayContainer.customMaterial, typeof(Material), false);
				
				if(customMaterial == null){
					if(GUILayout.Button(new GUIContent("+", "Create a new material that you can share with other containers."))){
						ClayContainer._inspectorUpdate();

						Undo.RecordObject(this.target, "changed clayContainer");

						this.addCustomMaterial(clayContainer);

						if(!Application.isPlaying){
							EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
						}

						this.inspectMaterial(clayContainer);

						return;
					}
				}

				GUILayout.EndHorizontal();

				if(EditorGUI.EndChangeCheck()){
					ClayContainer._inspectorUpdate();
					
					Undo.RecordObject(this.target, "changed clayContainer");

					if(customMaterial != clayContainer.customMaterial){
						clayContainer.setCustomMaterial(customMaterial);
					}
					
					if(!Application.isPlaying){
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}
				}
				// end of extras
			}

			if(!clayContainer.isFrozenToMesh()){
				this.inspectMaterial(clayContainer);
			}
		}

		MaterialEditor materialEditor = null;
		void inspectMaterial(ClayContainer clayContainer){
			this.drawMaterialInspector(clayContainer);
		}

		void drawMaterialInspector(ClayContainer container){
			EditorGUILayout.Space();

			Rect rect = EditorGUILayout.GetControlRect(false, 1);
       		rect.height = 1;
       		EditorGUI.DrawRect(rect, new Color ( 0.5f,0.5f,0.5f, 1 ) );

			Material material = container.getMaterial();

			if(material == null){
				return;
			}

			Shader shader = material.shader;

			string[] priorityNames = new string[]{
				"Smoothness",
				"Metallic",
				"SplatTexture"};

			string[] excludeNames = new string[]{
				"Emission Color",
				"Alpha Cutoff"};

			for(int i = 0; i < priorityNames.Length; ++i){
				for(int id = 0; id < shader.GetPropertyCount(); ++id){
					string desc = shader.GetPropertyDescription(id);

					if(desc == priorityNames[i]){
						this.drawMaterialProperty(container, material, id);
					}
				}
			}

			for(int i = 0; i < shader.GetPropertyCount(); ++i){
				ShaderPropertyType type = shader.GetPropertyType(i);
				string name = shader.GetPropertyName(i);
				string desc = shader.GetPropertyDescription(i);

				if(desc == ""){
					continue;
				}

				bool shouldSkip = false;
				for(int j = 0; j < excludeNames.Length; ++j){
					if(desc.StartsWith(excludeNames[j])){
						shouldSkip = true;
						break;
					}
				}

				if(shouldSkip){
					continue;
				}

				for(int j = 0; j < priorityNames.Length; ++j){
					if(desc == priorityNames[j]){
						shouldSkip = true;
						break;
					}
				}

				if(shouldSkip){
					continue;
				}
				
				bool userProperty = this.drawMaterialProperty(container, material, i);
				if(!userProperty){
					break;
				}
			}

			
		}

		void addCustomMaterial(ClayContainer container){
			string assetNameUnique = ClayContainer.defaultAssetsPath + "/" + container.storeAssetPath + container.gameObject.name + "_" + container.GetInstanceID();
			string materialUniqueName = assetNameUnique + "_mat";

			Material storedMat = new Material(container.getMaterial());
			AssetDatabase.CreateAsset(storedMat, "Assets/" + materialUniqueName + ".mat");
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			container.setCustomMaterial(storedMat);
		}

		bool drawMaterialProperty(ClayContainer clayContainer, Material material, int id){
			Shader shader = material.shader;
			ShaderPropertyType type = shader.GetPropertyType(id);
			string name = shader.GetPropertyName(id);
			string desc = shader.GetPropertyDescription(id);
			bool isEndParam = false;

			string[] attrs = shader.GetPropertyAttributes(id);
			if(attrs.Length > 0){
				for(int i = 0; i < attrs.Length; ++i){
					string attr = attrs[0];
					if(attr == "ASEEnd"){
						isEndParam = true;
					}

					if(attr.ToUpper() == attr){// all upper case? then it's a keyword
						float value = material.GetFloat(name);

						if(value <= 0.0f){
							material.DisableKeyword(attr);
						}
						else{
							material.EnableKeyword(attr);
						}
					}
				}
			}

			if(type == ShaderPropertyType.Float){
				float value = material.GetFloat(name);
				float newVal = EditorGUILayout.FloatField(desc, value);

				if(value != newVal){
					Undo.RecordObject(material, "changed clayxels material");

					material.SetFloat(name, newVal);

					if(!Application.isPlaying){
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}
				}
			}
			else if(type == ShaderPropertyType.Range){
				float value = material.GetFloat(name);
				Vector2 range = shader.GetPropertyRangeLimits(id);
				float newVal = EditorGUILayout.Slider(desc, value, range.x, range.y);

				if(value != newVal){
					Undo.RecordObject(material, "changed clayxels material");

					material.SetFloat(name, newVal);

					if(!Application.isPlaying){
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}
				}
			}
			else if(type == ShaderPropertyType.Texture){
				Texture value = material.GetTexture(name);
				Texture newVal = (Texture)EditorGUILayout.ObjectField(desc, value, typeof(Texture), false);

				if(value != newVal){
					Undo.RecordObject(material, "changed clayxels material");
					
					material.SetTexture(name, newVal);

					if(!Application.isPlaying){
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}
				}
			}
			else if(type == ShaderPropertyType.Color){
				Color value = material.GetColor(name);
				Color newVal = EditorGUILayout.ColorField(desc, value);

				if(value != newVal){
					Undo.RecordObject(material, "changed clayxels material");
					
					material.SetColor(name, newVal);

					if(!Application.isPlaying){
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}
				}
			}
			else if(type == ShaderPropertyType.Vector){
				Vector3 value = material.GetVector(name);
				Vector3 newVal = EditorGUILayout.Vector3Field(desc, value);

				if(value != newVal){
					Undo.RecordObject(material, "changed clayxels material");
					
					material.SetVector(name, newVal);

					if(!Application.isPlaying){
						EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
					}
				}
			}

			if(isEndParam){
				return false;
			}

			return true;
		}

		void OnDisable (){
			if(this.materialEditor != null) {
				DestroyImmediate(this.materialEditor);
				this.materialEditor = null;
			}
		}
	}

	[CustomEditor(typeof(ClayObject)), CanEditMultipleObjects]
	public class ClayObjectInspector : Editor{
		public override void OnInspectorGUI(){
			ClayObject clayObj = (ClayObject)this.targets[0];
			ClayContainer clayContainer = clayObj.getClayContainer();
			if(clayContainer == null || clayContainer.isFrozen()){
				return;
			}

			UnityEditor.EditorApplication.QueuePlayerLoopUpdate();

			EditorGUI.BeginChangeCheck();
			
			int primitiveType = 0;
			if(clayObj.mode != ClayObject.ClayObjectMode.clayGroup){
				string[] solidsLabels = clayContainer.getSolidsCatalogueLabels();
	 			primitiveType = EditorGUILayout.Popup("type", clayObj.primitiveType, solidsLabels);
	 		}

			float blend = EditorGUILayout.Slider("blend", Mathf.Abs(clayObj.blend) * 100.0f, 0.0f, 100.0f);
			if(clayObj.blend < 0.0f){
				if(blend < 0.001f){
					blend = 0.001f;
				}

				blend *= -1.0f;
			}

			blend *= 0.01f;
			if(blend > 1.0f){
				blend = 1.0f;
			}
			else if(blend < -1.0f){
				blend = -1.0f;
			}

			GUILayout.BeginHorizontal();

			Color defaultColor = GUI.backgroundColor;

			if(clayObj.blend >= 0.0f){
				GUI.backgroundColor = Color.yellow;
			}

			if(GUILayout.Button(new GUIContent("add", "Additive blend"))){
				blend = Mathf.Abs(blend);
			}
			
			GUI.backgroundColor = defaultColor;

			if(clayObj.blend < 0.0f){
				GUI.backgroundColor = Color.yellow;
			}

			if(GUILayout.Button(new GUIContent("sub", "Subtractive blend"))){
				if(blend == 0.0f){
					blend = 0.0001f;
				}

				blend = blend * -1.0f;
			}

			GUI.backgroundColor = defaultColor;

			GUILayout.EndHorizontal();

			EditorGUILayout.Space();

			GUILayout.BeginHorizontal();

			GUI.backgroundColor = defaultColor;

			bool isPainter = clayObj.getIsPainter();
			if(isPainter){
				GUI.backgroundColor = Color.yellow;
			}

			if(GUILayout.Button(new GUIContent("painter", "set this clayObject to be a painter that only affects colors on other clayObjects"))){
				clayObj.setIsPainter(!isPainter);
			}

			GUI.backgroundColor = defaultColor;

			bool mirror = clayObj.getMirror();
			if(mirror){
				GUI.backgroundColor = Color.yellow;
			}

			if(GUILayout.Button(new GUIContent("mirror", "mirror this clayObject on the X axis"))){
				clayObj.setMirror(!mirror);
			}

			GUI.backgroundColor = defaultColor;

			GUILayout.EndHorizontal();

			EditorGUILayout.Space();

			Color color = Color.white;
			Dictionary<string, float> paramValues = new Dictionary<string, float>();

	 		paramValues["x"] = clayObj.attrs.x;
	 		paramValues["y"] = clayObj.attrs.y;
	 		paramValues["z"] = clayObj.attrs.z;
	 		// paramValues["w"] = clayObj.attrs.w;
	 		paramValues["x2"] = clayObj.attrs2.x;
	 		paramValues["y2"] = clayObj.attrs2.y;
	 		paramValues["z2"] = clayObj.attrs2.z;
	 		paramValues["w2"] = clayObj.attrs2.w;

		 	if(clayObj.mode != ClayObject.ClayObjectMode.clayGroup){
		 		color = EditorGUILayout.ColorField("color", clayObj.color);

		 		List<string[]> parameters = ClayContainer.getSolidsCatalogueParameters(primitiveType);
		 		// List<string> wMaskLabels = new List<string>();
		 		for(int paramIt = 0; paramIt < parameters.Count; ++paramIt){
		 			string[] parameterValues = parameters[paramIt];
		 			string attr = parameterValues[0];
		 			string label = parameterValues[1];
		 			string defaultValue = parameterValues[2];
					
		 			if(primitiveType != clayObj.primitiveType){
		 				// reset to default params when changing primitive type
		 				paramValues[attr] = float.Parse(defaultValue, CultureInfo.InvariantCulture);
		 			}
		 			
		 			// if(attr.StartsWith("w")){
		 			// 	wMaskLabels.Add(label);
		 			// }
		 			// else{
		 				paramValues[attr] = EditorGUILayout.FloatField(label, paramValues[attr] * 100.0f) * 0.01f;
		 			// }
		 		}

		 		// if(wMaskLabels.Count > 0){
		 		// 	paramValues["w"] = (float)EditorGUILayout.MaskField("options", (int)clayObj.attrs.w, wMaskLabels.ToArray());
		 		// }
	 		}

	 		if(EditorGUI.EndChangeCheck()){
	 			ClayContainer._inspectorUpdate();
	 			ClayContainer._skipHierarchyChanges = true;
				
	 			Undo.RecordObjects(this.targets, "changed clayObject");

	 			for(int i = 1; i < this.targets.Length; ++i){
	 				bool somethingChanged = false;
	 				ClayObject currentClayObj = (ClayObject)this.targets[i];
	 				bool shouldAutoRename = false;

	 				if(Mathf.Abs(clayObj.blend - blend) > 0.001f || Mathf.Sign(clayObj.blend) != Mathf.Sign(blend)){
	 					currentClayObj.blend = blend;
	 					somethingChanged = true;
	 					shouldAutoRename = true;
	 				}

	 				if(clayObj.color != color){
	 					currentClayObj.color = color;
	 					somethingChanged = true;
	 				}
					
	 				if(clayObj.primitiveType != primitiveType){
	 					currentClayObj.primitiveType = primitiveType;
	 					somethingChanged = true;
	 					shouldAutoRename = true;
	 				}

	 				if(clayObj.attrs.x != paramValues["x"]){
	 					currentClayObj.attrs.x = paramValues["x"];
	 					somethingChanged = true;
	 				}

	 				if(clayObj.attrs.y != paramValues["y"]){
	 					currentClayObj.attrs.y = paramValues["y"];
	 					somethingChanged = true;
	 				}

	 				if(clayObj.attrs.z != paramValues["z"]){
	 					currentClayObj.attrs2.z = paramValues["z"];
	 					somethingChanged = true;
	 				}

	 				if(clayObj.attrs2.x != paramValues["x2"]){
	 					currentClayObj.attrs2.x = paramValues["x2"];
	 					somethingChanged = true;
	 				}

	 				if(clayObj.attrs2.y != paramValues["y2"]){
	 					currentClayObj.attrs2.y = paramValues["y2"];
	 					somethingChanged = true;
	 				}

	 				if(clayObj.attrs2.z != paramValues["z2"]){
	 					currentClayObj.attrs2.z = paramValues["z2"];
	 					somethingChanged = true;
	 				}

	 				if(clayObj.attrs2.w != paramValues["w2"]){
	 					currentClayObj.attrs2.w = paramValues["w2"];
	 					somethingChanged = true;
	 				}

	 				// if(clayObj.attrs.w != paramValues["w"]){
	 				// 	currentClayObj.attrs.w = paramValues["w"];
	 				// 	somethingChanged = true;
	 				// 	shouldAutoRename = true;
	 				// }

	 				if(somethingChanged){
	 					currentClayObj.getClayContainer().clayObjectUpdated(currentClayObj);

	 					if(shouldAutoRename){
		 					if(currentClayObj.gameObject.name.StartsWith("clay_")){
		 						clayContainer.autoRenameClayObject(currentClayObj);
		 					}
		 				}
	 				}

	 				ClayContainer._skipHierarchyChanges = false;
				}

	 			clayObj.blend = blend;
	 			clayObj.color = color;
	 			clayObj.primitiveType = primitiveType;
	 			clayObj.attrs.x = paramValues["x"];
	 			clayObj.attrs.y = paramValues["y"];
	 			clayObj.attrs.z = paramValues["z"];
	 			// clayObj.attrs.w = paramValues["w"];
	 			clayObj.attrs2.x = paramValues["x2"];
	 			clayObj.attrs2.y = paramValues["y2"];
	 			clayObj.attrs2.z = paramValues["z2"];
	 			clayObj.attrs2.w = paramValues["w2"];

	 			if(clayObj.gameObject.name.StartsWith("clay_")){
					clayContainer.autoRenameClayObject(clayObj);
				}

				clayObj.forceUpdate();
	 			
	 			UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
	 			ClayContainer.getSceneView().Repaint();

	 			if(!Application.isPlaying){
					EditorSceneManager.MarkSceneDirty(clayObj.gameObject.scene);
				}
			}

			EditorGUILayout.Space();

			EditorGUI.BeginChangeCheck();

			ClayObject.ClayObjectMode mode = (ClayObject.ClayObjectMode)EditorGUILayout.EnumPopup(
				new GUIContent("mode", 
					"change this clayObject into:\n\noffset: a series clones with an offset from each other\n\nspline: series of clones along a spline\n\nclayGroup: this clayObject becomes a group that can nest other clayObjects and blend them as a whole with the rest of your sculpt."), 
				clayObj.mode);
			
			if(EditorGUI.EndChangeCheck()){
				clayObj.setMode(mode);

				if(clayObj.gameObject.name.StartsWith("clay_") && mode == ClayObject.ClayObjectMode.clayGroup){
					clayContainer.autoRenameClayObject(clayObj);
				}

				UnityEditor.EditorApplication.QueuePlayerLoopUpdate();

				if(!Application.isPlaying){
					EditorSceneManager.MarkSceneDirty(clayContainer.gameObject.scene);
				}
			}

			EditorGUILayout.Space();

			if(clayObj.mode == ClayObject.ClayObjectMode.offset){
				this.drawOffsetMode(clayObj);
			}
			else if(clayObj.mode == ClayObject.ClayObjectMode.spline){
				this.drawSplineMode(clayObj);
			}
			else if(clayObj.mode == ClayObject.ClayObjectMode.clayGroup){
				this.drawClayGroupMode(clayObj);
			}

			EditorGUILayout.Space();
			GUILayout.BeginHorizontal();
			GUI.enabled = !clayContainer.isClayObjectsOrderLocked();
			int clayObjectId = EditorGUILayout.IntField("order", clayObj.clayObjectId);
			GUI.enabled = true;

			if(!clayContainer.isClayObjectsOrderLocked()){
				if(clayObjectId != clayObj.clayObjectId){
					int idOffset = clayObjectId - clayObj.clayObjectId; 
					clayContainer.reorderClayObject(clayObj.clayObjectId, idOffset);
				}
			}

			if(GUILayout.Button(new GUIContent("↑", ""))){
				clayContainer.reorderClayObject(clayObj.clayObjectId, -1);
			}
			if(GUILayout.Button(new GUIContent("↓", ""))){
				clayContainer.reorderClayObject(clayObj.clayObjectId, 1);
			}
			if(GUILayout.Button(new GUIContent("⋮", ""))){
				EditorUtility.DisplayPopupMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), "Component/Clayxels/ClayObject", null);
			}
			GUILayout.EndHorizontal();
		}

		[MenuItem("Component/Clayxels/ClayObject/Mirror Duplicate (m)")]
    	static void MirrorDuplicate(MenuCommand command){
    		ClayContainer.shortcutMirrorDuplicate();
    	}

		[MenuItem("Component/Clayxels/ClayObject/Unlock Order From Hierarchy")]
    	static void OrderFromHierarchyOff(MenuCommand command){
    		if(UnityEditor.Selection.gameObjects.Length > 0){
    			ClayObject clayObj = UnityEditor.Selection.gameObjects[0].GetComponent<ClayObject>();
    			if(clayObj != null){
    				clayObj.getClayContainer().setClayObjectsOrderLocked(false);
    			}
    		}
    	}

    	[MenuItem("Component/Clayxels/ClayObject/Lock Order To Hierarchy")]
    	static void OrderFromHierarchyOn(MenuCommand command){
    		if(UnityEditor.Selection.gameObjects.Length > 0){
    			ClayObject clayObj = UnityEditor.Selection.gameObjects[0].GetComponent<ClayObject>();
    			if(clayObj != null){
    				clayObj.getClayContainer().setClayObjectsOrderLocked(true);
    			}
    		}
    	}

		[MenuItem("Component/Clayxels/ClayObject/Send Before ClayObject")]
    	static void sendBeforeClayObject(MenuCommand command){
    		if(UnityEditor.Selection.gameObjects.Length > 0){
    			ClayObject clayObj = UnityEditor.Selection.gameObjects[0].GetComponent<ClayObject>();
    			if(clayObj != null){
    				clayObj.getClayContainer().selectToReorder(clayObj, 0);
    			}
    		}
    	}

		[MenuItem("Component/Clayxels/ClayObject/Send After ClayObject")]
    	static void sendAfterClayObject(MenuCommand command){
    		if(UnityEditor.Selection.gameObjects.Length > 0){
    			ClayObject clayObj = UnityEditor.Selection.gameObjects[0].GetComponent<ClayObject>();
    			if(clayObj != null){
    				clayObj.getClayContainer().selectToReorder(clayObj, 1);
    			}
    		}
    	}

    	[MenuItem("Component/Clayxels/ClayObject/Rename all ClayObjects to Animate")]
    	static void renameToAnimate(MenuCommand command){
    		if(UnityEditor.Selection.gameObjects.Length > 0){
    			ClayObject clayObj = UnityEditor.Selection.gameObjects[0].GetComponent<ClayObject>();
    			if(clayObj != null){
    				ClayContainer container = clayObj.getClayContainer();
    				ClayContainer._skipHierarchyChanges = true;// otherwise each rename will trigger onHierarchyChange

    				int numClayObjs = container.getNumClayObjects();

    				for(int i = 0; i < numClayObjs; ++i){
    					ClayObject currentClayObj = container.getClayObject(i);

    					if(currentClayObj.gameObject.name.StartsWith("clay_")){
    						container.autoRenameClayObject(currentClayObj);
    						currentClayObj.name = "(" + i + ")" + currentClayObj.gameObject.name;
    					}
    				}

    				ClayContainer._skipHierarchyChanges = false;
    			}
    		}
    	}

    	void drawClayGroupMode(ClayObject clayObj){
    		if(GUILayout.Button(new GUIContent("add clay", "add clay inside this clayGroup"))){
				ClayObject childClayObj = clayObj.getClayContainer().addClayObject();

				childClayObj.transform.parent = clayObj.transform;
				childClayObj.transform.localPosition = Vector3.zero;
				childClayObj.transform.localEulerAngles = Vector3.zero;

				Undo.RegisterCreatedObjectUndo(childClayObj.gameObject, "added clayObject");
				UnityEditor.Selection.objects = new GameObject[]{childClayObj.gameObject};

				clayObj.getClayContainer().scheduleClayObjectsScan();

				if(!Application.isPlaying){
					EditorSceneManager.MarkSceneDirty(clayObj.gameObject.scene);
				}

				return;
			}
    	}

		void drawSplineMode(ClayObject clayObj){
			EditorGUI.BeginChangeCheck();

			int subdivs = EditorGUILayout.IntField("subdivs", clayObj.getSplineSubdiv());

			GUILayout.BeginHorizontal();

			int numPoints = clayObj.splinePoints.Count - 2;
			EditorGUILayout.LabelField("control points: " + numPoints);

			if(GUILayout.Button(new GUIContent("+", ""))){
				clayObj.addSplineControlPoint();
			}

			if(GUILayout.Button(new GUIContent("-", ""))){
				clayObj.removeLastSplineControlPoint();
			}

			GUILayout.EndHorizontal();

			// var list = this.serializedObject.FindProperty("splinePoints");
			// EditorGUILayout.PropertyField(list, new GUIContent("spline points"), true);

			if(EditorGUI.EndChangeCheck()){
				// this.serializedObject.ApplyModifiedProperties();

				clayObj.setSplineSubdiv(subdivs);

				UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
			}
		}

		void drawOffsetMode(ClayObject clayObj){
			EditorGUI.BeginChangeCheck();
				
			int numSolids = EditorGUILayout.IntField("solids", clayObj.getNumSolids());
			bool allowSceneObjects = true;
			clayObj.offsetter = (GameObject)EditorGUILayout.ObjectField("offsetter", clayObj.offsetter, typeof(GameObject), allowSceneObjects);
			
			if(EditorGUI.EndChangeCheck()){
				if(numSolids < 1){
					numSolids = 1;
				}
				else if(numSolids > 100){
					numSolids = 100;
				}

				clayObj.setOffsetNum(numSolids);
				
				UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
			}
		}
	}
}

public class ClayxelsPrefsWindow : EditorWindow{
	static ClayxelsPrefs prefs;
	static bool somethingChanged = false;

    [MenuItem("Component/Clayxels/Config")]
    public static void Open(){
    	if(Application.isPlaying){
    		return;
    	}

    	ClayxelsPrefsWindow.somethingChanged = false;

    	ClayxelsPrefsWindow.prefs = ClayContainer.loadPrefs();

        ClayxelsPrefsWindow window = (ClayxelsPrefsWindow)EditorWindow.GetWindow(typeof(ClayxelsPrefsWindow));
        window.Show();
    }

    void OnLostFocus(){
    	if(Application.isPlaying){
    		return;
    	}
    	
    	ClayContainer.savePrefs(ClayxelsPrefsWindow.prefs);
    }

    void OnGUI(){
    	if(Application.isPlaying){
    		return;
    	}

    	if(GUILayout.Button("Open news window!")){
	    	ClayxelsIntroWindow.Open();
	    }

	    EditorGUILayout.Space();

    	if(ClayxelsPrefsWindow.prefs == null){
    		ClayxelsPrefsWindow.prefs = ClayContainer.loadPrefs();
    	}

    	EditorGUI.BeginChangeCheck();

    	Color boundsColor = new Color((float)ClayxelsPrefsWindow.prefs.boundsColor[0] / 255.0f, (float)ClayxelsPrefsWindow.prefs.boundsColor[1] / 255.0f, (float)ClayxelsPrefsWindow.prefs.boundsColor[2] / 255.0f, (float)ClayxelsPrefsWindow.prefs.boundsColor[3] / 255.0f);
    	boundsColor = EditorGUILayout.ColorField(new GUIContent("boundsColor", "Color of the bounds indicator in the viewport, enable Gizmos in the viewport to see this."), boundsColor);
    	ClayxelsPrefsWindow.prefs.boundsColor[0] = (byte)(boundsColor.r * 255);
    	ClayxelsPrefsWindow.prefs.boundsColor[1] = (byte)(boundsColor.g * 255);
    	ClayxelsPrefsWindow.prefs.boundsColor[2] = (byte)(boundsColor.b * 255);
    	ClayxelsPrefsWindow.prefs.boundsColor[3] = (byte)(boundsColor.a * 255);

    	ClayxelsPrefsWindow.prefs.directPickEnabled = EditorGUILayout.Toggle(new GUIContent("direct picking", "Enable this to pick clay without having to use a shortcut, disable it if you don't want this behaviour to interfer with unity's standard scene picking."), ClayxelsPrefsWindow.prefs.directPickEnabled);
    	
    	if(!ClayxelsPrefsWindow.prefs.directPickEnabled){
    		ClayxelsPrefsWindow.prefs.pickingKey = EditorGUILayout.TextField(new GUIContent("picking shortcut", "Press this shortcut to pick/select containers and clayObjects in scene."), ClayxelsPrefsWindow.prefs.pickingKey);
    	}
    	
    	ClayxelsPrefsWindow.prefs.mirrorDuplicateKey = EditorGUILayout.TextField(new GUIContent("mirrorDuplicate shortcut", "Press this shortcut to duplicate and mirror a clayObject on the X axis."), ClayxelsPrefsWindow.prefs.mirrorDuplicateKey);

    	string[] pointCountPreset = new string[]{"low", "mid", "high"};
    	ClayxelsPrefsWindow.prefs.maxPointCount = EditorGUILayout.Popup(new GUIContent("pointCloud memory", "Preset to allocate video ram to handle bigger point clouds."), ClayxelsPrefsWindow.prefs.maxPointCount, pointCountPreset);
    	
    	string[] solidsCountPreset = new string[]{"low", "mid", "high"};
    	ClayxelsPrefsWindow.prefs.maxSolidsCount = EditorGUILayout.Popup(new GUIContent("clayObjects memory", "Preset to allocate video ram to handle more clayObjects per container."), ClayxelsPrefsWindow.prefs.maxSolidsCount, solidsCountPreset);
    	
    	string[] solidsPerVoxelPreset = new string[]{"best performance", "balanced", "max sculpt detail"};
    	ClayxelsPrefsWindow.prefs.maxSolidsPerVoxel = EditorGUILayout.Popup(new GUIContent("clayObjects per voxel", "Preset to handle more clayObjects per voxel, it might fix some artifacts caused by having a lot of clayObjects all close to each other."), ClayxelsPrefsWindow.prefs.maxSolidsPerVoxel, solidsPerVoxelPreset);
    	
    	int frameSkip = EditorGUILayout.IntField(new GUIContent("frame skip", ""), ClayxelsPrefsWindow.prefs.frameSkip);
    	if(frameSkip < 0){
    		frameSkip = 0;
    	}
    	else if(frameSkip > 100){
    		frameSkip = 100;
    	}
    	ClayxelsPrefsWindow.prefs.frameSkip = frameSkip;

    	int maxBounds = EditorGUILayout.IntField(new GUIContent("max bounds size", "Smaller bounds use less video memory but give you less space to work with."), ClayxelsPrefsWindow.prefs.maxBounds);
    	if(maxBounds < 1){
    		maxBounds = 1;
    	}
    	else if(maxBounds > 3){
    		maxBounds = 3;
    	}
    	ClayxelsPrefsWindow.prefs.maxBounds = maxBounds;

    	ClayxelsPrefsWindow.prefs.limitSmoothMeshMemory = EditorGUILayout.Toggle(
				new GUIContent("limit smoothMesh memory", "Use smaller bounds for smoothMesh render-mode to avoid consuming too much vram memory."), 
				ClayxelsPrefsWindow.prefs.limitSmoothMeshMemory);

    	ClayxelsPrefsWindow.prefs.globalBlend = EditorGUILayout.Slider(new GUIContent("global blend", 
    		"The max amount of blend between clayObjects. Reduce it to increase performance when updating clay."), 
    		ClayxelsPrefsWindow.prefs.globalBlend, 0.0f, 2.0f);

    	if(ClayContainer.getRenderPipe() != "builtin"){
    		EditorGUILayout.Space();
    		EditorGUILayout.LabelField("Microvoxel options:");
    		ClayxelsPrefsWindow.prefs.renderSize = EditorGUILayout.Vector2IntField(new GUIContent("render target resolution", "Set the output pixel resolution used by the microvoxelSplats renderer. For best performance set this to half of your output resolution and enable Temporal Anti Aliasing on the main Camera."), ClayxelsPrefsWindow.prefs.renderSize);
    		
    		ClayxelsPrefsWindow.prefs.globalMicrovoxelSplatsQuality = EditorGUILayout.Slider(new GUIContent("global splats quality", 
	    		"Set the quality of microvoxel splats, 1.0 will get you beautiful textured splats that overlap with each other when close to camera, 0.0 will display blocky splats. Use this value to speedup the rendering of microvoxel containers."), 
	    		ClayxelsPrefsWindow.prefs.globalMicrovoxelSplatsQuality, 0.0f, 1.0f);

    		ClayxelsPrefsWindow.prefs.globalMicrovoxelRayIterations = EditorGUILayout.Slider(new GUIContent("global ray iterations", 
	    		"Set how far a ray will travel inside a microvoxel container. 1.0 will traverse every voxel in the container until a hit is found. 0.5 will stop the ray early and potentially miss the voxel that needs to be rendered. Use this value to speedup the rendering of microvoxel containers."), 
	    		ClayxelsPrefsWindow.prefs.globalMicrovoxelRayIterations, 0.5f, 1.0f);

    		ClayxelsPrefsWindow.prefs.microvoxelCameraCanGetInside = EditorGUILayout.Toggle(
				new GUIContent("camera can go inside", "Turning this on will allow the camera to go inside a microvoxel container, slows down rendering but lets you get very close with the camera while the game is running (has no effect while in editor viewport)."), 
				ClayxelsPrefsWindow.prefs.microvoxelCameraCanGetInside);

    		EditorGUILayout.Space();
    	}

    	if(EditorGUI.EndChangeCheck()){
    		ClayxelsPrefsWindow.somethingChanged = true;
    	}

    	EditorGUILayout.Space();

	    int[] memStats = ClayContainer.getMemoryStats();
    	EditorGUILayout.LabelField("- vram rough usage -");
	    EditorGUILayout.LabelField("upfront vram allocated: " + memStats[0] + "MB");
	    EditorGUILayout.LabelField("containers in scene: " + memStats[1] + "MB");

	    EditorGUILayout.Space();

	    Color defaultColor = GUI.backgroundColor;
	    if(ClayxelsPrefsWindow.somethingChanged){
	    	GUI.backgroundColor = Color.yellow;
	    }

		if(GUILayout.Button((new GUIContent("reload all", "This is necessary after you make changes to the shaders or to the claySDF file.")))){
			ClayContainer.savePrefs(ClayxelsPrefsWindow.prefs);
			ClayContainer.reloadAll();

			ClayxelsPrefsWindow.prefs = ClayContainer.loadPrefs();

			ClayxelsPrefsWindow.somethingChanged = false;
		}

		GUI.backgroundColor = defaultColor;
    }

    public class ClayxelsIntroWindow : EditorWindow{
    	static Texture headerTexture = null;
    	static UnityWebRequest webRequest = null;
    	static ClayxelsPrefs prefs = null;
    	string newsStr = "";
    	string errorStr = "Looks like we can't show you news using our servers right now, never mind, nothing to see here, go on with your day, enjoy Clayxels : )";
    	bool loading = true;
    	
    	public static void Open(){
    		if(EditorWindow.HasOpenInstances<ClayxelsIntroWindow>()){
	    		return;
	    	}

	    	ClayxelsIntroWindow.prefs = ClayContainer.loadPrefs();

	    	ClayxelsIntroWindow window = (ClayxelsIntroWindow)EditorWindow.GetWindow(typeof(ClayxelsIntroWindow));
	        window.titleContent = new GUIContent("Welcome to Clayxels!");
	        window.minSize = new Vector2(640, 500);
			Rect position = window.position;
			position.center = new Rect(0.0f, 0.0f, Screen.currentResolution.width, Screen.currentResolution.height).center;
			window.position = position;
	        window.Show();

	        if(ClayxelsIntroWindow.headerTexture == null){
	        	ClayxelsIntroWindow.headerTexture = (Texture)Resources.Load("clayxelsHeader", typeof(Texture));
	        }

	        ClayxelsIntroWindow.webRequest = UnityWebRequest.Get("https://www.clayxels.com/internal");
			ClayxelsIntroWindow.webRequest.SendWebRequest();
    	}

    	void Update(){
    		if(this.newsStr == ""){
    			if(ClayxelsIntroWindow.webRequest != null){
	    			if(ClayxelsIntroWindow.webRequest.isDone){
	    				this.loading = false;

	    				string pageText = ClayxelsIntroWindow.webRequest.downloadHandler.text;

	    				string[] tokens = pageText.Split(new[]{"<div class=\"paragraph\">!clayxelsNewsBlock!"}, StringSplitOptions.None);
	    				if(tokens.Length > 1){
	    					string[] subTokens = tokens[1].Split(new[]{"</div>"}, StringSplitOptions.None);
	    					if(subTokens.Length > 0){
	    						this.newsStr = subTokens[0];
	    					}
	    				}

	    				if(this.newsStr == ""){
	    					this.newsStr = this.errorStr;
	    				}

	    				this.Repaint();
	    			}
	    			else if(ClayxelsIntroWindow.webRequest.isNetworkError){
	    				this.loading = false;
	    				this.newsStr = this.errorStr;

	    				this.Repaint();
	    			}
	    		}
	    	}
    	}

    	void OnGUI(){
    		GUIStyle s = new GUIStyle();
			s.wordWrap = true;
			s.fontSize = 20;
			
			GUI.skin.button.stretchWidth = false;

    		if(ClayxelsIntroWindow.headerTexture != null){
    			GUI.DrawTexture(new Rect(0, 0, 640, 201), ClayxelsIntroWindow.headerTexture);

    			for(int i = 0; i < 34; ++i){
    				EditorGUILayout.Space();
    			}
    		}

    		if(!this.loading){
    			if(this.newsStr == ""){
    				this.newsStr = this.errorStr;
    			}
    			try{
	    			string[] tokens = this.newsStr.Split('@');
	    			for(int i = 0; i < tokens.Length; ++i){
	    				if(tokens[i].StartsWith("link:")){
	    					string linkStr = tokens[i].Replace("link:", "");
	    					string[] linkTokens = linkStr.Split(',');

	    					if(GUILayout.Button(linkTokens[0])){
	    						Application.OpenURL(linkTokens[1]);
	    					}
	    				}
	    				else{
	    					EditorGUILayout.LabelField(tokens[i], s);
	    				}
	    			}
	    		}
	    		catch{
	    			EditorGUILayout.LabelField("Failed to show server message, oops.");
	    		}
    		}

    		GUILayout.FlexibleSpace();

    		ClayxelsIntroWindow.prefs.showStartupNews = EditorGUILayout.Toggle("Show news on startup", ClayxelsIntroWindow.prefs.showStartupNews);
    		
    		EditorGUILayout.Space();

    		GUI.skin.button.stretchWidth = true;

    		if(GUILayout.Button("Close")){
    			ClayContainer.savePrefs(ClayxelsIntroWindow.prefs);
    			this.Close();
    		}
    	}
    }

    public class ClayxelsMessageWindow : EditorWindow{
    	public delegate void ClayxelsMessageWindoCallback();
		static public ClayxelsMessageWindoCallback onClosedCallback = null;

		static string message = "";

	    public static void Open(string msg){
	    	if(EditorWindow.HasOpenInstances<ClayxelsMessageWindow>() || msg == ""){
	    		return;
	    	}

	    	ClayxelsMessageWindow.message = msg;

	        ClayxelsMessageWindow window = (ClayxelsMessageWindow)EditorWindow.GetWindow(typeof(ClayxelsMessageWindow));
	        window.titleContent = new GUIContent("Clayxels Message");
	        window.minSize = new Vector2(500, 200);
	        window.maxSize = new Vector2(500, 200);
	        window.Show();
	    }

	    void OnGUI(){
	    	EditorGUILayout.Space();

	    	GUIStyle s = new GUIStyle();
			s.wordWrap = true;

	    	EditorGUILayout.LabelField(ClayxelsMessageWindow.message, s);

	    	EditorGUILayout.Space();
	    	EditorGUILayout.Space();
			
			GUILayout.FlexibleSpace();
			if(GUILayout.Button("Ok")){
				this.OnDestroy();

				this.Close();
			}
    	}

    	void OnDestroy(){
    		if(ClayxelsMessageWindow.onClosedCallback != null){
    			ClayxelsMessageWindow.onClosedCallback();

    			ClayxelsMessageWindow.onClosedCallback = null;
    		}
    	}
    }

    [InitializeOnLoad]
	public class ClayxelsEditorInit{
	    static ClayxelsEditorInit(){
		    if(EditorSettings.enterPlayModeOptions == EnterPlayModeOptions.DisableDomainReload){
		    	Debug.Log("Clayxels Warning: Domain Reload is disabled and will cause issues with resetting some of clayxels internal variables.");
		    }

		    string renderPipeAsset = "";

		    try{
				if(GraphicsSettings.renderPipelineAsset != null){
					renderPipeAsset = GraphicsSettings.renderPipelineAsset.GetType().Name;
				}
			}
			catch{
			}
			
			if(renderPipeAsset == "HDRenderPipelineAsset"){
				#if UNITY_EDITOR_WIN
		    		PlayerSettings.allowUnsafeCode = true;
			    	ClayxelsEditorInit.setDefine("CLAYXELS_RETOPO");
		    	#endif 

				ClayxelsEditorInit.setDefine("CLAYXELS_HDRP");
				ClayxelsEditorInit.checkPackage("HDRP");

				bool isNewHDRP = false;
				int majorVersion = int.Parse(Application.unityVersion.Split('.')[0]);
            	int minorVersion = int.Parse(Application.unityVersion.Split('.')[1]);
            	if(majorVersion > 2020){
        			isNewHDRP = true;
        		}
        		else if(majorVersion >= 2020 && minorVersion > 1){
            		isNewHDRP = true;
            	}
            
            	if(isNewHDRP){
            		string[] oldShaderAssets = AssetDatabase.FindAssets("ClayxelHDRPMeshShader.shader");
            		if(oldShaderAssets.Length > 0){
            			string oldShader = AssetDatabase.GUIDToAssetPath(oldShaderAssets[0]);
            			AssetDatabase.DeleteAsset(oldShader);
            		}
            		
            		oldShaderAssets = AssetDatabase.FindAssets("clayxelHDRPShader.shader");
            		if(oldShaderAssets.Length > 0){
            			string oldShader = AssetDatabase.GUIDToAssetPath(oldShaderAssets[0]);
            			AssetDatabase.DeleteAsset(oldShader);
            		}

            		oldShaderAssets = AssetDatabase.FindAssets("ClayxelHDRPShaderMicroVoxelASE.shader");
            		if(oldShaderAssets.Length > 0){
            			string oldShader = AssetDatabase.GUIDToAssetPath(oldShaderAssets[0]);
            			AssetDatabase.DeleteAsset(oldShader);
            		}
            	}
			}
			else if(renderPipeAsset == "UniversalRenderPipelineAsset"){
				#if UNITY_EDITOR_WIN
		    		PlayerSettings.allowUnsafeCode = true;
			    	ClayxelsEditorInit.setDefine("CLAYXELS_RETOPO");
		    	#endif 

				ClayxelsEditorInit.setDefine("CLAYXELS_URP");
				ClayxelsEditorInit.checkPackage("URP");
			}
			else{
				if(!ClayxelsEditorInit.checkDefined("CLAYXELS_BUILTIN")){
					string msg = "";

					// #if UNITY_EDITOR_WIN
						msg = "Hi! Clayxels has more render options when using the URP and HDRP render pipelines.\nPlease consider switching pipeline to get the best out of this tool.";
					// #else
					// 	msg = "Hi! Clayxels on Mac OS is meant to be used from the URP or HDRP render pipelines.\n";
					// #endif

					// this errors on 2021.1
					// ClayxelsMessageWindow.Open();
					// ClayxelsMessageWindow.onClosedCallback = ClayxelsEditorInit.finalizeBuiltinInit;
					
					if(EditorUtility.DisplayDialog("Clayxels Message", msg, "ok")){
						ClayxelsEditorInit.finalizeBuiltinInit();
					}
				}
				else{
					ClayxelsEditorInit.finalizeBuiltinInit();
				}
			}

			ClayContainer.showNewsCallback = ClayxelsIntroWindow.Open;
	    }

	    static void finalizeBuiltinInit(){
	    	#if UNITY_EDITOR_WIN
	    		PlayerSettings.allowUnsafeCode = true;
		    	ClayxelsEditorInit.setDefine("CLAYXELS_RETOPO");
	    	#endif 

	    	ClayxelsEditorInit.setDefine("CLAYXELS_BUILTIN");
	    	ClayxelsEditorInit.checkPackage("BuiltIn");
	    }

	    static bool checkDefined(string defstr){
	    	string currDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup( EditorUserBuildSettings.selectedBuildTargetGroup);
	        if(!currDefines.Contains(defstr)){
	        	return false;
	        }

	        return true;
	    }

	    static void setDefine(string defstr){
	    	string currDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup( EditorUserBuildSettings.selectedBuildTargetGroup);
	        if(!currDefines.Contains(defstr)){
	    		PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, defstr + ";" + currDefines);
	    		
	    		// this is a new project, reset news popup window to make it appear again
	    		EditorPrefs.SetInt("clayxelsNews" + ClayContainer.version, 0);
	    	}
	    }

		static void checkPackage(string renderPipe){
			string packageName = "clayxelShaders" + renderPipe;

			string[] packageAssets = AssetDatabase.FindAssets(packageName);

			string[] shaderAssets = AssetDatabase.FindAssets("clayxel" + renderPipe + "Shader");
			
			if(packageAssets.Length == 0 && shaderAssets.Length == 0){
				Debug.Log("Clayxels: you appear to be missing the relevant package for this render pipeline, please reimport Clayxels from the Asset Store making sure to include this package: " + packageName);
			}
			else if(shaderAssets.Length == 0 && packageAssets.Length > 0){
				AssetDatabase.ImportPackage(AssetDatabase.GUIDToAssetPath(packageAssets[0]), false);
			}
		}
	}
}

#endif // end if UNITY_EDITOR
