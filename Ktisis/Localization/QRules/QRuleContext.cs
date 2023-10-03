using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Ktisis.Common.Utility;

namespace Ktisis.Localization.QRules;

/** <summary>Runtime context for QRules evaluation.</summary> */
public struct QRuleContext {
	private string? currentValue = null;
	/** <summary>The current localization key being processed.</summary> */
	public readonly string LocaleKey;
	public readonly LocaleMetaData LocaleMeta;
	private Dictionary<string, string>? variables;

	public QRuleContext(string localeKey, Dictionary<string, string>? variables, LocaleMetaData localeMeta) {
		this.LocaleKey = localeKey;
		this.variables = variables;
		this.LocaleMeta = localeMeta;
	}

	public bool GetVariable(string key, [NotNullWhen(true)] out string? value) {
		value = null;
		if(this.variables?.TryGetValue(key, out value) ?? false)
			return true;

		return false;
	}

	public void SetVariable(string key, string value) {
		this.variables ??= new Dictionary<string, string>(1);
		this.variables[key] = value;
	}

	public void ProvideValue(string value) {
		if(this.currentValue != null)
			throw new QRuleRuntimeError("Cannot provide a value here; a previous statement's value has not yet been consumed.");
		this.currentValue = value;
	}

	public bool HasValue => this.currentValue != null;

	public string ConsumeValue() {
		if(this.currentValue == null)
			throw new QRuleRuntimeError("Cannot consume a value here; the previous statement did not provide one.");
		return Misc.Exchange(ref this.currentValue, null)!;
	}

	public string? TryConsumeValue() => Misc.Exchange(ref this.currentValue, null);
}
