// Copyright 2018-2021 tripolygon Inc. All Rights Reserved.

using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using tripolygon.UModeler;
using UnityEditor.SceneManagement;
using System.Reflection;

namespace TPUModelerEditor
{
    [InitializeOnLoadAttribute]
    public static class UModelerEditorInitializer
    {
        static UModelerEditorInitializer()
        {
            UMContext.Init(new EditorEngine());

            Selection.selectionChanged += HandleOnSelectionChanged;
            EditorUtil.SetSelectedRenderStateCallbackInst += OnSetSelectedRenderStateCallback;
            Builder.modelBuilt += OnMeshBuilt;
            Builder.modelBuilding += OnMeshBuilding;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpening += OnSceneLoading;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneLoaded;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += OnSceneSaved;
            PrefabUtility.prefabInstanceUpdated += PrefabInstanceUpdated;
            EditorUtil.TryGetUModelerComponent += TryGetComponent<UModeler>;
            EditorUtil.TryGetMeshFilterComponent += TryGetComponent<MeshFilter>;
            EditorUtil.GenerateSecondaryUVSet = GenerateSecondaryUVSet;

#if UNITY_2019_2_OR_NEWER
            Lightmapping.bakeStarted += OnLightmapBake;
#endif

#if UNITY_2019_3_OR_NEWER
            EditorDecl.SettingsBoxHeight = 410;
#else
            EditorDecl.SettingsBoxHeight = 360;
#endif

#if UNITY_2017_2_OR_NEWER
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
#else
            EditorApplication.playmodeStateChanged += HandleOnPlayModeChanged;
#endif

#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui += UModelerEditor.OnScene;
#else
            SceneView.onSceneGUIDelegate += UModelerEditor.OnScene;
#endif

        }

        private static void OnLightmapBake()
        {
            EditorUtil.RefreshAll(bOutputLog: false);
        }

        public static void HandleOnSelectionChanged()
        {
            UModelerEditor.SendMessage(UModelerMessage.SelectionChanged);

            if (UMContext.activeModeler != null)
            {
                EditorMode.commentaryViewer.AddTitleNoDuplilcation("[" + UMContext.activeModeler.gameObject.name + "] Object has been selected.");
            }
        }

#if UNITY_2017_2_OR_NEWER
        public static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (EditorMode.currentTool != null && UMContext.activeModeler != null)
                {
                    EditorMode.currentTool.End();
                    EditorMode.currentTool.Start();
                }

                if (Selection.activeGameObject != null)
                {
                    if (Selection.activeGameObject.GetComponent<UModeler>() != null)
                    {
                        Selection.activeGameObject = null;
                    }
                }

                UModeler.enableDelegate = false;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                UModeler.enableDelegate = true;
            }

            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                EditorUtil.DisableHasTransformed();
            }
        }
#else
        public static void HandleOnPlayModeChanged()
        {
            bool bExitingEditMode = !EditorApplication.isPlaying &&  EditorApplication.isPlayingOrWillChangePlaymode;
            bool bEnteredEditMode = !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode;
            bool bEnteredPlayMode =  EditorApplication.isPlaying &&  EditorApplication.isPlayingOrWillChangePlaymode;

            if (bExitingEditMode)
            {
                UModeler.enableDelegate = true;
                if (EditorMode.currentTool != null && UMContext.activeModeler != null)
                {
                    EditorMode.currentTool.End();
                    EditorMode.currentTool.Start();
                }

                if (Selection.activeGameObject != null)
                {
                    if (Selection.activeGameObject.GetComponent<UModeler>() != null)
                    {
                        Selection.activeGameObject = null;
                    }
                }
            }

            if (bEnteredPlayMode)
            {
                UModeler.enableDelegate = false;
            }

            if (bEnteredPlayMode || bEnteredEditMode)
            {
                EditorUtil.RefreshAll(false/*bOutputLog*/);
                EditorUtil.DisableHasTransformed();
            }
        }
#endif

        private static bool TryGetComponent<T>(GameObject go, out T outComponent)
        {
#if UNITY_2019_2_OR_NEWER
            return go.TryGetComponent<T>(out outComponent);
#else
            outComponent = go.GetComponent<T>();
            return outComponent != null;
#endif
        }

        private static void OnSceneLoading(string path, OpenSceneMode mode)
        {
            UModeler.ResetMeshContainer();
        }

        static void OnSceneLoaded(Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            EditorUtil.DisableHasTransformed();
            MenuGUICacheData.Invalidate();
            Selection.activeObject = null;
        }

        static void OnSceneSaving(Scene scene, string path)
        {
            if (UMContext.activeModeler != null && EditorMode.currentTool != null)
            {
                EditorMode.currentTool.End();
                EditorMode.currentTool.Start();

                if (!UMContext.activeModeler.editableMesh.IsEmpty(1))
                {
                    UMContext.activeModeler.editableMesh.Clear(1);
                    UMContext.activeModeler.Build(1);
                }
            }
        }

        static MethodInfo SetSelectedRenderState = typeof(EditorUtility).GetMethod("SetSelectedRenderState");
        private static void OnSetSelectedRenderStateCallback(bool enabled, UModeler modeler)
        {
            if (modeler == null)
            {
                return;
            }

            if (SetSelectedRenderState != null)
            {
                // EditorSelectedRenderState.Highlight : 2
                // EditorSelectedRenderState.Hidden : 0
#if UNITY_2021_1_OR_NEWER
                SetSelectedRenderState.Invoke(null, new object[] { modeler.meshRenderer, enabled ? 2 : 0 });
#else

                SetSelectedRenderState.Invoke(null, new object[] { modeler.meshRenderer, enabled ? 2 : 0 });
#endif
            }
            else
            {
                EditorUtil.EnableWireFrame(modeler.meshRenderer, enabled);
            }
        }

        private static void OnSceneSaved(Scene scene)
        {
        }

        static void OnMeshBuilt(UModeler modeler, int shelf)
        {
            UModelerEditor.OnChanged();

            if (shelf == 0)
            {
                modeler.editableMesh.IsBuilt = true;

                if (EditorUtil.HasStaticLightmap(modeler))
                {
                    EditorUtil.GenerateUV2(modeler);

                    EditorUtil.SetLightmap(modeler, false);
                    EditorUtil.SetLightmap(modeler, true);
                }
            }
        }

        private static void OnMeshBuilding(UModeler modeler, int shelf)
        {
            if (shelf == 0)
            {
                UModelerEditor.DisconnectPrefabMeshLink(modeler);
            }
            else if (shelf == 1)
            {
                using (new ShelfHolder(modeler.editableMesh))
                {
                    modeler.editableMesh.shelf = 1;
                    var polygonList = modeler.editableMesh.GetAllPolygons();

                    if (UMContext.activeModeler.autoHotspotLayout)
                    {
                        HotspotLayoutTool.HotspotTexturing(polygonList, true, false);
                        modeler.editableMesh.uvIslandManager.RemoveAllEmpty();
                    }
                }
            }
        }

        static public void PrefabInstanceUpdated(GameObject instance)
        {
            UpdateInstance(instance);
        }

        static private void UpdateInstance(GameObject instance)
        {
            UModeler modeler = instance.GetComponent<UModeler>();

            if (modeler != null)
            {
                modeler.editableMesh.InvalidateCache();
                using (new ActiveModelerHolder(modeler))
                {
                    modeler.Build(0, updateToGraphicsAPIImmediately: true);
                }
            }

            for (int i = 0; i < instance.transform.childCount; ++i)
            {
                UpdateInstance(instance.transform.GetChild(i).gameObject);
            }
        }

        static private void GenerateSecondaryUVSet(Mesh src, UnwrapParam settings)
        {
#if UNITY_2022_1_OR_NEWER
            Unwrapping.GenerateSecondaryUVSet(src, settings);
#else
            Unwrapping.GenerateSecondaryUVSet(src, settings);
#endif           
        }
    }
}