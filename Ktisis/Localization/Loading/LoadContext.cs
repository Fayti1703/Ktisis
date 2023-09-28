using System;
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
	internal class Frame {
		internal readonly QRuleStatement.Partial partial;
		internal JsonReaderState initialState;
		private readonly bool hasBuffer = false;
		private FrameBuffer buffer;
		internal readonly bool fromPrevFrame = false;

		internal Frame(QRuleStatement.Partial partial) {
			this.partial = partial;
			this.initialState = default;
			this.hasBuffer = false;
		}

		internal Frame(QRuleStatement.Partial partial, MemoryStream preLoad, JsonReaderState state, bool fromPrevFrame) {
			this.partial = partial;
			this.initialState = state;
			this.buffer = new FrameBuffer(preLoad);
			this.hasBuffer = true;
			this.fromPrevFrame = fromPrevFrame;
		}

		internal BlockBufferJsonReader CreateReader() {
			return this.hasBuffer ? this.buffer.CreateReader(this.initialState) : default;
		}

		internal void SaveReader(ref BlockBufferJsonReader reader) {
			if(this.hasBuffer)
				this.buffer.SaveReader(ref reader);
		}

	}

	/* TODO: Merge with main */
	/* TODO: Replace separately allocated byte array with a pooled one (:hammer-and-sickle:) */
	internal struct FrameBuffer {
		internal bool ran = false;
		internal readonly MemoryStream preLoad;
		internal readonly byte[] blockBuffer = new byte[4096];
		internal BlockBufferJsonReader.BufferState readerState;

		public FrameBuffer(MemoryStream preLoad) {
			this.preLoad = preLoad;
		}

		internal BlockBufferJsonReader CreateReader(JsonReaderState initialState) {
			return this.ran ?
				new BlockBufferJsonReader(this.preLoad, this.blockBuffer, this.readerState, false) :
				new BlockBufferJsonReader(this.preLoad, this.blockBuffer, initialState, false);
		}

		internal void SaveReader(ref BlockBufferJsonReader reader) {
			this.ran = true;
			this.readerState = reader.SaveBufferState();
			#if false
			Console.WriteLine("Saving reader state: " + this.readerState.DumpState(this.blockBuffer));
			#endif
		}
	}

}
