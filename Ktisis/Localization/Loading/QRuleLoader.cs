using System;
using System.Text.Json;
using Ktisis.Data.Json;
using Ktisis.Localization.QRules;

namespace Ktisis.Localization.Loading;

public static class QRuleLoader {
	public static QRuleStatement LoadStatement(ref BlockBufferJsonReader reader, string currentKey, string technicalName) {
		switch(reader.Reader.TokenType) {
			case JsonTokenType.String:
				return new StringStatement(reader.Reader.GetString()!);
			case JsonTokenType.StartArray: {
				LoadContext context = new(currentKey, technicalName, reader.Reader.CurrentDepth);
				reader.Read();
				return LoadStatementInner(ref reader, ref context, new LoadContext.Frame(new CompoundStatement.Partial()));
			}
			case JsonTokenType.StartObject: {
				LoadContext context = new(currentKey, technicalName, reader.Reader.CurrentDepth);
				return LoadStatementInner(ref reader, ref context, BeginNextStatement(ref reader, ref context, false));
			}
			default:
				throw new Exception("Cannot load a statement here (string, array or object required)");
		}
	}

	private static LoadContext.Frame BeginNextStatement(ref BlockBufferJsonReader reader, ref LoadContext context, bool fromPrevFrame) {
		throw new NotImplementedException();
	}

	private static QRuleStatement LoadStatementInner(ref BlockBufferJsonReader reader, ref LoadContext context, LoadContext.Frame initialFrame) {
		QRuleStatement? current = null;
		LoadContext.Frame frame = initialFrame;

		static void PropagateReaderState(ref DoubleBufferedJsonReader doubleBuffer, ref BlockBufferJsonReader reader, ref LoadContext.Frame frame, ref LoadContext context) {
			if(frame.fromPrevFrame)
				context.Stack.Peek().SaveReader(ref doubleBuffer.secondReader);
			else
				reader = doubleBuffer.secondReader;
		}

		static void PushFrame(ref DoubleBufferedJsonReader doubleBuffer, ref BlockBufferJsonReader reader, ref LoadContext.Frame frame, ref LoadContext context) {
			frame.SaveReader(ref doubleBuffer.firstReader);
			PropagateReaderState(ref doubleBuffer, ref reader, ref frame, ref context);

			context.Stack.Push(frame);
		}

		while(true) {
			DoubleBufferedJsonReader doubleBuffer = frame.fromPrevFrame ?
				new(frame.CreateReader(), context.Stack.Peek().CreateReader()) :
				new(frame.CreateReader(), reader);

			re_enter:
			QRuleStatement? result = frame.partial.Continue(ref doubleBuffer, ref context, current);

			if(result != null) {
				/* Done with this frame */
				current = result;
				/* TODO: Validate that we are at an EndObject/EndArray token at the correct depth. */
				bool saveReaderToFrame = frame.fromPrevFrame;
				if(!context.Stack.TryPop(out frame!)) {
					/* we need to specifically NOT `Read()` here, since our parent function calls `Read()` before continuing. */
					reader = doubleBuffer.secondReader;
					break;
				}
				doubleBuffer.Read();
				if(saveReaderToFrame)
					frame.SaveReader(ref doubleBuffer.secondReader);
				else
					reader = doubleBuffer.secondReader;
				continue;
			}

			switch(doubleBuffer.Reader.TokenType) {
				case JsonTokenType.String:
					/* fast track -> StringStatement */
					current = new StringStatement(doubleBuffer.Reader.GetString()!);
					doubleBuffer.Read();
					goto re_enter; /* µopt */
				case JsonTokenType.StartArray:
					/* TODO: record starting depth */
					doubleBuffer.Read();
					PushFrame(ref doubleBuffer, ref reader, ref frame, ref context);
					frame = new LoadContext.Frame(new CompoundStatement.Partial());
					break;
				case JsonTokenType.StartObject: {
					LoadContext.Frame nextFrame = BeginNextStatement(
						ref doubleBuffer.currentReader,
						ref context,
						doubleBuffer.currentIsFirst
					);
					PushFrame(ref doubleBuffer, ref reader, ref frame, ref context);
					frame = nextFrame;
					break;
				}
				default:
					throw new Exception("Cannot load a statement here (string, array or object required)");
			}
		}

		return current;
	}
}
