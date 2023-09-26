using System.Text.Json;

namespace Ktisis.Data.Json;

public static class JsonReaderExtensions {
	public static void SkipIt(this ref BlockBufferJsonReader reader) {
		if (reader.Reader.TrySkip()) return;
		if(reader.Reader.TokenType == JsonTokenType.PropertyName) {
			if(!reader.Read())
				throw new JsonException("Unexpected end of JSON data.");
		}

		if (reader.Reader.TokenType != JsonTokenType.StartObject && reader.Reader.TokenType != JsonTokenType.StartArray) {
			/* We don't skip primitives here since we also don't skip `EndObject`/`EndArray` --
			 * the expectation is that after this method call, the caller can safely call `reader.Read()` to move to the
			 * next value that they are interested in.
			 */
			return;
		}
		int depth = reader.Reader.CurrentDepth;
		do {
			if(!reader.Read())
				throw new JsonException("Unexpected end of JSON data.");
		} while (reader.Reader.CurrentDepth > depth);
	}

	public static void SkipIt(this ref DoubleBufferedJsonReader reader) {
		if(reader.Reader.TrySkip()) return;
		if(reader.Reader.TokenType == JsonTokenType.PropertyName) {
			if(!reader.Read())
				throw new JsonException("Unexpected end of JSON data.");
		}

		if(reader.Reader.TokenType != JsonTokenType.StartObject && reader.Reader.TokenType != JsonTokenType.StartArray) {
			/* see comment in BBJR overload */
			return;
		}
		int depth = reader.Reader.CurrentDepth;
		do {
			if(!reader.Read())
				throw new JsonException("Unexpected end of JSON data");
		} while(reader.Reader.CurrentDepth > depth);
	}
}
