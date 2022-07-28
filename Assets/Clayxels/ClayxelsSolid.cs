
using UnityEngine;

namespace Clayxels{

/*!\brief This is the primitive sent to the GPU to compute the signed distance field. 
		Each ClayObject can spawn multiple Solid.
		The final list of solids to be sent to the gpu is collected internally by ClayContainer,
		it can be retrieved by invoking ClayContainer.getSolids().
	*/
	public class Solid{
		public Vector3 position = Vector3.zero;
		public Quaternion rotation = Quaternion.identity;
		public Vector3 scale = Vector3.one * 0.5f;
		public float blend = 0.0f;
		public Vector3 color = Vector3.one;
		public Vector4 attrs = Vector4.zero;
		public Vector4 attrs2 = Vector4.zero;
		public int primitiveType = 0;
		public int id = -1;
		public int clayObjectId = 0;
	}
}
