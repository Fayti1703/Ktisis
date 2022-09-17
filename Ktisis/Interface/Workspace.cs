﻿using System.Numerics;

using ImGuiNET;
using ImGuizmoNET;

using Dalamud.Interface;
using Dalamud.Interface.Components;

using Ktisis.Util;
using Ktisis.Localization;
using Ktisis.Structs.Bones;

namespace Ktisis.Interface {
	public class Workspace {
		public static bool Visible = false;

		public static Vector4 ColGreen = new Vector4(0, 255, 0, 255);
		public static Vector4 ColRed = new Vector4(255, 0, 0, 255);

		public const ImGuiTreeNodeFlags BaseFlags = ImGuiTreeNodeFlags.OpenOnArrow;

		// Toggle visibility

		public static void Show() {
			Visible = true;
		}

		public static void Hide() {
			Visible = false;
		}

		// Draw window

		public static void Draw() {
			if (!Visible)
				return;

			var gposeOn = KtisisGui.IsInGpose();

			var size = new Vector2(-1, -1);
			ImGui.SetNextWindowSize(size, ImGuiCond.Always);
			ImGui.SetNextWindowSizeConstraints(size, size);

			ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));

			if (ImGui.Begin("Ktisis (Alpha)", ref Visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize)) {
				ImGui.BeginGroup();
				ImGui.AlignTextToFramePadding();

				ImGui.TextColored(
					gposeOn ? ColGreen : ColRed,
					gposeOn ? "GPose Enabled" : "GPose Disabled"
				);

				// Gizmo Controls

				if (ImGuiComponents.IconButton(FontAwesomeIcon.LocationArrow))
					KtisisGui.SkeletonEditor.GizmoOp = OPERATION.TRANSLATE;

				ImGui.SameLine();
				if (ImGuiComponents.IconButton(FontAwesomeIcon.Sync))
					KtisisGui.SkeletonEditor.GizmoOp = OPERATION.ROTATE;

				ImGui.SameLine();
				if (ImGuiComponents.IconButton(FontAwesomeIcon.ExpandArrowsAlt))
					KtisisGui.SkeletonEditor.GizmoOp = OPERATION.SCALE;

				ImGui.SameLine();
				if (ImGuiComponents.IconButton(FontAwesomeIcon.DotCircle))
					KtisisGui.SkeletonEditor.GizmoOp = OPERATION.UNIVERSAL;

				// Second row

				var gizmode = KtisisGui.SkeletonEditor.Gizmode;
				if (GuiHelpers.IconButtonTooltip(
					gizmode == MODE.WORLD ? FontAwesomeIcon.Globe : FontAwesomeIcon.Home, "Local / World orientation mode switch."))
					KtisisGui.SkeletonEditor.Gizmode = gizmode == MODE.WORLD ? MODE.LOCAL : MODE.WORLD;

				ImGui.SameLine();
				if (GuiHelpers.IconButtonTooltip(FontAwesomeIcon.PencilAlt, "Edit targeted Actor's appearance.")) {
					CustomizeGui.Show(KtisisGui.SkeletonEditor.Subject);
				}

				// Config

				var cfg = Ktisis.Configuration;

				ImGui.SameLine();
				if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
					ConfigGui.Show();

				ImGui.Separator();

				Coordinates();

				ImGui.Separator();

				var _ = false;
				if (ImGui.Checkbox("Toggle Posing", ref _)) {
					// TODO
				}

				var showSkeleton = cfg.ShowSkeleton;
				if (ImGui.Checkbox("Toggle Skeleton", ref showSkeleton)) {
					cfg.ShowSkeleton = showSkeleton;
					if (!showSkeleton)
						KtisisGui.SkeletonEditor.ResetState();
				}

				if (ImGui.CollapsingHeader("Toggle Bone Categories  ")) {

					ImGui.Indent(16.0f);
					foreach (Category category in Category.Categories.Values) {
						if (!category.ShouldDisplay) continue;

						bool categoryState = cfg.IsBoneCategoryVisible(category);
						if (!cfg.ShowSkeleton) categoryState = false;

						if (ImGui.Checkbox(category.Name, ref categoryState)) {
							if (!cfg.ShowSkeleton && categoryState) {
								cfg.ShowSkeleton = true;
							}
							cfg.ShowBoneByCategory[category.Name] = categoryState;
						}
					}
					ImGui.Unindent(16.0f);
				}

				ImGui.Separator();

				// Bone tree

				DrawBoneTree();
			}

			ImGui.PopStyleVar(1);
			ImGui.End();
		}

		// Coordinates table
		private static void Coordinates() {
			Bone? selectedBone = KtisisGui.SkeletonEditor.GetSelectedBone();
			if (KtisisGui.SkeletonEditor.Skeleton == null || selectedBone == null) {
				ImGuiComponents.HelpMarker("Select a bone to spawn Coordinates table.");
				return;
			};

			GuiHelpers.DragVec4intoVec3("Position", ref selectedBone.Transform.Position, 0.0001f);
			GuiHelpers.DragQuatIntoEuler("Rotation", ref selectedBone.Transform.Rotation, 0.1f);
			GuiHelpers.DragVec4intoVec3("Scale", ref selectedBone.Transform.Scale, 0.01f);

			// Use the same functions found in SkeletonEditor.Draw()
			var delta = KtisisGui.SkeletonEditor.BoneMod.GetDelta();
			selectedBone.Transform.Rotation *= delta.Rotation;
			selectedBone.TransformBone(delta, KtisisGui.SkeletonEditor.Skeleton);
		}

		// Bone Tree

		public static void DrawBoneTree() {
			var editor = KtisisGui.SkeletonEditor;
			if (editor.Skeleton != null && editor.Skeleton.Count > 0)
				DrawBoneTree(editor.Skeleton[0].Bones[0]);
		}

		public static void DrawBoneTree(Bone bone) {
			var flag = BaseFlags;

			if (KtisisGui.SkeletonEditor.BoneSelector.IsSelected(bone))
				flag |= ImGuiTreeNodeFlags.Selected;

			var children = bone.GetChildren();
			if (children.Count == 0)
				flag |= ImGuiTreeNodeFlags.Leaf;

			var show = bone.IsRoot;
			if (!show) {
				show = ImGui.TreeNodeEx(bone.HkaBone.Name, flag, Locale.GetBoneName(bone.HkaBone.Name!));

				var rectMin = ImGui.GetItemRectMin() + new Vector2(ImGui.GetTreeNodeToLabelSpacing(), 0);
				var rectMax = ImGui.GetItemRectMax();

				var mousePos = ImGui.GetMousePos();
				if (
					ImGui.IsMouseClicked(ImGuiMouseButton.Left)
					&& mousePos.X > rectMin.X && mousePos.X < rectMax.X
					&& mousePos.Y > rectMin.Y && mousePos.Y < rectMax.Y
				) {
					KtisisGui.SkeletonEditor.SelectBone(bone);
				}
			}

			if (show) {
				// Show children
				foreach (var child in children)
					DrawBoneTree(child);
				ImGui.TreePop();
			}
		}
	}
}
