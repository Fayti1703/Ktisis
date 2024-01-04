using System.Collections.Generic;

using Ktisis.Editor.Display;
using Ktisis.Scene.Types;

namespace Ktisis.Data.Config.Sections;

public class EditorConfig {
	// Values

	public bool OpenOnEnterGPose = true; // TODO
	
	public Dictionary<EntityType, EntityDisplay> Display = EntityDisplay.GetDefaults();
	
	// Transform Window

	public bool TransformHide = false;
	
	// Helpers

	public EntityDisplay GetDisplayForType(EntityType type)
		=> this.Display.GetValueOrDefault(type, new EntityDisplay());
}
