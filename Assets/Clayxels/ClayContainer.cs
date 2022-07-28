
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Reflection;
#endif

namespace Clayxels{
	
	/*!\brief This class is the main interface to work with Clayxels, it is designed to work in editor and in game.
		Each container nests one or more ClayObject as children to generate the final clay result.
	*/
	[ExecuteInEditMode, DefaultExecutionOrder(-999)]
	public class ClayContainer : MonoBehaviour{
		public static string version = "1.9.21";

		/*!\brief An interactive container can be changed while the game is running, 
			switching interactive OFF saves on vram memory. 
			You can switch containers to interactive while the game is running. 
		*/
		public void setInteractive(bool state){
			if(state == this.interactive){
				return;
			}

			this.interactive = state;

			if(this.needsInit){
				this.init();
			}
			
			if(state){
				this.expandMemory();
				
				this.needsUpdate = true;

				if(Application.isPlaying){
					// switch on clayobjects but only if in playMode
					// we don't want to switch them back off though, it's up to the user at that point
					this.enableAllClayObjects(true);
				}
			}
			else{
				this.optimizeMemory();

				this.needsUpdate = false;
			}
		}

		public bool isInteractive(){
			return this.interactive;
		}

		public void setVisible(bool state){
			this.visible = state;
			this.enabled = state;
		}

		public bool isVisible(){
			return this.visible;
		}

		/*!\brief Check if this container will set the bounds automatically. */
		public bool isAutoBoundsActive(){
			return this.autoBounds;
		}

		/*!\brief Set this container to update its bounds automatically.
			The bounds of a clayContainer are used to increase the work area in wich clay is processed and displayed. */
		public void setAutoBoundsActive(bool state){
			this.autoBounds = state;

			this.needsInit = true;

			if(Application.isPlaying){
				this.setInteractive(true);
			}

			this.init();

			#if UNITY_EDITOR
				this.editingThisContainer = true;
				this.needsUpdate = true;
				
				UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
				ClayContainer.getSceneView().Repaint();
			#endif
		}

		/*!\brief Max number of points per chunk (number of chunks is set via boundsScale on the inspector)
        	this only affects video memory while sculpting or moving clayObjects at runtime.
        	num: a bounds size of 1,1,1 can have a max of (256^3) points. 
        	Since 3,3,3 bounds is the max allowed, you can have a max of (256*256*256) * (3*3*3) points.
        	If the user runs out of points, a warning will be issued and logged using clayContainer.getUserWarning();
        	bufferSizeReduceFactor: allows for a size reduction of some of the heavier buffers, min 0.5 (half zize) to max 1.0 (no size reduction)*/
		public static void setPointCloudLimit(int num, float bufferSizeReduceFactor = 1.0f){
			if(num < 256 * 256 * 256){
				num = 256 * 256 * 256;
			}
			else if(num > (256 * 256 * 256) * 27){
				num = (256 * 256 * 256) * 27;
			}

			if(bufferSizeReduceFactor < 0.01f){
				bufferSizeReduceFactor = 0.01f;
			}
			else if(bufferSizeReduceFactor > 1.0f){
				bufferSizeReduceFactor = 1.0f;
			}

			ClayContainer.maxPointCount = num;
			ClayContainer.bufferSizeReduceFactor = bufferSizeReduceFactor;
			ClayContainer.globalDataNeedsInit = true;
		}
		
		/*!\brief Skip N frames before updating to reduce stress on GPU and increase FPS count. 
			See ClayxelPrefs.cs */
		public static void setUpdateFrameSkip(int frameSkip){
			ClayContainer.frameSkip = frameSkip;
		}

		/*!\brief Set this from 0.0 to 1.0 in order to reduce the max blend that a clayObject can have,
			the smaller the number the better performance will be when evaluating clay. */
		public static void setGlobalBlend(float value){
			if(value < 0.0f){
				value = 0.0f;
			}

			ClayContainer.globalBlend = value;
		}

		/*!\brief How many soldis can this container work with in total.
			Valid values: 64, 128, 256, 512, 1024, 4096, 16384
			See ClayxelPrefs.cs */
		public static void setMaxSolids(int num){
			if(!ClayContainer.prefsOverridden){
				ClayContainer.prefsOverridden = true;
				ClayContainer.applyPrefs();
			}

			ClayContainer.maxSolids = num;
			ClayContainer.globalDataNeedsInit = true;
		}

		/*!\brief Limit the bounds in x,y,z dimentions. 
			A value of 1 will get you a small area to work with but will use very little video ram.
			A value of 3 is the max limit and it will give you a very large area at the expense of more video ram.
			Video ram needed is occupied upfront, you don't pay this cost for each new container.
			
			limitSmoothMeshMemory will limit the bounds to 2,2,2 max to avoid using too much vram.
			*/
		public static void setMaxBounds(int value, bool limitSmoothMeshMemory = true){
			if(!ClayContainer.prefsOverridden){
				ClayContainer.prefsOverridden = true;
				ClayContainer.applyPrefs();
			}

			if(value < 1){
				value = 1;
			}
			else if(value > 3){
				value = 3;
			}

			ClayContainer.limitSmoothMeshMemory = limitSmoothMeshMemory;

			ClayContainer.maxChunkX = value;
			ClayContainer.maxChunkY = value;
			ClayContainer.maxChunkZ = value;
			ClayContainer.totalMaxChunks = value * value * value;
			ClayContainer.globalDataNeedsInit = true;
		}

		/*!\brief Get the global bounds limit. */
		public static int getMaxBounds(){
			return ClayContainer.maxChunkX;
		}

		/*!\brief How many solids can stay one next to another while occupying the same voxel.
			Keeping this value low will increase overall performance but will cause disappearing clayxels if the number is exceeded.
			Valid values: 32, 64, 128, 256, 512, 1024, 2048
			See ClayxelPrefs.cs */
		public static void setMaxSolidsPerVoxel(int num){
			if(!ClayContainer.prefsOverridden){
				ClayContainer.prefsOverridden = true;
				ClayContainer.applyPrefs();
			}

			ClayContainer.maxSolidsPerVoxel = num;
			ClayContainer.globalDataNeedsInit = true;
		}

		/*!\brief Sets how finely detailed are your clayxels, range 0 to 100.*/
		public void setClayxelDetail(int value){
			if(value == this.clayxelDetail){
				return;
			}

			this.clayxelDetail = value;

			if(this.frozen){
				return;
			}

			if(this.needsInit){
				this.init();
			}

			if(Application.isPlaying){
				this.setInteractive(true);
			}

			this.forceUpdateAllSolids();

			if(ClayContainer.lastUpdatedContainerId != this.containerId){
				this.switchComputeData();
			}

			if(this.memoryOptimized){
				this.expandMemory();
			}

			this.editingThisContainer = true;

			this.updateInternalBounds();

			this.computeClay();
		}

		/*!\brief Get the value specified by setClayxelDetail()*/		
		public int getClayxelDetail(){
			return this.clayxelDetail;
		}

		/*!\brief Limits the work area available for a sculpt, a value of 1 will perform better and consume less video memory, a value of 3 will give you more detail but will require more gpu resources.*/
		public void setAutoBoundsLimit(int value){
			if(value == this.autoBoundsLimit){
				return;
			}

			this.autoBoundsLimit = Mathf.Clamp(value, 1, ClayContainer.maxChunkX);

			if(this.frozen){
				return;
			}

			if(this.needsInit){
				this.init();
			}

			if(Application.isPlaying){
				this.setInteractive(true);
			}

			this.forceUpdateAllSolids();

			if(ClayContainer.lastUpdatedContainerId != this.containerId){
				this.switchComputeData();
			}

			if(this.memoryOptimized){
				this.expandMemory();
			}

			this.editingThisContainer = true;

			this.updateInternalBounds();

			this.computeClay();
		}

		public int getAutoBoundsLimit(){
			return this.autoBoundsLimit;
		}

		/*!\brief Determines how much work area you have for your sculpt within this container.
			These values are not expressed in scene units, 
			the final size of this container is determined by the value specified with setClayxelDetail().
			Performance tip: The bigger the bounds, the slower this container will be to compute clay in-game.*/
		public void setBoundsScale(int x, int y, int z){
			this.chunksX = x;
			this.chunksY = y;
			this.chunksZ = z;
			this.limitChunkValues();

			this.needsInit = true;

			if(Application.isPlaying){
				this.setInteractive(true);
			}
			else{
				#if UNITY_EDITOR
					this.init();

					this.editingThisContainer = true;
					
					UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
				#endif
			}
		}

		/*!\brief Get the values specified by setBoundsScale()*/		
		public Vector3Int getBoundsScale(){
			return new Vector3Int(this.chunksX, this.chunksY, this.chunksZ);
		}

		/*!\brief How many solids can a container work with.*/
		public int getMaxSolids(){
			return ClayContainer.maxSolids;
		}

		/*!\brief How many solids are currently used in this container.*/
		public int getNumSolids(){
			return this.solids.Count;
		}

		/*!\brief How many ClayObjects currently in this container, each ClayObject will spawn a certain amount of Solids.*/
		public int getNumClayObjects(){
			return  this.clayObjects.Count;
		}

		/*!\brief Invoke this after adding a new ClayObject in scene to have the container notified instantly.*/
		public void scanClayObjectsHierarchy(){
			this.clayObjects.Clear();
			this.solidsUpdatedDict.Clear();
			this.solids.Clear();
			
			List<ClayObject> collectedClayObjs = new List<ClayObject>();
			this.scanRecursive(this.transform, collectedClayObjs);
			
			for(int i = 0; i < collectedClayObjs.Count; ++i){
				this.collectClayObject(collectedClayObjs[i]);
			}

			this.solidsHierarchyNeedsScan = false;

			if(this.numChunks == 1){
				this.genericIntBufferArray[0] = this.solids.Count;
				ClayContainer.numSolidsPerChunkBuffer.SetData(this.genericIntBufferArray);
			}
		}

		/*!\brief Get and own the list of solids in this container. 
			Useful when you don't want a heavy hierarchy of ClayObject in scene (ex. working with particles). */
		public List<Solid> getSolids(){
			return this.solids;
		}

		/*!\brief If you work directly with the list of solids in this container, invoke this to notify when a solid has changed.*/
		public void solidUpdated(int id){
			if(id < ClayContainer.maxSolids){
				this.solidsUpdatedDict[id] = 1;

				this.needsUpdate = true;
			}
		}

		/*!\brief If you are manipulating the internal list of solids, use this after you add or remove solids in the list.*/
		public void updatedSolidCount(){
			if(this.numChunks == 1){
				this.genericIntBufferArray[0] = this.solids.Count;
				ClayContainer.numSolidsPerChunkBuffer.SetData(this.genericIntBufferArray);
			}
			
			for(int i = 0; i < this.solids.Count; ++i){
				Solid solid = this.solids[i];
				solid.id = i;
				
				if(solid.id < ClayContainer.maxSolids){
					this.solidsUpdatedDict[solid.id] = 1;
				}
				else{
					break;
				}
			}
		}

		/*!\brief Set a material with a clayxels-compatible shader or set it to null to return to the standard clayxels shader.*/
		public void setCustomMaterial(Material material){
			this.customMaterial = material;
			this.material = material;

			this.initMaterialProperties();
		}

		/*!\brief Automatically invoked once when the game starts, 
			you only need to invoke this yourself if you change what's declared in ClayxelsPrefs.cs at runtime.*/
		static public void initGlobalData(){
			if(!ClayContainer.globalDataNeedsInit){
				return;
			}

			if(UnityEngine.Object.FindObjectsOfType<ClayContainer>().Length == 0){
				// avoid doing anything if the current scene still hasn't got any clayContainer
				return;
			}

			ClayContainer.globalDataNeedsInit = false;

			ClayContainer.containersToRender.Clear();
			ClayContainer.containersInScene.Clear();

			#if UNITY_EDITOR
				ClayContainer.checkPrefsIntegrity();

				// check news, only if in editor and only once if not disabled in prefs
				int clayxelsFirstContanier = EditorPrefs.GetInt("clayxelsNews" + ClayContainer.version);
				if(clayxelsFirstContanier == 0){
				 	ClayxelsPrefs prefs = ClayContainer.loadPrefs();
				 	if(prefs.showStartupNews){
			 			if(ClayContainer.showNewsCallback != null){
			 				EditorPrefs.SetInt("clayxelsNews" + ClayContainer.version, 1);
			 				ClayContainer.showNewsCallback();
			 			}
			 		}
			 	}
			#endif

			if(!ClayContainer.prefsOverridden){
				ClayContainer.applyPrefs();
			}

			ClayContainer.numThreadsComputeStartRes = 64 / ClayContainer.maxThreads;
			ClayContainer.numThreadsComputeFullRes = 256 / ClayContainer.maxThreads;

			string renderPipeAsset = "";
			if(GraphicsSettings.renderPipelineAsset != null){
				renderPipeAsset = GraphicsSettings.renderPipelineAsset.GetType().Name;
			}
			
			if(renderPipeAsset == "HDRenderPipelineAsset"){
				ClayContainer.renderPipe = "hdrp";
			}
			else if(renderPipeAsset == "UniversalRenderPipelineAsset"){
				ClayContainer.renderPipe = "urp";
			}
			else{
				ClayContainer.renderPipe = "builtin";
			}

			#if UNITY_EDITOR
				if(!Application.isPlaying){
					ClayContainer.setupScenePicking();
					ClayContainer.pickingMode = false;
					ClayContainer.pickedObj = null;
				}
			#endif

			ClayContainer.reloadSolidsCatalogue();

			ClayContainer.lastUpdatedContainerId = -1;

			ClayContainer.releaseGlobalBuffers();

			#if UNITY_EDITOR// fix reimport issues on unity 2020+
				if(Resources.Load("clayCoreLock") != null){
					AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(Resources.Load("clayCoreLock")), ImportAssetOptions.ForceUpdate);
					AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(Resources.Load("clayxelMicroVoxelUtils")), ImportAssetOptions.ForceUpdate);
				}
			#endif

			UnityEngine.Object clayCore = Resources.Load("clayCoreLock");
			if(clayCore == null){
				// #if UNITY_EDITOR// fix reimport issues on unity 2020+? broken again in 2021.2
				// 	AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(Resources.Load("claySDF")), ImportAssetOptions.ForceUpdate);
				// 	AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(Resources.Load("clayCore")), ImportAssetOptions.ForceUpdate);
				// 	AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(Resources.Load("clayxelMicroVoxelUtils")), ImportAssetOptions.ForceUpdate);
				// #endif

				clayCore = Resources.Load("clayCore");
			}

			ClayContainer.claycoreCompute = (ComputeShader)Instantiate(clayCore);

			ClayContainer.gridDataBuffer = new ComputeBuffer(256 * 256 * 256, sizeof(float) * 3);
			ClayContainer.globalCompBuffers.Add(ClayContainer.gridDataBuffer);

			ClayContainer.gridDataLowResBuffer = new ComputeBuffer(64 * 64 * 64, sizeof(float) * 2);
			ClayContainer.globalCompBuffers.Add(ClayContainer.gridDataLowResBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGrid, "gridDataLowRes", ClayContainer.gridDataLowResBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridMip3, "gridDataLowRes", ClayContainer.gridDataLowResBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloudMicroVoxels, "gridDataLowRes", ClayContainer.gridDataLowResBuffer);

			ClayContainer.prefilteredSolidIdsBuffer = new ComputeBuffer((64 * 64 * 64) * ClayContainer.maxSolidsPerVoxel, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.prefilteredSolidIdsBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGrid, "prefilteredSolidIds", ClayContainer.prefilteredSolidIdsBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridMip3, "prefilteredSolidIds", ClayContainer.prefilteredSolidIdsBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloudMicroVoxels, "prefilteredSolidIds", ClayContainer.prefilteredSolidIdsBuffer);
			
			int maxSolidsPerVoxelMask = ClayContainer.maxSolidsPerVoxel / 32;
			ClayContainer.solidsFilterBuffer = new ComputeBuffer((64 * 64 * 64) * maxSolidsPerVoxelMask, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.solidsFilterBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGrid, "solidsFilter", ClayContainer.solidsFilterBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridMip3, "solidsFilter", ClayContainer.solidsFilterBuffer);
			
			ClayContainer.claycoreCompute.SetInt("maxSolidsPerVoxel", maxSolidsPerVoxel);
			ClayContainer.claycoreCompute.SetInt("maxSolidsPerVoxelMask", maxSolidsPerVoxelMask);

			ClayContainer.claycoreCompute.SetFloat("globalBlendReduce", 1.0f - ClayContainer.globalBlend);
			
			ClayContainer.triangleConnectionTable = new ComputeBuffer(256 * 16, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.triangleConnectionTable);

			ClayContainer.triangleConnectionTable.SetData(MeshUtils.TriangleConnectionTable);
			
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloud, "triangleConnectionTable", ClayContainer.triangleConnectionTable);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloudMicroVoxels, "triangleConnectionTable", ClayContainer.triangleConnectionTable);

			ClayContainer.claycoreCompute.SetInt("maxSolids", ClayContainer.maxSolids);

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGrid, "gridData", ClayContainer.gridDataBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloud, "gridData", ClayContainer.gridDataBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloudMicroVoxels, "gridData", ClayContainer.gridDataBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridMip3, "gridData", ClayContainer.gridDataBuffer);
			
			ClayContainer.numSolidsPerChunkBuffer = new ComputeBuffer(64, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.numSolidsPerChunkBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.filterSolidsPerChunk, "numSolidsPerChunk", ClayContainer.numSolidsPerChunkBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGrid, "numSolidsPerChunk", ClayContainer.numSolidsPerChunkBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridMip3, "numSolidsPerChunk", ClayContainer.numSolidsPerChunkBuffer);
			
			ClayContainer.solidsUpdatedBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.solidsUpdatedBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.filterSolidsPerChunk, "solidsUpdated", ClayContainer.solidsUpdatedBuffer);

			int maxChunks = 64;
			ClayContainer.solidsPerChunkBuffer = new ComputeBuffer(ClayContainer.maxSolids * maxChunks, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.solidsPerChunkBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.filterSolidsPerChunk, "solidsPerChunk", ClayContainer.solidsPerChunkBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGrid, "solidsPerChunk", ClayContainer.solidsPerChunkBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridMip3, "solidsPerChunk", ClayContainer.solidsPerChunkBuffer);
			
			ClayContainer.solidsInSingleChunkArray = new int[ClayContainer.maxSolids];
			for(int i = 0; i < ClayContainer.maxSolids; ++i){
				ClayContainer.solidsInSingleChunkArray[i] = i;
			}

			ClayContainer.solidsUpdatedArray = new int[ClayContainer.maxSolids];

			ClayContainer.meshIndicesBuffer = null;
			ClayContainer.meshVertsBuffer = null;
			ClayContainer.meshColorsBuffer = null;

			ClayContainer.claycoreCompute.SetInt("maxPointCount", ClayContainer.maxPointCount);
			
			// polySplat data
			ClayContainer.pointCloudDataToSolidIdBuffer = new ComputeBuffer(ClayContainer.maxPointCount * ClayContainer.totalMaxChunks, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.pointCloudDataToSolidIdBuffer);

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.compactPointCloud, "pointCloudDataToSolidId", ClayContainer.pointCloudDataToSolidIdBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizePointCloud, "pointCloudDataToSolidId", ClayContainer.pointCloudDataToSolidIdBuffer);

			ClayContainer.chunkPointCloudDataToSolidIdBuffer = new ComputeBuffer(ClayContainer.maxPointCount * ClayContainer.totalMaxChunks, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.chunkPointCloudDataToSolidIdBuffer);

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloud, "chunkPointCloudDataToSolidId", ClayContainer.chunkPointCloudDataToSolidIdBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.compactPointCloud, "chunkPointCloudDataToSolidId", ClayContainer.chunkPointCloudDataToSolidIdBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizePointCloud, "chunkPointCloudDataToSolidId", ClayContainer.chunkPointCloudDataToSolidIdBuffer);

			ClayContainer.claycoreCompute.SetInt("storeSolidId", 0);

			ClayContainer.chunkPointCloudDataBuffer = new ComputeBuffer(ClayContainer.maxPointCount * ClayContainer.totalMaxChunks, sizeof(int) * 2);
			ClayContainer.globalCompBuffers.Add(ClayContainer.chunkPointCloudDataBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloud, "chunkPointCloudData", ClayContainer.chunkPointCloudDataBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.compactPointCloud, "chunkPointCloudData", ClayContainer.chunkPointCloudDataBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizePointCloud, "chunkPointCloudData", ClayContainer.chunkPointCloudDataBuffer);
			
			ClayContainer.pointsInChunkDefaultValues = new int[maxChunks];
			ClayContainer.updateChunksDefaultValues = new int[maxChunks];
			ClayContainer.indirectChunk1DefaultValues = new int[maxChunks * 3];
			ClayContainer.indirectChunk2DefaultValues = new int[maxChunks * 3];

			ClayContainer.microvoxelBoundingBoxData = new int[maxChunks * 6];

			int indirectChunkSize1 = 64 / ClayContainer.maxThreads;
			int indirectChunkSize2 = 256 / ClayContainer.maxThreads;
			
			for(int i = 0; i < maxChunks; ++i){
				ClayContainer.pointsInChunkDefaultValues[i] = 0;

				ClayContainer.updateChunksDefaultValues[i] = 1;

				int indirectChunkId = i * 3;
				ClayContainer.indirectChunk1DefaultValues[indirectChunkId] = indirectChunkSize1;
				ClayContainer.indirectChunk1DefaultValues[indirectChunkId + 1] = indirectChunkSize1;
				ClayContainer.indirectChunk1DefaultValues[indirectChunkId + 2] = indirectChunkSize1;

				ClayContainer.indirectChunk2DefaultValues[indirectChunkId] = indirectChunkSize2;
				ClayContainer.indirectChunk2DefaultValues[indirectChunkId + 1] = indirectChunkSize2;
				ClayContainer.indirectChunk2DefaultValues[indirectChunkId + 2] = indirectChunkSize2;

				ClayContainer.microvoxelBoundingBoxData[i * 6] = 64;
				ClayContainer.microvoxelBoundingBoxData[(i * 6) + 1] = 64;
				ClayContainer.microvoxelBoundingBoxData[(i * 6) + 2] = 64;
				ClayContainer.microvoxelBoundingBoxData[(i * 6) + 3] = 0;
				ClayContainer.microvoxelBoundingBoxData[(i * 6) + 4] = 0;
				ClayContainer.microvoxelBoundingBoxData[(i * 6) + 5] = 0;
			}

			ClayContainer.microvoxelBoundingBoxData[ClayContainer.totalMaxChunks * 6] = 0; // storing the chunkId offset used by the first chunk, which is always zero

			if(ClayContainer.renderPipe != "builtin"){
				ClayContainer.initMicroVoxelGlobal();
			}

			ClayContainer.lastEditedMicrovoxelContainerId = -1;

			ClayContainer.reassignContainerIds();
			ClayContainer.scanInstances();

			#if UNITY_EDITOR_OSX
				// on mac disable warnings about missing bindings
				PlayerSettings.enableMetalAPIValidation = false;
			#endif

			ClayContainer.globalDataNeedsInit = false;
		}

		/*!\brief If you happen to change one of the global settings at runtime, 
			this will make sure all containers are properly reinitialized to reflect those changes. */
		static public void forceAllContainersInit(){
			ClayContainer[] containers = UnityEngine.Object.FindObjectsOfType<ClayContainer>();
    		for(int i = 0; i < containers.Length; ++i){
    			ClayContainer container = containers[i];
    			container.needsInit = true;

    			if(!container.frozen && container.gameObject.activeSelf && container.enabled && container.instanceOf == null){
    				container.enableAllClayObjects(true);
    				container.needsInit = true;
    				container.init();
    			}
    		}
		}

		/*!\brief Automatically invoked once when the game starts, you might need to invoke this yourself for procedurally built scenes.*/
		public void init(){
			if(!this.needsInit){
				return;
			}

			this.needsInit = false;
			
			#if UNITY_EDITOR
				if(!Application.isPlaying){
					ClayContainer.checkNeedsGlobalInit();

					this.reinstallEditorEvents();

					if(PrefabUtility.IsPartOfAnyPrefab(this.gameObject)){
						this.setupPrefab();
					}
				}
			#endif

			if(ClayContainer.globalDataNeedsInit){
				ClayContainer.initGlobalData();
			}

			if(ClayContainer.containersToRender.Contains(this)){
				ClayContainer.containersToRender.Remove(this);
			}

			if(ClayContainer.containersInScene.ContainsKey(this.containerId)){
				ClayContainer.containersInScene.Remove(this.containerId);
			}
			
			this.needsInit = false;

			if(ClayContainer.renderPipe == "builtin" && this.renderMode == ClayContainer.RenderModes.microVoxel){
				this.renderMode = ClayContainer.RenderModes.polySplat;
			}

			this.memoryOptimized = false;

			this.editingThisContainer = false;

			this.userWarning = "";

			this.releaseBuffers();

			this.addToScene();

			if(this.instanceOf != null){
				this.initInstancedContainer();

				return;
			}

			if(this.frozen){
				this.releaseBuffers();
				return;
			}

			this.memoryOptimized = false;

			this.chunkSize = this.clayDetailToChunkSize();
			this.limitChunkValues();

			this.clayObjects.Clear();
			this.solidsUpdatedDict.Clear();

			this.solidsHierarchyNeedsScan = true;
			this.scanClayObjectsHierarchy();

			this.voxelSize = (float)this.chunkSize / 256;
			this.splatRadius = this.voxelSize * ((this.transform.lossyScale.x + this.transform.lossyScale.y + this.transform.lossyScale.z) / 3.0f);

			this.initChunks();

			this.initMaterialProperties();

			if(this.renderMode == ClayContainer.RenderModes.microVoxel){
				this.initMicroVoxelBuffer();
			}

			this.genericNumberBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
			this.compBuffers.Add(this.genericNumberBuffer);

			this.needsUpdate = true;
			ClayContainer.lastUpdatedContainerId = -1;

			this.initSolidsData();

			this.updateFrame = 0;
			this.autoBoundsChunkSize = 0;
			
			this.computeClay();

			if(!this.interactive){
				this.optimizeMemory();
			}
			
			if(Application.isPlaying){
				if(!this.interactive){
					this.enableAllClayObjects(false);
				}
			}

			this.needsInit = false;
			this.needsUpdate = false;

			#if UNITY_EDITOR
				if(!Application.isPlaying){
					// check if this container is currently being edited
					for(int i = 0; i < UnityEditor.Selection.gameObjects.Length; ++i){
						GameObject sel = UnityEditor.Selection.gameObjects[i];
						ClayContainer parentContainer = sel.GetComponentInParent<ClayContainer>();
						
						if(parentContainer == this){
							this.editingThisContainer = true;
							this.needsUpdate = true;
							this.expandMemory();
							this.computeClay();
						}
					}
				}
			#endif
		}

		/*!\brief Spawn a new ClayObject in scene under this container.*/
		public ClayObject addClayObject(){
			if(this.frozen){
				return null;
			}

			if(Application.isPlaying){
				this.setInteractive(true);
			}

			GameObject clayObj = new GameObject("clay_cube+");
			clayObj.transform.parent = this.transform;
			clayObj.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);

			ClayObject clayObjComp = clayObj.AddComponent<ClayObject>();
			clayObjComp.clayxelContainerRef = new WeakReference(this);

			this.collectClayObject(clayObjComp);

			this.needsUpdate = true;

			return clayObjComp;
		}

		/*!\brief Get a ClayObject inside this container by id.*/
		public ClayObject getClayObject(int id){
			return this.clayObjects[id];
		}

		/*!\brief Scan for ClayObjects in this container at the next update.*/
		public void scheduleClayObjectsScan(){
			this.solidsHierarchyNeedsScan = true;
			this.needsUpdate = true;
		}

		/*!\brief Invoke this when you need all solids in a container to be updated, ex. if you change the material attributes.*/
		public void forceUpdateAllSolids(){
			for(int i = 0; i < this.solids.Count; ++i){
				int id = this.solids[i].id;
				if(id < ClayContainer.maxSolids){
					this.solidsUpdatedDict[id] = 1;
				}
				else{
					break;
				}
			}

			this.needsUpdate = true;
		}

		/*!\brief Notify this container that one of the nested ClayObject has changed.*/
		public void clayObjectUpdated(ClayObject clayObj){
			if(!this.transform.hasChanged || this.interactive){
				for(int i = 0; i < clayObj.getNumSolids(); ++i){
					int id = clayObj.getSolid(i).id;
					if(id < ClayContainer.maxSolids){
						this.solidsUpdatedDict[id] = 1;
					}
				}

				this.needsUpdate = true;
			}
		}

		/*!\brief Get the material currently in use by this container. */
		public Material getMaterial(){
			return this.material;
		}

		/*!\brief Force this container to compute the final clay result now.
			Invoking this method at play time will also cause setInteractive(true) to be invoked.*/
		public void computeClay(){
			if(this.invalidated){
				return;
			}

			if(this.needsInit){
				this.init();

				return;
			}
			
			this.needsUpdate = false;
			this.updateFrame = 0;
			
			if(Application.isPlaying){
				if(this.memoryOptimized && !this.interactive){
					this.setInteractive(true);
				}
			}
			else{
				// special optimized case for microvoxel, when computing clay we need to make sure we switch the shared buffers
				if(this.renderMode == ClayContainer.RenderModes.microVoxel){
					if(this.memoryOptimized && !this.interactive){
						this.expandMemoryMicroVoxel();
					}
				}
			}

			if(this.solidsHierarchyNeedsScan){
				this.scanClayObjectsHierarchy();
			}

			if(ClayContainer.lastUpdatedContainerId != this.containerId){
				this.switchComputeData();
			}

			this.updateSolids();
						
			if(this.autoBounds){
				this.updateAutoBounds();
			}

			this.updateChunks();

			if(this.renderMode == ClayContainer.RenderModes.microVoxel){
				this.computeClayMicroVoxel();
			}
			else if(this.renderMode == ClayContainer.RenderModes.polySplat){
				this.computeClayPolySplat();
			}
			else if(this.renderMode == ClayContainer.RenderModes.smoothMesh){
				this.computeClaySmoothMesh();
			}
			
			this.needsUpdate = false;
		}

		/*!\brief */
		public void setCastShadows(bool state){
			if(state){
				this.castShadows = ShadowCastingMode.On;
			}
			else{
				this.castShadows = ShadowCastingMode.Off;
			}
		}

		/*!\brief */
		public bool getCastShadows(){
			if(this.castShadows == ShadowCastingMode.On){
				return true;
			}

			return false;
		}

		/*!\brief */
		public void setReceiveShadows(bool state){
			this.receiveShadows = state;
		}

		/*!\brief */
		public bool getReceiveShadows(){
			return this.receiveShadows;
		}

		/*!\brief This is only useful if you disable this container's Update and want to manually draw its content.
		*/
		public void drawClayxels(){
			if(this.needsInit){
				return;
			}

			if(this.renderMode == ClayContainer.RenderModes.microVoxel){
				this.drawClayxelsMicroVoxel();
			}
			else if(this.renderMode == ClayContainer.RenderModes.polySplat){
				this.drawClayxelsPolySplat();
			}
			else if(this.renderMode == ClayContainer.RenderModes.smoothMesh){
				this.drawClayxelsSmoothMesh();
			}
		}

		/*!\brief ClayContainer.generateMesh() can optionally retain some buffers to speedup multiple calls to generateMesh (freeMemory = false), 
			you can manually free up that memory once you are done using this method.*/
		public static void clearFrozenMeshBuffers(){
			if(ClayContainer.meshIndicesBuffer != null){
				ClayContainer.meshIndicesBuffer.Release();
				ClayContainer.globalCompBuffers.Remove(ClayContainer.meshIndicesBuffer);
			}
			
			if(ClayContainer.meshVertsBuffer != null){
				ClayContainer.meshVertsBuffer.Release();
				ClayContainer.globalCompBuffers.Remove(ClayContainer.meshVertsBuffer);
			}

			if(ClayContainer.meshColorsBuffer != null){
				ClayContainer.meshColorsBuffer.Release();
				ClayContainer.globalCompBuffers.Remove(ClayContainer.meshColorsBuffer);
			}

			ClayContainer.meshIndicesBuffer = null;
			ClayContainer.meshVertsBuffer = null;
			ClayContainer.meshColorsBuffer = null;
		}

		/*!\brief Returns a mesh at the specified level of detail, clayxelDetail will range from 0 to 100.
			Useful to generate mesh colliders, to improve performance leave colorizeMesh and generateNormals to false.
			Use "bool freeMemory = false" if you want to invoke this method multiple times to retain some allocated buffers. */
		public Mesh generateMesh(int detail, bool colorizeMesh = false, bool computeNormals = false, float smoothNormalAngle = 100.0f, bool freeMemory = true){
			if(ClayContainer.lastUpdatedContainerId != this.containerId){
				this.switchComputeData();
			}

			int prevDetail = this.clayxelDetail;

			if(detail != this.clayxelDetail){
				this.setClayxelDetail(detail);
			}

			if(computeNormals){
				colorizeMesh = true;
			}
			
			if(ClayContainer.meshIndicesBuffer == null){
				ClayContainer.meshIndicesBuffer = new ComputeBuffer(ClayContainer.maxPointCount*6, sizeof(int) * 3, ComputeBufferType.Counter);
				ClayContainer.globalCompBuffers.Add(ClayContainer.meshIndicesBuffer);
				
				ClayContainer.meshVertsBuffer = new ComputeBuffer(ClayContainer.maxPointCount*6, sizeof(float) * 3);
				ClayContainer.globalCompBuffers.Add(ClayContainer.meshVertsBuffer);

				ClayContainer.meshColorsBuffer = new ComputeBuffer(ClayContainer.maxPointCount*6, sizeof(float) * 4);
				ClayContainer.globalCompBuffers.Add(ClayContainer.meshColorsBuffer);
			}

			List<Vector3> totalVertices = null;
			List<int> totalIndices = null;
			List<Color> totalColors = null;
			List<float> totalWeights = null;

			if(this.numChunks > 1){
				totalVertices = new List<Vector3>();
				totalIndices = new List<int>();

				if(colorizeMesh){
					totalColors = new List<Color>();
				}

				totalWeights = new List<float>();
			}

			int totalNumVerts = 0;

			Mesh mesh = new Mesh();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

			ClayContainer.claycoreCompute.SetInt("numSolids", this.solids.Count);
			ClayContainer.claycoreCompute.SetFloat("chunkSize", (float)this.chunkSize);

			float oldOffset = this.seamOffset;
			this.seamOffset = 3.0f;

			this.forceUpdateAllSolids();

			this.computeClay();

			this.userWarning = "";

			ComputeBuffer skinBonesGridBuffer = null;
			ComputeBuffer skinWeightsGridBuffer = null;
			ComputeBuffer meshOutSkinWeightsBuffer = null;

			int maxBonesPerVert = 16;
			int numWeightsElementsPerVert = maxBonesPerVert * 2;

			BoneWeight1[] boneWeights = null;
			byte[] numBonesPerVert = null;

			if(!this.autoRigEnabled){
				this.bindSolidsBuffers((int)Kernels.computeGridForMesh);

				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMesh, "chunksCenter", this.chunksCenterBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMesh, "gridData", ClayContainer.gridDataBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMesh, "numSolidsPerChunk", ClayContainer.numSolidsPerChunkBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMesh, "solidsPerChunk", ClayContainer.solidsPerChunkBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMesh, "prefilteredSolidIds", ClayContainer.prefilteredSolidIdsBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMesh, "solidsFilter", ClayContainer.solidsFilterBuffer);

				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMesh, "chunksCenter", this.chunksCenterBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMesh, "triangleConnectionTable", ClayContainer.triangleConnectionTable);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMesh, "gridData", ClayContainer.gridDataBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMesh, "meshOutIndices", ClayContainer.meshIndicesBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMesh, "meshOutPoints", ClayContainer.meshVertsBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMesh, "meshOutColors", ClayContainer.meshColorsBuffer);
			}
			else{
				skinBonesGridBuffer = new ComputeBuffer((256 * 256 * 256), sizeof(float) * maxBonesPerVert);
				skinWeightsGridBuffer = new ComputeBuffer((256 * 256 * 256), sizeof(float) * maxBonesPerVert);
				
				meshOutSkinWeightsBuffer = new ComputeBuffer(ClayContainer.maxPointCount * 2, sizeof(float) * numWeightsElementsPerVert);

				this.bindSolidsBuffers((int)Kernels.computeGridForMeshSkinned);

				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMeshSkinned, "chunksCenter", this.chunksCenterBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMeshSkinned, "skinBonesGrid", skinBonesGridBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMeshSkinned, "skinWeightsGrid", skinWeightsGridBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMeshSkinned, "triangleConnectionTable", ClayContainer.triangleConnectionTable);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMeshSkinned, "gridData", ClayContainer.gridDataBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMeshSkinned, "numSolidsPerChunk", ClayContainer.numSolidsPerChunkBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMeshSkinned, "solidsPerChunk", ClayContainer.solidsPerChunkBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMeshSkinned, "prefilteredSolidIds", ClayContainer.prefilteredSolidIdsBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridForMeshSkinned, "solidsFilter", ClayContainer.solidsFilterBuffer);

				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned1, "chunksCenter", this.chunksCenterBuffer);	
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned1, "gridData", ClayContainer.gridDataBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned1, "meshOutIndices", ClayContainer.meshIndicesBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned1, "meshOutPoints", ClayContainer.meshVertsBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned1, "meshOutColors", ClayContainer.meshColorsBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned1, "meshOutSkinWeights", meshOutSkinWeightsBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned1, "skinBonesGrid", skinBonesGridBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned1, "skinWeightsGrid", skinWeightsGridBuffer);

				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned2, "chunksCenter", this.chunksCenterBuffer);	
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned2, "gridData", ClayContainer.gridDataBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned2, "meshOutIndices", ClayContainer.meshIndicesBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned2, "meshOutPoints", ClayContainer.meshVertsBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned2, "meshOutColors", ClayContainer.meshColorsBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned2, "meshOutSkinWeights", meshOutSkinWeightsBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned2, "skinBonesGrid", skinBonesGridBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned2, "skinWeightsGrid", skinWeightsGridBuffer);

				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned3, "chunksCenter", this.chunksCenterBuffer);	
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned3, "gridData", ClayContainer.gridDataBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned3, "meshOutIndices", ClayContainer.meshIndicesBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned3, "meshOutPoints", ClayContainer.meshVertsBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned3, "meshOutColors", ClayContainer.meshColorsBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned3, "meshOutSkinWeights", meshOutSkinWeightsBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned3, "skinBonesGrid", skinBonesGridBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshSkinned3, "skinWeightsGrid", skinWeightsGridBuffer);
			}

			smoothNormalAngle *=  1.0f - this.meshVoxelize;
			ClayContainer.claycoreCompute.SetFloat("meshVoxelize", this.meshVoxelize);

			for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
				ClayContainer.meshIndicesBuffer.SetCounterValue(0);

				ClayContainer.claycoreCompute.SetInt("chunkId", chunkIt);
				ClayContainer.claycoreCompute.SetInt("outMeshIndexOffset", totalNumVerts);

				if(!this.autoRigEnabled){
					ClayContainer.claycoreCompute.Dispatch((int)Kernels.computeGridForMesh, ClayContainer.numThreadsComputeStartRes, ClayContainer.numThreadsComputeStartRes, ClayContainer.numThreadsComputeStartRes);
					ClayContainer.claycoreCompute.Dispatch((int)Kernels.computeMesh, ClayContainer.numThreadsComputeFullRes, ClayContainer.numThreadsComputeFullRes, ClayContainer.numThreadsComputeFullRes);
				}
				else{
					ClayContainer.claycoreCompute.Dispatch((int)Kernels.computeGridForMeshSkinned, ClayContainer.numThreadsComputeStartRes, ClayContainer.numThreadsComputeStartRes, ClayContainer.numThreadsComputeStartRes);
					ClayContainer.claycoreCompute.Dispatch((int)Kernels.computeMeshSkinned1, ClayContainer.numThreadsComputeFullRes, ClayContainer.numThreadsComputeFullRes, ClayContainer.numThreadsComputeFullRes);
					ClayContainer.claycoreCompute.Dispatch((int)Kernels.computeMeshSkinned2, ClayContainer.numThreadsComputeFullRes, ClayContainer.numThreadsComputeFullRes, ClayContainer.numThreadsComputeFullRes);
					ClayContainer.claycoreCompute.Dispatch((int)Kernels.computeMeshSkinned3, ClayContainer.numThreadsComputeFullRes, ClayContainer.numThreadsComputeFullRes, ClayContainer.numThreadsComputeFullRes);
				}

				int numTris = this.getBufferCount(ClayContainer.meshIndicesBuffer);
				int numVerts = numTris * 3;

				if(numVerts > ClayContainer.maxPointCount * 6){
					this.userWarning = "max point count exceeded, increase limit from Global Config window";
					Debug.Log("Clayxels: container " + this.gameObject.name + " has exceeded the limit of points allowed, increase limit from Global Config window");
					mesh = null;

					break;
				}
				
				totalNumVerts += numVerts;
				
				if(mesh != null){
					if(this.numChunks > 1){
						Vector3[] vertices = new Vector3[numVerts];
						ClayContainer.meshVertsBuffer.GetData(vertices);

						int[] indices = new int[numVerts];
						ClayContainer.meshIndicesBuffer.GetData(indices);

						totalVertices.AddRange(vertices);
						totalIndices.AddRange(indices);

						if(colorizeMesh){
							Color[] colors = new Color[numVerts];
							ClayContainer.meshColorsBuffer.GetData(colors);

							totalColors.AddRange(colors);
						}

						if(this.autoRigEnabled){
			        		float[] weights = new float[(numVerts / 3) * numWeightsElementsPerVert];
							meshOutSkinWeightsBuffer.GetData(weights);

							totalWeights.AddRange(weights);
						}
					}
				}
			}

			if(mesh != null){
				if(this.numChunks > 1){
					mesh.vertices = totalVertices.ToArray();
					mesh.triangles = totalIndices.ToArray();

					if(colorizeMesh){
						mesh.colors = totalColors.ToArray();
					}
				}
				else{
					Vector3[] vertices = new Vector3[totalNumVerts];
					ClayContainer.meshVertsBuffer.GetData(vertices);

					mesh.vertices = vertices;

					int[] indices = new int[totalNumVerts];
					ClayContainer.meshIndicesBuffer.GetData(indices);

					mesh.triangles = indices;

					if(colorizeMesh){
						Color[] colors = new Color[totalNumVerts];
						meshColorsBuffer.GetData(colors);

						mesh.colors = colors;
					}
				}

				if(this.autoRigEnabled){
					float[] weights;

					if(this.numChunks > 1){
						weights = totalWeights.ToArray();
					}
					else{
	        			weights = new float[(totalNumVerts / 3) * numWeightsElementsPerVert];
						meshOutSkinWeightsBuffer.GetData(weights);
					}

					boneWeights = new BoneWeight1[totalNumVerts * maxBonesPerVert];
					numBonesPerVert = new byte[totalNumVerts];

					for(int i = 0; i < totalNumVerts / 3; ++i){
						byte numBones = (byte)maxBonesPerVert;

						for(int j = 0; j < maxBonesPerVert; ++j){
							int boneId = (int)weights[(i * numWeightsElementsPerVert) + j];
							float weight = weights[(i * numWeightsElementsPerVert) + maxBonesPerVert + j];

							boneWeights[(((i * 3) + 0) * maxBonesPerVert) + j].boneIndex = boneId;
							boneWeights[(((i * 3) + 0) * maxBonesPerVert) + j].weight = weight;

							boneWeights[(((i * 3) + 1) * maxBonesPerVert) + j].boneIndex = boneId;
							boneWeights[(((i * 3) + 1) * maxBonesPerVert) + j].weight = weight;

							boneWeights[(((i * 3) + 2) * maxBonesPerVert) + j].boneIndex = boneId;
							boneWeights[(((i * 3) + 2) * maxBonesPerVert) + j].weight = weight;
						}

						numBonesPerVert[(i * 3)] = numBones;
						numBonesPerVert[(i * 3) + 1] = numBones;
						numBonesPerVert[(i * 3) + 2] = numBones;
					}

					skinBonesGridBuffer.Release();
					skinWeightsGridBuffer.Release();
					meshOutSkinWeightsBuffer.Release();
				}

				if(!computeNormals){
					smoothNormalAngle = 0.0f;
				}

				MeshUtils.freezeMeshPostPass(mesh, smoothNormalAngle, this.gameObject, boneWeights, numBonesPerVert, maxBonesPerVert);
			}

			if(prevDetail != this.clayxelDetail){
				this.setClayxelDetail(prevDetail);
			}
			
			this.seamOffset = oldOffset;

			if(freeMemory){
				ClayContainer.clearFrozenMeshBuffers();
			}
			
			return mesh;
		}

		public bool isAutoRigEnabled(){
			return this.autoRigEnabled;
		}

		public void enableAutoRiggedFrozenMesh(bool state){
			this.autoRigEnabled = state;

			if(state){
				if(this.isFrozenToMesh()){
					this.defrostContainersHierarchy();
				}

				bool freeMemory = true;
				this.freezeToMesh(this.clayxelDetail, this.meshNormalSmooth, freeMemory);
			}
			else{
				MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();
				if(renderer != null){
					renderer.enabled = true;
				}
				else{
					renderer = this.gameObject.AddComponent<MeshRenderer>();
				}

				SkinnedMeshRenderer skinRender = this.gameObject.GetComponent<SkinnedMeshRenderer>();
				if(skinRender != null){
					skinRender.enabled = false;

					renderer.sharedMaterial = skinRender.sharedMaterial;
				}
			}
		}

		/*!\brief Freeze this container to a mesh. 
			meshDetail: specify meshDetail from 0 to 100.
			smoothAngle: angle threshold for normal smoothing.
			freeMemory: set to true if you plan on freezing multiple containers, then use clearFrozenMeshBuffers() at the end.
		*/
		public void freezeToMesh(int meshDetail, float smoothAngle = 100.0f, bool freeMemory = true){
			if(this.instanceOf != null){
				return;
			}

			if(this.needsInit){
				this.init();
			}

			bool vertexColors = true;
			bool smoothNormals = true;
			Mesh mesh = this.generateMesh(meshDetail, vertexColors, smoothNormals, smoothAngle, freeMemory);
			if(mesh == null){
				return;
			}

			this.frozen = true;
			this.enabled = false;

			if(this.gameObject.GetComponent<MeshFilter>() == null){
				this.gameObject.AddComponent<MeshFilter>();
			}

			Material mat = null;
			
			if(this.autoRigEnabled){
				MeshRenderer oldRender = this.gameObject.GetComponent<MeshRenderer>();
				if(oldRender != null){
					oldRender.enabled = false;
				}

				SkinnedMeshRenderer render = this.gameObject.GetComponent<SkinnedMeshRenderer>();
				if(render == null){
					render = this.gameObject.AddComponent<SkinnedMeshRenderer>();
				}

				render.enabled = true;

				mat = render.sharedMaterial;
			}
			else{
				SkinnedMeshRenderer oldRender = this.gameObject.GetComponent<SkinnedMeshRenderer>();
				if(oldRender != null){
					oldRender.enabled = false;
				}

				MeshRenderer render = this.gameObject.GetComponent<MeshRenderer>();
				if(render == null){
					render = this.gameObject.AddComponent<MeshRenderer>();
				}

				render.enabled = true;

				mat = render.sharedMaterial;
			}
			
			#if UNITY_EDITOR
				string matAssetNameUnique = this.storeAssetPath + "_" + this.GetInstanceID();
				string matAssetPath = "Assets/" + ClayContainer.defaultAssetsPath + "/" + matAssetNameUnique + ".mat";

				if(mat == null){
					mat = (Material)AssetDatabase.LoadAssetAtPath(matAssetPath, typeof(Material));
				}
			#endif
				
			bool createdNewMaterial = false;

			if(mat == null){
				if(ClayContainer.renderPipe == "hdrp"){
					string renderModeMatSuffix = "";
					if(ClayContainer.renderPipe == "hdrp"){
		            	if(ClayContainer.isUnityVersionOrAfter(2020, 2)){
		            		renderModeMatSuffix += "_2020_2";
		            	}
		            }
		            
					mat = new Material(Shader.Find("Clayxels/ClayxelHDRPMeshShaderASE" + renderModeMatSuffix));
					
					createdNewMaterial = true;
				}
				else if(ClayContainer.renderPipe == "urp"){
					string renderModeMatSuffix = "";
					if(ClayContainer.isUnityVersionOrAfter(2021, 2)){
	            		renderModeMatSuffix += "_2021_2";
	            	}

					mat = new Material(Shader.Find("Clayxels/ClayxelURPMeshShaderASE" + renderModeMatSuffix));
					
					createdNewMaterial = true;
				}
				else{
					mat = new Material(Shader.Find("Clayxels/ClayxelBuiltInMeshShader"));

					createdNewMaterial = true;
				}

				#if UNITY_EDITOR
					if(ClayContainer.defaultAssetsPath != ""){
						if(!AssetDatabase.IsValidFolder("Assets/" + ClayContainer.defaultAssetsPath)){
							AssetDatabase.CreateFolder("Assets", ClayContainer.defaultAssetsPath);
						}
					}
					
					if(createdNewMaterial){
						AssetDatabase.CreateAsset(mat, matAssetPath);
					}
				#endif

				if(createdNewMaterial){
					this.transferMaterialPropertiesToMesh(mat);
				}

				Texture texture = mat.GetTexture("_MainTex");
				if(texture == null){
					if(this.renderMode == ClayContainer.RenderModes.microVoxel){
						mat.SetTexture("_MainTex", (Texture)Resources.Load("clayxelDotBlur"));
					}
					else{
						mat.SetTexture("_MainTex", (Texture)Resources.Load("clayxelDot"));
					}
				}
			}

			this.gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
			
			if(this.autoRigEnabled){
				this.gameObject.GetComponent<SkinnedMeshRenderer>().sharedMaterial = mat;
				this.gameObject.GetComponent<SkinnedMeshRenderer>().sharedMesh = mesh;

				for(int i = 0; i < this.clayObjects.Count; ++i){
					this.clayObjects[i].enabled = false;
				}

				this.updateAutoRig();
			}
			else{
				this.gameObject.GetComponent<MeshRenderer>().sharedMaterial = mat;

				this.enableAllClayObjects(false);
			}

			this.releaseBuffers();
			this.removeFromScene();

			#if UNITY_EDITOR
				if(this.storeAssetPath != ""){
					this.storeMesh(this.storeAssetPath);
				}
			#endif
		}

		/*!\brief Is this container using a mesh filter to display a mesh? */
		public bool isFrozenToMesh(){
			if(this.frozen && this.gameObject.GetComponent<MeshFilter>() != null){
				return true;
			}

			return false;
		}

		public bool isFrozen(){
			return this.frozen;
		}

		/*!\brief Disable the frozen state and get back to live clayxels. */
		public void defrostToLiveClayxels(){
			this.frozen = false;
			this.needsInit = true;
			this.enabled = true;

			#if UNITY_EDITOR
				this.retopoApplied = false;
			#endif

			if(this.gameObject.GetComponent<MeshFilter>() != null){
				DestroyImmediate(this.gameObject.GetComponent<MeshFilter>());
			}

			if(this.gameObject.GetComponent<SkinnedMeshRenderer>() != null){
				this.gameObject.GetComponent<SkinnedMeshRenderer>().enabled = false;
			}

			if(this.gameObject.GetComponent<MeshRenderer>() != null){
				this.gameObject.GetComponent<MeshRenderer>().enabled = false;
			}

			this.enableAllClayObjects(true);
		}

		/*!\brief ClayContainers that have interactive set to false will disable all their clayObjects when the game starts.
			Use this method to re-enable all clayObjects in a container while the game is running. */
		public void enableAllClayObjects(bool state){
			List<GameObject> objs = new List<GameObject>();
			ClayContainer.collectClayObjectsRecursive(this.gameObject, ref objs);
			
			for(int i = 0; i < objs.Count; ++i){
				objs[i].SetActive(state);
				objs[i].GetComponent<ClayObject>().enabled = true;
			}
		}

		/*!\brief Freeze this container plus all the containers that are part of this hierarchy. */
		public void freezeContainersHierarchyToMesh(){
			bool freeMemory = false;

			ClayContainer[] containers = this.GetComponentsInChildren<ClayContainer>();
			for(int i = 0; i < containers.Length; ++i){
				ClayContainer container = containers[i];
				if(!container.isFrozen() && container.gameObject.activeSelf){
					container.needsInit = true;
					container.editingThisContainer = false;
					container.freezeToMesh(container.clayxelDetail, this.meshNormalSmooth, freeMemory);
				}
			}

			ClayContainer.clearFrozenMeshBuffers();
		}

		/*!\brief Smooth normals on this container after it got frozen to mesh, plus all the containers that are part of this hierarchy. */
		public void smoothNormalsContainersHierarchy(float smoothNormalAngle){
			this.meshNormalSmooth = smoothNormalAngle;

			ClayContainer[] containers = this.GetComponentsInChildren<ClayContainer>();
			for(int i = 0; i < containers.Length; ++i){
				ClayContainer container = containers[i];
				if(container.isFrozenToMesh()){
					container.meshNormalSmooth = smoothNormalAngle;
					MeshUtils.freezeMeshPostPass(container.GetComponent<MeshFilter>().sharedMesh, smoothNormalAngle, container.gameObject);
				}
			}
		}

		/*!\brief Defrost this container plus all the containers that are part of this hierarchy. */
		public void defrostContainersHierarchy(){
			ClayContainer[] containers = this.GetComponentsInChildren<ClayContainer>();
			for(int i = 0; i < containers.Length; ++i){
				containers[i].defrostToLiveClayxels();
			}
		}

		/*!\brief Set this container to be an instance of another container, or set this to null to remove an old instance link.
			Instances have the advantage of not consuming vram budget and they won't increase draw calls. */
		public void setIsInstanceOf(ClayContainer sourceContainer){
			if(sourceContainer != null){
				if(sourceContainer == this){
					return;
				}

				if(sourceContainer.instances.Contains(this)){
					return;
				}

				if(sourceContainer.instanceOf != null){
					return;
				}
			}

			if(this.instanceOf == sourceContainer){
				return;
			}

			this.editingThisContainer = false;

			this.needsInit = true;

			ClayContainer oldSourceContainer = this.instanceOf;
			
			this.instanceOf = sourceContainer;

			if(this.instanceOf != null){
				if(!this.instanceOf.instances.Contains(this) && this.instanceOf.instances.Count < ClayContainer.maxInstances){
					this.instanceOf.instances.Add(this);
				}

				if(ClayContainer.containersToRender.Contains(this)){
					ClayContainer.containersToRender.Remove(this);
				}

				if(!this.instanceOf.needsInit){
					this.instanceOf.initInstancesData();
				}
			}
			else{
				this.addToScene();
			}

			if(oldSourceContainer != null){
				List<ClayContainer> oldSourceInstances = new List<ClayContainer>();

				for(int i = 0; i < oldSourceContainer.instances.Count; ++i){
					if(oldSourceContainer.instances[i].instanceOf == oldSourceContainer && oldSourceInstances.Count < ClayContainer.maxInstances){
						oldSourceInstances.Add(oldSourceContainer.instances[i]);
					}
				}

				oldSourceContainer.instances = oldSourceInstances;

				oldSourceContainer.initInstancesData();
			}
		}

		public ClayContainer getInstanceOf(){
			return this.instanceOf;
		}

		public Bounds getRenderBounds(){
			return this.renderBounds;
		}

		/*!\brief When using microvoxels clayxels will render to a texture and you can override its resolution.
		 	Higher resolutions will decrease performance at runtime and improve visual quality. 
		 	Using this method at turntime will disable using the value from the globalPrefs. */
		public static void setOutputRenderTextureSize(int width, int height){
			if(!ClayContainer.prefsOverridden){
				ClayContainer.prefsOverridden = true;
				ClayContainer.applyPrefs();
			}

			ClayContainer.microvoxelRTSizeOverride = new Vector2Int(width, height);
			ClayContainer.globalDataNeedsInit = true;
		}

		/*!\brief Used to communicate with the user, for example if user runs out of points as specified by clayContainer.setPointCloudLimit()
		*/
		public string getUserWarning(){
			return ClayContainer.globalUserWarning + this.userWarning;
		}

		/*!\brief Set the quality of microvoxel splats, 
			1.0 will get you beautiful textured splats that overlap with each other when close to camera, 
			0.0 will display blocky splats.
			Use this value to speedup the rendering of microvoxel containers.
		*/
		public static void setGlobalMicrovoxelSplatsQuality(float value){
			if(value < 0.0f){
				value = 0.0f;
			}
			else if(value > 1.0f){
				value = 1.0f;
			}

			ClayContainer.globalMicrovoxelSplatsQuality = value;
		}

		public static float getGlobalMicrovoxelSplatsQuality(){
			return ClayContainer.globalMicrovoxelSplatsQuality;
		}

		/*!\brief Set how far a ray will travel inside a microvoxel container.
			1.0 will traverse every voxel in the container until a hit is found.
			0.5 will stop the ray early and potentially miss the voxel that needs to be rendered.
			Use this value to speedup the rendering of microvoxel containers.
		*/
		public static void setGlobalMicrovoxelRayIterations(float value){
			if(value < 0.5f){
				value = 0.5f;
			}
			else if(value > 1.0f){
				value = 1.0f;
			}

			ClayContainer.globalMicrovoxelRayIterations = value;
		}

		public static float getGlobalMicrovoxelRayIterations(){
			return ClayContainer.globalMicrovoxelRayIterations;
		}

		/*!\brief Set if the camera can go inside this container, 
			slows down rendering but lets you get very close with the camera while the game is running (has no effect while in editor viewport).
		*/
		public static void setMicrovoxelCanGetInside(bool state){
			ClayContainer.microvoxelCanGetInside = state;
		}

		public static bool getMicrovoxelCanGetInside(){
			return ClayContainer.microvoxelCanGetInside;
		}

		public void setSmoothMeshVoxelize(float value){
			if(value < 0.0f){
				value = 0.0f;
			}
			else if(value > 1.0f){
				value = 1.0f;
			}

			this.meshVoxelize = value;
			this.needsUpdate = true;
		}

		public float getSmoothMeshVoxelize(){
			return this.meshVoxelize;
		}

		public void setSmoothMeshNormalAngle(float value){
			if(value < 0.0f){
				value = 0.0f;
			}
			else if(value > 180.0f){
				value = 180.0f;
			}

			this.meshNormalSmooth = value;
			this.needsUpdate = true;
		}

		public float getSmoothMeshNormalAngle(){
			return this.meshNormalSmooth;
		}

		public void setMicrovoxelSplatsQuality(float value){
			if(value < 0.0f){
				value = 0.0f;
			}
			else if(value > 1.0f){
				value = 1.0f;
			}

			this.microvoxelSplatsQuality = value;
		}

		public float getMicrovoxelSplatsQuality(){
			return this.microvoxelSplatsQuality;
		}

		public void setMicrovoxelRayIterations(float value){
			if(value < 0.5f){
				value = 0.5f;
			}
			else if(value > 1.0f){
				value = 1.0f;
			}

			this.microvoxelRayIterations = value;
		}

		public float getMicrovoxelRayIterations(){
			return this.microvoxelRayIterations;
		}

		// public members for internal use

		public bool isMemoryOptimized(){
			return this.memoryOptimized;
		}

		public static void reloadSolidsCatalogue(){
			ClayContainer.solidsCatalogueLabels.Clear();
			ClayContainer.solidsCatalogueParameters.Clear();

			int lastParsed = -1;
			try{
				string claySDF = ((TextAsset)Resources.Load("claySDF", typeof(TextAsset))).text;
				ClayContainer.parseSolidsAttrs(claySDF, ref lastParsed);

				string numThreadsDef = "MAXTHREADS";
				ClayContainer.maxThreads = (int)char.GetNumericValue(claySDF[claySDF.IndexOf(numThreadsDef) + numThreadsDef.Length + 1]);
			}
			catch{
				Debug.Log("error trying to parse parameters in claySDF.compute, solid #" + lastParsed);
			}
		}

		public string[] getSolidsCatalogueLabels(){
			return ClayContainer.solidsCatalogueLabels.ToArray();
		}
		
		public static List<string[]> getSolidsCatalogueParameters(int solidId){
			if(solidId < ClayContainer.solidsCatalogueParameters.Count){
				return ClayContainer.solidsCatalogueParameters[solidId];
			}

			return new List<string[]>();
		}

		public bool isClayObjectsOrderLocked(){
			return this.clayObjectsOrderLocked;
		}

		public void setClayObjectsOrderLocked(bool state){
			this.clayObjectsOrderLocked = state;
		}

		public void reorderClayObject(int clayObjOrderId, int offset){
			List<ClayObject> tmpList = new List<ClayObject>(this.clayObjects.Count);
			for(int i = 0; i < this.clayObjects.Count; ++i){
				tmpList.Add(this.clayObjects[i]);
			}
			
			int newOrderId = clayObjOrderId + offset;
			if(newOrderId < 0){
				newOrderId = 0;
			}
			else if(newOrderId > this.clayObjects.Count - 1){
				newOrderId = this.clayObjects.Count - 1;
			}
			
			ClayObject clayObj1 = tmpList[clayObjOrderId];
			ClayObject clayObj2 = tmpList[newOrderId];

			tmpList.Remove(clayObj1);
			tmpList.Insert(newOrderId, clayObj1);

			clayObj1.clayObjectId = tmpList.IndexOf(clayObj1);
			clayObj2.clayObjectId = tmpList.IndexOf(clayObj2);

			if(this.clayObjectsOrderLocked){
				clayObj1.transform.SetSiblingIndex(tmpList.IndexOf(clayObj1));
			}
			
			this.scanClayObjectsHierarchy();
		}

		public static void pickingMicrovoxel(Camera camera, float mousePosX, float mousePosY, out int pickedContainerId, out int pickedClayObjectId, bool invertVerticalMouseCoords = false){
			pickedContainerId = -1;
			pickedClayObjectId = -1;

			if(camera == null){
				return;
			}

			#if !UNITY_EDITOR
				ClayContainer.mvRenderTexturePicking0 = ClayContainer.mvRenderTexture0;
				ClayContainer.mvRenderTexturePicking1 = ClayContainer.mvRenderTexture1;
				ClayContainer.mvRenderTexturePicking2 = ClayContainer.mvRenderTexture2;
			#endif

			int rectWidth = (int)(ClayContainer.microvoxelRTSize.x * ((float)mousePosX / (float)camera.pixelWidth));
			int rectHeight = (int)(ClayContainer.microvoxelRTSize.y * ((float)mousePosY / (float)camera.pixelHeight));

			if(invertVerticalMouseCoords){
				rectHeight = (int)ClayContainer.microvoxelRTSize.y - rectHeight;
			}

			if(ClayContainer.pickingTextureResult == null){
				ClayContainer.pickingTextureResult = new Texture2D(1, 1, TextureFormat.ARGB32, false);
				ClayContainer.pickingRect = new Rect(0, 0, 1, 1);
			}
			
			ClayContainer.pickingRect.Set(
				rectWidth, 
				rectHeight, 
				1, 1);

			RenderTexture oldRT = RenderTexture.active;

			RenderTexture.active = ClayContainer.mvRenderTexturePicking0;
			ClayContainer.pickingTextureResult.ReadPixels(ClayContainer.pickingRect, 0, 0);
			ClayContainer.pickingTextureResult.Apply();
			Color pickCol1 = ClayContainer.pickingTextureResult.GetPixel(0, 0);

			RenderTexture.active = oldRT;
			
			pickedContainerId = (int)((pickCol1.r * 255) + (pickCol1.g * 255.0f) * 256.0f + (pickCol1.b * 255.0f) * 256.0f * 256.0f) - 1;
			
			if(pickedContainerId == -1){
				return;
			}

			RenderTexture.active = ClayContainer.mvRenderTexturePicking1;
			ClayContainer.pickingTextureResult.ReadPixels(ClayContainer.pickingRect, 0, 0);
			ClayContainer.pickingTextureResult.Apply();
			Color pickCol2 = ClayContainer.pickingTextureResult.GetPixel(0, 0);
			
			RenderTexture.active = ClayContainer.mvRenderTexturePicking2;
			ClayContainer.pickingTextureResult.ReadPixels(ClayContainer.pickingRect, 0, 0);
			ClayContainer.pickingTextureResult.Apply();
			Color pickCol3 = ClayContainer.pickingTextureResult.GetPixel(0, 0);

			pickedClayObjectId = (int)((pickCol1.a + pickCol2.a * 255.0f + pickCol3.a * 255.0f) * 255.0f) - 1;
		}

		public int getRenderMode(){
			return (int)this.renderMode;
		}

		public void setRenderMode(int renderMode){
			if(renderMode < 0){
				renderMode = 0;
			}
			else if(renderMode > 2){
				renderMode = 2;
			}

			if(ClayContainer.renderPipe == "builtin"){
				if(renderMode == 0){
					renderMode = 1;// no microvoxels in builtin
				}
			}

			this.renderMode = (ClayContainer.RenderModes)renderMode;
			this.needsInit = true;
			this.init();
		}

		public static string getRenderPipe(){
			return ClayContainer.renderPipe;
		}

		public static int getNumMicrovoxelContainers(){
			return ClayContainer.containersToRender.Count;
		}

		public bool needsUpdate = true;
		public static string defaultAssetsPath = "clayxelsFrozen";
		public Material customMaterial = null;

		// end of public interface, following functions are for internal use //

		static ComputeBuffer gridPointersMip3GlobBuffer = null;
		static ComputeBuffer gridPointersMip2GlobBuffer = null;
		static ComputeBuffer pointCloudDataMip3GlobBuffer = null;
		static ComputeBuffer solidsUpdatedBuffer = null;
		static ComputeBuffer solidsPerChunkBuffer = null;
		static ComputeBuffer meshIndicesBuffer = null;
		static ComputeBuffer meshVertsBuffer = null;
		static ComputeBuffer meshColorsBuffer = null;
		static ComputeBuffer pointCloudDataToSolidIdBuffer = null;
		static ComputeBuffer chunkPointCloudDataBuffer = null;
		static ComputeBuffer chunkPointCloudDataToSolidIdBuffer = null;
		static ComputeBuffer gridDataBuffer = null;
		static ComputeBuffer triangleConnectionTable = null;
		static ComputeBuffer prefilteredSolidIdsBuffer = null;
		static ComputeBuffer solidsFilterBuffer = null;
		static ComputeBuffer numSolidsPerChunkBuffer = null;
		static ComputeBuffer gridDataLowResBuffer = null;
		static ComputeShader claycoreCompute = null;
		static int maxInstances = 200;
		static int maxSolids = 512;
		static int maxSolidsPerVoxel = 128;
		static int maxPointCount = (256 * 256 * 256);
		static int inspectorUpdated;
		static public bool globalDataNeedsInit = true;
		static List<string> solidsCatalogueLabels = new List<string>();
		static List<List<string[]>> solidsCatalogueParameters = new List<List<string[]>>();
		static List<ComputeBuffer> globalCompBuffers = new List<ComputeBuffer>();
		static int lastUpdatedContainerId = -1;
		static int maxThreads = 8;
		static int[] solidsInSingleChunkArray;
		static int frameSkip = 0;
		static string renderPipe = "";
		static RenderTexture pickingRenderTexture;
		static RenderTargetIdentifier pickingRenderTextureId;
		static CommandBuffer pickingCommandBuffer = null;
		static Texture2D pickingTextureResult;
		static Rect pickingRect;
		static int pickedClayObjectId = -1;
		static int pickedContainerId = -1;
		static int pickedClayObjectIdMV = -1;
		static int pickedContainerIdMV = -1;
		static GameObject pickedObj = null;
		static bool pickingMode = false;
		static bool pickingShiftPressed = false;
		static int maxChunkX = 3;
		static int maxChunkY = 3;
		static int maxChunkZ = 3;
		static float globalBlend = 1.0f;
		static int totalMaxChunks = 27;
		static int[] tmpPointCloudDataStorage;
		static Vector3[] tmpStorageVec3 = new Vector3[1];
		static Vector4[] tmpStorageVec4 = new Vector4[1];
		static int[] indirectArgsData = new int[]{0, 1, 0, 0};
		static int[] microvoxelDrawData = new int[]{0, 0, 0, 0, 0, 0, 0, 0};
		static int[] microvoxelBoundingBoxData;
		static int[] solidsUpdatedArray;
		static Material pickingMeshMaterialPolySplat = null;
		static Material pickingMeshMaterialSmoothMesh = null;
		static MaterialPropertyBlock pickingMeshMaterialProperties;
		static MaterialPropertyBlock pickingMaterialPropertiesMicroVoxel;
		static int numThreadsComputeStartRes;
		static int numThreadsComputeFullRes;
		static int[] updateChunksDefaultValues;
		static int[] indirectChunk1DefaultValues;
		static int[] indirectChunk2DefaultValues;
		static int[] pointsInChunkDefaultValues;
		static int lastPickedContainerId = -1;
		static Mesh microVoxelMesh = null;
		static Dictionary<int, ClayContainer> containersInScene = new Dictionary<int, ClayContainer>();
		static List<ClayContainer> containersToRender = new List<ClayContainer>();
		static Material microvoxelPrePassMat = null;
		static RenderTexture mvRenderTexture0 = null;
		static RenderTexture mvRenderTexture1 = null;
		static RenderTexture mvRenderTexture2 = null;
		static RenderTexture mvRenderTexture3 = null;
		static RenderTexture mvRenderTexture4 = null;
		static RenderTexture mvRenderTexture5 = null;
		static RenderTexture mvRenderTexturePicking0 = null;
		static RenderTexture mvRenderTexturePicking1 = null;
		static RenderTexture mvRenderTexturePicking2 = null;
		static RenderTargetIdentifier[] mvRenderBuffers;
		static int[] chunkIdOffsetDefaultData;
		static Vector2 microvoxelRTSize;
		static Vector2Int microvoxelRTSizeOverride = new Vector2Int(1024, 512);
		static float bufferSizeReduceFactor = 0.1f;
		static bool directPick = false;
		static bool directPickEnabled = false;
		static string globalUserWarning = "";
		static bool prefsOverridden = false;
		static int lastEditedMicrovoxelContainerId = -1;
		static float[] defaultChunksCenter = new float[]{0.0f, 0.0f, 0.0f};
		static float globalMicrovoxelSplatsQuality = 1.0f;
		static float globalMicrovoxelRayIterations = 1.0f;
		static bool microvoxelCanGetInside = false;
		static bool limitSmoothMeshMemory = true;
		public static bool microvoxelPickingValid = true;
		
		public delegate void RenderPipelineInitCallback();
		static public RenderPipelineInitCallback renderPipelineInitCallback = null;
		static public RenderPipelineInitCallback showNewsCallback = null;

		public enum RenderModes{
			polySplat,
			microVoxel,
			smoothMesh
		}
		
		[SerializeField] int clayxelDetail = 88;
		[SerializeField] int chunksX = 1;
		[SerializeField] int chunksY = 1;
		[SerializeField] int chunksZ = 1;
		[SerializeField] Material material = null;
		[SerializeField] ShadowCastingMode castShadows = ShadowCastingMode.On;
		[SerializeField] bool receiveShadows = true;
		[SerializeField] public string storeAssetPath = "";
		[SerializeField] bool frozen = false;
		[SerializeField] bool clayObjectsOrderLocked = true;
		[SerializeField] bool autoBounds = true;
		[SerializeField] RenderModes renderMode = RenderModes.microVoxel;
		[SerializeField] ClayContainer instanceOf = null;
		[SerializeField] bool interactive = false;
		[SerializeField] float meshNormalSmooth = 180.0f;
		[SerializeField] float meshVoxelize = 0.0f;
		[SerializeField] float microvoxelSplatsQuality = 1.0f;
		[SerializeField] float microvoxelRayIterations = 1.0f;
		[SerializeField] int autoBoundsLimit = 1;
		[SerializeField] bool autoRigEnabled = false;
		
		int containerId = -1;
		bool invalidated = false;
		int chunkSize = 8;
		bool memoryOptimized = false;
		// float globalSmoothing = 0.0f;
		Dictionary<int, int> solidsUpdatedDict = new Dictionary<int, int>();
		List<ComputeBuffer> compBuffers = new List<ComputeBuffer>();
		bool needsInit = true;
		int[] genericIntBufferArray = new int[1]{0};
		float[] genericFloatBufferArray = new float[1]{0.0f};
		Vector4[] genericFloat4BufferArray = new Vector4[1]{new Vector4()};
		List<Vector3> solidsPos;
		List<Quaternion> solidsRot;
		List<Vector3> solidsScale;
		List<float> solidsBlend;
		List<int> solidsType;
		List<Vector3> solidsColor;
		List<Vector4> solidsAttrs;
		List<Vector4> solidsAttrs2;
		List<int> solidsClayObjectId;
		ComputeBuffer solidsPosBuffer = null;
		ComputeBuffer solidsRotBuffer = null;
		ComputeBuffer solidsScaleBuffer = null;
		ComputeBuffer solidsBlendBuffer = null;
		ComputeBuffer solidsTypeBuffer = null;
		ComputeBuffer solidsColorBuffer = null;
		ComputeBuffer solidsAttrsBuffer = null;
		ComputeBuffer solidsAttrs2Buffer = null;
		ComputeBuffer solidsClayObjectIdBuffer = null;
		ComputeBuffer genericNumberBuffer = null;
		ComputeBuffer indirectChunkArgs1Buffer = null;
		ComputeBuffer indirectChunkArgs2Buffer = null;
		ComputeBuffer indirectChunkArgs3Buffer = null;
		ComputeBuffer updateChunksBuffer = null;
		ComputeBuffer chunksCenterBuffer = null;
		ComputeBuffer indirectDrawArgsBuffer = null;
		ComputeBuffer indirectDrawArgsBuffer2 = null;
		ComputeBuffer pointCloudDataMip3Buffer = null;
		ComputeBuffer gridPointersMip2Buffer = null;
		ComputeBuffer gridPointersMip3Buffer = null;
		ComputeBuffer boundingBoxBuffer = null;
		ComputeBuffer numPointsInChunkBuffer = null;
		ComputeBuffer renderIndirectDrawArgsBuffer = null;
		ComputeBuffer pointToChunkIdBuffer = null;
		ComputeBuffer volumetricDrawBuffer = null;
		ComputeBuffer chunkIdOffsetBuffer = null;
		ComputeBuffer chunkSizeBuffer = null;
		ComputeBuffer localChunkIdBuffer = null;
		ComputeBuffer chunkIdToContainerIdBuffer = null;
		ComputeBuffer splatTexIdBuffer = null;
		ComputeBuffer instanceToContainerIdBuffer = null;
		ComputeBuffer metallicBuffer = null;
		ComputeBuffer smoothnessBuffer = null;
		ComputeBuffer smoothMeshPointsBuffer = null;
		ComputeBuffer smoothMeshNormalsBuffer = null;
		ComputeBuffer smoothMeshNormalsTempBuffer = null;
		ComputeBuffer smoothMeshGridDataBuffer = null;
		Vector3 boundsScale = new Vector3(0.0f, 0.0f, 0.0f);
		Vector3 boundsCenter = new Vector3(0.0f, 0.0f, 0.0f);
		Bounds renderBounds = new Bounds();
		bool solidsHierarchyNeedsScan = false;
		List<ClayObject> clayObjects = new List<ClayObject>();
		List<Solid> solids = new List<Solid>();
		int numChunks = 0;
		float deltaTime = 0.0f;
		float voxelSize = 0.0f;
		int updateFrame = 0;
		float splatRadius = 1.0f;
		bool editingThisContainer = false;
		int autoBoundsChunkSize = 0;
		int autoFrameSkip = 0;
		string userWarning = "";
		MaterialPropertyBlock materialProperties;
		bool pickingThis = false;
		bool visible = true;
		int splatTexId = 0;
		float seamOffset = 0.0f;
		
		// instances data
		List<ClayContainer> instances = new List<ClayContainer>();
		Matrix4x4[] instancesMatrix;
		Matrix4x4[] instancesMatrixInv;
		ComputeBuffer instancesMatrixBuffer = null;
		ComputeBuffer instancesMatrixInvBuffer = null;
		ComputeBuffer alphaCutoutBuffer = null;
		ComputeBuffer roughPosBuffer = null;
		ComputeBuffer splatSizeMultBuffer = null;
		ComputeBuffer backFillDarkBuffer = null;
		ComputeBuffer emissiveColorBuffer = null;
		ComputeBuffer roughOrientXBuffer = null;
		ComputeBuffer roughOrientYBuffer = null;
		ComputeBuffer roughOrientZBuffer = null;
		ComputeBuffer roughColorBuffer = null;
		ComputeBuffer roughTwistBuffer = null;
		ComputeBuffer roughSizeBuffer = null;
		ComputeBuffer splatBillboardBuffer = null;
		ComputeBuffer backFillAlphaBuffer = null;
		ComputeBuffer emissionPowerBuffer = null;
		ComputeBuffer subsurfaceScatterBuffer = null;
		ComputeBuffer subsurfaceCenterBuffer = null;

		enum Kernels{
			computeGrid,
			generatePointCloud,
			debugDisplayGridPoints,
			computeGridForMesh,
			computeMesh,
			filterSolidsPerChunk,
			compactPointCloud,
			optimizePointCloud,
			generatePointCloudMicroVoxels,
			optimizeMicrovoxels,
			computeGridMip3,
			computeMeshRealTime,
			computeMeshRealTime2,
			compactSmoothMesh,
			optimizeSmoothMesh,
			computeGridForMeshSkinned,
			computeMeshSkinned1,
			computeMeshSkinned2,
			computeMeshSkinned3
		}

		void Start(){
			Application.targetFrameRate = 10000;
			QualitySettings.vSyncCount = 0;

			if(this.needsInit){
				this.init();
			}
		}

		void Update(){
			if(this.instanceOf != null || this.frozen){
				return;
			}

			if(this.visible){
				this.updateClay();
				
				this.drawClayxels();
			}
		}

		static void initMicroVoxelGlobal(){
			int reducedMip3BufferSize = (int)(((float)(256 * 256 * 256) * ClayContainer.totalMaxChunks) * ClayContainer.bufferSizeReduceFactor);

			ClayContainer.tmpPointCloudDataStorage = new int[reducedMip3BufferSize * 2];

			ClayContainer.gridPointersMip3GlobBuffer = new ComputeBuffer(reducedMip3BufferSize, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.gridPointersMip3GlobBuffer);

			ClayContainer.pointCloudDataMip3GlobBuffer = new ComputeBuffer(reducedMip3BufferSize, sizeof(int) * 2);
			ClayContainer.globalCompBuffers.Add(ClayContainer.pointCloudDataMip3GlobBuffer);

			ClayContainer.gridPointersMip2GlobBuffer = new ComputeBuffer((64 * 64 * 64) * ClayContainer.totalMaxChunks, sizeof(int));
			ClayContainer.globalCompBuffers.Add(ClayContainer.gridPointersMip2GlobBuffer);

			if(ClayContainer.microVoxelMesh != null){
				DestroyImmediate(ClayContainer.microVoxelMesh);
			}

			ClayContainer.microVoxelMesh = new Mesh();
			ClayContainer.microVoxelMesh.indexFormat = IndexFormat.UInt32;

			/* cube topology
		       5---6
			 / |     |
			/  4    |
			1---2  7
			|    |  /
			0---3/

			y  z
			|/
			---x
			*/

			Vector3[] cubeVerts = new Vector3[]{
				new Vector3(-0.5f, -0.5f, -0.5f),// 0
				new Vector3(-0.5f, 0.5f, -0.5f),// 1
				new Vector3(0.5f, 0.5f, -0.5f),// 2
				new Vector3(0.5f, -0.5f, -0.5f),// 3
				new Vector3(-0.5f, -0.5f, 0.5f),// 4
				new Vector3(-0.5f, 0.5f, 0.5f),// 5
				new Vector3(0.5f, 0.5f, 0.5f),// 6
				new Vector3(0.5f, -0.5f, 0.5f)// 7
				};

			Vector2[] cubeUV = new Vector2[]{
				new Vector2(0.0f, 0.0f),
				new Vector2(0.0f, 1.0f),
				new Vector2(1.0f, 1.0f),
				new Vector2(1.0f, 0.0f),
				new Vector2(0.0f, 1.0f),
				new Vector2(0.0f, 1.0f),
				new Vector2(1.0f, 1.0f),
				new Vector2(1.0f, 0.0f)
			};
			
			int[] cubeIndices = new int[]{
				3, 1, 0, 
				2, 1, 3, 

				2, 5, 1,
				6, 5, 2,

				7, 2, 3,
				6, 2, 7,

				4, 5, 7,
				7, 5, 6,

				0, 1, 4,
				4, 1, 5,

				0, 4, 3,
				3, 4, 7};
			
			int gridSize = ClayContainer.totalMaxChunks;

			List<Vector3> meshVerts = new List<Vector3>(cubeVerts.Length * gridSize);
			List<Vector2> meshUVs = new List<Vector2>(cubeVerts.Length * gridSize);
			List<int> meshIndices = new List<int>(cubeIndices.Length * gridSize);

			for(int i = 0; i < gridSize; ++i){
				meshVerts.AddRange(cubeVerts);
				meshIndices.AddRange(cubeIndices);
				meshUVs.AddRange(cubeUV);

				for(int j = 0; j < cubeIndices.Length; ++j){
					cubeIndices[j] += cubeVerts.Length;
				}
			}

			ClayContainer.chunkIdOffsetDefaultData = new int[ClayContainer.totalMaxChunks];
			for(int i = 0; i < ClayContainer.totalMaxChunks; ++i){
				ClayContainer.chunkIdOffsetDefaultData[i] = i;
			}

			ClayContainer.microVoxelMesh.vertices = meshVerts.ToArray();
			ClayContainer.microVoxelMesh.uv = meshUVs.ToArray();
			ClayContainer.microVoxelMesh.triangles = meshIndices.ToArray();

			// editor RT size
			int resX = 1920;
			int resY = 1080;

			if(Application.isPlaying){
				resX = ClayContainer.microvoxelRTSizeOverride.x;
				resY = ClayContainer.microvoxelRTSizeOverride.y;
			}
			
			ClayContainer.microvoxelRTSize = new Vector2(resX, resY);
			
			ClayContainer.mvRenderTexture0 = new RenderTexture(resX, resY, 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        	ClayContainer.mvRenderTexture1 = new RenderTexture(resX, resY, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        	ClayContainer.mvRenderTexture2 = new RenderTexture(resX, resY, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        	ClayContainer.mvRenderTexture3 = new RenderTexture(resX, resY, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        	ClayContainer.mvRenderTexture4 = new RenderTexture(resX, resY, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        	ClayContainer.mvRenderTexture5 = new RenderTexture(resX, resY, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        	
        	#if UNITY_EDITOR
	        	ClayContainer.mvRenderTexturePicking0 = new RenderTexture(resX, resY, 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
	        	ClayContainer.mvRenderTexturePicking1 = new RenderTexture(resX, resY, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
	        	ClayContainer.mvRenderTexturePicking2 = new RenderTexture(resX, resY, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
	        #endif

        	ClayContainer.mvRenderTexture0.autoGenerateMips = false;
			ClayContainer.mvRenderTexture0.useMipMap = false;
			ClayContainer.mvRenderTexture0.antiAliasing = 1;
			ClayContainer.mvRenderTexture0.anisoLevel = 0;
			ClayContainer.mvRenderTexture0.filterMode = FilterMode.Point;
			ClayContainer.mvRenderTexture0.Create();

        	ClayContainer.mvRenderTexture1.autoGenerateMips = false;
			ClayContainer.mvRenderTexture1.useMipMap = false;
			ClayContainer.mvRenderTexture1.antiAliasing = 1;
			ClayContainer.mvRenderTexture1.anisoLevel = 0;
			ClayContainer.mvRenderTexture1.filterMode = FilterMode.Point;
			ClayContainer.mvRenderTexture1.Create();

			ClayContainer.mvRenderTexture2.autoGenerateMips = false;
			ClayContainer.mvRenderTexture2.useMipMap = false;
			ClayContainer.mvRenderTexture2.antiAliasing = 1;
			ClayContainer.mvRenderTexture2.anisoLevel = 0;
			ClayContainer.mvRenderTexture2.filterMode = FilterMode.Point;
			ClayContainer.mvRenderTexture2.Create();

			ClayContainer.mvRenderTexture3.autoGenerateMips = false;
			ClayContainer.mvRenderTexture3.useMipMap = false;
			ClayContainer.mvRenderTexture3.antiAliasing = 1;
			ClayContainer.mvRenderTexture3.anisoLevel = 0;
			ClayContainer.mvRenderTexture3.filterMode = FilterMode.Point;
			ClayContainer.mvRenderTexture3.Create();

			ClayContainer.mvRenderTexture4.autoGenerateMips = false;
			ClayContainer.mvRenderTexture4.useMipMap = false;
			ClayContainer.mvRenderTexture4.antiAliasing = 1;
			ClayContainer.mvRenderTexture4.anisoLevel = 0;
			ClayContainer.mvRenderTexture4.filterMode = FilterMode.Point;
			ClayContainer.mvRenderTexture4.Create();

			ClayContainer.mvRenderTexture5.autoGenerateMips = false;
			ClayContainer.mvRenderTexture5.useMipMap = false;
			ClayContainer.mvRenderTexture5.antiAliasing = 1;
			ClayContainer.mvRenderTexture5.anisoLevel = 0;
			ClayContainer.mvRenderTexture5.filterMode = FilterMode.Point;
			ClayContainer.mvRenderTexture5.Create();

        	ClayContainer.mvRenderBuffers = new RenderTargetIdentifier[5];
        	ClayContainer.mvRenderBuffers[0] = new RenderTargetIdentifier(ClayContainer.mvRenderTexture0);
        	ClayContainer.mvRenderBuffers[1] = new RenderTargetIdentifier(ClayContainer.mvRenderTexture1);
        	ClayContainer.mvRenderBuffers[2] = new RenderTargetIdentifier(ClayContainer.mvRenderTexture2);
        	ClayContainer.mvRenderBuffers[3] = new RenderTargetIdentifier(ClayContainer.mvRenderTexture3);
        	ClayContainer.mvRenderBuffers[4] = new RenderTargetIdentifier(ClayContainer.mvRenderTexture5);

        	// Shader.SetGlobalFloat("_nan", System.Single.NaN);

        	Shader.SetGlobalInt("maxChunks", ClayContainer.totalMaxChunks);

        	if(ClayContainer.renderPipe == "urp"){
        		ClayContainer.microvoxelPrePassMat = new Material(Shader.Find("Clayxels/ClayxelMicroVoxelPassURP"));
        		ClayContainer.microvoxelPrePassMat.EnableKeyword("CLAYXELS_URP");
        		ClayContainer.microvoxelPrePassMat.enableInstancing = true;
				ClayContainer.microvoxelPrePassMat.EnableKeyword("UNITY_INSTANCING_ENABLED");
        	}
        	else if(ClayContainer.renderPipe == "hdrp"){
        		ClayContainer.microvoxelPrePassMat = new Material(Shader.Find("Clayxels/ClayxelMicroVoxelPassHDRP"));
        		ClayContainer.microvoxelPrePassMat.EnableKeyword("CLAYXELS_HDRP");
        		ClayContainer.microvoxelPrePassMat.enableInstancing = true;
				ClayContainer.microvoxelPrePassMat.EnableKeyword("UNITY_INSTANCING_ENABLED");
        	}

        	if(Application.isPlaying){
        		// at play time, optionally enable early-z cull
        		if(ClayContainer.microvoxelCanGetInside){
        			ClayContainer.microvoxelPrePassMat.DisableKeyword("CLAYXEL_EARLY_Z_OPTIMIZE_ON");
		        	ClayContainer.microvoxelPrePassMat.EnableKeyword("CLAYXEL_EARLY_Z_OPTIMIZE_OFF");
		        	ClayContainer.microvoxelPrePassMat.SetFloat("_Cull", (float)CullMode.Front);
        		}
        		else{
					ClayContainer.microvoxelPrePassMat.DisableKeyword("CLAYXEL_EARLY_Z_OPTIMIZE_OFF");
	        		ClayContainer.microvoxelPrePassMat.EnableKeyword("CLAYXEL_EARLY_Z_OPTIMIZE_ON");
	        		ClayContainer.microvoxelPrePassMat.SetFloat("_Cull", (float)CullMode.Back);
	        	}
        	}
        	else{
        		// whie sculpting we need these flags to be able to get inside a container with the camera
	        	ClayContainer.microvoxelPrePassMat.DisableKeyword("CLAYXEL_EARLY_Z_OPTIMIZE_ON");
	        	ClayContainer.microvoxelPrePassMat.EnableKeyword("CLAYXEL_EARLY_Z_OPTIMIZE_OFF");
	        	ClayContainer.microvoxelPrePassMat.SetFloat("_Cull", (float)CullMode.Front);
        	}

        	#if UNITY_EDITOR
	        	if(ClayContainer.renderPipelineInitCallback != null){
	        		ClayContainer.renderPipelineInitCallback();
	        	}
	        #endif
		}

		static void reassignContainerIds(){
			#if UNITY_EDITOR
				EditorPrefs.SetInt("clayxelsUniqueId", 0);
			#else
				PlayerPrefs.SetInt("clayxelsUniqueId", 0);
			#endif

			ClayContainer[] containers = UnityEngine.Object.FindObjectsOfType<ClayContainer>();

			// clear all instancing network
			for(int i = 0; i < containers.Length; ++i){
				ClayContainer container = containers[i];

				container.needsInit = true;
				container.containerId = -1;
				container.instances.Clear();
			}
		}

		static void scanInstances(){
			// check if new containers got added to the scene and prepare them
			ClayContainer[] containers = UnityEngine.Object.FindObjectsOfType<ClayContainer>();

			for(int i = 0; i < containers.Length; ++i){
				ClayContainer container = containers[i];

				if(container.instanceOf != null){
					if(!container.instanceOf.instances.Contains(container) && container.instanceOf.instances.Count < ClayContainer.maxInstances){
						container.instanceOf.instances.Add(container);
					}

					if(!container.instanceOf.needsInit){
						container.instanceOf.initInstancesData();
					}
				}
			}
		}

		void checkContainerId(){
			if(this.containerId == -1){
				#if UNITY_EDITOR
					this.containerId = EditorPrefs.GetInt("clayxelsUniqueId");
					ClayContainer.containersInScene[this.containerId] = this;

					EditorPrefs.SetInt("clayxelsUniqueId", this.containerId + 1);
				#else
					this.containerId = PlayerPrefs.GetInt("clayxelsUniqueId");
					ClayContainer.containersInScene[this.containerId] = this;

					PlayerPrefs.SetInt("clayxelsUniqueId", this.containerId + 1);
				#endif
			}
		}

		int clayDetailToChunkSize(){
			int chunkSize = 0;
			
			if(this.clayxelDetail >= 0 && this.clayxelDetail <= 100){
				chunkSize = (int)Mathf.Lerp(40.0f, 4.0f, (float)this.clayxelDetail / 100.0f);
			}
			else if(this.clayxelDetail < 0){
				chunkSize = (int)Mathf.Round(40.0f * (1.0f - ((float)this.clayxelDetail / 100.0f)));
			}
			else{
				chunkSize = (int)Mathf.Lerp(4.0f, 1.0f, (float)(this.clayxelDetail - 100) / 100.0f);
			}

			return chunkSize;
		}

		void transferMaterialAttributesToLocalBuffers(){
			this.genericFloatBufferArray[0] = this.material.GetFloat("_Smoothness");
			this.smoothnessBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_Metallic");
			this.metallicBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_alphaCutout");
			this.alphaCutoutBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_backFillAlpha");
			this.backFillAlphaBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_roughPos");
			this.roughPosBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_splatSizeMult");
			this.splatSizeMultBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_backFillDark");
			this.backFillDarkBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloat4BufferArray[0] = this.material.GetColor("_emissiveColor");
			this.emissiveColorBuffer.SetData(this.genericFloat4BufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_roughOrientX");
			this.roughOrientXBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_roughOrientY");
			this.roughOrientYBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_roughOrientZ");
			this.roughOrientZBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_roughColor");
			this.roughColorBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_roughTwist");
			this.roughTwistBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_roughSize");
			this.roughSizeBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_splatBillboard");
			this.splatBillboardBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_emissionPower");
			this.emissionPowerBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloatBufferArray[0] = this.material.GetFloat("_subsurfaceScatter");
			this.subsurfaceScatterBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);

			this.genericFloat4BufferArray[0] = this.material.GetColor("_subsurfaceCenter");
			this.subsurfaceCenterBuffer.SetData(this.genericFloat4BufferArray, 0, 0, 1);
		}

		static void checkNeedsGlobalInit(){
			if(ClayContainer.globalDataNeedsInit){
				return;
			}

			if(ClayContainer.claycoreCompute == null && !ReferenceEquals(ClayContainer.claycoreCompute , null)){
				ClayContainer.globalDataNeedsInit = true;
			}
		}

		void removeFromScene(){
			if(ClayContainer.containersInScene.ContainsKey(this.containerId)){
				ClayContainer.containersInScene.Remove(this.containerId);
			}

			if(ClayContainer.containersToRender.Contains(this)){
				ClayContainer.containersToRender.Remove(this);
			}
		}

		void addToScene(){
			this.checkContainerId();
			
			ClayContainer.containersInScene[this.containerId] = this;

			if(this.renderMode == ClayContainer.RenderModes.microVoxel){
				if(this.instanceOf == null){
					if(!ClayContainer.containersToRender.Contains(this)){
						ClayContainer.containersToRender.Add(this);
					}
				}
			}

			if(this.frozen){
				if(ClayContainer.containersToRender.Contains(this)){
					ClayContainer.containersToRender.Remove(this);
				}
			}
		}

		static bool isUnityVersionOrAfter(int major, int minor){
			// identify changes in SRP packages that happened from unity 2020.2 onwards

			int majorVersion = int.Parse(Application.unityVersion.Split('.')[0]);
        	int minorVersion = int.Parse(Application.unityVersion.Split('.')[1]);

        	if(majorVersion == major){
        		if(minorVersion >= minor){
        			return true;
        		}
        	}
        	else if(majorVersion > major){
        		return true;
        	}

        	return false;
		}

		void initInstancedContainer(){
			if(Application.isPlaying){
				bool wasVisible = this.visible;
				this.enabled = false;// this will trigger onDisable
				this.visible = wasVisible;
			}

			if(!this.instanceOf.instances.Contains(this)){
				ClayContainer.scanInstances();

				if(!this.instanceOf.needsInit){
					this.instanceOf.initInstancesData();
				}
			}
		}

		void initInstancesData(){
			if(this.renderMode != ClayContainer.RenderModes.microVoxel){
				this.checkContainerId();

				for(int i = 0; i < this.instances.Count; ++i){
					this.instances[i].checkContainerId();
				}

				return;
			}

			int numChunks = this.numChunks;
			if(this.autoBounds){
				numChunks = ClayContainer.totalMaxChunks;

				if(this.renderMode == ClayContainer.RenderModes.smoothMesh && ClayContainer.limitSmoothMeshMemory){
					if(numChunks > 8){
						numChunks = 8;
					}
				}
			}

			int numInstances = this.instances.Count + 1;
			
			ClayContainer.microvoxelDrawData[0] = 36;
			ClayContainer.microvoxelDrawData[1] = numChunks * (this.instances.Count + 1);
			this.volumetricDrawBuffer.SetData(ClayContainer.microvoxelDrawData);
			
			this.instancesMatrixBuffer.SetData(this.instancesMatrix, 0, 0, numInstances);
			this.instancesMatrixInvBuffer.SetData(this.instancesMatrixInv, 0, 0, numInstances);

			this.checkContainerId();

			for(int chunkIt = 0; chunkIt < numChunks; ++chunkIt){
				this.genericIntBufferArray[0] = 0;
				this.instanceToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, chunkIt, 1);
				
				this.genericIntBufferArray[0] = this.containerId;
				this.chunkIdToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, chunkIt, 1);
			}

			for(int i = 0; i < this.instances.Count; ++i){
				this.instances[i].checkContainerId();
				
				int instanceId = i + 1;
				
				for(int chunkIt = 0; chunkIt < numChunks; ++chunkIt){
					this.genericIntBufferArray[0] = instanceId;
					this.instanceToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * numChunks) + chunkIt, 1);

					this.genericIntBufferArray[0] = this.instances[i].containerId;
					this.chunkIdToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * numChunks) + chunkIt, 1);
				}
			}
		}
		
		void initMicroVoxelBuffer(){
        	this.materialProperties.SetTexture("microVoxRenderTex0", ClayContainer.mvRenderTexture0, RenderTextureSubElement.Color);
			this.materialProperties.SetTexture("microVoxRenderTex1", ClayContainer.mvRenderTexture1, RenderTextureSubElement.Color);
			this.materialProperties.SetTexture("microVoxRenderTex2", ClayContainer.mvRenderTexture2, RenderTextureSubElement.Color);
			this.materialProperties.SetTexture("microVoxRenderTex3", ClayContainer.mvRenderTexture3, RenderTextureSubElement.Color);
			this.materialProperties.SetTexture("microVoxRenderTex4", ClayContainer.mvRenderTexture5, RenderTextureSubElement.Color);
			this.materialProperties.SetTexture("microVoxRenderTexDepth", ClayContainer.mvRenderTexture4, RenderTextureSubElement.Color);
			
			this.material.enableInstancing = true;
			this.material.EnableKeyword("UNITY_INSTANCING_ENABLED");

			this.instanceToContainerIdBuffer = new ComputeBuffer(ClayContainer.totalMaxChunks * ClayContainer.maxInstances, sizeof(int));
			this.compBuffers.Add(this.instanceToContainerIdBuffer);

			this.splatTexIdBuffer = new ComputeBuffer(1, sizeof(int));
			this.compBuffers.Add(this.splatTexIdBuffer);

			this.genericIntBufferArray[0] = this.splatTexId;
			this.splatTexIdBuffer.SetData(this.genericIntBufferArray, 0, 0, 1);

			this.chunkIdToContainerIdBuffer = new ComputeBuffer(ClayContainer.totalMaxChunks * ClayContainer.maxInstances, sizeof(int));
			this.compBuffers.Add(this.chunkIdToContainerIdBuffer);

			this.chunkSizeBuffer = new ComputeBuffer(ClayContainer.totalMaxChunks * ClayContainer.maxInstances, sizeof(float));
			this.compBuffers.Add(this.chunkSizeBuffer);

			this.genericFloatBufferArray[0] = this.chunkSize;
			this.chunkSizeBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);
			
			this.localChunkIdBuffer = new ComputeBuffer(ClayContainer.totalMaxChunks * ClayContainer.maxInstances, sizeof(int));
			this.compBuffers.Add(this.localChunkIdBuffer);

			int numChunks = this.numChunks;
			if(this.autoBounds){
				numChunks = ClayContainer.totalMaxChunks;
			}

			for(int i = 0; i < numChunks; ++i){
				this.genericIntBufferArray[0] = i;
				this.localChunkIdBuffer.SetData(this.genericIntBufferArray, 0, i, 1);
			}

			this.smoothnessBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.smoothnessBuffer);

			this.metallicBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.metallicBuffer);

			this.alphaCutoutBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.alphaCutoutBuffer);

			this.backFillAlphaBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.backFillAlphaBuffer);

			this.roughPosBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.roughPosBuffer);

			this.splatSizeMultBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.splatSizeMultBuffer);

			this.backFillDarkBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.backFillDarkBuffer);

			this.emissiveColorBuffer = new ComputeBuffer(1, sizeof(float) * 4);
			this.compBuffers.Add(this.emissiveColorBuffer);

			this.roughOrientXBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.roughOrientXBuffer);

			this.roughOrientYBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.roughOrientYBuffer);

			this.roughOrientZBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.roughOrientZBuffer);

			this.roughColorBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.roughColorBuffer);

			this.roughTwistBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.roughTwistBuffer);

			this.roughSizeBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.roughSizeBuffer);

			this.splatBillboardBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.splatBillboardBuffer);

			this.emissionPowerBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.emissionPowerBuffer);

			this.subsurfaceScatterBuffer = new ComputeBuffer(1, sizeof(float));
			this.compBuffers.Add(this.subsurfaceScatterBuffer);

			this.subsurfaceCenterBuffer = new ComputeBuffer(1, sizeof(float) * 4);
			this.compBuffers.Add(this.subsurfaceCenterBuffer);

			this.instancesMatrixBuffer = new ComputeBuffer(ClayContainer.maxInstances, sizeof(float) * 16);
			this.compBuffers.Add(this.instancesMatrixBuffer);

			this.instancesMatrixInvBuffer = new ComputeBuffer(1000, sizeof(float) * 16);
			this.compBuffers.Add(this.instancesMatrixInvBuffer);

			this.instancesMatrix = new Matrix4x4[ClayContainer.maxInstances];
			this.instancesMatrixInv = new Matrix4x4[ClayContainer.maxInstances];

			this.initInstancesData();
		}

		void drawClayxelsSmoothMesh(){
			this.renderBounds.center = this.transform.position;

			this.materialProperties.SetBuffer("smoothMeshPoints", this.smoothMeshPointsBuffer);
			this.materialProperties.SetBuffer("smoothMeshNormals", this.smoothMeshNormalsBuffer);
			this.materialProperties.SetMatrix("objectMatrix", this.transform.localToWorldMatrix);
			this.materialProperties.SetBuffer("pointToChunkId", this.pointToChunkIdBuffer);
			this.materialProperties.SetInt("solidHighlightId", -1);

			if(this.memoryOptimized){
				this.materialProperties.SetInt("memoryOptimized", 1);
			}
			else{
				this.materialProperties.SetInt("memoryOptimized", 0);
			}

			int reducedMip3BufferSizeMesh = (int)((float)(256 * 256 * 256) * ClayContainer.bufferSizeReduceFactor);
			this.materialProperties.SetInt("maxPointCount", reducedMip3BufferSizeMesh);

			if(this.pickingThis && ClayContainer.pickedObj == null){
				if(!this.editingThisContainer){
					this.materialProperties.SetInt("solidHighlightId", -2);
				}
				else{
					this.materialProperties.SetInt("solidHighlightId", ClayContainer.pickedClayObjectId);
				}
			}

			Graphics.DrawProceduralIndirect(this.material, 
				this.renderBounds,
				MeshTopology.Triangles, this.indirectDrawArgsBuffer, 0,
				null, this.materialProperties,
				this.castShadows, this.receiveShadows, this.gameObject.layer);

			for(int i = 0; i < this.instances.Count; ++i){
				ClayContainer instance = this.instances[i];
				this.materialProperties.SetMatrix("objectMatrix", instance.transform.localToWorldMatrix);
				this.materialProperties.SetInt("solidHighlightId", -1);

				if(instance.pickingThis){
					this.materialProperties.SetInt("solidHighlightId", -2);
				}

				this.renderBounds.center = this.transform.position;

				Graphics.DrawProceduralIndirect(this.material, 
					this.renderBounds,
					MeshTopology.Triangles, this.indirectDrawArgsBuffer, 0,
					null, this.materialProperties,
					this.castShadows, this.receiveShadows, this.gameObject.layer);
			}
		}

		void drawClayxelsPolySplat(){
			this.renderBounds.center = this.transform.position;

			this.splatRadius = this.voxelSize * ((this.transform.lossyScale.x + this.transform.lossyScale.y + this.transform.lossyScale.z) / 3.0f);
			
			this.materialProperties.SetMatrix("objectMatrix", this.transform.localToWorldMatrix);
			this.materialProperties.SetFloat("chunkSize", (float)this.chunkSize);
			this.materialProperties.SetFloat("splatRadius", this.splatRadius);
			this.materialProperties.SetInt("solidHighlightId", -1);

			this.materialProperties.SetBuffer("pointCloudDataToSolidId", ClayContainer.pointCloudDataToSolidIdBuffer);

			if(this.pickingThis && ClayContainer.pickedObj == null){
				if(!this.editingThisContainer){
					this.materialProperties.SetInt("solidHighlightId", -2);
				}
				else{
					this.materialProperties.SetInt("solidHighlightId", ClayContainer.pickedClayObjectId);
				}
			}

			this.materialProperties.SetBuffer("chunkPoints", this.pointCloudDataMip3Buffer);
			this.materialProperties.SetBuffer("chunksCenter", this.chunksCenterBuffer);
			this.materialProperties.SetBuffer("pointToChunkId", this.pointToChunkIdBuffer);

			Graphics.DrawProceduralIndirect(this.material, 
				this.renderBounds,
				MeshTopology.Triangles, this.renderIndirectDrawArgsBuffer, 0,
				null, this.materialProperties,
				this.castShadows, this.receiveShadows, this.gameObject.layer);

			for(int i = 0; i < this.instances.Count; ++i){
				ClayContainer instance = this.instances[i];
				this.materialProperties.SetMatrix("objectMatrix", instance.transform.localToWorldMatrix);
				this.materialProperties.SetInt("solidHighlightId", -1);

				if(instance.pickingThis){
					this.materialProperties.SetInt("solidHighlightId", -2);
				}

				this.renderBounds.center = this.transform.position;

				Graphics.DrawProceduralIndirect(this.material, 
					this.renderBounds,
					MeshTopology.Triangles, this.renderIndirectDrawArgsBuffer, 0,
					null, this.materialProperties,
					this.castShadows, this.receiveShadows, this.gameObject.layer);
			}
		}

		void drawClayxelsMicroVoxel(){
			this.materialProperties.SetInt("containerHighlightId", ClayContainer.pickedContainerIdMV);
			this.materialProperties.SetInt("solidHighlightId", ClayContainer.pickedClayObjectIdMV);

			this.materialProperties.SetBuffer("chunkIdOffsetGlob", this.chunkIdOffsetBuffer);
			this.materialProperties.SetBuffer("chunkSizeGlob", this.chunkSizeBuffer);
			this.materialProperties.SetBuffer("chunksCenterGlob", this.chunksCenterBuffer);
			this.materialProperties.SetBuffer("boundingBoxGlob", this.boundingBoxBuffer);
			this.materialProperties.SetBuffer("pointCloudDataMip3Glob", this.pointCloudDataMip3Buffer);
			this.materialProperties.SetBuffer("gridPointersMip2Glob", this.gridPointersMip2Buffer);
			this.materialProperties.SetBuffer("gridPointersMip3Glob", this.gridPointersMip3Buffer);
			this.materialProperties.SetBuffer("instancesObjectMatrixGlob", this.instancesMatrixBuffer);
			this.materialProperties.SetBuffer("instancesObjectMatrixInvGlob", this.instancesMatrixInvBuffer);
			this.materialProperties.SetBuffer("localChunkIdGlob", this.localChunkIdBuffer);
			this.materialProperties.SetBuffer("chunkIdToSourceContainerIdGlob", this.chunkIdToContainerIdBuffer);
			this.materialProperties.SetBuffer("chunkIdToContainerIdGlob", this.chunkIdToContainerIdBuffer);
			this.materialProperties.SetBuffer("instanceToContainerIdGlob", this.instanceToContainerIdBuffer);
			this.materialProperties.SetVector("renderBoundsCenterGlob", this.renderBounds.center);
			this.materialProperties.SetFloat("bufferSizeReduceFactor", ClayContainer.bufferSizeReduceFactor);
			this.materialProperties.SetInt("numChunks", this.numChunks);

			if(this.memoryOptimized){
				this.materialProperties.SetInt("memoryOptimized", 1);
			}
			else{
				this.materialProperties.SetInt("memoryOptimized", 0);
			}

			this.materialProperties.SetInt("solidHighlightId", ClayContainer.pickedClayObjectIdMV);
			
			Graphics.DrawMeshInstancedIndirect(
				ClayContainer.microVoxelMesh, 0, this.material, 
				this.renderBounds,
				this.volumetricDrawBuffer,
				0, this.materialProperties,
				this.castShadows, this.receiveShadows, this.gameObject.layer);
		}

		public static RenderTargetIdentifier[] getMicroVoxelRenderBuffers(){
			return ClayContainer.mvRenderBuffers;
		}

		public static RenderBuffer getMicroVoxelDepthBuffer(){
			return ClayContainer.mvRenderTexture0.depthBuffer;
		}

		public static void drawMicroVoxelPrePass(CommandBuffer cmd){
			for(int i = 0; i < ClayContainer.containersToRender.Count; ++i){
				ClayContainer container = ClayContainer.containersToRender[i];
				if(!container.frozen){
					container.updateInstances();
					container.drawContainerMicroVoxelsPrePass(cmd);
				}
			}

			cmd.CopyTexture(ClayContainer.mvRenderTexture3, ClayContainer.mvRenderTexture4);

			#if UNITY_EDITOR
				if(ClayContainer.microvoxelPickingValid){
					// this is to make sure we store the render texture for picking only when rendering from the editor viewport
					cmd.CopyTexture(ClayContainer.mvRenderTexture0, ClayContainer.mvRenderTexturePicking0);
					cmd.CopyTexture(ClayContainer.mvRenderTexture1, ClayContainer.mvRenderTexturePicking1);
					cmd.CopyTexture(ClayContainer.mvRenderTexture2, ClayContainer.mvRenderTexturePicking2);
				}
			#endif
		}

		void drawContainerMicroVoxelsPrePass(CommandBuffer cmd){
			if(this.invalidated){
				return;
			}

			if(!this.visible || 
				this.renderMode != ClayContainer.RenderModes.microVoxel || 
				this.needsInit || 
				this.frozen || 
				!this.gameObject.activeSelf){

				return;
			}

			if(!Application.isPlaying){// disable LOD while editing or we might get artifacts when displaying multiple cameras
				this.materialProperties.SetInt("lodEnabled", 0);
			}
			else{
				this.materialProperties.SetInt("lodEnabled", 1);
			}
			
			this.materialProperties.SetBuffer("chunkIdOffsetGlob", this.chunkIdOffsetBuffer);
			this.materialProperties.SetBuffer("chunkSizeGlob", this.chunkSizeBuffer);
			this.materialProperties.SetBuffer("chunksCenterGlob", this.chunksCenterBuffer);
			this.materialProperties.SetBuffer("boundingBoxGlob", this.boundingBoxBuffer);
			this.materialProperties.SetBuffer("pointCloudDataMip3Glob", this.pointCloudDataMip3Buffer);
			this.materialProperties.SetBuffer("gridPointersMip2Glob", this.gridPointersMip2Buffer);
			this.materialProperties.SetBuffer("gridPointersMip3Glob", this.gridPointersMip3Buffer);
			this.materialProperties.SetBuffer("instancesObjectMatrixGlob", this.instancesMatrixBuffer);
			this.materialProperties.SetBuffer("instancesObjectMatrixInvGlob", this.instancesMatrixInvBuffer);
			this.materialProperties.SetBuffer("localChunkIdGlob", this.localChunkIdBuffer);
			this.materialProperties.SetBuffer("chunkIdToSourceContainerIdGlob", this.chunkIdToContainerIdBuffer);
			this.materialProperties.SetBuffer("chunkIdToContainerIdGlob", this.chunkIdToContainerIdBuffer);
			this.materialProperties.SetBuffer("instanceToContainerIdGlob", this.instanceToContainerIdBuffer);
			
			this.materialProperties.SetVector("renderBoundsCenterGlob", this.renderBounds.center);
			this.materialProperties.SetFloat("bufferSizeReduceFactor", ClayContainer.bufferSizeReduceFactor);
			this.materialProperties.SetInt("numChunks", this.numChunks);
			if(this.memoryOptimized){
				this.materialProperties.SetInt("memoryOptimized", 1);
			}
			else{
				this.materialProperties.SetInt("memoryOptimized", 0);
			}
			this.materialProperties.SetInt("interactiveContainerId", this.containerId);

			this.materialProperties.SetTexture("_MainTex", this.material.GetTexture("_MainTex"));

			this.materialProperties.SetBuffer("_smoothnessGlob", this.smoothnessBuffer);
			this.materialProperties.SetBuffer("_metallicGlob", this.metallicBuffer);
			this.materialProperties.SetBuffer("_alphaCutoutGlob", this.alphaCutoutBuffer);
			this.materialProperties.SetBuffer("_roughPosGlob", this.roughPosBuffer);
			this.materialProperties.SetBuffer("_splatSizeMultGlob", this.splatSizeMultBuffer);
			this.materialProperties.SetBuffer("_backFillDarkGlob", this.backFillDarkBuffer);
			this.materialProperties.SetBuffer("_emissiveColorGlob", this.emissiveColorBuffer);
			this.materialProperties.SetBuffer("_roughOrientXGlob", this.roughOrientXBuffer);
			this.materialProperties.SetBuffer("_roughOrientYGlob", this.roughOrientYBuffer);
			this.materialProperties.SetBuffer("_roughOrientZGlob", this.roughOrientZBuffer);
			this.materialProperties.SetBuffer("_roughColorGlob", this.roughColorBuffer);
			this.materialProperties.SetBuffer("_roughTwistGlob", this.roughTwistBuffer);
			this.materialProperties.SetBuffer("_roughSizeGlob", this.roughSizeBuffer);
			this.materialProperties.SetBuffer("_splatBillboardGlob", this.splatBillboardBuffer);
			this.materialProperties.SetBuffer("_backFillAlphaGlob", this.backFillAlphaBuffer);
			this.materialProperties.SetBuffer("_emissionPowerGlob", this.emissionPowerBuffer);
			this.materialProperties.SetBuffer("_subsurfaceScatterGlob", this.subsurfaceScatterBuffer);
			this.materialProperties.SetBuffer("_subsurfaceCenterGlob", this.subsurfaceCenterBuffer);

			this.materialProperties.SetFloat("microvoxelSplatsQuality", this.microvoxelSplatsQuality * ClayContainer.globalMicrovoxelSplatsQuality);
			this.materialProperties.SetFloat("microvoxelRayIterations", this.microvoxelRayIterations * ClayContainer.globalMicrovoxelRayIterations);

			int passId = 0;
			cmd.DrawMeshInstancedIndirect(
				ClayContainer.microVoxelMesh, 0, 
				ClayContainer.microvoxelPrePassMat, 
				passId, 
				this.volumetricDrawBuffer, 0, this.materialProperties);
		}

		void computeClaySmoothMesh(){
			if(this.memoryOptimized){
				return;
			}

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGrid, "chunksCenter", this.chunksCenterBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridMip3, "chunksCenter", this.chunksCenterBuffer);

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshRealTime, "gridData", ClayContainer.gridDataBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshRealTime, "chunksCenter", this.chunksCenterBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshRealTime, "meshPoints", this.smoothMeshPointsBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshRealTime, "meshNormalsTemp", this.smoothMeshNormalsTempBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshRealTime, "smoothMeshGridData", this.smoothMeshGridDataBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshRealTime, "numPointsInChunk", this.numPointsInChunkBuffer);
			
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshRealTime2, "gridData", ClayContainer.gridDataBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshRealTime2, "meshNormalsTemp", this.smoothMeshNormalsTempBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshRealTime2, "meshPoints", this.smoothMeshPointsBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshRealTime2, "meshNormals", this.smoothMeshNormalsBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeMeshRealTime2, "smoothMeshGridData", this.smoothMeshGridDataBuffer);

			ClayContainer.claycoreCompute.SetFloat("meshNormalSmooth", this.meshNormalSmooth * (1.0f - this.meshVoxelize));
			ClayContainer.claycoreCompute.SetFloat("meshVoxelize", this.meshVoxelize);

			int reducedMip3BufferSizeMesh = (int)((float)(256 * 256 * 256) * ClayContainer.bufferSizeReduceFactor);
			ClayContainer.claycoreCompute.SetInt("maxPointCount", reducedMip3BufferSizeMesh);
			
			for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
				uint indirectChunkId = sizeof(int) * ((uint)chunkIt * 3);

				ClayContainer.claycoreCompute.SetInt("chunkId", chunkIt);

				ClayContainer.claycoreCompute.DispatchIndirect((int)Kernels.computeGrid, this.indirectChunkArgs1Buffer, indirectChunkId);
				ClayContainer.claycoreCompute.DispatchIndirect((int)Kernels.computeGridMip3, this.indirectChunkArgs2Buffer, indirectChunkId);
				ClayContainer.claycoreCompute.DispatchIndirect((int)Kernels.computeMeshRealTime, this.indirectChunkArgs2Buffer, indirectChunkId);
				ClayContainer.claycoreCompute.DispatchIndirect((int)Kernels.computeMeshRealTime2, this.indirectChunkArgs2Buffer, indirectChunkId);
			}

			ClayContainer.indirectArgsData[0] = 0;
			this.indirectDrawArgsBuffer.SetData(ClayContainer.indirectArgsData);

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.compactSmoothMesh, "numPointsInChunk", this.numPointsInChunkBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.compactSmoothMesh, "indirectDrawArgs", this.indirectDrawArgsBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.compactSmoothMesh, "pointToChunkId", this.pointToChunkIdBuffer);

			ClayContainer.claycoreCompute.DispatchIndirect((int)Kernels.compactSmoothMesh, this.indirectChunkArgs3Buffer, 0);
		}

		void computeClayPolySplat(){
			if(this.memoryOptimized){
				return;
			}

			#if UNITY_EDITOR
				if(this.editingThisContainer){
					ClayContainer.claycoreCompute.SetInt("storeSolidId", 1);
				}
				else{
					ClayContainer.claycoreCompute.SetInt("storeSolidId", 0);
				}
			#endif

			ClayContainer.indirectArgsData[0] = 0;
			this.indirectDrawArgsBuffer.SetData(ClayContainer.indirectArgsData);

			ClayContainer.claycoreCompute.SetInt("maxPointCount", ClayContainer.maxPointCount);

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGrid, "chunksCenter", this.chunksCenterBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridMip3, "chunksCenter", this.chunksCenterBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloud, "numPointsInChunk", this.numPointsInChunkBuffer);

			for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
				uint indirectChunkId = sizeof(int) * ((uint)chunkIt * 3);

				ClayContainer.claycoreCompute.SetInt("chunkId", chunkIt);
				ClayContainer.claycoreCompute.DispatchIndirect((int)Kernels.computeGrid, this.indirectChunkArgs1Buffer, indirectChunkId);
				ClayContainer.claycoreCompute.DispatchIndirect((int)Kernels.computeGridMip3, this.indirectChunkArgs2Buffer, indirectChunkId);
				ClayContainer.claycoreCompute.DispatchIndirect((int)Kernels.generatePointCloud, this.indirectChunkArgs2Buffer, indirectChunkId);
			}

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.compactPointCloud, "numPointsInChunk", this.numPointsInChunkBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.compactPointCloud, "pointCloudDataMip3", this.pointCloudDataMip3Buffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.compactPointCloud, "indirectDrawArgs", this.indirectDrawArgsBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.compactPointCloud, "pointToChunkId", this.pointToChunkIdBuffer);
			ClayContainer.claycoreCompute.DispatchIndirect((int)Kernels.compactPointCloud, this.indirectChunkArgs3Buffer, 0);
			
			this.splatRadius = (this.voxelSize * ((this.transform.lossyScale.x + this.transform.lossyScale.y + this.transform.lossyScale.z) / 3.0f));
		}

		void computeClayMicroVoxel(){
			ClayContainer.claycoreCompute.SetFloat("bufferSizeReduceFactor", ClayContainer.bufferSizeReduceFactor);

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGrid, "chunksCenter", this.chunksCenterBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.computeGridMip3, "chunksCenter", this.chunksCenterBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloudMicroVoxels, "boundingBox", this.boundingBoxBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloudMicroVoxels, "gridPointersMip3", this.gridPointersMip3Buffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloudMicroVoxels, "gridPointersMip2", this.gridPointersMip2Buffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloudMicroVoxels, "pointCloudDataMip3", this.pointCloudDataMip3Buffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.generatePointCloudMicroVoxels, "volumetricDraw", this.volumetricDrawBuffer);

			ClayContainer.microvoxelDrawData[0] = 36;
			ClayContainer.microvoxelDrawData[1] = this.numChunks * (this.instances.Count + 1);
			this.volumetricDrawBuffer.SetData(ClayContainer.microvoxelDrawData);

			if(this.numChunks == 1){
				this.boundingBoxBuffer.SetData(ClayContainer.microvoxelBoundingBoxData, 0, 0, 6 * ClayContainer.totalMaxChunks);
			}

			for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
				uint indirectChunkId = sizeof(int) * ((uint)chunkIt * 3);

				this.volumetricDrawBuffer.SetData(ClayContainer.microvoxelDrawData);

				ClayContainer.claycoreCompute.SetInt("chunkId", chunkIt);
				ClayContainer.claycoreCompute.DispatchIndirect((int)Kernels.computeGrid, this.indirectChunkArgs1Buffer, indirectChunkId);
				ClayContainer.claycoreCompute.DispatchIndirect((int)Kernels.computeGridMip3, this.indirectChunkArgs2Buffer, indirectChunkId);
				ClayContainer.claycoreCompute.DispatchIndirect((int)Kernels.generatePointCloudMicroVoxels, this.indirectChunkArgs1Buffer, indirectChunkId);
			}

			this.renderBounds.center = this.transform.position;

			//////
			// int numChunks = this.numChunks;
			// if(this.autoBounds){
			// 	numChunks = ClayContainer.totalMaxChunks;

			// 	if(this.renderMode == ClayContainer.RenderModes.smoothMesh && ClayContainer.limitSmoothMeshMemory){
			// 		if(numChunks > 8){
			// 			numChunks = 8;
			// 		}
			// 	}
			// }

			// int numInstances = this.instances.Count + 1;
			
			// // ClayContainer.microvoxelDrawData[0] = 36;
			// // ClayContainer.microvoxelDrawData[1] = numChunks * (this.instances.Count + 1);
			// // this.volumetricDrawBuffer.SetData(ClayContainer.microvoxelDrawData);
			
			// // this.instancesMatrixBuffer.SetData(this.instancesMatrix, 0, 0, numInstances);
			// // this.instancesMatrixInvBuffer.SetData(this.instancesMatrixInv, 0, 0, numInstances);

			// // this.checkContainerId();

			// // for(int chunkIt = 0; chunkIt < numChunks; ++chunkIt){
			// // 	this.genericIntBufferArray[0] = 0;
			// // 	this.instanceToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, chunkIt, 1);
				
			// // 	this.genericIntBufferArray[0] = this.containerId;
			// // 	this.chunkIdToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, chunkIt, 1);
			// // }

			// for(int i = 0; i < this.instances.Count; ++i){
			// 	// this.instances[i].checkContainerId();
				
			// 	int instanceId = i + 1;
				
			// 	for(int chunkIt = 0; chunkIt < numChunks; ++chunkIt){
			// 		this.genericIntBufferArray[0] = instanceId;
			// 		this.instanceToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * numChunks) + chunkIt, 1);

			// 		this.genericIntBufferArray[0] = this.instances[i].containerId;
			// 		this.chunkIdToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * numChunks) + chunkIt, 1);
			// 	}
			// }
/////////////////////////////////

			// int instanceId = 0;
			// for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
			// 	this.genericIntBufferArray[0] = instanceId;
			// 	this.instanceToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * this.numChunks) + chunkIt, 1);

			// 	this.genericIntBufferArray[0] = this.containerId;
			// 	this.chunkIdToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * this.numChunks) + chunkIt, 1);
			// }
			
			// for(int i = 0; i < this.instances.Count; ++i){
			// 	instanceId = i + 1;
				
			// 	for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
			// 		this.genericIntBufferArray[0] = instanceId;
			// 		this.instanceToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * this.numChunks) + chunkIt, 1);

			// 		this.genericIntBufferArray[0] = this.instances[i].containerId;
			// 		this.chunkIdToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * this.numChunks) + chunkIt, 1);
			// 	}
			// }
		
		}

		void updateInternalBounds(){
			this.chunkSize = this.clayDetailToChunkSize();
			
			if(this.autoBoundsChunkSize > this.chunkSize){
				this.chunkSize = this.autoBoundsChunkSize;
			}

			if(this.renderMode == ClayContainer.RenderModes.microVoxel){
				this.genericFloatBufferArray[0] = this.chunkSize;
				this.chunkSizeBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);
			}
			
			float voxelSize = (float)this.chunkSize / 256;

			this.voxelSize = voxelSize;
			this.splatRadius = this.voxelSize * ((this.transform.lossyScale.x + this.transform.lossyScale.y + this.transform.lossyScale.z) / 3.0f);
			
			this.boundsScale.x = (float)this.chunkSize * this.chunksX;
			this.boundsScale.y = (float)this.chunkSize * this.chunksY;
			this.boundsScale.z = (float)this.chunkSize * this.chunksZ;
			this.renderBounds.size = this.boundsScale * this.transform.lossyScale.x;

			float gridCenterOffset = (this.chunkSize * 0.5f);
			this.boundsCenter.x = ((this.chunkSize * (this.chunksX - 1)) * 0.5f) - (gridCenterOffset*(this.chunksX-1));
			this.boundsCenter.y = ((this.chunkSize * (this.chunksY - 1)) * 0.5f) - (gridCenterOffset*(this.chunksY-1));
			this.boundsCenter.z = ((this.chunkSize * (this.chunksZ - 1)) * 0.5f) - (gridCenterOffset*(this.chunksZ-1));

			if(this.autoBounds){
				this.needsUpdate = true;
			}
		}

		void drawClayxelPickingMesh(int containerId, CommandBuffer pickingCommandBuffer, bool pickClayObjects){
			if(this.needsInit && this.instanceOf == null){
				return;
			}

			if(this.instanceOf != null){
				return;
			}

			if(pickClayObjects){
				ClayContainer.pickingMeshMaterialProperties.SetInt("selectMode", 1);
			}
			else{
				ClayContainer.pickingMeshMaterialProperties.SetInt("selectMode", 0);
			}

			ClayContainer.pickingMeshMaterialProperties.SetMatrix("objectMatrix", this.transform.localToWorldMatrix);
			ClayContainer.pickingMeshMaterialProperties.SetInt("containerId", this.containerId);

			if(this.renderMode == ClayContainer.RenderModes.polySplat){
				ClayContainer.pickingMeshMaterialProperties.SetInt("pickRenderMode", 0);
				ClayContainer.pickingMeshMaterialProperties.SetFloat("chunkSize", (float)this.chunkSize);
				ClayContainer.pickingMeshMaterialProperties.SetBuffer("chunksCenter", this.chunksCenterBuffer);
				ClayContainer.pickingMeshMaterialProperties.SetBuffer("pointCloudDataToSolidId", ClayContainer.pointCloudDataToSolidIdBuffer);
				ClayContainer.pickingMeshMaterialProperties.SetFloat("splatRadius",  this.splatRadius);
				ClayContainer.pickingMeshMaterialProperties.SetBuffer("chunkPoints", this.pointCloudDataMip3Buffer);
				ClayContainer.pickingMeshMaterialProperties.SetBuffer("pointToChunkId", this.pointToChunkIdBuffer);

				ClayContainer.pickingCommandBuffer.DrawProceduralIndirect(Matrix4x4.identity, ClayContainer.pickingMeshMaterialPolySplat, -1, 
					MeshTopology.Triangles, this.indirectDrawArgsBuffer, 0, ClayContainer.pickingMeshMaterialProperties);
			}
			else if(this.renderMode == ClayContainer.RenderModes.smoothMesh){
				ClayContainer.pickingMeshMaterialProperties.SetInt("pickRenderMode", 1);
				ClayContainer.pickingMeshMaterialProperties.SetBuffer("smoothMeshPoints", this.smoothMeshPointsBuffer);
				ClayContainer.pickingMeshMaterialProperties.SetBuffer("smoothMeshNormals", this.smoothMeshNormalsBuffer);
				ClayContainer.pickingMeshMaterialProperties.SetBuffer("pointToChunkId", this.pointToChunkIdBuffer);

				if(this.memoryOptimized){
					ClayContainer.pickingMeshMaterialProperties.SetInt("memoryOptimized", 1);
				}
				else{
					ClayContainer.pickingMeshMaterialProperties.SetInt("memoryOptimized", 0);
				}

				ClayContainer.pickingCommandBuffer.DrawProceduralIndirect(Matrix4x4.identity, ClayContainer.pickingMeshMaterialSmoothMesh, -1, 
					MeshTopology.Triangles, this.indirectDrawArgsBuffer, 0, ClayContainer.pickingMeshMaterialProperties);
			}

			for(int i = 0; i < this.instances.Count; ++i){
				ClayContainer instance = this.instances[i];

				ClayContainer.pickingMeshMaterialProperties.SetInt("containerId", instance.containerId);
				ClayContainer.pickingMeshMaterialProperties.SetMatrix("objectMatrix", instance.transform.localToWorldMatrix);

				if(this.renderMode == ClayContainer.RenderModes.polySplat){
					ClayContainer.pickingCommandBuffer.DrawProceduralIndirect(Matrix4x4.identity, ClayContainer.pickingMeshMaterialPolySplat, -1, 
						MeshTopology.Triangles, this.indirectDrawArgsBuffer, 0, ClayContainer.pickingMeshMaterialProperties);
				}
				else if(this.renderMode == ClayContainer.RenderModes.smoothMesh){
					ClayContainer.pickingCommandBuffer.DrawProceduralIndirect(Matrix4x4.identity, ClayContainer.pickingMeshMaterialSmoothMesh, -1, 
						MeshTopology.Triangles, this.indirectDrawArgsBuffer, 0, ClayContainer.pickingMeshMaterialProperties);
				}			
			}
		}

		static void collectClayObjectsRecursive(GameObject obj, ref List<GameObject> collection){
			if(obj.GetComponent<ClayObject>() != null){
				collection.Add(obj);
			}

			for(int i = 0; i < obj.transform.childCount; ++i){
				GameObject childObj = obj.transform.GetChild(i).gameObject;

				if(childObj.GetComponent<ClayContainer>() == null){
					ClayContainer.collectClayObjectsRecursive(childObj, ref collection);
				}
			}
		}

		public static int[] getMemoryStats(){
			int[] memStats = new int[]{0, 0};
			
			try{
				float upfrontMem = 0;
				for(int i = 0; i < ClayContainer.globalCompBuffers.Count; ++i){
					if(ClayContainer.globalCompBuffers[i] != null){
						long size = ((long)ClayContainer.globalCompBuffers[i].count * (ClayContainer.globalCompBuffers[i].stride/4)) * 32;
						float sizeMb = size / (float)8e+6;

						upfrontMem += sizeMb;
					}
				}

				memStats[0] = (int)upfrontMem;

				float sceneContainersMb = 0;
				foreach(var item in ClayContainer.containersInScene){
					ClayContainer container = (ClayContainer)item.Value;

					if(container != null){
						for(int i = 0; i < container.compBuffers.Count; ++i){
							if(container.compBuffers[i] != null){
								long size = ((long)container.compBuffers[i].count * (container.compBuffers[i].stride/4)) * 32;
								float sizeMb = size / (float)8e+6;

								sceneContainersMb += sizeMb;
							}
						}
					}
				}

				memStats[1] = (int)sceneContainersMb;
			}
			catch{
			}
			
			return memStats;
		}

		public static void applyPrefs(){
			ClayxelsPrefs prefs = ClayContainer.loadPrefs();

			if(prefs == null){
				Debug.Log("Clayxels: invalid prefs file detected!");
				return;
			}

			ClayContainer.directPickEnabled = prefs.directPickEnabled;
			ClayContainer.directPick = ClayContainer.directPickEnabled;

			ClayContainer.boundsColor = new Color32((byte)prefs.boundsColor[0], (byte)prefs.boundsColor[1], (byte)prefs.boundsColor[2], (byte)prefs.boundsColor[3]);
			ClayContainer.pickingKey = prefs.pickingKey;
			ClayContainer.mirrorDuplicateKey = prefs.mirrorDuplicateKey;
			
			int[] pointCountPreset = new int[]{300000, 900000, 2000000};
			ClayContainer.maxPointCount = pointCountPreset[prefs.maxPointCount];

			float[] reduction = new float[]{0.25f, 0.5f, 1.0f};
			ClayContainer.bufferSizeReduceFactor = reduction[prefs.maxPointCount];
			
			int[] solidsCountPreset = new int[]{512, 4096, 16384};
			ClayContainer.maxSolids = solidsCountPreset[prefs.maxSolidsCount];

			int[] solidsPerVoxelPreset = new int[]{128, 512, 2048};
			ClayContainer.maxSolidsPerVoxel = solidsPerVoxelPreset[prefs.maxSolidsPerVoxel];

			ClayContainer.frameSkip = prefs.frameSkip;
			ClayContainer.setMaxBounds(prefs.maxBounds, prefs.limitSmoothMeshMemory);

			ClayContainer.globalBlend = prefs.globalBlend;

			ClayContainer.globalMicrovoxelSplatsQuality = prefs.globalMicrovoxelSplatsQuality;

			ClayContainer.globalMicrovoxelRayIterations = prefs.globalMicrovoxelRayIterations;

			ClayContainer.microvoxelCanGetInside = prefs.microvoxelCameraCanGetInside;

			ClayContainer.defaultAssetsPath = prefs.defaultAssetsPath;

			int cameraWidth = 2048;
			int cameraHeight = 2048;
			if(Camera.main != null){
				cameraWidth = Camera.main.pixelWidth;
				cameraHeight = Camera.main.pixelHeight;
			}

			if(prefs.renderSize.x < 512){
				prefs.renderSize.x = 512;
			}
			else if(prefs.renderSize.x > cameraWidth){
				prefs.renderSize.x = cameraWidth;	
			}

			if(prefs.renderSize.y < 512){
				prefs.renderSize.y = 512;
			}
			else if(prefs.renderSize.y > cameraHeight){
				prefs.renderSize.y = cameraHeight;	
			}
			
			ClayContainer.microvoxelRTSizeOverride = prefs.renderSize;

			#if UNITY_EDITOR
				if(!AssetDatabase.IsValidFolder("Assets/" + ClayContainer.defaultAssetsPath)){
					AssetDatabase.CreateFolder("Assets", ClayContainer.defaultAssetsPath);
				}
			#endif
		}

		public static ClayxelsPrefs loadPrefs(){
			ClayxelsPrefs prefs = null;

			try{
	    		TextAsset configTextAsset = (TextAsset)Resources.Load("clayxelsPrefs", typeof(TextAsset));
	    		prefs = JsonUtility.FromJson<ClayxelsPrefs>(configTextAsset.text);
	    	}
	    	catch{
	    		#if UNITY_EDITOR
		    		ClayContainer.checkPrefsIntegrity();

		    		TextAsset configTextAsset = (TextAsset)Resources.Load("clayxelsPrefs", typeof(TextAsset));
		    		prefs = JsonUtility.FromJson<ClayxelsPrefs>(configTextAsset.text);
		    	#endif
	    	}

	    	return prefs;
		}

		void transferMaterialPropertiesToMesh(Material sharedMat){
			if(sharedMat == null){
				return;
			}

			for(int propertyId = 0; propertyId < this.material.shader.GetPropertyCount(); ++propertyId){
				ShaderPropertyType type = this.material.shader.GetPropertyType(propertyId);
				string name = this.material.shader.GetPropertyName(propertyId);
				
				if(sharedMat.shader.FindPropertyIndex(name) != -1){
					if(type == ShaderPropertyType.Color || type == ShaderPropertyType.Vector){
						sharedMat.SetVector(name, this.material.GetVector(name));
					}
					else if(type == ShaderPropertyType.Float || type == ShaderPropertyType.Range){
						sharedMat.SetFloat(name, this.material.GetFloat(name));
					}
					else if(type == ShaderPropertyType.Texture){
						sharedMat.SetTexture(name, this.material.GetTexture(name));
					}
				}
			}
		}

		void optimizeMemoryPolySplat(){
			this.memoryOptimized = true;
			this.userWarning = "";
			this.updateFrame = 0;

			int reducedMip3BufferSize = (int)(((float)(256 * 256 * 256) * this.numChunks) * ClayContainer.bufferSizeReduceFactor);
			int[] tmpPointCloudDataStorage = new int[reducedMip3BufferSize * 2];

			this.indirectDrawArgsBuffer.GetData(ClayContainer.indirectArgsData);	
			
			int pointCount = ClayContainer.indirectArgsData[0] / 3;

			if(pointCount > ClayContainer.maxPointCount){
				pointCount = ClayContainer.maxPointCount;

				this.userWarning = "max point count exceeded, increase limit from Global Config window";

				Debug.Log("Clayxels: container " + this.gameObject.name + " has exceeded the limit of points allowed, increase limit from Global Config window");
			}
			
			if(pointCount > 0){
				float accurateBoundsSize = this.computeBoundsSize() * this.transform.lossyScale.x;
				this.renderBounds.size = new Vector3(accurateBoundsSize, accurateBoundsSize, accurateBoundsSize);

				int bufferId = this.compBuffers.IndexOf(this.pointCloudDataMip3Buffer);
				this.pointCloudDataMip3Buffer.GetData(tmpPointCloudDataStorage, 0, 0, pointCount * 2);
				this.pointCloudDataMip3Buffer.Release();
				this.pointCloudDataMip3Buffer = new ComputeBuffer(pointCount, sizeof(int) * 2);
				this.compBuffers[bufferId] = this.pointCloudDataMip3Buffer;
				this.pointCloudDataMip3Buffer.SetData(tmpPointCloudDataStorage, 0, 0, pointCount * 2);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizePointCloud, "pointCloudDataMip3", this.pointCloudDataMip3Buffer);
				this.materialProperties.SetBuffer("chunkPoints", this.pointCloudDataMip3Buffer);

				bufferId = this.compBuffers.IndexOf(this.pointToChunkIdBuffer);
				int pointToChunkBufferSize = (pointCount / 5) + 1;
				this.pointToChunkIdBuffer.GetData(tmpPointCloudDataStorage, 0, 0, pointToChunkBufferSize);
				this.pointToChunkIdBuffer.Release();
				this.pointToChunkIdBuffer = new ComputeBuffer(pointToChunkBufferSize, sizeof(int));
				this.compBuffers[bufferId] = this.pointToChunkIdBuffer;
				this.pointToChunkIdBuffer.SetData(tmpPointCloudDataStorage, 0, 0, pointToChunkBufferSize);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizePointCloud, "pointToChunkId", this.pointToChunkIdBuffer);
				this.materialProperties.SetBuffer("pointToChunkId", this.pointToChunkIdBuffer);

				this.materialProperties.SetInt("solidHighlightId", -1);

				this.indirectDrawArgsBuffer2.SetData(ClayContainer.indirectArgsData);

				this.renderIndirectDrawArgsBuffer = this.indirectDrawArgsBuffer2;

				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizePointCloud, "indirectDrawArgs", this.indirectDrawArgsBuffer);
					
				ClayContainer.claycoreCompute.Dispatch((int)Kernels.optimizePointCloud, 1, 1, 1);
			}
			
			ClayContainer.indirectArgsData[0] = 0;
		}

		void expandMemorySmoothMesh(){
			this.memoryOptimized = false;

			ClayContainer.indirectArgsData[0] = 0;
			this.indirectDrawArgsBuffer.SetData(ClayContainer.indirectArgsData);

			int meshMaxChunks = this.numChunks;
			if(this.autoBounds){
				meshMaxChunks = ClayContainer.totalMaxChunks;
				if(meshMaxChunks > 8){
					meshMaxChunks = 8;
				}
			}

			int reducedMip3BufferSizeMesh = (int)(((float)(256 * 256 * 256) * meshMaxChunks) * ClayContainer.bufferSizeReduceFactor);

			int bufferId = this.compBuffers.IndexOf(this.smoothMeshPointsBuffer);
			this.smoothMeshPointsBuffer.Release();
			this.smoothMeshPointsBuffer = new ComputeBuffer(reducedMip3BufferSizeMesh, sizeof(float) * 3);
			this.compBuffers[bufferId] = this.smoothMeshPointsBuffer;

			bufferId = this.compBuffers.IndexOf(this.smoothMeshNormalsBuffer);
			this.smoothMeshNormalsBuffer.Release();
			this.smoothMeshNormalsBuffer = new ComputeBuffer(reducedMip3BufferSizeMesh, sizeof(float) * 4);
			this.compBuffers[bufferId] = this.smoothMeshNormalsBuffer;

			this.smoothMeshNormalsTempBuffer = new ComputeBuffer((256 * 256 * 256), sizeof(float) * 3);
			this.compBuffers.Add(this.smoothMeshNormalsTempBuffer); 

			this.smoothMeshGridDataBuffer = new ComputeBuffer((256 * 256 * 256), sizeof(int) * 3);
			this.compBuffers.Add(this.smoothMeshGridDataBuffer); 

			bufferId = this.compBuffers.IndexOf(this.pointToChunkIdBuffer);
			this.pointToChunkIdBuffer.Release();
			this.pointToChunkIdBuffer = new ComputeBuffer(reducedMip3BufferSizeMesh, sizeof(int));
			this.compBuffers[bufferId] = this.pointToChunkIdBuffer;
		}

		void expandMemoryPolySplat(){
			this.memoryOptimized = false;

			int numChunks = this.numChunks;
			if(this.autoBounds){
				numChunks = ClayContainer.totalMaxChunks;
			}

			int bufferId = this.compBuffers.IndexOf(this.pointCloudDataMip3Buffer);
			this.pointCloudDataMip3Buffer.Release();
			this.pointCloudDataMip3Buffer = new ComputeBuffer(ClayContainer.maxPointCount * this.numChunks, sizeof(int) * 2);
			this.compBuffers[bufferId] = this.pointCloudDataMip3Buffer;

			this.materialProperties.SetBuffer("chunkPoints", this.pointCloudDataMip3Buffer);

			bufferId = this.compBuffers.IndexOf(this.pointToChunkIdBuffer);
			this.pointToChunkIdBuffer.Release();
			this.pointToChunkIdBuffer = new ComputeBuffer((ClayContainer.maxPointCount / 5) * this.numChunks, sizeof(int));
			this.compBuffers[bufferId] = this.pointToChunkIdBuffer;

			this.renderIndirectDrawArgsBuffer = this.indirectDrawArgsBuffer;
		}

		void optimizeMemory(){
			if(this.memoryOptimized){
				return;
			}

			if(this.interactive){
				return;
			}
			
			if(this.needsUpdate){
				this.computeClay();
			}

			if(this.renderMode == ClayContainer.RenderModes.polySplat){
				this.optimizeMemoryPolySplat();
			}
			else if(this.renderMode == ClayContainer.RenderModes.microVoxel){
				this.optimizeMemoryMicrovoxels();
			}
			else if(this.renderMode == ClayContainer.RenderModes.smoothMesh){
				this.optimizeMemorySmoothMesh();
			}
		}

		void optimizeMemoryNoCompute(){
			if(this.memoryOptimized){
				return;
			}

			if(this.interactive){
				return;
			}
			
			if(this.renderMode == ClayContainer.RenderModes.polySplat){
				this.optimizeMemoryPolySplat();
			}
			else if(this.renderMode == ClayContainer.RenderModes.microVoxel){
				this.optimizeMemoryMicrovoxels();
			}
			else if(this.renderMode == ClayContainer.RenderModes.smoothMesh){
				this.optimizeMemorySmoothMesh();
			}
		}

		void optimizeMemorySmoothMesh(){
			this.memoryOptimized = true;
			this.userWarning = "";
			this.updateFrame = 0;

			this.smoothMeshNormalsTempBuffer.Release();
			this.compBuffers.Remove(this.smoothMeshNormalsTempBuffer);
			this.smoothMeshNormalsTempBuffer = null;

			this.smoothMeshGridDataBuffer.Release();
			this.compBuffers.Remove(this.smoothMeshGridDataBuffer);
			this.smoothMeshGridDataBuffer = null;

			this.pointToChunkIdBuffer.Release();
			this.compBuffers.Remove(this.pointToChunkIdBuffer);
			this.pointToChunkIdBuffer = new ComputeBuffer(1, sizeof(int));// we still need this as a dummy buffer
			this.compBuffers.Add(this.pointToChunkIdBuffer);

			this.indirectDrawArgsBuffer.GetData(ClayContainer.indirectArgsData);
			int numElements = ClayContainer.indirectArgsData[0];

			if(numElements == 0){
				this.smoothMeshPointsBuffer.Release();
				this.compBuffers.Remove(this.smoothMeshPointsBuffer);
				this.smoothMeshPointsBuffer = new ComputeBuffer(1, sizeof(float) * 3);
				this.compBuffers.Add(this.smoothMeshPointsBuffer);

				this.smoothMeshNormalsBuffer.Release();
				this.compBuffers.Remove(this.smoothMeshNormalsBuffer);
				this.smoothMeshNormalsBuffer = new ComputeBuffer(1, sizeof(float) * 3);
				this.compBuffers.Add(this.smoothMeshNormalsBuffer);
				
				return;
			}

			int reducedMip3BufferSizeMesh = (int)(((float)(256 * 256 * 256) * this.numChunks) * ClayContainer.bufferSizeReduceFactor);
			if(numElements >= reducedMip3BufferSizeMesh){
				this.userWarning = "max point count exceeded, increase limit from Global Config window";
				Debug.Log("Clayxels: container " + this.gameObject.name + " has exceeded the limit of points allowed, increase limit from Global Config window");
				
				return;
			}
			
			ComputeBuffer smoothMeshPointsOptBuffer = new ComputeBuffer(numElements, sizeof(float) * 3);
			ComputeBuffer smoothMeshNormalsOptBuffer = new ComputeBuffer(numElements, sizeof(float) * 4);

			ClayContainer.indirectArgsData[0] = 0;
			this.indirectDrawArgsBuffer.SetData(ClayContainer.indirectArgsData);

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeSmoothMesh, "numPointsInChunk", this.numPointsInChunkBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeSmoothMesh, "meshPoints", this.smoothMeshPointsBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeSmoothMesh, "meshNormals", this.smoothMeshNormalsBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeSmoothMesh, "meshPointsOpt", smoothMeshPointsOptBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeSmoothMesh, "meshNormalsOpt", smoothMeshNormalsOptBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeSmoothMesh, "indirectDrawArgs", this.indirectDrawArgsBuffer);

			reducedMip3BufferSizeMesh = (int)((float)(256 * 256 * 256) * ClayContainer.bufferSizeReduceFactor);
			ClayContainer.claycoreCompute.SetInt("maxPointCount", reducedMip3BufferSizeMesh);
			
			ClayContainer.claycoreCompute.Dispatch((int)Kernels.optimizeSmoothMesh, this.chunksX, this.chunksY, this.chunksZ);

			this.smoothMeshPointsBuffer.Release();
			this.compBuffers.Remove(this.smoothMeshPointsBuffer);
			this.smoothMeshPointsBuffer = smoothMeshPointsOptBuffer;
			this.compBuffers.Add(this.smoothMeshPointsBuffer);

			this.smoothMeshNormalsBuffer.Release();
			this.compBuffers.Remove(this.smoothMeshNormalsBuffer);
			this.smoothMeshNormalsBuffer = smoothMeshNormalsOptBuffer;
			this.compBuffers.Add(this.smoothMeshNormalsBuffer);
		}

		void optimizeMemoryMicrovoxels(){
			this.memoryOptimized = true;
			this.userWarning = "";
			this.updateFrame = 0;

			ComputeBuffer microvoxelCountersBuffer = new ComputeBuffer(3, sizeof(int));

			int[] counters = new int[]{0, 0, 0};
			microvoxelCountersBuffer.SetData(new int[]{0, 0, 0});

			int reducedMip3BufferSize = (int)(((float)(256 * 256 * 256) * this.numChunks) * ClayContainer.bufferSizeReduceFactor);

			ComputeBuffer gridPointersMip3OptBuffer = new ComputeBuffer(reducedMip3BufferSize, sizeof(int));
			ComputeBuffer gridPointersMip2OptBuffer = new ComputeBuffer((64 * 64 * 64) * this.numChunks, sizeof(int));
			ComputeBuffer pointCloudDataMip3OptBuffer = new ComputeBuffer(reducedMip3BufferSize, sizeof(int) * 2);

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeMicrovoxels, "microvoxelCounters", microvoxelCountersBuffer);

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeMicrovoxels, "gridPointersMip3", this.gridPointersMip3Buffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeMicrovoxels, "gridPointersMip2", this.gridPointersMip2Buffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeMicrovoxels, "pointCloudDataMip3", this.pointCloudDataMip3Buffer);

			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeMicrovoxels, "chunkIdOffset", this.chunkIdOffsetBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeMicrovoxels, "gridPointersMip3Opt", gridPointersMip3OptBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeMicrovoxels, "gridPointersMip2Opt", gridPointersMip2OptBuffer);
			ClayContainer.claycoreCompute.SetBuffer((int)Kernels.optimizeMicrovoxels, "pointCloudDataMip3Opt", pointCloudDataMip3OptBuffer);

			ClayContainer.claycoreCompute.Dispatch((int)Kernels.optimizeMicrovoxels, this.chunksX, this.chunksY, this.chunksZ);

			// remove expanded buffers if they aren't borrowed from global memory
			if(this.gridPointersMip3Buffer !=  null && this.gridPointersMip3Buffer != ClayContainer.gridPointersMip3GlobBuffer){
				this.compBuffers.Remove(this.gridPointersMip3Buffer);
				this.gridPointersMip3Buffer.Release();
			}

			if(this.gridPointersMip2Buffer != null && this.gridPointersMip2Buffer != ClayContainer.gridPointersMip2GlobBuffer){
				this.compBuffers.Remove(this.gridPointersMip2Buffer);
				this.gridPointersMip2Buffer.Release();
			}

			if(this.pointCloudDataMip3Buffer != null && this.pointCloudDataMip3Buffer != ClayContainer.pointCloudDataMip3GlobBuffer){
				this.compBuffers.Remove(this.pointCloudDataMip3Buffer);
				this.pointCloudDataMip3Buffer.Release();
			}
			
			// extract counters of optimized buffers
			microvoxelCountersBuffer.GetData(counters);
			int numMip2Elements = counters[0]; 
			int numMip3Elements = counters[1];
			int numPointCloudMip3Elements = counters[2];

			if(numMip3Elements == 0){
				microvoxelCountersBuffer.Release();
				gridPointersMip3OptBuffer.Release();
				gridPointersMip2OptBuffer.Release();
				pointCloudDataMip3OptBuffer.Release();

				return;
			}
			
			if(numMip3Elements >= reducedMip3BufferSize){
				this.userWarning = "max point count exceeded, increase limit from Global Config window";
				Debug.Log("Clayxels: container " + this.gameObject.name + " has exceeded the limit of points allowed, increase limit from Global Config window");
				
				microvoxelCountersBuffer.Release();
				gridPointersMip3OptBuffer.Release();
				gridPointersMip2OptBuffer.Release();
				pointCloudDataMip3OptBuffer.Release();

				return;
			}

			microvoxelCountersBuffer.Release();

			// trim compacted buffers
			gridPointersMip2OptBuffer.GetData(ClayContainer.tmpPointCloudDataStorage, 0, 0, numMip2Elements);
			this.gridPointersMip2Buffer = new ComputeBuffer(numMip2Elements, sizeof(int));
			this.gridPointersMip2Buffer.SetData(ClayContainer.tmpPointCloudDataStorage, 0, 0, numMip2Elements);
			this.compBuffers.Add(this.gridPointersMip2Buffer);
			gridPointersMip2OptBuffer.Release();

			gridPointersMip3OptBuffer.GetData(ClayContainer.tmpPointCloudDataStorage, 0, 0, numMip3Elements);
			this.gridPointersMip3Buffer = new ComputeBuffer(numMip3Elements, sizeof(int));
			this.gridPointersMip3Buffer.SetData(ClayContainer.tmpPointCloudDataStorage, 0, 0, numMip3Elements);
			this.compBuffers.Add(this.gridPointersMip3Buffer);
			gridPointersMip3OptBuffer.Release();

			pointCloudDataMip3OptBuffer.GetData(ClayContainer.tmpPointCloudDataStorage, 0, 0, numPointCloudMip3Elements * 2);
			this.pointCloudDataMip3Buffer = new ComputeBuffer(numPointCloudMip3Elements, sizeof(int) * 2);
			this.pointCloudDataMip3Buffer.SetData(ClayContainer.tmpPointCloudDataStorage, 0, 0, numPointCloudMip3Elements * 2);
			this.compBuffers.Add(this.pointCloudDataMip3Buffer);
			pointCloudDataMip3OptBuffer.Release();
		}

		void expandMemory(){
			if(!this.memoryOptimized){
				return;
			}
			
			if(this.renderMode == ClayContainer.RenderModes.polySplat){
				this.expandMemoryPolySplat();
			}
			else if(this.renderMode == ClayContainer.RenderModes.microVoxel){
				this.expandMemoryMicroVoxel();
			}
			else if(this.renderMode == ClayContainer.RenderModes.smoothMesh){
				this.expandMemorySmoothMesh();
			}

			this.forceUpdateAllSolids();
		}

		void expandMemoryMicroVoxel(){
			this.switchMicrovoxelSharedMemory();
			
			this.memoryOptimized = false;
			
			int numChunks = this.numChunks;
			if(this.autoBounds){
				numChunks = ClayContainer.totalMaxChunks;
			}

			// remove expanded buffers if they aren't borrowed from global memory
			if(this.gridPointersMip3Buffer !=  null && this.gridPointersMip3Buffer != ClayContainer.gridPointersMip3GlobBuffer){
				this.compBuffers.Remove(this.gridPointersMip3Buffer);
				this.gridPointersMip3Buffer.Release();
			}

			if(this.gridPointersMip2Buffer != null && this.gridPointersMip2Buffer != ClayContainer.gridPointersMip2GlobBuffer){
				this.compBuffers.Remove(this.gridPointersMip2Buffer);
				this.gridPointersMip2Buffer.Release();
			}

			if(this.pointCloudDataMip3Buffer != null && this.pointCloudDataMip3Buffer != ClayContainer.pointCloudDataMip3GlobBuffer){
				this.compBuffers.Remove(this.pointCloudDataMip3Buffer);
				this.pointCloudDataMip3Buffer.Release();
			}

			int reducedMip3BufferSize = (int)(((float)(256 * 256 * 256) * numChunks) * ClayContainer.bufferSizeReduceFactor);

			this.chunkIdOffsetBuffer.SetData(ClayContainer.chunkIdOffsetDefaultData);

			if(this.interactive){
				this.gridPointersMip3Buffer = new ComputeBuffer(reducedMip3BufferSize, sizeof(int));
				this.compBuffers.Add(this.gridPointersMip3Buffer);

				this.pointCloudDataMip3Buffer = new ComputeBuffer(reducedMip3BufferSize, sizeof(int) * 2);
				this.compBuffers.Add(this.pointCloudDataMip3Buffer);

				this.gridPointersMip2Buffer = new ComputeBuffer((64 * 64 * 64) * numChunks, sizeof(int));
				this.compBuffers.Add(this.gridPointersMip2Buffer);
			}
			else{
				this.gridPointersMip3Buffer = ClayContainer.gridPointersMip3GlobBuffer;
				this.pointCloudDataMip3Buffer = ClayContainer.pointCloudDataMip3GlobBuffer;
				this.gridPointersMip2Buffer = ClayContainer.gridPointersMip2GlobBuffer;
			}

			ClayContainer.microvoxelDrawData[0] = 36;
			ClayContainer.microvoxelDrawData[1] = this.numChunks * (this.instances.Count + 1);
			this.volumetricDrawBuffer.SetData(ClayContainer.microvoxelDrawData);

			this.updateChunksBuffer.SetData(ClayContainer.updateChunksDefaultValues, 0, 0, numChunks);
			this.indirectChunkArgs1Buffer.SetData(ClayContainer.indirectChunk1DefaultValues, 0, 0, numChunks * 3);
			this.indirectChunkArgs2Buffer.SetData(ClayContainer.indirectChunk2DefaultValues, 0, 0, numChunks * 3);
			this.indirectChunkArgs3Buffer.SetData(new int[]{this.chunksX, this.chunksY, this.chunksZ});

			int instanceId = 0;
			for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
				this.genericIntBufferArray[0] = instanceId;
				this.instanceToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * this.numChunks) + chunkIt, 1);

				this.genericIntBufferArray[0] = this.containerId;
				this.chunkIdToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * this.numChunks) + chunkIt, 1);
			}
			
			for(int i = 0; i < this.instances.Count; ++i){
				instanceId = i + 1;
				
				for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
					this.genericIntBufferArray[0] = instanceId;
					this.instanceToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * this.numChunks) + chunkIt, 1);

					this.genericIntBufferArray[0] = this.instances[i].containerId;
					this.chunkIdToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * this.numChunks) + chunkIt, 1);
				}
			}
		}

		static void parseSolidsAttrs(string content, ref int lastParsed){
			string[] lines = content.Split(new[]{ "\r\n", "\r", "\n" }, StringSplitOptions.None);
			for(int i = 0; i < lines.Length; ++i){
				string line = lines[i];
				if(line.Contains("label: ")){
					if(line.Split('/').Length == 3){// if too many comment slashes, it's a commented out solid,
						lastParsed += 1;

						string[] parameters = line.Split(new[]{"label:"}, StringSplitOptions.None)[1].Split(',');
						string label = parameters[0].Trim();
						
						ClayContainer.solidsCatalogueLabels.Add(label);

						List<string[]> paramList = new List<string[]>();

						for(int paramIt = 1; paramIt < parameters.Length; ++paramIt){
							string param = parameters[paramIt];
							string[] attrs = param.Split(':');
							string paramId = attrs[0];
							string[] paramLabelValue = attrs[1].Split(' ');
							string paramLabel = paramLabelValue[1];
							string paramValue = paramLabelValue[2];

							paramList.Add(new string[]{paramId.Trim(), paramLabel.Trim(), paramValue.Trim()});
						}

						ClayContainer.solidsCatalogueParameters.Add(paramList);
					}
				}
			}
		}

		void initSolidsData(){
			this.solidsPosBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(float) * 3);
			this.compBuffers.Add(this.solidsPosBuffer);
			this.solidsRotBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(float) * 4);
			this.compBuffers.Add(this.solidsRotBuffer);
			this.solidsScaleBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(float) * 3);
			this.compBuffers.Add(this.solidsScaleBuffer);
			this.solidsBlendBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(float));
			this.compBuffers.Add(this.solidsBlendBuffer);
			this.solidsTypeBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(int));
			this.compBuffers.Add(this.solidsTypeBuffer);
			this.solidsColorBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(float) * 3);
			this.compBuffers.Add(this.solidsColorBuffer);
			this.solidsAttrsBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(float) * 4);
			this.compBuffers.Add(this.solidsAttrsBuffer);
			this.solidsAttrs2Buffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(float) * 4);
			this.compBuffers.Add(this.solidsAttrs2Buffer);
			this.solidsClayObjectIdBuffer = new ComputeBuffer(ClayContainer.maxSolids, sizeof(int));
			this.compBuffers.Add(this.solidsClayObjectIdBuffer);

			this.solidsPos = new List<Vector3>(new Vector3[ClayContainer.maxSolids]);
			this.solidsRot = new List<Quaternion>(new Quaternion[ClayContainer.maxSolids]);
			this.solidsScale = new List<Vector3>(new Vector3[ClayContainer.maxSolids]);
			this.solidsBlend = new List<float>(new float[ClayContainer.maxSolids]);
			this.solidsType = new List<int>(new int[ClayContainer.maxSolids]);
			this.solidsColor = new List<Vector3>(new Vector3[ClayContainer.maxSolids]);
			this.solidsAttrs = new List<Vector4>(new Vector4[ClayContainer.maxSolids]);
			this.solidsAttrs2 = new List<Vector4>(new Vector4[ClayContainer.maxSolids]);
			this.solidsClayObjectId = new List<int>(new int[ClayContainer.maxSolids]);
		}

		void OnDisable(){
			this.visible = false;
		}

		void OnEnable(){
			this.visible = true;
		}

		void OnDestroy(){
			this.invalidated = true;

			this.releaseBuffers();
			
			this.removeFromScene();

			if(ClayContainer.containersInScene.Count == 0){
				ClayContainer.releaseGlobalBuffers();
			}

			for(int i = 0; i < this.instances.Count; ++i){
				if(this.instances[i] != null){
					this.instances[i].instanceOf = null;
				}
			}

			this.instances.Clear();
			
			if(this.instanceOf != null){
				this.instanceOf.instances.Remove(this);
				if(!this.instanceOf.needsInit){
					this.instanceOf.initInstancesData();
				}
			}

			#if UNITY_EDITOR
				this.removeEditorEvents();
				if(!Application.isPlaying){
					if(ClayContainer.containersToRender.Count == 0){
						ClayContainer.globalDataNeedsInit = true;
					}
				}
			#endif
		}

		void releaseBuffers(){
			for(int i = 0; i < this.compBuffers.Count; ++i){
				if(this.compBuffers[i] != null){
					this.compBuffers[i].Release();
				}
			}

			this.compBuffers.Clear();
		}

		static void releaseGlobalBuffers(){
			for(int i = 0; i < ClayContainer.globalCompBuffers.Count; ++i){
				ClayContainer.globalCompBuffers[i].Release();
			}

			ClayContainer.globalCompBuffers.Clear();

			ClayContainer.globalDataNeedsInit = true;
		}

		void limitChunkValues(){
			int maxX = ClayContainer.maxChunkX;
			int maxY = ClayContainer.maxChunkY;
			int maxZ = ClayContainer.maxChunkZ;

			if(this.renderMode == ClayContainer.RenderModes.smoothMesh && ClayContainer.limitSmoothMeshMemory){
				maxX = 2;
				maxY = 2;
				maxZ = 2;
			}

			if(this.chunksX > maxX){
				this.chunksX = maxX;
			}
			if(this.chunksY > maxY){
				this.chunksY = maxY;
			}
			if(this.chunksZ > maxZ){
				this.chunksZ = maxZ;
			}
			if(this.chunksX < 1){
				this.chunksX = 1;
			}
			if(this.chunksY < 1){
				this.chunksY = 1;
			}
			if(this.chunksZ < 1){
				this.chunksZ = 1;
			}
		}

		void initChunks(){
			if(this.autoBounds){
				int maxX = ClayContainer.maxChunkX;
				int maxY = ClayContainer.maxChunkY;
				int maxZ = ClayContainer.maxChunkZ;
				
				if(this.renderMode == ClayContainer.RenderModes.smoothMesh && ClayContainer.limitSmoothMeshMemory){
					maxX = 2;
					maxY = 2;
					maxZ = 2;
				}

				this.chunksX = maxX;
				this.chunksY = maxY;
				this.chunksZ = maxZ;
			}

			this.numChunks = this.chunksX * this.chunksY * this.chunksZ;

			this.boundsScale.x = (float)this.chunkSize * this.chunksX;
			this.boundsScale.y = (float)this.chunkSize * this.chunksY;
			this.boundsScale.z = (float)this.chunkSize * this.chunksZ;
			this.renderBounds.size = this.boundsScale * this.transform.lossyScale.x;

			float gridCenterOffset = (this.chunkSize * 0.5f);
			this.boundsCenter.x = ((this.chunkSize * (this.chunksX - 1)) * 0.5f) - (gridCenterOffset*(this.chunksX-1));
			this.boundsCenter.y = ((this.chunkSize * (this.chunksY - 1)) * 0.5f) - (gridCenterOffset*(this.chunksY-1));
			this.boundsCenter.z = ((this.chunkSize * (this.chunksZ - 1)) * 0.5f) - (gridCenterOffset*(this.chunksZ-1));

			this.materialProperties = new MaterialPropertyBlock();

			this.numPointsInChunkBuffer = new ComputeBuffer(this.numChunks, sizeof(int));
			this.compBuffers.Add(this.numPointsInChunkBuffer);

			int reducedMip3BufferSize = (int)(((float)(256 * 256 * 256) * this.numChunks) * ClayContainer.bufferSizeReduceFactor);

			if(this.renderMode == ClayContainer.RenderModes.polySplat){
				this.pointToChunkIdBuffer = new ComputeBuffer((ClayContainer.maxPointCount / 5) * this.numChunks, sizeof(int));
				this.compBuffers.Add(this.pointToChunkIdBuffer);

				this.pointCloudDataMip3Buffer = new ComputeBuffer(reducedMip3BufferSize, sizeof(int) * 2);
				this.compBuffers.Add(this.pointCloudDataMip3Buffer);

				this.seamOffset = 4.0f;
			}
			else if(this.renderMode == ClayContainer.RenderModes.microVoxel){ 
				if(this.interactive){
					this.gridPointersMip3Buffer = new ComputeBuffer(reducedMip3BufferSize, sizeof(int));
					this.compBuffers.Add(this.gridPointersMip3Buffer);

					this.pointCloudDataMip3Buffer = new ComputeBuffer(reducedMip3BufferSize, sizeof(int) * 2);
					this.compBuffers.Add(this.pointCloudDataMip3Buffer);

					this.gridPointersMip2Buffer = new ComputeBuffer((64 * 64 * 64) * this.numChunks, sizeof(int));
					this.compBuffers.Add(this.gridPointersMip2Buffer);
				}
				else{
					this.gridPointersMip3Buffer = ClayContainer.gridPointersMip3GlobBuffer;
					this.pointCloudDataMip3Buffer = ClayContainer.pointCloudDataMip3GlobBuffer;
					this.gridPointersMip2Buffer = ClayContainer.gridPointersMip2GlobBuffer;
				}

				this.chunkIdOffsetBuffer = new ComputeBuffer(ClayContainer.totalMaxChunks, sizeof(int));
				this.compBuffers.Add(this.chunkIdOffsetBuffer);
				this.chunkIdOffsetBuffer.SetData(ClayContainer.chunkIdOffsetDefaultData);

				this.volumetricDrawBuffer = new ComputeBuffer(8, sizeof(int), ComputeBufferType.IndirectArguments);
				this.compBuffers.Add(this.volumetricDrawBuffer);

				ClayContainer.microvoxelDrawData[0] = 36;
				ClayContainer.microvoxelDrawData[1] = this.numChunks * (this.instances.Count + 1);
				this.volumetricDrawBuffer.SetData(ClayContainer.microvoxelDrawData);

				this.seamOffset = 8.0f;
			}
			else if(this.renderMode == ClayContainer.RenderModes.smoothMesh){
				int reducedMip3BufferSizeMesh = (int)(((float)(256 * 256 * 256) * this.numChunks) * ClayContainer.bufferSizeReduceFactor);
				
				this.smoothMeshPointsBuffer = new ComputeBuffer(reducedMip3BufferSizeMesh, sizeof(float) * 3);
				this.compBuffers.Add(this.smoothMeshPointsBuffer); 
				
				this.smoothMeshNormalsBuffer = new ComputeBuffer(reducedMip3BufferSizeMesh, sizeof(float) * 4);
				this.compBuffers.Add(this.smoothMeshNormalsBuffer);  
				
				this.smoothMeshNormalsTempBuffer = new ComputeBuffer((256 * 256 * 256), sizeof(float) * 3);
				this.compBuffers.Add(this.smoothMeshNormalsTempBuffer); 
				
				this.smoothMeshGridDataBuffer = new ComputeBuffer((256 * 256 * 256), sizeof(int) * 3);
				this.compBuffers.Add(this.smoothMeshGridDataBuffer);

				this.pointToChunkIdBuffer = new ComputeBuffer(reducedMip3BufferSizeMesh, sizeof(int));
				this.compBuffers.Add(this.pointToChunkIdBuffer);

				this.seamOffset = 3.0f;
			}

			this.boundingBoxBuffer = new ComputeBuffer(ClayContainer.totalMaxChunks * 6, sizeof(int)); 
			this.compBuffers.Add(this.boundingBoxBuffer);

			this.chunksCenterBuffer = new ComputeBuffer(this.numChunks, sizeof(float) * 3);
			this.compBuffers.Add(this.chunksCenterBuffer);

			this.chunksCenterBuffer.SetData(ClayContainer.defaultChunksCenter);
			
			this.indirectDrawArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
			this.compBuffers.Add(this.indirectDrawArgsBuffer);

			this.indirectDrawArgsBuffer2 = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
			this.compBuffers.Add(this.indirectDrawArgsBuffer2);

			this.renderIndirectDrawArgsBuffer = this.indirectDrawArgsBuffer;

			ClayContainer.indirectArgsData[0] = 0;
			this.indirectDrawArgsBuffer.SetData(ClayContainer.indirectArgsData);
			this.indirectDrawArgsBuffer2.SetData(ClayContainer.indirectArgsData);

			this.updateChunksBuffer = new ComputeBuffer(this.numChunks, sizeof(int));
			this.compBuffers.Add(this.updateChunksBuffer);

			this.indirectChunkArgs1Buffer = new ComputeBuffer(this.numChunks * 3, sizeof(int), ComputeBufferType.IndirectArguments);
			this.compBuffers.Add(this.indirectChunkArgs1Buffer);

			this.indirectChunkArgs2Buffer = new ComputeBuffer(this.numChunks * 3, sizeof(int), ComputeBufferType.IndirectArguments);
			this.compBuffers.Add(this.indirectChunkArgs2Buffer);

			this.indirectChunkArgs3Buffer = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
			this.compBuffers.Add(this.indirectChunkArgs3Buffer);

			this.updateChunksBuffer.SetData(ClayContainer.updateChunksDefaultValues, 0, 0, this.numChunks);
			this.indirectChunkArgs1Buffer.SetData(ClayContainer.indirectChunk1DefaultValues, 0, 0, this.numChunks * 3);
			this.indirectChunkArgs2Buffer.SetData(ClayContainer.indirectChunk2DefaultValues, 0, 0, this.numChunks * 3);
			this.indirectChunkArgs3Buffer.SetData(new int[]{this.chunksX, this.chunksY, this.chunksZ});
		}

		void initMaterialProperties(){
			bool initDefaults = false;

			if(this.customMaterial != null){
				this.material = this.customMaterial;
			}
			else{
				string renderModeMatSuffix = "";
				if(this.renderMode == ClayContainer.RenderModes.microVoxel){
					renderModeMatSuffix = "MicroVoxel";
				}
				else if(this.renderMode == ClayContainer.RenderModes.polySplat){
					renderModeMatSuffix = "PolySplat";	
				}
				else if(this.renderMode == ClayContainer.RenderModes.smoothMesh){
					renderModeMatSuffix = "SmoothMesh";	
				}

				if(ClayContainer.renderPipe != "builtin"){
					renderModeMatSuffix += "ASE";
				}

            	bool unity2020_2 = ClayContainer.isUnityVersionOrAfter(2020, 2);
            	bool unity2021_2 = ClayContainer.isUnityVersionOrAfter(2021, 2);

				if(ClayContainer.renderPipe == "hdrp" && unity2020_2){
            		renderModeMatSuffix += "_2020_2";
	            }
	            else if(ClayContainer.renderPipe == "urp" && unity2021_2){
	            	renderModeMatSuffix += "_2021_2";	
	            }

				if(this.material != null && this.customMaterial == null){// validate default shader
					if(ClayContainer.renderPipe == "hdrp" && this.material.shader.name != "Clayxels/ClayxelHDRPShader" + renderModeMatSuffix){
						this.material = null;
					}
					else if(ClayContainer.renderPipe == "urp" && this.material.shader.name != "Clayxels/ClayxelURPShader" + renderModeMatSuffix){
						this.material = null;
					}
					else if(ClayContainer.renderPipe == "builtin" && this.material.shader.name != "Clayxels/ClayxelBuiltInShader" + renderModeMatSuffix){
						this.material = null;
					}
				}
				
				if(this.material != null && this.customMaterial == null){
					// if material is still not null, means it's a valid shader,
					// probably this container got duplicated in scene
					this.material = new Material(this.material);
				}
				else{
					// brand new container, lets create a new material
					if(ClayContainer.renderPipe == "hdrp"){
						this.material = new Material(Shader.Find("Clayxels/ClayxelHDRPShader" + renderModeMatSuffix));
					}
					else if(ClayContainer.renderPipe == "urp"){
						this.material = new Material(Shader.Find("Clayxels/ClayxelURPShader" + renderModeMatSuffix));
					}
					else{
						this.material = new Material(Shader.Find("Clayxels/ClayxelBuiltInShader" + renderModeMatSuffix));
					}

					initDefaults = true;
				}

				if(ClayContainer.renderPipe == "urp" && unity2020_2){
					Shader.EnableKeyword("CLAYXELS_UNITY_2020_2");
            	}
			}

			if(this.customMaterial == null && initDefaults && this.renderMode != ClayContainer.RenderModes.smoothMesh){
				// set the default clayxel texture to a dot on the standard material
				Texture texture = this.material.GetTexture("_MainTex");
				if(texture == null){
					this.material.SetTexture("_MainTex", (Texture)Resources.Load("clayxelDot"));
				}

				if(this.renderMode == ClayContainer.RenderModes.microVoxel){
					this.material.SetTexture("_MainTex", (Texture)Resources.Load("clayxelDotBlur"));
				}
			}
			
			this.material.SetFloat("chunkSize", (float)this.chunkSize);
		}

		int _compoundLastId = -1;
		void scanRecursive(Transform trn, List<ClayObject> collectedClayObjs){
			bool insideCompound = false;

			ClayObject clayObj = trn.gameObject.GetComponent<ClayObject>();
			if(clayObj != null){
				if(clayObj.isValid() && trn.gameObject.activeSelf){
					clayObj._setGroupEnd(-1);

					if(clayObj.getMode() == ClayObject.ClayObjectMode.clayGroup){
						if(this.checkIsNestedClayGroup(clayObj)){
							Debug.Log("Clayxels Warning: detected nested clayGroup, excluding ClayObject " + clayObj.name);

							return;
						}

						insideCompound = true;
					}

					if(this.clayObjectsOrderLocked){
						clayObj.clayObjectId = collectedClayObjs.Count;
						collectedClayObjs.Add(clayObj);
					}
					else{
						int id = clayObj.clayObjectId;
						if(id < 0){
							id = 0;
						}

						if(id > collectedClayObjs.Count - 1){
							collectedClayObjs.Add(clayObj);
						}
						else{
							collectedClayObjs.Insert(id, clayObj);
						}
					}

					this._compoundLastId = clayObj.clayObjectId;
				}
			}

			for(int i = 0; i < trn.childCount; ++i){
				GameObject childObj = trn.GetChild(i).gameObject;
				if(childObj.activeSelf && childObj.GetComponent<ClayContainer>() == null){
					this.scanRecursive(childObj.transform, collectedClayObjs);
				}
			}

			if(insideCompound){
				int compoundClayObjId = clayObj.clayObjectId;
				collectedClayObjs[this._compoundLastId]._setGroupEnd(compoundClayObjId);
			}
		}

		bool checkIsNestedClayGroup(ClayObject clayObj){
			ClayObject[] parents = clayObj.GetComponentsInParent<ClayObject>();

			for(int i = 1; i < parents.Length; ++i){
				if(parents[i].getMode() == ClayObject.ClayObjectMode.clayGroup && parents[i].getClayContainerPtr() == this){
					return true;
				}
			}

			return false;
		}

		void collectClayObject(ClayObject clayObj){
			if(clayObj.getNumSolids() == 0){
				clayObj.init();
			}

			clayObj.clayObjectId = this.clayObjects.Count;
			this.clayObjects.Add(clayObj);

			int numSolids = clayObj.getNumSolids();
			if(clayObj.getMode() == ClayObject.ClayObjectMode.clayGroup){
				numSolids = 1;
			}

			for(int i = 0; i < numSolids; ++i){
				Solid solid = clayObj.getSolid(i);
				solid.id = this.solids.Count;
				solid.clayObjectId = clayObj.clayObjectId;
				this.solids.Add(solid);

				if(solid.id < ClayContainer.maxSolids){
					this.solidsUpdatedDict[solid.id] = 1;
				}
				else{
					break;
				}
			}

			if(clayObj._isGroupEnd()){
				int compoundClayObjId = clayObj._getGroupClayObjectId();
				ClayObject compoundClayObj = this.clayObjects[compoundClayObjId];
				
				Solid solid = compoundClayObj._getGroupEndSolid();
				solid.id = this.solids.Count;
				solid.clayObjectId = compoundClayObj.clayObjectId;
				this.solids.Add(solid);
				
				if(solid.id < ClayContainer.maxSolids){
					this.solidsUpdatedDict[solid.id] = 1;
				}
			}

			clayObj.transform.hasChanged = true;
			clayObj.setClayxelContainer(this);
		}

		int getBufferCount(ComputeBuffer buffer){
			ComputeBuffer.CopyCount(buffer, this.genericNumberBuffer, 0);
			this.genericNumberBuffer.GetData(this.genericIntBufferArray);
			int count = this.genericIntBufferArray[0];

			return count;
		}

		void updateSolids(){
			foreach(int i in this.solidsUpdatedDict.Keys){
				if(i > this.solids.Count - 1 || i < 0){
					this.solidsHierarchyNeedsScan = true;
					return;
				}

				Solid solid = this.solids[i];

				int clayObjId = solid.clayObjectId;
				if(clayObjId > -1 && this.clayObjects.Count > clayObjId){
					ClayObject clayObj = this.clayObjects[clayObjId];
					clayObj.pullUpdate();
				}

				this.solidsPos[i] = solid.position;
				this.solidsRot[i] = solid.rotation;
				this.solidsScale[i] = solid.scale;
				this.solidsBlend[i] = solid.blend * ClayContainer.globalBlend;
				this.solidsType[i] = solid.primitiveType;
				this.solidsColor[i] = solid.color;
				this.solidsAttrs[i] = solid.attrs;
				this.solidsAttrs2[i] = solid.attrs2;
				this.solidsClayObjectId[i] = clayObjId;
			}

			if(this.solids.Count > 0){
				this.solidsPosBuffer.SetData(this.solidsPos);
				this.solidsRotBuffer.SetData(this.solidsRot);
				this.solidsScaleBuffer.SetData(this.solidsScale);
				this.solidsBlendBuffer.SetData(this.solidsBlend);
				this.solidsTypeBuffer.SetData(this.solidsType);
				this.solidsColorBuffer.SetData(this.solidsColor);
				this.solidsAttrsBuffer.SetData(this.solidsAttrs);
				this.solidsAttrs2Buffer.SetData(this.solidsAttrs2);
				this.solidsClayObjectIdBuffer.SetData(this.solidsClayObjectId);
			}
		}

		void updateChunks(){
			ClayContainer.claycoreCompute.SetInt("numSolids", this.solids.Count);
			ClayContainer.claycoreCompute.SetFloat("chunkSize", (float)this.chunkSize);
			ClayContainer.claycoreCompute.SetFloat("seamOffsetMultiplier", this.seamOffset);

			if(this.numChunks > 1){
				ClayContainer.claycoreCompute.SetInt("numSolidsUpdated", this.solidsUpdatedDict.Count);

				this.solidsUpdatedDict.Keys.CopyTo(ClayContainer.solidsUpdatedArray, 0);
				ClayContainer.solidsUpdatedBuffer.SetData(ClayContainer.solidsUpdatedArray);
				
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.filterSolidsPerChunk, "numPointsInChunk", this.numPointsInChunkBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.filterSolidsPerChunk, "chunksCenter", this.chunksCenterBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.filterSolidsPerChunk, "boundingBox", this.boundingBoxBuffer);

				ClayContainer.claycoreCompute.Dispatch((int)Kernels.filterSolidsPerChunk, this.chunksX, this.chunksY, this.chunksZ);

				// Debug.Log("-----");
				// int[] tmp = new int[this.numChunks * 3];
				// this.indirectChunkArgs1Buffer.GetData(tmp);
				// for(int i = 0; i < this.numChunks; ++i){
				// 	Debug.Log(tmp[i * 3]);
				// }
			}
			else{
				// reset point cloud to render zero number of points
				this.numPointsInChunkBuffer.SetData(ClayContainer.pointsInChunkDefaultValues, 0, 0, this.numChunks);
			}

			this.solidsUpdatedDict.Clear();
		}

		float _debugAutoBounds = 0.0f;
		void updateAutoBounds(bool forceInit = false){
			int maxChunk = ClayContainer.maxChunkX;

			if(this.autoBoundsLimit < maxChunk){
				maxChunk = this.autoBoundsLimit;
			}

			if(this.renderMode == ClayContainer.RenderModes.smoothMesh && ClayContainer.limitSmoothMeshMemory){
				if(maxChunk > 2){
					maxChunk = 2;
				}
			}
			
			float boundsSize = this.computeBoundsSize();

			this._debugAutoBounds = boundsSize;

			int newChunkSize = Mathf.CeilToInt(boundsSize / maxChunk);

			int detailChunkSize = this.clayDetailToChunkSize();

			int estimatedNumChunks = Mathf.CeilToInt(boundsSize / detailChunkSize);
			
			if(estimatedNumChunks < 1){
				estimatedNumChunks = 1;
			}
			else if(estimatedNumChunks > maxChunk){
				estimatedNumChunks = maxChunk;
			}

			this.autoFrameSkip = estimatedNumChunks - 1;
			
			if(estimatedNumChunks != this.chunksX || forceInit){
				this.autoBoundsChunkSize = newChunkSize;
				
				this.resizeChunks(estimatedNumChunks);
				this.updateInternalBounds();
				this.forceUpdateAllSolids();

				this.bindBuffersToChunks();
			}
			else if(newChunkSize != this.autoBoundsChunkSize){
				this.autoBoundsChunkSize = newChunkSize;

				if(this.autoBoundsChunkSize > detailChunkSize){
					this.updateInternalBounds();
					this.forceUpdateAllSolids();
				}
			}
		}

		void bindBuffersToChunks(){
			if(this.numChunks == 1){
				this.genericIntBufferArray[0] = this.solids.Count;
				ClayContainer.numSolidsPerChunkBuffer.SetData(this.genericIntBufferArray);

				ClayContainer.solidsPerChunkBuffer.SetData(ClayContainer.solidsInSingleChunkArray);

				this.chunksCenterBuffer.SetData(ClayContainer.defaultChunksCenter);
			}
			else{
				this.bindSolidsBuffers((int)Kernels.filterSolidsPerChunk);

				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.filterSolidsPerChunk, "updateChunks", this.updateChunksBuffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.filterSolidsPerChunk, "indirectChunkArgs1", this.indirectChunkArgs1Buffer);
				ClayContainer.claycoreCompute.SetBuffer((int)Kernels.filterSolidsPerChunk, "indirectChunkArgs2", this.indirectChunkArgs2Buffer);
			}
		}

		void resizeChunks(int estimatedNumChunks){
			if(this.renderMode == ClayContainer.RenderModes.smoothMesh && ClayContainer.limitSmoothMeshMemory){
				if(estimatedNumChunks > 2){
					estimatedNumChunks = 2;
				}
			}

			this.chunksX = estimatedNumChunks;
			this.chunksY = estimatedNumChunks;
			this.chunksZ = estimatedNumChunks;

			if(this.autoBoundsChunkSize > this.chunkSize){
				this.chunkSize = this.autoBoundsChunkSize;

				if(this.renderMode == ClayContainer.RenderModes.microVoxel){
					this.genericFloatBufferArray[0] = this.chunkSize;
					this.chunkSizeBuffer.SetData(this.genericFloatBufferArray, 0, 0, 1);
				}
			}

			this.boundsScale.x = (float)this.chunkSize * this.chunksX;
			this.boundsScale.y = (float)this.chunkSize * this.chunksY;
			this.boundsScale.z = (float)this.chunkSize * this.chunksZ;
			this.renderBounds.size = this.boundsScale * this.transform.lossyScale.x;

			float gridCenterOffset = (this.chunkSize * 0.5f);
			this.boundsCenter.x = ((this.chunkSize * (this.chunksX - 1)) * 0.5f) - (gridCenterOffset*(this.chunksX-1));
			this.boundsCenter.y = ((this.chunkSize * (this.chunksY - 1)) * 0.5f) - (gridCenterOffset*(this.chunksY-1));
			this.boundsCenter.z = ((this.chunkSize * (this.chunksZ - 1)) * 0.5f) - (gridCenterOffset*(this.chunksZ-1));

			float seamOffset = (this.chunkSize / 256.0f);
			float chunkOffset = this.chunkSize - seamOffset;

			this.numChunks = this.chunksX * this.chunksY * this.chunksZ;
			
			ClayContainer.claycoreCompute.SetInt("numChunksX", this.chunksX);
			ClayContainer.claycoreCompute.SetInt("numChunksY", this.chunksY);
			ClayContainer.claycoreCompute.SetInt("numChunksZ", this.chunksZ);
			
			this.updateChunksBuffer.SetData(ClayContainer.updateChunksDefaultValues, 0, 0, this.numChunks);

			this.indirectChunkArgs3Buffer.SetData(new int[]{this.chunksX, this.chunksY, this.chunksZ});

			if(this.renderMode == ClayContainer.RenderModes.microVoxel){
				ClayContainer.microvoxelDrawData[0] = 36;
				ClayContainer.microvoxelDrawData[1] = this.numChunks * (this.instances.Count + 1);
				this.volumetricDrawBuffer.SetData(ClayContainer.microvoxelDrawData);

				int instanceId = 0;
				for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
					this.genericIntBufferArray[0] = instanceId;
					this.instanceToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * this.numChunks) + chunkIt, 1);

					this.genericIntBufferArray[0] = this.containerId;
					this.chunkIdToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * this.numChunks) + chunkIt, 1);
				}
				
				for(int i = 0; i < this.instances.Count; ++i){
					instanceId = i + 1;
					
					for(int chunkIt = 0; chunkIt < this.numChunks; ++chunkIt){
						this.genericIntBufferArray[0] = instanceId;
						this.instanceToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * this.numChunks) + chunkIt, 1);

						this.genericIntBufferArray[0] = this.instances[i].containerId;
						this.chunkIdToContainerIdBuffer.SetData(this.genericIntBufferArray, 0, (instanceId * this.numChunks) + chunkIt, 1);
					}
				}
			}
		}

		float computeBoundsSize(){
			Vector3 autoBoundsScale = Vector3.zero;

			int solidCount = this.solids.Count;
			if(solidCount > ClayContainer.maxSolids){
				solidCount = ClayContainer.maxSolids;
			}

			float cellSizeOffset = ((float)this.chunkSize / 256.0f) * (16.0f * this.chunksX);
			
			for(int i = 0; i < solidCount; ++i){
				Solid solid = this.solids[i];

				float boundingSphere = Mathf.Sqrt(Vector3.Dot(solid.scale, solid.scale)) * 1.732f;
				float autoBoundsPosX = Mathf.Abs(solid.position.x * 2.0f) + boundingSphere + cellSizeOffset;
				float autoBoundsPosY = Mathf.Abs(solid.position.y * 2.0f) + boundingSphere + cellSizeOffset;
				float autoBoundsPosZ = Mathf.Abs(solid.position.z * 2.0f) + boundingSphere + cellSizeOffset;

				if(autoBoundsPosX > autoBoundsScale.x){
					autoBoundsScale.x = autoBoundsPosX;
				}
				if(autoBoundsPosY > autoBoundsScale.y){
					autoBoundsScale.y = autoBoundsPosY;
				}
				if(autoBoundsPosZ > autoBoundsScale.z){
					autoBoundsScale.z = autoBoundsPosZ;
				}
			}

			float autoBoundsChunkSize = Mathf.Max(autoBoundsScale.x, Mathf.Max(autoBoundsScale.y, autoBoundsScale.z));

			return autoBoundsChunkSize;
		}

		float getFPS(){
			this.deltaTime += (Time.unscaledDeltaTime - this.deltaTime) * 0.1f;
			float fps = 1.0f / this.deltaTime;
			return fps;
		}

		void switchMicrovoxelSharedMemory(){
			if(!this.interactive){
				if(ClayContainer.lastEditedMicrovoxelContainerId != this.containerId){
					if(ClayContainer.containersInScene.ContainsKey(ClayContainer.lastEditedMicrovoxelContainerId)){
						ClayContainer lastEditedContainer = ClayContainer.containersInScene[ClayContainer.lastEditedMicrovoxelContainerId];
						if(lastEditedContainer != null){
							if(!lastEditedContainer.interactive && !lastEditedContainer.memoryOptimized){
								lastEditedContainer.optimizeMemoryNoCompute();
							}
						}
					}
				}
				
				ClayContainer.lastEditedMicrovoxelContainerId = this.containerId;
			}
		}

		void switchComputeData(){
			ClayContainer.lastUpdatedContainerId = this.containerId;
			
			ClayContainer.claycoreCompute.SetInt("numChunksX", this.chunksX);
			ClayContainer.claycoreCompute.SetInt("numChunksY", this.chunksY);
			ClayContainer.claycoreCompute.SetInt("numChunksZ", this.chunksZ);

			this.bindSolidsBuffers((int)Kernels.computeGrid);
			this.bindSolidsBuffers((int)Kernels.computeGridMip3);
			
			this.bindBuffersToChunks();
		}

		void bindSolidsBuffers(int kernId){
			ClayContainer.claycoreCompute.SetBuffer(kernId, "solidsPos", this.solidsPosBuffer);
			ClayContainer.claycoreCompute.SetBuffer(kernId, "solidsRot", this.solidsRotBuffer);
			ClayContainer.claycoreCompute.SetBuffer(kernId, "solidsScale", this.solidsScaleBuffer);
			ClayContainer.claycoreCompute.SetBuffer(kernId, "solidsBlend", this.solidsBlendBuffer);
			ClayContainer.claycoreCompute.SetBuffer(kernId, "solidsType", this.solidsTypeBuffer);
			ClayContainer.claycoreCompute.SetBuffer(kernId, "solidsColor", this.solidsColorBuffer);
			ClayContainer.claycoreCompute.SetBuffer(kernId, "solidsAttrs", this.solidsAttrsBuffer);
			ClayContainer.claycoreCompute.SetBuffer(kernId, "solidsAttrs2", this.solidsAttrs2Buffer);
			ClayContainer.claycoreCompute.SetBuffer(kernId, "solidsClayObjectId", this.solidsClayObjectIdBuffer);
		}

		bool checkNeedsInit(){
			// we need to perform these checks because prefabs will reset some of these attributes upon instancing
			if(this.needsInit || this.numChunks == 0 || this.material == null){
				return true;
			}

			return false;
		}

		void updateClay(){
			if(this.checkNeedsInit()){
				this.init();
				this.updateFrame = 0;
			}
			else{
				if(this.transform.hasChanged){
					this.transform.hasChanged = false;

					if(this.solidsUpdatedDict.Count == 0){
						return;
					}
					
					this.needsUpdate = true;
				}
			}

			// if we're in editor, check if this container is being asked for an update while it's not actively edited
			if(!Application.isPlaying){
				if(!this.interactive){
					if(!this.editingThisContainer && !this.solidsHierarchyNeedsScan){
						return;
					}
				}
			}
			else{
				if(this.memoryOptimized){
					return;
				}
			}

			if(this.needsUpdate && this.updateFrame == 0){
				this.computeClay();
			}

			int updateCounter = ClayContainer.frameSkip + this.autoFrameSkip;
			if(updateCounter > 0){
				this.updateFrame = (this.updateFrame + 1) % updateCounter;
			}
		}

		void updateInstances(){
			this.instancesMatrix[0] = this.transform.localToWorldMatrix;
			this.instancesMatrixInv[0] = this.transform.worldToLocalMatrix;

			this.renderBounds.center = this.transform.position;

			this.transferMaterialAttributesToLocalBuffers();
			
			for(int i = 0; i < this.instances.Count; ++i){
				ClayContainer instance = this.instances[i];
				
				this.instancesMatrix[i + 1] = instance.transform.localToWorldMatrix;
				this.instancesMatrixInv[i + 1] = instance.transform.worldToLocalMatrix;

				if(!instance.visible || !instance.gameObject.activeSelf){
					this.instancesMatrix[i + 1] = Matrix4x4.zero;
				}

				instance.renderBounds = this.renderBounds;
				instance.renderBounds.center = instance.transform.position;
				
				this.renderBounds.Encapsulate(instance.renderBounds);
			}

			this.instancesMatrixBuffer.SetData(this.instancesMatrix);
			this.instancesMatrixInvBuffer.SetData(this.instancesMatrixInv);
		}

		void updateAutoRigBone(GameObject obj, ref List<GameObject> autoRigBones){
			ClayObject clayObj = obj.GetComponent<ClayObject>();

			if(clayObj.getMode() == ClayObject.ClayObjectMode.single){
				autoRigBones.Add(obj);
			}
			else if(clayObj.getMode() == ClayObject.ClayObjectMode.offset){
				for(int i = 0; i < clayObj.getNumSolids(); ++i){
					Transform boneTrn = obj.transform.Find("clayBoneExtra_" + obj.name + "_" + i);
					if(boneTrn == null){
						GameObject newBoneObj = new GameObject("clayBoneExtra_" + obj.name + "_" + i);
						boneTrn = newBoneObj.transform;
						boneTrn.parent = obj.transform;
					}

					Solid solid = clayObj.getSolid(i);
					boneTrn.position = solid.position;
					boneTrn.rotation = solid.rotation;
					boneTrn.localScale = Vector3.one;

					autoRigBones.Add(boneTrn.gameObject);
				}
			}
			else if(clayObj.getMode() == ClayObject.ClayObjectMode.spline){
				Transform prevTrn = obj.transform;

				Transform[] childTransforms = obj.GetComponentsInChildren<Transform>();
				
				for(int i = 0; i < clayObj.getNumSolids(); ++i){
					Transform boneTrn = null;
					string boneName = "clayBoneExtra_" + obj.name + "_" + i;
					for(int j = 0; j < childTransforms.Length; ++j){
						if(childTransforms[j].name == boneName){
							boneTrn = childTransforms[j];
							break;
						}
					}

					if(boneTrn == null){
						GameObject newBoneObj = new GameObject("clayBoneExtra_" + obj.name + "_" + i);
						boneTrn = newBoneObj.transform;
						boneTrn.parent = prevTrn;

						prevTrn = boneTrn;
					}

					Solid solid = clayObj.getSolid(i);
					boneTrn.position = solid.position;
					boneTrn.rotation = solid.rotation;
					boneTrn.localScale = Vector3.one;

					autoRigBones.Add(boneTrn.gameObject);
				}
			}
			else if(clayObj.getMode() == ClayObject.ClayObjectMode.clayGroup){
				autoRigBones.Add(obj);
			}

			if(clayObj._isGroupEnd()){
				autoRigBones.Add(obj);
			}
		}

		void updateAutoRig(){
			List<GameObject> autoRigBones = new List<GameObject>();

			for(int i = 0; i < this.clayObjects.Count; ++i){
				this.updateAutoRigBone(this.clayObjects[i].gameObject, ref autoRigBones);
			}

			Transform[] bones = new Transform[autoRigBones.Count];
			Matrix4x4[] bindPoses = new Matrix4x4[autoRigBones.Count];

			for(int i = 0; i < autoRigBones.Count; ++i){
				bones[i] = autoRigBones[i].transform;
				bindPoses[i] = autoRigBones[i].transform.worldToLocalMatrix * this.transform.localToWorldMatrix;
			}

			SkinnedMeshRenderer render = this.GetComponent<SkinnedMeshRenderer>();
			if(render != null){
				render.bones = bones;
				render.sharedMesh.bindposes = bindPoses;
			}

			render.localBounds = new Bounds(Vector3.zero, this.renderBounds.size);
		}

		// All functions past this point are for editor only use
		public static Color boundsColor = new Color(0.5f, 0.5f, 1.0f, 0.1f);
		public static string pickingKey = "p";
		public static string mirrorDuplicateKey = "m";

		#if UNITY_EDITOR

		void Awake(){
			if(!Application.isPlaying){
				this.needsInit = true;
			}
		}

		bool checkEditorSelectingThisContainer(){
			if(Application.isPlaying){
				return false;
			}

			if(UnityEditor.Selection.gameObjects.Length == 0){
				return false;
			}

			if(UnityEditor.Selection.Contains(this.gameObject)){
				return true;
			}

			for(int i = 0; i < UnityEditor.Selection.gameObjects.Length; ++i){
				GameObject sel = UnityEditor.Selection.gameObjects[i];
				ClayObject clayObj = sel.GetComponent<ClayObject>();
				if(clayObj != null){
					if(clayObj.getClayContainer() == this){
						return true;
					}
				}
			}

			return false;
		}
		
		public static void linkAllPrefabInstances(ClayContainer clayContainer){
			GameObject thisPrefab = PrefabUtility.GetNearestPrefabInstanceRoot(clayContainer.gameObject);

			if(!thisPrefab.name.EndsWith("_clayPrefab")){
				if(thisPrefab.name.Contains("_clayPrefab")){
					string[] tokens = thisPrefab.name.Split(new string[]{"_clayPrefab"}, StringSplitOptions.None);
					thisPrefab.name = tokens[0];
				}

				thisPrefab.name += "_clayPrefab";
			}

			ClayContainer[] sourceContainers = thisPrefab.GetComponentsInChildren<ClayContainer>();
			for(int i = 0; i < sourceContainers.Length; ++i){
				ClayContainer container = sourceContainers[i];
				container.setIsInstanceOf(null);
				container.enableAllClayObjects(true);
				clayContainer.needsInit = true;
				clayContainer.init();

				PrefabUtility.RecordPrefabInstancePropertyModifications(clayContainer);
			}

			PrefabUtility.RecordPrefabInstancePropertyModifications(thisPrefab);

			string thisPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(thisPrefab);

			ClayContainer[] containers = UnityEngine.Object.FindObjectsOfType<ClayContainer>();
			for(int i = 0; i < containers.Length; ++i){
				ClayContainer container = containers[i];

				if(container != clayContainer && PrefabUtility.IsPartOfAnyPrefab(container.gameObject)){
					
					GameObject otherPrefab = PrefabUtility.GetNearestPrefabInstanceRoot(container.gameObject);

					if(otherPrefab != thisPrefab){
						string otherPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(otherPrefab);

						if(otherPrefabPath == thisPrefabPath){

							if(otherPrefab.name.EndsWith("_clayPrefab")){
								otherPrefab.name = otherPrefab.name.Replace("_clayPrefab", "");
							}

							container.setIsInstanceOf(null);
							container.enableAllClayObjects(false);

							ClayContainer.linkNestedInstances(thisPrefab, otherPrefab);
							
							container.needsInit = true;
							container.init();

							PrefabUtility.RecordPrefabInstancePropertyModifications(container);
							PrefabUtility.RecordPrefabInstancePropertyModifications(otherPrefab);
						}
					}
				}
			}
		}

		void setupPrefab(){			
			if(this.instanceOf != null){
				return;
			}
			
			if(!PrefabUtility.IsPartOfAnyPrefab(this.gameObject)){
				return;
			}
			
			GameObject thisPrefab = PrefabUtility.GetNearestPrefabInstanceRoot(this.gameObject);
			
			if(thisPrefab.name.EndsWith("_clayPrefab")){
				return;
			}

			string thisPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(thisPrefab);
			
			GameObject sourcePrefab = null;

			ClayContainer[] containers = UnityEngine.Object.FindObjectsOfType<ClayContainer>();
			for(int i = 0; i < containers.Length; ++i){
				ClayContainer container = containers[i];

				if(PrefabUtility.IsPartOfAnyPrefab(container.gameObject)){
					GameObject otherPrefab = PrefabUtility.GetNearestPrefabInstanceRoot(container.gameObject);
					
					string otherPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(otherPrefab);

					if(otherPrefab.name.EndsWith("_clayPrefab") &&
						otherPrefab != thisPrefab &&
						otherPrefabPath == thisPrefabPath){

						sourcePrefab = otherPrefab;
						break;
					}
				}
			}

			if(sourcePrefab != null){
				ClayContainer.linkNestedInstances(sourcePrefab, thisPrefab);

				PrefabUtility.RecordPrefabInstancePropertyModifications(thisPrefab);
			}
		}

		static void linkNestedInstances(GameObject source, GameObject dest){
			ClayContainer[] sourceContainers = source.GetComponentsInChildren<ClayContainer>();
			ClayContainer[] destContainers = dest.GetComponentsInChildren<ClayContainer>();

			if(sourceContainers.Length != destContainers.Length){
				return;
			}
			
			for(int i = 0; i < sourceContainers.Length; ++i){
				ClayContainer sourceContainer = sourceContainers[i];
				ClayContainer destContainer = destContainers[i];

				if(sourceContainer != destContainer){
					destContainer.enableAllClayObjects(false);
					destContainer.setIsInstanceOf(sourceContainer);

					PrefabUtility.RecordPrefabInstancePropertyModifications(destContainer);
				}
			}
		}

		static void checkPrefsIntegrity(){
			string configFileName = "";

			string[] assets = AssetDatabase.FindAssets("clayxelsPrefs t:TextAsset");
			for(int i = 0; i < assets.Length; ++i){
	    		string filename = AssetDatabase.GUIDToAssetPath(assets[i]);
	    		string[] tokens = filename.Split('.');
	    		if(tokens[tokens.Length - 1] == "json"){
	    			configFileName = filename;
	    			break;
	    		}
	    	}

	    	TextAsset configTextAsset = (TextAsset)Resources.Load("clayxelsPrefs", typeof(TextAsset));
    		
			if(configFileName == "" || configTextAsset.text == ""){
				ClayxelsPrefs prefs = new ClayxelsPrefs();
				
				string jsonText = JsonUtility.ToJson(prefs);
    			File.WriteAllText("Assets/Clayxels/Resources/clayxelsPrefs.json" , jsonText);
    			AssetDatabase.Refresh();
			}
		}

		public static void savePrefs(ClayxelsPrefs prefs){
			ClayContainer.prefsOverridden = false;

			string[] assets = AssetDatabase.FindAssets("clayxelsPrefs t:TextAsset");
	    	string configFileName = "";
	    	for(int i = 0; i < assets.Length; ++i){
	    		string filename = AssetDatabase.GUIDToAssetPath(assets[i]);
	    		string[] tokens = filename.Split('.');
	    		if(tokens[tokens.Length - 1] == "json"){
	    			configFileName = filename;
	    			break;
	    		}
	    	}

	    	string jsonText = JsonUtility.ToJson(prefs);
	    	
    		File.WriteAllText(configFileName , jsonText);
    		AssetDatabase.Refresh();
		}

		public void autoRenameClayObject(ClayObject clayObj){
			 List<string> solidsLabels = ClayContainer.solidsCatalogueLabels;

			string blendSign = "+";
			if(clayObj.blend < 0.0f){
				blendSign = "-";
			}

			string isColoring = "";
			if(clayObj.attrs.w == 1.0f){
				blendSign = "";
				isColoring = "[paint]";
			}

			string typeStr = "";

			if(clayObj.getMode() == ClayObject.ClayObjectMode.clayGroup){
				typeStr = "group";
			}
			else{
				typeStr = solidsLabels[clayObj.primitiveType];
			}

			clayObj.gameObject.name = "clay_" + typeStr + blendSign + isColoring;
		}

		public static void shortcutMirrorDuplicate(){
			for(int i = 0; i < UnityEditor.Selection.gameObjects.Length; ++i){
				GameObject gameObj = UnityEditor.Selection.gameObjects[i];
				if(gameObj.GetComponent<ClayObject>()){
					gameObj.GetComponent<ClayObject>().mirrorDuplicate();
				}
			}
		}

		static void shortcutAddClay(){
			if(UnityEditor.Selection.gameObjects.Length > 0){
				if(UnityEditor.Selection.gameObjects[0].GetComponent<ClayObject>()){
					ClayContainer container = UnityEditor.Selection.gameObjects[0].GetComponent<ClayObject>().getClayContainer();
					ClayObject clayObj = container.addClayObject();
					UnityEditor.Selection.objects = new GameObject[]{clayObj.gameObject};
				}
				else if(UnityEditor.Selection.gameObjects[0].GetComponent<ClayContainer>() != null){
					ClayObject clayObj = UnityEditor.Selection.gameObjects[0].GetComponent<ClayContainer>().addClayObject();
					UnityEditor.Selection.objects = new GameObject[]{clayObj.gameObject};
				}
			}
		}

		public static float getEditorUIScale(){
			PropertyInfo p =
				typeof(GUIUtility).GetProperty("pixelsPerPoint", BindingFlags.Static | BindingFlags.NonPublic);

			float editorUiScaling = 1.0f;
			if(p != null){
				editorUiScaling = (float)p.GetValue(null, null);
			}

			return editorUiScaling;
		}

		[MenuItem("GameObject/3D Object/Clayxel Container" )]
		public static ClayContainer createNewContainer(){
			 GameObject newObj = new GameObject("ClayxelContainer");
			 ClayContainer newClayContainer = newObj.AddComponent<ClayContainer>();

			 UnityEditor.Selection.objects = new GameObject[]{newObj};

			 return newClayContainer;
		}

		void OnValidate(){
			// called on a few (annoying) occasions, like every time any script recompiles
			this.needsInit = true;
			this.numChunks = 0;
		}

		void removeEditorEvents(){
			AssemblyReloadEvents.beforeAssemblyReload -= this.onBeforeAssemblyReload;

			EditorApplication.hierarchyChanged -= this.onHierarchyChanged;

			UnityEditor.Selection.selectionChanged -= this.onSelectionChanged;

			Undo.undoRedoPerformed -= this.onUndoPerformed;
		}

		void reinstallEditorEvents(){
			this.removeEditorEvents();
			
			AssemblyReloadEvents.beforeAssemblyReload += this.onBeforeAssemblyReload;

			EditorApplication.hierarchyChanged += this.onHierarchyChanged;

			UnityEditor.Selection.selectionChanged += this.onSelectionChanged;

			Undo.undoRedoPerformed += this.onUndoPerformed;

			PrefabUtility.prefabInstanceUpdated -= ClayContainer.onPrefabUpdate;
			PrefabUtility.prefabInstanceUpdated += ClayContainer.onPrefabUpdate;
		}

		static void onPrefabUpdate(GameObject obj){
			// called when storing a new prefab

			if(Application.isPlaying){
				return;
			}

			ClayContainer[] containers = obj.GetComponentsInChildren<ClayContainer>();

			if(containers.Length == 0){
				// not part of a clayxels container
				return;
			}

			if(containers[0].isFrozen() || 
				!containers[0].enabled){
				return;				
			}

			string prefabFile = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
			string prefabPath = Path.GetDirectoryName(prefabFile);
			
			for(int i = 0; i < containers.Length; ++i){
				ClayContainer container = containers[i];
				
				if(container.customMaterial != null){
					return;
				}
				
				if(container.material != null){
					if(!AssetDatabase.Contains(container.material)){
						Material storedMat = new Material(container.material);

						string assetNameUnique = obj.name + "_" + container.name + "_" + obj.GetInstanceID();
						string materialFile = Path.Combine(prefabPath, assetNameUnique + ".mat");
						AssetDatabase.CreateAsset(storedMat, materialFile);

						container.customMaterial = (Material)AssetDatabase.LoadAssetAtPath(materialFile, typeof(Material));
						container.material = container.customMaterial;
					}
				}
			}

			PrefabUtility.ApplyPrefabInstance(obj, InteractionMode.AutomatedAction);

			GameObject thisPrefab = PrefabUtility.GetNearestPrefabInstanceRoot(obj);
			if(!thisPrefab.name.EndsWith("_clayPrefab")){
				thisPrefab.name += "_clayPrefab";
			}
		}

		void onBeforeAssemblyReload(){
			// called when this script recompiles
			
			if(Application.isPlaying){
				return;
			}

			this.releaseBuffers();
			ClayContainer.releaseGlobalBuffers();

			ClayContainer.globalDataNeedsInit = true;
			this.needsInit = true;
		}

		void onUndoPerformed(){
			if(this.invalidated){
				return;
			}

			this.updateFrame = 0;

			if(Undo.GetCurrentGroupName() == "changed clayobject" ||
				Undo.GetCurrentGroupName() == "changed clayxel container"){
				this.needsUpdate = true;
			}
			else if(Undo.GetCurrentGroupName() == "added clayxel solid"){
				this.scheduleClayObjectsScan();
			}
			else if(Undo.GetCurrentGroupName() == "Selection Change"){
				if(!UnityEditor.Selection.Contains(this.gameObject)){
					if(UnityEditor.Selection.gameObjects.Length > 0){
						ClayObject clayObj = UnityEditor.Selection.gameObjects[0].GetComponent<ClayObject>();
						if(clayObj != null){
							if(clayObj.getClayContainer() == this){
								this.needsUpdate = true;
							}
						}
					}
				}
			}

			EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
			UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
			ClayContainer.getSceneView().Repaint();
		}

		public static bool _skipHierarchyChanges = false;
		void onHierarchyChanged(){
			if(this.invalidated){
				return;
			}
			
			if(this.frozen){
				return;
			}

			if(!this.enabled){
				return;
			}

			if(this.instanceOf != null){
				return;
			}

			if(ClayContainer._skipHierarchyChanges){
				ClayContainer._skipHierarchyChanges = false;
				return;
			}

			// the order of operations here is important to successfully move clayOjects from one container to another
			if(this.editingThisContainer){
				this.solidsHierarchyNeedsScan = true;
			}

			this.onSelectionChanged();
			
			if(this.editingThisContainer){
				this.forceUpdateAllSolids();
				this.computeClay();
			}
		}

		public static void _inspectorUpdate(){
			ClayContainer.inspectorUpdated = UnityEngine.Object.FindObjectsOfType<ClayContainer>().Length;
		}

		static ClayContainer selectionReoderContainer = null;
		static int selectionReorderId = -1;
		static int selectionReorderIdOffset = 0;

		public void selectToReorder(ClayObject clayObjToReorder, int reorderOffset){
			ClayContainer.selectionReoderContainer = this;
			ClayContainer.selectionReorderId = clayObjToReorder.clayObjectId;
			ClayContainer.selectionReorderIdOffset = reorderOffset;
		}

		void reorderSelected(){
			if(ClayContainer.selectionReoderContainer != this){
				ClayContainer.selectionReoderContainer = null;
				return;
			}

			if(UnityEditor.Selection.gameObjects.Length == 0){
				return;
			}

			ClayObject selectedClayObj = UnityEditor.Selection.gameObjects[0].GetComponent<ClayObject>();
			if(selectedClayObj == null){
				return;
			}

			if(selectedClayObj.getClayContainer() != ClayContainer.selectionReoderContainer){
				return;
			}

			ClayObject reoderedClayObj = this.clayObjects[ClayContainer.selectionReorderId];

			int idOffset = selectedClayObj.clayObjectId - ClayContainer.selectionReorderId; 
			this.reorderClayObject(ClayContainer.selectionReorderId, idOffset + ClayContainer.selectionReorderIdOffset);

			ClayContainer.pickedObj = reoderedClayObj.gameObject;
			ClayContainer.pickingMode = true;

			ClayContainer.selectionReoderContainer = null;
		}

		void onSelectionChanged(){
			// for some reason this callback is also triggered by the inspector
			// so we first have to check if this is really a selection change or an inspector update. wtf. 
			if(ClayContainer.inspectorUpdated > 0){
				ClayContainer.inspectorUpdated -= 1;
				return;
			}

			if(this.invalidated){
				return;
			}

			if(this.needsInit){
				// this.init();
				return;
			}

			if(this.frozen){
				return;
			}

			if(this.instanceOf != null){
				return;
			}

			if(ClayContainer.selectionReoderContainer != null){
				this.reorderSelected();
			}

			if(!this.enabled){
				return;
			}
			
			bool wasEditingThis = this.editingThisContainer;
			this.editingThisContainer = false;

			// check if one of this container's clayObject got selected
			for(int i = 0; i < UnityEditor.Selection.gameObjects.Length; ++i){
				GameObject sel = UnityEditor.Selection.gameObjects[i];
				ClayContainer parentContainer = sel.GetComponentInParent<ClayContainer>();
				
				if(parentContainer == this){
					this.editingThisContainer = true;
					this.needsUpdate = true;
					this.expandMemory();
					this.computeClay();

					return;
				}
			}

			if(!this.editingThisContainer && wasEditingThis && !this.interactive){// we're changing selection
				this.optimizeMemory();
				UnityEditor.EditorApplication.QueuePlayerLoopUpdate();// fix instances disappearing
			}
		}

		static void finalizeSculptAction(){
			for(int i = 0; i < UnityEditor.Selection.gameObjects.Length; ++i){
				GameObject sel = UnityEditor.Selection.gameObjects[i];
				ClayObject clayObj = sel.GetComponent<ClayObject>();
				if(clayObj != null){
					ClayContainer container = clayObj.getClayContainer();
					if(container != null){
						if(container.needsUpdate && !container.frozen){
							container.computeClay();
						}
					}
				}
			}
		}

		static void onSceneGUI(SceneView sceneView){
			if(Application.isPlaying){
				return;
			}

			if(!UnityEditorInternal.InternalEditorUtility.isApplicationActive){
				// this callback keeps running even in the background
				return;
			}

			if(ClayContainer.globalDataNeedsInit){
				return;
			}

			Event ev = Event.current;

			if(ev.alt){
				return;
			}

			if(ev.type == EventType.MouseUp){
				ClayContainer.finalizeSculptAction();
			}
				
			if(ev.isKey){
				ClayContainer.clearPickingMesh();
				
				if(ev.keyCode.ToString().ToLower() == ClayContainer.pickingKey){
					if(!ClayContainer.directPick){
						ClayContainer.startScenePickingMesh();
					}
				}
				else if(ev.keyCode.ToString().ToLower() == ClayContainer.mirrorDuplicateKey){
					ClayContainer.shortcutMirrorDuplicate();
				}

				return;
			}

			float uiScale = ClayContainer.getEditorUIScale();
			int pickingMousePosX = (int)(ev.mousePosition.x * uiScale);
			int pickingMousePosY = (int)(ev.mousePosition.y * uiScale);

			if(pickingMousePosX < 0 || pickingMousePosX >= sceneView.camera.pixelWidth || 
				pickingMousePosY < 0 || pickingMousePosY >= sceneView.camera.pixelHeight){

				return;
			}

			if(ClayContainer.directPick){
				if(ev.type == EventType.Used){
					ClayContainer.mouseClickMicrovoxelPicking(pickingMousePosX, pickingMousePosY);
					ClayContainer.finalizePickingMesh(sceneView);
					ClayContainer.clearPickingMesh();

					return;
				}
				
				if(ev.type == EventType.MouseMove){
					ClayContainer.mouseMoveMicrovoxelPicking(pickingMousePosX, pickingMousePosY);
				}

				if(ev.type == EventType.Repaint){
					ClayContainer.performScenePickingMesh(Camera.current, pickingMousePosX, pickingMousePosY);
					
					sceneView.Repaint();
				}

				return;
			}

			if(!ClayContainer.pickingMode){
				return;
			}

			if(ClayContainer.pickedObj != null){
				if(ClayContainer.pickingShiftPressed){
					List<UnityEngine.Object> sel = new List<UnityEngine.Object>();
		   			for(int i = 0; i < UnityEditor.Selection.objects.Length; ++i){
		   				sel.Add(UnityEditor.Selection.objects[i]);
		   			}
		   			sel.Add(ClayContainer.pickedObj);
		   			UnityEditor.Selection.objects = sel.ToArray();
	   			}
	   			else{
					UnityEditor.Selection.objects = new GameObject[]{ClayContainer.pickedObj};
				}
			}
			
			if(ev.type == EventType.MouseMove){
				if(ClayContainer.pickedObj != null){
					ClayContainer.clearPickingMesh();
				}
			}
			else if(ev.type == EventType.MouseDown && !ev.alt){
				if(pickingMousePosX < 0 || pickingMousePosX >= sceneView.camera.pixelWidth || 
					pickingMousePosY < 0 || pickingMousePosY >= sceneView.camera.pixelHeight){
					clearPickingMesh();
					return;
				}

				ev.Use();

				ClayContainer.finalizePickingMesh(sceneView);
			}
			else if((int)ev.type == 7){ // on repaint
				ClayContainer.performScenePickingMesh(sceneView.camera, pickingMousePosX, pickingMousePosY);
			}

			sceneView.Repaint();
		}	

		static void setupScenePicking(){
			SceneView.duringSceneGui -= ClayContainer.onSceneGUI;
			SceneView.duringSceneGui += ClayContainer.onSceneGUI;

			ClayContainer.setupPickingMesh();
		}

		public static void startScenePickingMesh(){
			ClayContainer[] containers = GameObject.FindObjectsOfType<ClayContainer>();

			for(int i = 0; i < containers.Length; ++i){
				ClayContainer container = containers[i];
				container.pickingThis = false;
			}

			ClayContainer.pickingMode = true;
			ClayContainer.pickedObj = null;

			ClayContainer.pickedClayObjectId = -1;
	  		ClayContainer.pickedContainerId = -1;
			ClayContainer.lastPickedContainerId = -1;

			for(int i = 0; i < SceneView.sceneViews.Count; ++i){
				((SceneView)(SceneView.sceneViews[i])).Repaint();
			}

			if(!ClayContainer.directPickEnabled){
				ClayContainer.directPick = true;
			}
		}

		static void performScenePickingMesh(Camera camera, float mousePosX, float mousePosY){
			if(camera == null){
				return;
			}

			if(mousePosX < 0 || mousePosX >= camera.pixelWidth || 
				mousePosY < 0 || mousePosY >= camera.pixelHeight){

				pickedContainerId = -1;
				pickedClayObjectId = -1;
				return;
			}

			if(ClayContainer.containersInScene.ContainsKey(ClayContainer.lastPickedContainerId)){
				ClayContainer lastContainer = ClayContainer.containersInScene[ClayContainer.lastPickedContainerId];
				lastContainer.pickingThis = false;
				ClayContainer.lastPickedContainerId = -1;
			}
				
			if(ClayContainer.containersInScene.ContainsKey(ClayContainer.pickedContainerId)){
				ClayContainer container = ClayContainer.containersInScene[ClayContainer.pickedContainerId];
				ClayContainer.lastPickedContainerId = ClayContainer.pickedContainerId;
				
				if(container.renderMode == ClayContainer.RenderModes.polySplat){
					if(container.editingThisContainer && !container.pickingThis && container.instanceOf == null){
						ClayContainer.claycoreCompute.SetInt("storeSolidId", 1);
						container.forceUpdateAllSolids();
			  			container.computeClay();
			  			ClayContainer.claycoreCompute.SetInt("storeSolidId", 0);
			  		}
			  	}
				
				container.pickingThis = true;
			}
			
			ClayContainer.pickedClayObjectId = -1;
	  		ClayContainer.pickedContainerId = -1;

			ClayContainer.pickingCommandBuffer.Clear();
			ClayContainer.pickingCommandBuffer.SetRenderTarget(ClayContainer.pickingRenderTextureId);
			ClayContainer.pickingCommandBuffer.ClearRenderTarget(true, true, Color.black, 1.0f);

			foreach(int key in ClayContainer.containersInScene.Keys){
				ClayContainer container = ClayContainer.containersInScene[key];
				if(container.enabled && container.getRenderMode() != 1){
					container.drawClayxelPickingMesh(key, ClayContainer.pickingCommandBuffer, container.pickingThis);
				}
			}
			
			Graphics.ExecuteCommandBuffer(ClayContainer.pickingCommandBuffer);
			
			int rectWidth = (int)(1024.0f * ((float)mousePosX / (float)camera.pixelWidth));
			int rectHeight = (int)(768.0f * ((float)mousePosY / (float)camera.pixelHeight));
			
			#if UNITY_EDITOR_OSX
				rectHeight = 768 - rectHeight;
			#endif

			ClayContainer.pickingRect.Set(
				rectWidth, 
				rectHeight, 
				1, 1);

			RenderTexture oldRT = RenderTexture.active;
			RenderTexture.active = ClayContainer.pickingRenderTexture;
			ClayContainer.pickingTextureResult.ReadPixels(ClayContainer.pickingRect, 0, 0);
			ClayContainer.pickingTextureResult.Apply();
			RenderTexture.active = oldRT;
			
			Color pickCol = ClayContainer.pickingTextureResult.GetPixel(0, 0);
			int pickId = (int)((pickCol.r + pickCol.g * 255.0f + pickCol.b * 255.0f) * 255.0f);
	  		ClayContainer.pickedClayObjectId = pickId - 1;
	  		ClayContainer.pickedContainerId = (int)(pickCol.a * 256.0f);
	  		
	  		if(ClayContainer.pickedContainerId >= 255){
	  			ClayContainer.pickedContainerId = -1;
	  		}
		}

		static void setupPickingMesh(){
			ClayContainer.pickingCommandBuffer = new CommandBuffer();
			
			ClayContainer.pickingTextureResult = new Texture2D(1, 1, TextureFormat.ARGB32, false);
			ClayContainer.pickingRect = new Rect(0, 0, 1, 1);

			if(ClayContainer.pickingRenderTexture != null){
				ClayContainer.pickingRenderTexture.Release();
				ClayContainer.pickingRenderTexture = null;
			}

			ClayContainer.pickingRenderTexture = new RenderTexture(1024, 768, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			ClayContainer.pickingRenderTexture.Create();
			ClayContainer.pickingRenderTextureId = new RenderTargetIdentifier(ClayContainer.pickingRenderTexture);

			if(ClayContainer.pickingMeshMaterialPolySplat ==  null){
				Shader pickingShader = Shader.Find("Clayxels/ClayxelPickingShaderPolySplat");
				ClayContainer.pickingMeshMaterialPolySplat = new Material(pickingShader);

				pickingShader = Shader.Find("Clayxels/ClayxelPickingShaderSmoothMesh");
				ClayContainer.pickingMeshMaterialSmoothMesh = new Material(pickingShader);

				ClayContainer.pickingMeshMaterialProperties = new MaterialPropertyBlock();
			}
		}

		static void clearPickingMesh(){
			ClayContainer[] containers = GameObject.FindObjectsOfType<ClayContainer>();

			for(int i = 0; i < containers.Length; ++i){
				ClayContainer container = containers[i];
				container.pickingThis = false;
			}

			bool continuePicking = false;
			if(ClayContainer.pickedContainerId > -1 && ClayContainer.pickedContainerId < containers.Length){
				if(containers[ClayContainer.pickedContainerId].instanceOf == null){
					if(ClayContainer.pickedObj != null){
						if(ClayContainer.pickedObj.GetComponent<ClayContainer>() != null){
							ClayContainer.pickedObj = null;
							ClayContainer.pickingMode = true;

							continuePicking = true;
						}
					}
				}
			}

			if(!continuePicking){
				ClayContainer.pickingMode = false;
				ClayContainer.pickedObj = null;
				ClayContainer.pickedContainerId = -1;
				ClayContainer.pickedClayObjectId = -1;
				ClayContainer.lastPickedContainerId = -1;
			}

			UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
		}

		static void finalizePickingMesh(SceneView sceneView){
	  		if(ClayContainer.containersInScene.ContainsKey(ClayContainer.pickedContainerId)){
	  			ClayContainer container = ClayContainer.containersInScene[ClayContainer.pickedContainerId];

	  			GameObject newSel = null;
	  			if(ClayContainer.pickedClayObjectId > -1 && container.editingThisContainer){
		  			newSel = container.getClayObject(ClayContainer.pickedClayObjectId).gameObject;
		  		}
		  		else{
		  			newSel = container.gameObject;
		  		}

		  		if(newSel != null){
					if(Event.current.shift){
						List<UnityEngine.Object> sel = new List<UnityEngine.Object>();
			   			for(int i = 0; i < UnityEditor.Selection.objects.Length; ++i){
			   				sel.Add(UnityEditor.Selection.objects[i]);
			   			}
			   			sel.Add(newSel);
			   			UnityEditor.Selection.objects = sel.ToArray();
		   			}
		   			else{
						UnityEditor.Selection.objects = new GameObject[]{newSel};
					}
				}
				
	  			ClayContainer.pickedObj = newSel;
	  			ClayContainer.pickingShiftPressed = Event.current.shift;
	  			
	  			return;
	  		}
			
			ClayContainer.clearPickingMesh();
		}

		void OnDrawGizmos(){
			if(Application.isPlaying){
				return;
			}

			if(!this.editingThisContainer){
				return;
			}

			Gizmos.color = ClayContainer.boundsColor;
			Gizmos.matrix = this.transform.localToWorldMatrix;
			Gizmos.DrawWireCube(this.boundsCenter, this.boundsScale);

			// debug auto bounds
			// if(this.autoBounds){
				// Gizmos.color = Color.red;
				// Gizmos.matrix = this.transform.localToWorldMatrix;
				// Gizmos.DrawWireCube(Vector3.zero, new Vector3(this._debugAutoBounds, this._debugAutoBounds, this._debugAutoBounds));
			// }
			
			// debug bounds
			// Gizmos.color = Color.red;
			// Gizmos.matrix = Matrix4x4.identity;
			// Gizmos.DrawWireCube(ClayContainer.renderBoundsGlob.center, ClayContainer.renderBoundsGlob.size);
		}

		static public void reloadAll(){
			ClayContainer.globalDataNeedsInit = true;
			ClayContainer.initGlobalData();

			ClayContainer[] containers = UnityEngine.Object.FindObjectsOfType<ClayContainer>();
			for(int i = 0; i < containers.Length; ++i){
				ClayContainer container = containers[i];
				container.needsInit = true;
				container.init();

				container.needsUpdate = true;
			}

			UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
			((SceneView)SceneView.sceneViews[0]).Repaint();

			if(ClayContainer.globalUserWarning != ""){
				Debug.Log("Clayxels Warning: " + ClayContainer.globalUserWarning);
			}
		}

		public static SceneView getSceneView(){
			SceneView sceneView = SceneView.currentDrawingSceneView;
			if(sceneView != null){
				return sceneView;
			}

			sceneView = SceneView.lastActiveSceneView;
			if(sceneView != null){
				return sceneView;
			}

			return (SceneView)SceneView.sceneViews[0];
		}
		
		public int retopoMaxVerts = -1;
		public bool retopoApplied = false;
		
		public void storeMesh(string assetName){
			if(this.gameObject.GetComponent<MeshFilter>().sharedMesh == null){
				return;
			}

			if(ClayContainer.defaultAssetsPath != ""){
				if(!AssetDatabase.IsValidFolder("Assets/" + ClayContainer.defaultAssetsPath)){
					AssetDatabase.CreateFolder("Assets", ClayContainer.defaultAssetsPath);
				}
			}

			string assetNameUnique = this.storeAssetPath + "_" + this.GetInstanceID();
			string assetFile = "Assets/" + ClayContainer.defaultAssetsPath + "/" + assetNameUnique + ".mesh";
			
			AssetDatabase.CreateAsset(this.gameObject.GetComponent<MeshFilter>().sharedMesh, assetFile);
			AssetDatabase.SaveAssets();
		}

		static void frameSelected(SceneView sceneView){
			int numClayxelsObjs = 0;
			Bounds bounds = new Bounds();

			for(int i = 0; i < UnityEditor.Selection.gameObjects.Length; ++i){
				GameObject selObj = UnityEditor.Selection.gameObjects[i];

				ClayContainer container = selObj.GetComponent<ClayContainer>();
				if(container != null){
					// bounds.Encapsulate(container.renderBounds);
					// numClayxelsObjs += 1;
				}
				else{
					ClayObject clayObj = selObj.GetComponent<ClayObject>();
					if(clayObj != null){
						bounds.Encapsulate(new Bounds(clayObj.transform.position, clayObj.transform.lossyScale));
						numClayxelsObjs += 1;
					}
				}
			}

			if(numClayxelsObjs > 0){
				sceneView.Frame(bounds, false);
			}
		}

		static void mouseClickMicrovoxelPicking(int mouseX, int mouseY){
			if(ClayContainer.globalDataNeedsInit){
				return;
			}

			if(ClayContainer.containersInScene.Count == 0){
				return;
			}

			int pickedContainerId = -1;
			int pickedClayObjectId = -1;

			bool invertVerticalMouseCoords = false;

			#if UNITY_EDITOR_OSX
				invertVerticalMouseCoords = true;
			#endif

			ClayContainer.pickingMicrovoxel(Camera.current, mouseX, mouseY, out pickedContainerId, out pickedClayObjectId, invertVerticalMouseCoords);

			GameObject pickedObj = null;
			bool pickComplete = false;
			if(ClayContainer.containersInScene.ContainsKey(pickedContainerId)){
				ClayContainer container = ClayContainer.containersInScene[pickedContainerId];

				if(container.instanceOf == null && container.editingThisContainer && pickedClayObjectId > -1){
					pickedObj = container.getClayObject(pickedClayObjectId).gameObject;
					pickComplete = true;
				}
				else{
					pickedObj = container.gameObject;
				}
			}
			
			if(pickedObj != null){
				if(Event.current.shift){
					List<UnityEngine.Object> sel = new List<UnityEngine.Object>();
		   			for(int i = 0; i < UnityEditor.Selection.objects.Length; ++i){
		   				sel.Add(UnityEditor.Selection.objects[i]);
		   			}
		   			sel.Add(pickedObj);
		   			UnityEditor.Selection.objects = sel.ToArray();
	   			}
	   			else{
					UnityEditor.Selection.objects = new GameObject[]{pickedObj};
				}
			}

			if(!ClayContainer.directPickEnabled){
				if(ClayContainer.pickedContainerIdMV == -1 || pickComplete){
					ClayContainer.directPick = false;
				}

				ClayContainer.pickedContainerIdMV = -1;
				ClayContainer.pickedClayObjectIdMV = -1;
			}
		}

		static void mouseMoveMicrovoxelPicking(int mouseX, int mouseY){
			if(ClayContainer.globalDataNeedsInit){
				return;
			}

			if(ClayContainer.containersToRender.Count == 0){
				return;
			}
			
			int pickedContainerId = -1;
			int pickedClayObjectId = -1;

			bool invertVerticalMouseCoords = false;

			#if UNITY_EDITOR_OSX
				invertVerticalMouseCoords = true;
			#endif

			ClayContainer.pickingMicrovoxel(Camera.current, mouseX, mouseY, out pickedContainerId, out pickedClayObjectId, invertVerticalMouseCoords);
			
			ClayContainer container = null;
			if(pickedContainerId > -1){
				if(ClayContainer.containersInScene.ContainsKey(pickedContainerId)){
					container = ClayContainer.containersInScene[pickedContainerId];
				}
			}
			
			ClayContainer.pickedContainerIdMV = -1;
			ClayContainer.pickedClayObjectIdMV = -1;

			if(container != null){
				ClayContainer.pickedContainerIdMV = pickedContainerId;

				if(!container.editingThisContainer || container.instanceOf != null){
					ClayContainer.pickedClayObjectIdMV = -2;
				}
				else{
					if(pickedClayObjectId > -1 && pickedClayObjectId < container.getNumClayObjects()){
						ClayObject clay = container.getClayObject(pickedClayObjectId);
						if(clay != null){
							GameObject clayObj = clay.gameObject;
							if(!UnityEditor.Selection.Contains(clayObj)){
								ClayContainer.pickedClayObjectIdMV = pickedClayObjectId;
							}
						}
					}
				}
			}
		}

		static public bool directPickingEnabled(){
			return ClayContainer.directPickEnabled;
		}

		static public void _displayGlobalUserWarning(string msg){
			ClayContainer.globalUserWarning = "";

			if(msg != ""){
				ClayContainer.globalUserWarning = msg + "\n";

				Debug.Log("Clayxels Warning: " + ClayContainer.globalUserWarning);
			}
		}

		#endif// end if UNITY_EDITOR
	}
}
