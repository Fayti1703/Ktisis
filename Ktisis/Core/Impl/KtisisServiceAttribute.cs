using System;
using JetBrains.Annotations;

namespace Ktisis.Core.Impl;

[Flags]
public enum ServiceFlags {
	None = 0
}

[MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
[AttributeUsage(AttributeTargets.Class)]
public class KtisisServiceAttribute : Attribute {
	// Properties
	
	public readonly ServiceFlags Flags;
	
	// Constructor
	
	public KtisisServiceAttribute() {}

	public KtisisServiceAttribute(ServiceFlags flags)
		=> this.Flags = flags;
	
	// Methods

	public bool HasFlag(ServiceFlags flag)
		=> this.Flags.HasFlag(flag);
}
