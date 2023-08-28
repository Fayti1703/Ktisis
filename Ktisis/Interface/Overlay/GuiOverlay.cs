using System;

using Dalamud.Interface;
using Dalamud.Logging;

using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

using ImGuiNET;

using Ktisis.Services;

namespace Ktisis.Interface.Overlay;

public delegate void OverlayDraw(GuiOverlay sender);

public class GuiOverlay {
	// Dependencies

	private readonly GPoseService _gpose;
		
    // State
    
    public bool Visible = true;

	public readonly Gizmo? Gizmo;
	
	// Constructor

	public GuiOverlay(GPoseService _gpose, NotifyService _notify) {
		this._gpose = _gpose;
		
		this.Gizmo = Gizmo.Create();
		if (this.Gizmo == null) {
			_notify.Warning(
				"Failed to create gizmo. This may be due to version incompatibilities.\n" +
				"Please check your error log for more information."
			);
		}
	}
	
	// UI draw

	public event OverlayDraw? OnOverlayDraw;

	public void Draw() {
		// TODO: Toggle

		if (!this.Visible || !this._gpose.IsInGPose) return;

		try {
			if (BeginFrame())
				BeginGizmo();
			else return;

			try {
				ImGui.Text("hallo");
				this.OnOverlayDraw?.Invoke(this);
			} catch (Exception err) {
				PluginLog.Error($"Error while drawing overlay:\n{err}");
			}
		} finally {
			EndFrame();
		}
	}

	private bool BeginFrame() {
		const ImGuiWindowFlags flags = ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs;
		
		ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

		var io = ImGui.GetIO();
		ImGui.SetNextWindowSize(io.DisplaySize);
		ImGui.SetNextWindowPos(Vector2.Zero);
		
		ImGuiHelpers.ForceNextWindowMainViewport();

		return ImGui.Begin("Ktisis Overlay", flags);
	}

	private unsafe void BeginGizmo() {
		if (this.Gizmo is null) return;

		var camMgr = CameraManager.Instance;
		var camera = camMgr != null ? camMgr->GetActiveCamera() : null;
		if (camera == null) return;

		var render = camera->CameraBase.SceneCamera.RenderCamera;
		if (render == null) return;

		var proj = render->ProjectionMatrix;
		var view = camera->CameraBase.SceneCamera.ViewMatrix;
		view.M44 = 1f;
		
		this.Gizmo.BeginFrame(view, proj);
	}

	private void EndFrame() {
		ImGui.End();
		ImGui.PopStyleVar();
	}
}