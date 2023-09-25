using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Ktisis.Data.Json;

/**
 * <summary>Synchronous JSON-Reader-over-Stream abstraction that uses a potentially stack-allocated block buffer.</summary>
 */
public ref struct BlockBufferJsonReader {

	private enum Stage {
		INIT,
		READING,
		FINAL_READ,
		CLOSED
	}

	public Utf8JsonReader Reader = default;
	private readonly Stream stream;
	private readonly Span<byte> blockBuffer;
	private Span<byte> readSlice = default;
	private JsonReaderState jsonState;
	private Stage _stage = Stage.INIT;

	public BlockBufferJsonReader(Stream stream, Span<byte> blockBuffer, JsonReaderOptions options) {
		this.stream = stream;
		this.blockBuffer = blockBuffer;
		this.jsonState = new JsonReaderState(options);
	}

	public bool Read() {
		switch(this._stage) {
			case Stage.CLOSED:
				return false;
			case Stage.READING:
			case Stage.FINAL_READ:
				if(this.Reader.Read()) return true;
				if(this._stage == Stage.FINAL_READ) {
					this._stage = Stage.CLOSED;
					return false;
				}
				goto case Stage.INIT;
			case Stage.INIT:
				this.acquireReader();
				goto case Stage.READING;
		}
		Debug.Assert(false, "This point is unreachable");
		throw new Exception("This point is unreachable");
	}

	private void acquireReader() {
		int preRead = 0;
		Debug.Assert(this._stage != Stage.FINAL_READ, "Shouldn't be in final read here");
		if(this._stage != Stage.INIT) {
			if(this.Reader.BytesConsumed == 0)
				throw new Exception("JSON value appears to exceed the bounds of the block buffer. Increase the buffer size or decrease your JSON value size.");
			this.jsonState = this.Reader.CurrentState;
			Span<byte> remainingSlice = this.readSlice[(int) this.Reader.BytesConsumed..];
			remainingSlice.CopyTo(this.blockBuffer);
			preRead = remainingSlice.Length;
		}

		int read = this.stream.Read(this.blockBuffer[preRead..]);
		this.readSlice = this.blockBuffer[..(preRead+read)];
		this._stage = this.readSlice.Length == 0 ? Stage.FINAL_READ : Stage.READING;

		this.Reader = new Utf8JsonReader(this.readSlice, this.readSlice.Length == 0, this.jsonState);
	}
}
