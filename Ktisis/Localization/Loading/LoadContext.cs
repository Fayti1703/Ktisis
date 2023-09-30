using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Ktisis.Data.Json;
using Ktisis.Localization.QRules;

namespace Ktisis.Localization.Loading;

/** <summary>Load-time context for QRule loading.</summary> */
public struct LoadContext {
	public readonly string TranslationKey;
	public readonly string TechnicalName;
	public string JsonPath { get; private set; }

	internal readonly int MinDepth;
	internal Stack<Frame> Stack = new(1);
	internal Stack<string> pathStack = new(1);

	public LoadContext(string translationKey, string technicalName, int minDepth) {
		this.TranslationKey = translationKey;
		this.TechnicalName = technicalName;
		this.MinDepth = minDepth;
		this.JsonPath = "%." + this.TranslationKey;
	}

	public void EnterProperty(string propertyName) {
		this.pathStack.Push(this.JsonPath);
		this.JsonPath += "." + propertyName;
	}

	public void EnterItem(int index) {
		this.pathStack.Push(this.JsonPath);
		this.JsonPath += "[" + index + "]";
	}

	public void ExitElement() {
		this.JsonPath = this.pathStack.Pop();
	}


	/** <summary>A single frame on the QRule load stack.</summary> */
	internal struct Frame : IDisposable {
		internal readonly QRuleStatement.Partial partial;
		internal BlockBufferJsonReader.BufferState bufferState;
		internal MemoryStream? preLoad = null;
		internal byte[]? blockBuffer = null;
		internal readonly bool fromPrevFrame = false;

		private const int MAXIMUM_BUFFER_SIZE = 4096;

		/* TODO?: Use private array pool? */
		private static ArrayPool<byte> bufferPool => ArrayPool<byte>.Shared;

		internal Frame(QRuleStatement.Partial partial) {
			this.partial = partial;
			this.bufferState = default;
		}

		internal Frame(QRuleStatement.Partial partial, MemoryStream preLoad, JsonReaderState state, bool fromPrevFrame) {
			this.partial = partial;
			this.bufferState = new BlockBufferJsonReader.BufferState(state);
			this.preLoad = preLoad;
			this.fromPrevFrame = fromPrevFrame;
		}

		internal BlockBufferJsonReader CreateReader() {
			if(this.preLoad == null)
				return default; /* no preload data */
			this.blockBuffer ??= bufferPool.Rent(Math.Min((int) this.preLoad.Length, MAXIMUM_BUFFER_SIZE));
			return new BlockBufferJsonReader(this.preLoad!, this.blockBuffer, this.bufferState, false);
		}

		internal void SaveReader(ref BlockBufferJsonReader reader) {
			if(this.blockBuffer == null) return;
			if(reader.Drained)
				this.ReleaseBuffer();
			else
				this.bufferState = reader.SaveBufferState();
		}

		public void Dispose() {
			this.ReleaseBuffer();
		}

		private void ReleaseBuffer() {
			if(this.blockBuffer != null) {
				bufferPool.Return(this.blockBuffer);
				this.blockBuffer = null;
			}

			this.preLoad = null;
		}
	}
}
