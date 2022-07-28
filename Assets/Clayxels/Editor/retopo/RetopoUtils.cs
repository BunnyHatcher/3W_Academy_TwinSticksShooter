
using System;
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;

#if CLAYXELS_RETOPO

namespace Clayxels{
	public static class RetopoUtils{
		public static int getRetopoTargetVertsCount(GameObject gameObj, int retopoMaxVerts){
			int targetVerts = 0;
			if(gameObj.GetComponent<MeshFilter>() != null){
				if(gameObj.GetComponent<MeshFilter>().sharedMesh == null){
					// means something went wrong with mesh freezing
					return 0;
				}

				int meshVerts = gameObj.GetComponent<MeshFilter>().sharedMesh.vertices.Length;
				targetVerts = retopoMaxVerts;
				if(targetVerts == -1){
					targetVerts = meshVerts / 2;

					if(targetVerts > 10000){
						targetVerts = 10000;
					}
				}
				else{
					int minVerts = 100;
					int maxVerts = meshVerts;
					
					if(targetVerts < minVerts){
						targetVerts = minVerts;
					}
					if(targetVerts > maxVerts){
						targetVerts = maxVerts;
					}
				}
			}

			return targetVerts;
		}

		static class NativeLib
		{
			[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
			static public extern IntPtr LoadLibrary(string lpFileName);

			[DllImport("kernel32", SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			static public extern bool FreeLibrary(IntPtr hModule);

			[DllImport("kernel32")]
			static public extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void RetopoMeshDelegate(IntPtr verts, int numVerts, IntPtr indices, int numTriangles, IntPtr colors, int maxVerts, int maxFaces);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int GetRetopoMeshVertsCountDelegate();

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate int GetRetopoMeshTrisCountDelegate();

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void GetRetopoMeshDelegate(IntPtr verts, IntPtr indices, IntPtr normals, IntPtr colors);
		
		public static unsafe void retopoMesh(Mesh mesh, int maxVerts = -1, int maxFaces = -1){
			IntPtr? libPtr = null;
			
			string libFile = "";
			string[] assets = AssetDatabase.FindAssets("retopoLib");
			if(assets.Length > 0){
				libFile = AssetDatabase.GUIDToAssetPath(assets[0]);
			}

			libPtr = NativeLib.LoadLibrary(libFile);
			
			if(libPtr == IntPtr.Zero){
				Debug.Log("Clayxels failed to find retopoLib.dll, was this file excluded while importing clayxels from the asset store?");
				return;
			}

			try{
				MeshUtils.weldVertices(mesh);
				mesh.Optimize();
				
				IntPtr retopoMeshFuncPtr = NativeLib.GetProcAddress(libPtr.Value, "retopoMesh");
				RetopoMeshDelegate retopoMesh = (RetopoMeshDelegate)Marshal.GetDelegateForFunctionPointer(retopoMeshFuncPtr, typeof(RetopoMeshDelegate));
				
				IntPtr getRetopoMeshVertsCountFuncPtr = NativeLib.GetProcAddress(libPtr.Value, "getRetopoMeshVertsCount");
				GetRetopoMeshVertsCountDelegate getRetopoMeshVertsCount = (GetRetopoMeshVertsCountDelegate)Marshal.GetDelegateForFunctionPointer(getRetopoMeshVertsCountFuncPtr, typeof(GetRetopoMeshVertsCountDelegate));
				
				IntPtr getRetopoMeshTrisCountFuncPtr = NativeLib.GetProcAddress(libPtr.Value, "getRetopoMeshTrisCount");
				GetRetopoMeshTrisCountDelegate getRetopoMeshTrisCount = (GetRetopoMeshTrisCountDelegate)Marshal.GetDelegateForFunctionPointer(getRetopoMeshTrisCountFuncPtr, typeof(GetRetopoMeshTrisCountDelegate));
				
				IntPtr getRetopoMeshFuncPtr = NativeLib.GetProcAddress(libPtr.Value, "getRetopoMesh");
				GetRetopoMeshDelegate getRetopoMesh = (GetRetopoMeshDelegate)Marshal.GetDelegateForFunctionPointer(getRetopoMeshFuncPtr, typeof(GetRetopoMeshDelegate));
				
				Vector3[] vertsArray = mesh.vertices;
				int[] indices = mesh.triangles;
				Color[] colors = mesh.colors;
				fixed(Vector3* vertsPtr = vertsArray){
					fixed(int* indicesPtr = indices){
						fixed(Color* colorsPtr = colors){
							retopoMesh((IntPtr)vertsPtr, mesh.vertices.Length, (IntPtr)indicesPtr, indices.Length, (IntPtr)colorsPtr, maxVerts, maxFaces);
						}
					}
				}
				
				int newVertsCount = getRetopoMeshVertsCount();
				int newTrisCount = getRetopoMeshTrisCount();
				
				Vector3[] newVerts = new Vector3[newVertsCount];
				int[] newIndices = new int[newTrisCount];
				Vector3[] normals = new Vector3[newVertsCount];
				Color[] newColors = new Color[newVertsCount];

				fixed(Vector3* vertsPtr = newVerts){
					fixed(int* indicesPtr = newIndices){
						fixed(Vector3* normalsPtr = normals){
							fixed(Color* newColorsPtr = newColors){
								getRetopoMesh((IntPtr)vertsPtr, (IntPtr)indicesPtr, (IntPtr)normalsPtr, (IntPtr)newColorsPtr);
							}
						}
					}
				}
				
				mesh.Clear();
				mesh.vertices = newVerts;
				mesh.triangles = newIndices;
				mesh.normals = normals;
				mesh.colors = newColors;

				g3.DMesh3 d3mesh = g3UnityUtils.UnityMeshToDMesh(mesh);
				g3.MeshBoundaryLoops loopsFinder = new g3.MeshBoundaryLoops(d3mesh);
				for(int i = 0; i < loopsFinder.Count; ++i){
					gs.MinimalHoleFill filler = new gs.MinimalHoleFill(d3mesh, loopsFinder.Loops[i]);
					filler.Apply();
				}

				Mesh newMesh = g3UnityUtils.DMeshToUnityMesh(d3mesh);
				// newMesh.RecalculateNormals();

				int[] triangles = newMesh.triangles;
		        Vector3[] vertices = new Vector3[newMesh.triangles.Length];
		        normals = new Vector3[newMesh.triangles.Length];
		        colors = new Color[newMesh.triangles.Length];
		        
				for (int i = 0; i < newMesh.triangles.Length; i++) {
					int vid = triangles[i];
					vertices[i] = newMesh.vertices[vid];
					normals[i] = newMesh.normals[vid];
					colors[i] = newMesh.colors[vid];
					triangles[i] = i;
				}

		        mesh.Clear();
				mesh.vertices = vertices;
				mesh.normals = normals;
				mesh.colors = colors;
				mesh.triangles = triangles;
			}
			catch{
				Debug.Log("Clayxels: failed to perform retopology");
			}
			
			NativeLib.FreeLibrary(libPtr.Value);
			libPtr = null;
		}
	}
}

#endif
