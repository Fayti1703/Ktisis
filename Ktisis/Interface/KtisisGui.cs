﻿using Ktisis.Overlay;
using Ktisis.Interface.Windows;
using Ktisis.Interface.Windows.ActorEdit;

namespace Ktisis.Interface {
	public class KtisisGui {
		public static void Draw() {
			// Overlay
			OverlayWindow.Draw();

			// GUI
			Workspace.Draw();
			ConfigGui.Draw();
			EditActor.Draw();
		}
	}
}