using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Ktisis.Data.Json;
using Ktisis.Localization.QRules;

namespace Ktisis.Localization.Loading;

public static class LocaleDataLoader {
	internal static LocaleData LoadDataFromStream(string technicalName, LocaleMetaData? meta, Stream stream) {
		var reader = new BlockBufferJsonReader(stream, stackalloc byte[4096], LocaleLoader.readerOptions);

		reader.Read();
		if(reader.Reader.TokenType != JsonTokenType.StartObject)
			throw new Exception($"Locale Data file '{technicalName}' does not contain a top-level object.");

		Dictionary<string, QRuleStatement> translationData = new();

		Stack<string> keyStack = new();
		string? currentKey = null;

		int metaCount = 0;

		while(reader.Read()) {
			switch(reader.Reader.TokenType) {
				case JsonTokenType.PropertyName:
					if(keyStack.Count == 0 && reader.Reader.GetString() == "$meta") {
						metaCount++;
						if(meta == null)
							meta = LocaleMetaLoader.ReadMetaObject(technicalName, ref reader);
						else
							reader.SkipIt();
					} else if(reader.Reader.GetString() == "__comment") {
						reader.SkipIt();
					} else {
						keyStack.TryPeek(out string? prevKey);
						if(prevKey != null) {
							currentKey = prevKey + "." + reader.Reader.GetString();
						} else {
							currentKey = reader.Reader.GetString();
						}
					}

					break;
				case JsonTokenType.String:
					translationData.Add(currentKey!, QRuleLoader.LoadStatement(ref reader, currentKey!, technicalName));
					break;
				case JsonTokenType.StartObject:
					keyStack.Push(currentKey!);
					break;
				case JsonTokenType.EndObject:
					if(keyStack.TryPop(out string? _)) /* non-top-level object */
						break;
					goto done;
				case JsonTokenType.StartArray:
					translationData.Add(currentKey!, QRuleLoader.LoadStatement(ref reader, currentKey!, technicalName));
					break;
				case JsonTokenType.True:
				case JsonTokenType.False:
					WarnUnsupported(technicalName, (string) "boolean", currentKey!);
					break;
				case JsonTokenType.Number:
					WarnUnsupported(technicalName, (string) "number", currentKey!);
					break;
				case JsonTokenType.Null:
					WarnUnsupported(technicalName, (string) "null", currentKey!);
					break;
			}
		}

		done:
		switch(metaCount) {
			case 0:
				throw new Exception($"Locale Data file '{technicalName}.json' is is missing the top-level '$meta' object.");
			case > 1:
				Logger.Warning($"Locale Data file '{technicalName}.json' has {{0}} top-level '$meta' objects?!", metaCount);
				break;
		}

		translationData.TrimExcess();

		return new LocaleData(meta!, translationData);
	}

	private static void WarnUnsupported(string technicalName, string elementType, string currentKey) {
		Logger.Warning("Locale Data File '{0}.json' has an unsupported {1} at '%.{2}'.", technicalName, elementType, currentKey);
	}
}
