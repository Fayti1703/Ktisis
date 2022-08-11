﻿using System.Numerics;
using System.Collections.Generic;

using ImGuizmoNET;

using Ktisis.Structs.Havok;

namespace Ktisis.Structs.Ktisis {
	public class Bone {
		public int Index;
		public short ParentId;
		public Transform Transform;

		public HkaBone HkaBone;

		public SharpDX.Matrix Matrix;

		// Constructor

		public Bone(BoneList bones, int index) {
			Index = index;
			ParentId = bones.Skeleton.ParentIndex[index];
			Transform = bones.Transforms[index];

			HkaBone = bones.Skeleton.Bones[index];

			UpdateTransform(bones);
		}

		// Update stored transform from matrix

		public void UpdateTransform(BoneList bones) {
			var t = bones.Transforms[Index];
			Transform = t;
			ImGuizmo.RecomposeMatrixFromComponents(ref t.Translate.X, ref t.Rotate.X, ref t.Scale.X, ref Matrix.M11);
		}

		// Apply stored transform

		public void ApplyTransform(BoneList bones) {
			bones.Transforms[Index] = Transform;
		}

		// Quaternion rotation

		public Vector3 Rotate(Quaternion quat) {
			var t = Transform.Translate;
			return Vector3.Transform(new Vector3(t.X, t.Y, t.Z), quat);
		}

		// Transform bone

		public void TransformBone(Transform t) {
			Transform.Translate += t.Translate;
			// doesn't work, disable this for now.
			//bone.Transform.Rotate += delta.Rotate;
			Transform.Scale *= t.Scale;
		}

		public void TransformBone(Transform t, BoneList bones, bool parenting = false) {
			TransformBone(t);
			ApplyTransform(bones);
			if (parenting)
				TransformChildren(t, bones);
		}

		// Transform children

		public void TransformChildren(Transform t, BoneList bones) {
			var children = new List<Bone>();
			bones.GetChildrenRecursive(this, ref children);

			foreach (var child in children) {
				child.TransformBone(t, bones);
			}
		}
	}
}
