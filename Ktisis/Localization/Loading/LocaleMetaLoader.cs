using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Ktisis.Data.Json;

namespace Ktisis.Localization.Loading;

public static class LocaleMetaLoader {
	internal static LocaleMetaData LoadMetaDataFromStream(string technicalName, Stream stream) {
		var reader = new BlockBufferJsonReader(stream, stackalloc byte[4096], LocaleLoader.readerOptions);

		reader.Read();
		if(reader.Reader.TokenType != JsonTokenType.StartObject)
			throw new Exception($"Locale Data file '{technicalName}.json' does not contain a top-level object.");

		while(reader.Read()) {
			switch(reader.Reader.TokenType) {
				case JsonTokenType.PropertyName:
					if(reader.Reader.GetString() == "$meta") {
						return ReadMetaObject(technicalName, ref reader);
					}

					reader.SkipIt();
					break;
				case JsonTokenType.EndObject:
					throw new Exception($"Locale Data file '{technicalName}' is is missing the top-level '$meta' object.");
				default:
					Debug.Assert(false, "Should not reach this point.");
					throw new Exception("Should not reach this point.");
			}
		}

		throw new Exception($"Locale Data file '{technicalName}.json' is missing its meta data (top-level '$meta' key not found)");
	}

	public static LocaleMetaData ReadMetaObject(string technicalName, ref BlockBufferJsonReader reader) {
		reader.Read();
		if(reader.Reader.TokenType != JsonTokenType.StartObject)
			throw new Exception($"Locale Data file '{technicalName}.json' has a non-object at the top-level '$meta' key.");

		string? displayName = null;
		string? selfName = null;
		string?[]? maintainers = null;

		while(true) {
			reader.Reader.Read();
			switch(reader.Reader.TokenType) {
				case JsonTokenType.PropertyName:
					string propertyName = reader.Reader.GetString()!;
					reader.Read();
					switch(propertyName) {
						case "__comment":
							break;
						case "displayName":
							if(reader.Reader.TokenType != JsonTokenType.String)
								throw new Exception($"Locale data file '{technicalName}.json' has an invalid '%.$meta.displayName' value (not a string).");
							displayName = reader.Reader.GetString();
							break;
						case "selfName":
							if(reader.Reader.TokenType != JsonTokenType.String)
								throw new Exception($"Locale data file '{technicalName}.json' has an invalid '%.$meta.selfName' value (not a string).");
							selfName = reader.Reader.GetString();
							break;
						/* FIXME: "contributors", not "maintainers". */
						case "maintainers":
							if(reader.Reader.TokenType != JsonTokenType.StartArray)
								throw new Exception($"Locale data file '{technicalName}.json' has an invalid '%.$meta.maintainers' value (not an array).");
							List<string?> collectMaintainers = new List<string?>();
							int i = 0;
							while(reader.Read()) {
								switch(reader.Reader.TokenType) {
									case JsonTokenType.Null:
										collectMaintainers.Add(null);
										break;
									case JsonTokenType.String:
										collectMaintainers.Add(reader.Reader.GetString());
										break;
									case JsonTokenType.EndArray:
										goto endArray;
									default:
										throw new Exception(
											$"Locale data file '{technicalName}' has an invalid value at '%.$meta.maintainers.{i}' (not a string or null).");
								}

								i++;
							}

							endArray:
							maintainers = collectMaintainers.ToArray();
							break;
						default:
							Logger.Warning($"Locale data file '{technicalName}.json' has unknown meta key at '%.$meta.{reader.Reader.GetString()}'");
							reader.SkipIt();
							break;
					}

					break;
				case JsonTokenType.EndObject:
					goto done;
			}
		}

		done:
		if(displayName == null)
			throw new Exception($"Locale data file '{technicalName}.json' is missing the '%.$meta.displayName' value.");
		if(selfName == null)
			throw new Exception($"Locale data file '{technicalName}.json' is missing the '%.$meta.selfName' value.");
		maintainers ??= new string?[] { null };

		return new LocaleMetaData(technicalName, displayName, selfName, maintainers);
	}
}
