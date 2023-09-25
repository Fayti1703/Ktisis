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
			case JsonTokenType.StartArray:
			case JsonTokenType.StartObject:
				throw new NotImplementedException();
			default:
				throw new Exception("Cannot load a statement here (string, array or object required)");
		}
	}
}
