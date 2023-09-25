using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Ktisis.Localization.QRules;

public class StringStatement : QRuleStatement {
	private readonly string value;
	private StringPart[]? parsedData;

	public StringStatement(string value) {
		this.value = value;
	}

	public void Run(ref QRuleContext context) {
		this.parsedData ??= ParseString(this.value, ref context);

		StringBuilder builder = new(this.value.Length);

		foreach(StringPart part in this.parsedData) {
			if(part.isVar) {
				if(context.GetVariable(part.value, out string? value))
					builder.Append(value);
				else {
					builder.Append('%');
					builder.Append(part.value);
					builder.Append('%');
				}
			} else
				builder.Append(part.value);
		}

		context.ProvideValue(builder.ToString());
	}

	private static StringPart[] ParseString(string value, ref QRuleContext context) {
		List<StringPart> parts = new();
		StringBuilder buffer = new(16);
		bool inVariable = false;
		bool bufferIsVariable = false;

		foreach(char c in value) {
			if(!inVariable) {
				if(c == '%') {
					/* don't push the buffer yet, since this may be a '%%' escape sequence
					   and we don't want to unnecessarily split in that case! */
					inVariable = true;
				}  else
					buffer.Append(c);
			} else {
				if(c == '%') {
					if(!bufferIsVariable) { /* '%%' escape sequence */
						buffer.Append('%');
					} else {
						parts.Add(new StringPart { value = buffer.ToString(), isVar = true });
						buffer.Clear();
						bufferIsVariable = false;
						inVariable = false;
					}
				} else {
					if(!bufferIsVariable) {
						if(buffer.Length > 0) {
							/* flush the buffer first */
							parts.Add(new StringPart { value = buffer.ToString(), isVar = false });
							buffer.Clear();
						}
						bufferIsVariable = true;
					}
					buffer.Append(c);
				}
			}
		}

		if(inVariable) {
			Logger.Warning("Unfinished variable substitution in key '{0}' for locale '{1}'!", context.LocaleKey, context.LocaleMeta.TechnicalName);
			/* We pretend that we didn't see this as a variable substitution.
			   Don't rely on this, use '%%' where appropriate! */
			buffer.Insert(0, '%');
		}
		if(buffer.Length > 0)
			parts.Add(new StringPart { value = buffer.ToString(), isVar = false});

		return parts.ToArray();
	}

	private struct StringPart {
		public string value;
		public bool isVar;
	}
}
