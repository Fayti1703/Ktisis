using System.Numerics;

namespace Ktisis.Interface.SceneUi.Logic; 

public interface IManipulable : ITransform {
	public Matrix4x4? ComposeMatrix()
		=> GetTransform()?.ComposeMatrix();

	public void SetMatrix(Matrix4x4 mx) {
		var trans = GetTransform();
		if (trans is null) return;
		trans.DecomposeMatrix(mx);
		SetTransform(trans);
	}
}