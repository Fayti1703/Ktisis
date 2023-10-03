using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
			default: {
				LoadContext context = new(currentKey, technicalName, reader.Reader.CurrentDepth);
				throw new QRuleSyntaxError("Cannot load a statement here (string, array or object required)", ref context);
			}
		}
	}

	private static LoadContext.Frame BeginNextStatement(ref BlockBufferJsonReader reader, ref LoadContext context, bool fromPrevFrame) {
		Debug.Assert(reader.Reader.TokenType == JsonTokenType.StartObject);
		/* time to allocate some memory! */
		JsonReaderState state = reader.Reader.CurrentState;
		BlockBufferJsonReader.BufferRecorder recorder = reader.BeginBufferRecorder();
		/* TODO?: Can we skip recording the `type` key, if it's the first key? (Potential difference between saved/unsaved here, not sure if we're okay with that) */
		while(reader.Read()) {
			switch(reader.Reader.TokenType) {
				case JsonTokenType.PropertyName:
					string propertyName = reader.Reader.GetString()!;
					if(propertyName == "type") {
						reader.Read();
						if(reader.Reader.TokenType != JsonTokenType.String)
							throw new QRuleSyntaxError("Statement `type` must be a string!", ref context, ".type");

						string typeName = reader.Reader.GetString()!;
						QRuleStatement.Partial partial = CreatePartialOf(typeName, ref context);
						reader.Read();

						/* TODO: record starting depth */
						return new LoadContext.Frame(
							partial,
							reader.FinishBufferRecorder(recorder),
							state,
							fromPrevFrame
						);
					}
					reader.SkipIt();
					break;
				case JsonTokenType.EndObject:
					throw new QRuleSyntaxError("Statement is missing the `type` key!", ref context);
				default:
					Debug.Assert(false, "Should not be able to reach this point.");
					throw new QRuleInternalError("Should not be able to reach this point.");
			}
		}

		throw new QRuleInternalError("Unexpected end of JSON data.");
	}

	private static Dictionary<string, Type>? qRulePartials = null;

	private static QRuleStatement.Partial CreatePartialOf(string typeName, ref LoadContext context) {
		qRulePartials ??= new Dictionary<string, Type>(typeof(LocaleDataLoader).Assembly.GetTypes()
			.Select(type => (type, metaData: type.GetCustomAttribute<QRuleStatementAttribute>()))
			.Where(x => x.metaData != null)
			.Select(x => {
				Type partialType = x.type.GetNestedType("Partial") ?? throw new Exception($"QRuleStatement Type '{x.type.FullName}' is missing the 'Partial' inner type!");
				if(!partialType.IsAssignableTo(typeof(QRuleStatement.Partial)))
					throw new Exception($"QRuleStatement.Partial Type '{partialType.FullName}' does not implement {typeof(QRuleStatement.Partial).FullName}!");
				return new KeyValuePair<string, Type>(
					x.metaData!.TypeName,
					partialType
				);
			})
		);

		if(!qRulePartials.TryGetValue(typeName, out Type? partialType)) {
			throw new QRuleSyntaxError($"Unknown statement type '{typeName}'", ref context, ".type");
		}

		return (QRuleStatement.Partial) Activator.CreateInstance(partialType)!;
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
				frame.Dispose();
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
					throw new QRuleSyntaxError("Statement expected here (string, array or object required)", ref context);
			}
		}

		return current;
	}
}
