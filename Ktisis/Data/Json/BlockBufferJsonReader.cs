using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Ktisis.Common.Utility;

namespace Ktisis.Data.Json;

/**
 * <summary>Synchronous JSON-Reader-over-Stream abstraction that uses a potentially stack-allocated block buffer.</summary>
 */
public ref struct BlockBufferJsonReader {

	public readonly struct BufferState {
		internal readonly JsonReaderState jsonState;
		internal readonly int readStart;
		internal readonly int readEnd;

		internal BufferState(ref BlockBufferJsonReader reader) {
			this.jsonState = reader.saveState;
			int sliceOffset = (int) Unsafe.ByteOffset(ref MemoryMarshal.GetReference(reader.blockBuffer), ref MemoryMarshal.GetReference(reader.readSlice));
			this.readStart = sliceOffset + reader.bytesShifted;
			this.readEnd = sliceOffset + reader.readSlice.Length;
		}
	}

	public class BufferRecorder {
		internal MemoryStream stream = new();
		internal int bufferPosition;

		internal BufferRecorder(int bufferPosition) {
			this.bufferPosition = bufferPosition;
		}
	}

	private enum Stage {
		INIT,
		REINIT,
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
	private HashSet<BufferRecorder>? recorders = null;
	private int bytesShifted = 0;
	private JsonReaderState saveState;

	public BlockBufferJsonReader(Stream stream, Span<byte> blockBuffer, JsonReaderOptions options)
		: this(stream, blockBuffer, new JsonReaderState(options)) {}

	/**
	 * <summary>Resume reading from a given state.</summary>
	 * <remarks><c>stream</c> must return the next bytes to pass into the <c>Utf8JsonReader</c>, including any bytes that were already read out of the original stream.</remarks>
	 */
	public BlockBufferJsonReader(Stream stream, Span<byte> blockBuffer, JsonReaderState state) {
		this.stream = stream;
		this.blockBuffer = blockBuffer;
		this.jsonState = state;
		this.saveState = state;
	}

	/**
	 * <summary>Resume reading from a saved buffer state.</summary>
	 * <remarks><c>stream</c> and <c>blockBuffer</c> must be identical to the values passed to the instance the <c>state</c> comes from.</remarks>
	 */
	public BlockBufferJsonReader(Stream stream, Span<byte> blockBuffer, BufferState state) : this(stream, blockBuffer, state.jsonState) {
		this.readSlice = this.blockBuffer[state.readStart..state.readEnd];
		this._stage = Stage.REINIT;
	}

	/** <summary>Save the reader state in a way that can be resumed later with the same <c>blockBuffer</c> and <c>stream</c></summary> */
	public BufferState SaveBufferState() => new(ref this);

	public bool Read() {
		switch(this._stage) {
			case Stage.CLOSED:
				return false;
			case Stage.READING:
			case Stage.FINAL_READ:
				this.saveState = this.Reader.CurrentState;
				this.bytesShifted = (int) this.Reader.BytesConsumed;
				if(this.Reader.Read()) return true;
				if(this._stage == Stage.FINAL_READ) {
					this._stage = Stage.CLOSED;
					return false;
				}
				goto case Stage.INIT;
			case Stage.INIT:
			case Stage.REINIT:
				this.acquireReader();
				goto case Stage.READING;
		}
		Debug.Assert(false, "This point is unreachable");
		throw new Exception("This point is unreachable");
	}

	public BufferRecorder BeginBufferRecorder() {
		this.recorders ??= new HashSet<BufferRecorder>(1);
		BufferRecorder recorder = new((int) this.Reader.BytesConsumed);
		this.recorders.Add(recorder);
		return recorder;
	}

	public MemoryStream FinishBufferRecorder(BufferRecorder recorder) {
		if(!(this.recorders?.Contains(recorder) ?? false))
			throw new ArgumentException("Passed recorder is not attached to this reader", nameof(recorder));
		if(this.bytesShifted > recorder.bufferPosition)
			recorder.stream.Write(this.readSlice[recorder.bufferPosition..this.bytesShifted]);
		/* need to Exchange late for exception guarantee */
		MemoryStream stream = Misc.Exchange(ref recorder.stream, null!);
		stream.Position = 0;
		this.recorders.Remove(recorder);
		return stream;
	}

	public void CancelBufferRecorder(BufferRecorder recorder) {
		if(!(this.recorders?.Contains(recorder) ?? false))
			throw new ArgumentException("Passed recorder is not attached to this reader", nameof(recorder));
		this.recorders.Remove(recorder);

		/* drop the recorded data */
		recorder.stream.SetLength(0);
		recorder.stream.Capacity = 0;
		recorder.stream = null!;
	}

	private void acquireReader() {
		if(this._stage != Stage.REINIT) {
			int preRead = 0;
			Debug.Assert(this._stage != Stage.FINAL_READ, "Shouldn't be in final read here");
			if(this._stage != Stage.INIT) {
				if(this.Reader.BytesConsumed == 0)
					throw new Exception("JSON value appears to exceed the bounds of the block buffer. Increase the buffer size or decrease your JSON value size.");
				this.jsonState = this.Reader.CurrentState;
				if(this.recorders != null) {
					Span<byte> readSlice = this.readSlice[..(int) this.Reader.BytesConsumed];
					foreach(BufferRecorder recorder in this.recorders) {
						/* NOTE: This may be a zero-length slice, but
						   we ensure that it is not out of range. */
						recorder.stream.Write(readSlice[recorder.bufferPosition..]);
						recorder.bufferPosition = 0;
					}
				}

				Span<byte> remainingSlice = this.readSlice[(int) this.Reader.BytesConsumed..];
				remainingSlice.CopyTo(this.blockBuffer);
				preRead = remainingSlice.Length;
			}

			int read = this.stream.Read(this.blockBuffer[preRead..]);
			this.readSlice = this.blockBuffer[..(preRead + read)];
		}

		this._stage = this.readSlice.Length == 0 ? Stage.FINAL_READ : Stage.READING;
		this.Reader = new Utf8JsonReader(this.readSlice, this.readSlice.Length == 0, this.jsonState);
	}
}
