// Copyright 2018-2021 tripolygon Inc. All Rights Reserved.

using UnityEngine;
using UnityEditor;
using tripolygon.UModeler;

namespace TPUModelerEditor
{
    [CustomEditor(typeof(MeshFilter))]
    public class MeshFilterEditor : Editor
    {
        bool foldedOut = true;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            MeshFilter mf = (MeshFilter)target;

            UModeler modeler = mf.GetComponent<UModeler>();
            if (modeler != null && !EditorUtil.IsPrefabOnProject(modeler.gameObject))
            {
                foldedOut = EditorUtil.Foldout(foldedOut, "UModeler .asset");
                Mesh mesh = modeler.renderableMeshFilter != null ? modeler.renderableMeshFilter.sharedMesh : null;
                if (foldedOut && mesh != null)
                {   
                    if (modeler.IsAssetPathValid())
                        GUILayout.Label("File name : " + modeler.AssetFileName);
                    else
                        GUILayout.Label(".Asset file of this mesh doesn't exist yet.");

                    GUI.changed = false;

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Save"))
                    {
                        string path = modeler.IsAssetPathValid() ? modeler.assetPath : SystemUtil.MeshAssetFolder + "/" + mesh.name + ".asset";
                        if (SystemUtil.SaveMeshAsset(modeler, path))
                        {
                            EditorUtility.SetDirty(mf);
                        }
                    }
                    else if (GUILayout.Button("Save As"))
                    {
                        if (SystemUtil.SaveMeshAsset(modeler))
                        {
                            EditorUtility.SetDirty(mf);
                        }
                    }
                    else
                    {
                        GUILayout.EndHorizontal();
                    }

                    if (GUI.changed)
                    {
                        if (modeler.IsAssetPathValid())
                        {
                            modeler.renderableMeshFilter.sharedMesh.name = modeler.MeshName;
                            EditorMode.commentaryViewer.AddTitle("The mesh has been saved as " + modeler.AssetFileName);
                        }
                    }
                }
            }
        }
    }
}