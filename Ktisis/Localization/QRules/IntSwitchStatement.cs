using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Ktisis.Data.Json;
using Ktisis.Localization.Loading;

namespace Ktisis.Localization.QRules;

[QRuleStatement("int-switch")]
public class IntSwitchStatement : QRuleStatement {

	private readonly string variableName;
	private readonly Dictionary<Range, QRuleStatement> cases;

	public struct Range {
		public readonly int? Start;
		public readonly int? End;
		public readonly bool NaN = true;

		public Range() {}
		public Range(int? start, int? end) {
			this.Start = start;
			this.End = end;
			this.NaN = false;
		}
	}

	public IntSwitchStatement(string variableName, Dictionary<Range, QRuleStatement> cases) {
		this.variableName = variableName;
		this.cases = cases;
	}

	public bool ProducesValue => this.cases.Select(x => x.Value.ProducesValue).FirstOrDefault();

	public void Run(ref QRuleContext context) {
		int? value = null;
		if(context.GetVariable(this.variableName, out string? strValue)) {
			if(int.TryParse(strValue, out int tempValue))
				value = tempValue;
		}

		if(value == null) {
			if(!this.cases.TryGetValue(new Range(), out QRuleStatement? statement))
				throw new QRuleRuntimeError("Unhandled 'NaN' case in int-switch");

			statement.Run(ref context);
		} else {
			/* try an exact match first */
			if(this.cases.TryGetValue(new Range(value, value), out QRuleStatement? statement)) {
				statement.Run(ref context);
			} else {
				/* find a matching case and run it */
				statement = this.cases.FirstOrDefault(x => {
					if(x.Key.NaN) return false;

					if(x.Key.Start != null && !(x.Key.Start <= value))
						return false;
					if(x.Key.End != null && !(x.Key.End >= value))
						return false;

					return true;
				}).Value;
				/* `.Value` is null if `FirstOrDefault` returns `default(KVP<Range, QRulesStatement>)` */
				/* ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract */
				if(statement != null)
					statement.Run(ref context);
				else
					throw new QRuleRuntimeError($"Cannot find a case for '{value}' in int-switch");
			}
		}

	}

	public class Partial : QRuleStatement.Partial {
		private string? variableName;
		private Dictionary<Range, QRuleStatement>? cases = null;
		private Range? currentRange = default;
		/* TODO: Should LoadContext track this? We *could*... */
		private string statementPath = null!;
		private bool? producesValue = null;

		public QRuleStatement? Continue(ref DoubleBufferedJsonReader reader, ref LoadContext context, QRuleStatement? parseReturn) {
			if(parseReturn == null)
				this.statementPath = context.JsonPath;
			if(parseReturn != null) {
				if(!this.producesValue.HasValue)
					this.producesValue = parseReturn.ProducesValue;
				else if(this.producesValue.Value != parseReturn.ProducesValue) {
					throw new QRuleSemanticError($"Inconsistent value production in `int-switch` (this statement {(this.producesValue.Value ? "should" : "should not")} produce a value to align with previous cases)", ref context);
				}
				context.ExitElement();
				if(this.currentRange != null) {
					if(!this.cases!.TryAdd(this.currentRange.Value, parseReturn))
						Logger.Warning("Duplicate cases in int-switch statement at '{0}' for locale {1}, ignoring.", this.statementPath, context.TechnicalName);
				}

				if(reader.Reader.TokenType == JsonTokenType.PropertyName) {
					this.currentRange = ParseRange(reader.Reader.GetString()!, ref context);
					reader.Read();
					return null;
				}

				Debug.Assert(reader.Reader.TokenType == JsonTokenType.EndObject);
				reader.Read();
				context.ExitElement();
			}

			do {
				if(reader.Reader.TokenType == JsonTokenType.EndObject) {
					if(this.variableName == null) throw new QRuleSyntaxError("Missing variable name to switch `on`", ref context);
					if(this.cases == null) throw new QRuleSyntaxError("Missing switch `cases`.", ref context);
					return new IntSwitchStatement(this.variableName, this.cases);
				}

				Debug.Assert(reader.Reader.TokenType == JsonTokenType.PropertyName);
				string propertyName = reader.Reader.GetString()!;
				reader.Read();
				switch(propertyName) {
					case "on":
						if(reader.Reader.TokenType != JsonTokenType.String)
							throw new QRuleSyntaxError("`on` must be a variable-name string", ref context, ".on");
						this.variableName = reader.Reader.GetString()!;
						break;
					case "cases": {
						context.EnterProperty("cases");
						if(reader.Reader.TokenType != JsonTokenType.StartObject)
							throw new QRuleSyntaxError("`cases` must be an object", ref context);
						reader.Read();
						this.cases = new Dictionary<Range, QRuleStatement>();
						if(reader.Reader.TokenType == JsonTokenType.EndObject)
							Logger.Warning("Empty cases object at '{0}' in locale '{1}'", context.JsonPath, context.TechnicalName);
						Debug.Assert(reader.Reader.TokenType == JsonTokenType.PropertyName);
						this.currentRange = ParseRange(reader.Reader.GetString()!, ref context);
						reader.Read();
						return null;
					}
					default:
						reader.SkipIt();
						break;
				}
			} while(reader.Read());

			throw new QRuleInternalError("Unexpected end of JSON stream!");
		}

		private static Range? ParseRange(string range, ref LoadContext context) {
			context.EnterProperty(range);
			if(range == "NaN") return new Range();
			if(int.TryParse(
				   range,
				   NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
				   CultureInfo.InvariantCulture,
				   out int result
			   ))
				return new Range(result, result);

			if(!((range[0] == '[' || range[0] == ']') && (range[^1] == ']' || range[^1] == '[')))
				throw new QRuleSyntaxError("Cannot parse this range: Invalid format (must be a single integer or a range indicated with square brackets)", ref context);

			int separator = range.IndexOf(';');
			if(separator == -1)
				throw new QRuleSyntaxError("Cannot parse this range: Missing `;` separator", ref context);

			int? begin = ParseInt(range.AsSpan()[1..separator], ref context, true);
			int? end = ParseInt(range.AsSpan()[(separator + 1)..^1], ref context, false);

			/* For integers we can fake non-inclusive/open ranges by moving the end points.
			   Well, except for infinities, but infinity isn't a valid `int` value anyway. */
			if(range[0] == ']' && begin != null) {
				if(begin == int.MaxValue) {
					/* cannot match */
					Logger.Warning(context.JsonPath, "This range will never be matched and will be ignored.");
					return null;
				}

				begin++;
			}

			if(range[^1] == '[' && end != null) {
				if(end == int.MinValue) {
					/* cannot match */
					Logger.Warning(context.JsonPath, "This range will never be matched and will be ignored.");
					return null;
				}
				end--;
			}

			return new Range(begin, end);
		}

		private static int? ParseInt(ReadOnlySpan<char> part, ref LoadContext context, bool lowerRange) {
			if(int.TryParse(
				   part,
				   NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
				   CultureInfo.InvariantCulture,
				   out int result
			))
				return result;
			if(part.Equals("-Inf", StringComparison.Ordinal)) {
				if(lowerRange) return null;
				throw new QRuleSyntaxError("Cannot parse this range: `-Inf` is not a valid maximum value.", ref context);
			}

			if(part.Equals("Inf", StringComparison.Ordinal)) {
				if(!lowerRange) return null;
				throw new QRuleSyntaxError("Cannot parse this range: `Inf` is not a valid minimum value.", ref context);
			}

			throw new QRuleSyntaxError($"Cannot parse this range: `{part}` is not a valid {(lowerRange ? "minimum" : "maximum")} value.", ref context);
		}
	}

}
