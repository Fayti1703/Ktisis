using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Ktisis.Data.Json;

/**
 * <summary>Abstraction to join two <see cref="BlockBufferJsonReader"/>s into a single, coherent stream.</summary>
 */
public ref struct DoubleBufferedJsonReader {
	internal BlockBufferJsonReader firstReader;
	internal BlockBufferJsonReader secondReader;

	[UnscopedRef]
	internal ref BlockBufferJsonReader currentReader {
		get {
			if(this.currentIsFirst)
				return ref this.firstReader;
			return ref this.secondReader;
		}
	}

	internal bool currentIsFirst = true;

	public DoubleBufferedJsonReader(BlockBufferJsonReader firstReader, BlockBufferJsonReader secondReader) {
		Debug.Assert(!this.firstReader.IsFinal);
		this.firstReader = firstReader;
		this.secondReader = secondReader;

		if(!BumpReader(ref this.firstReader))
			this.currentIsFirst = false;
	}

	private static bool BumpReader(ref BlockBufferJsonReader reader) {
		if(reader.Reader.TokenType == JsonTokenType.None)
			return reader.Read();

		return true;
	}

	[UnscopedRef]
	public ref Utf8JsonReader Reader => ref this.currentReader.Reader;

	public bool Read() {
		if(!this.currentIsFirst)
			return this.secondReader.Read();
		if(this.TryFirstReader())
			return true;

		/* `TryFirstReader` just swapped us to the second reader. */
		return BumpReader(ref this.secondReader);
	}

	private bool TryFirstReader() {
		Debug.Assert(this.currentIsFirst);
		bool success = this.firstReader.Read();
		if(success)
			return true;

		this.currentIsFirst = false;
		return false;
	}
}
