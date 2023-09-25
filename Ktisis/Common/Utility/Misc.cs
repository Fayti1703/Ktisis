using System.Runtime.CompilerServices;

namespace Ktisis.Common.Utility;

public static class Misc {
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Exchange<T>(ref T value, T newValue) {
		T v = value;
		value = newValue;
		return v;
	}
}
