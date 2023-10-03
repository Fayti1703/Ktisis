using System;
using System.Runtime.Serialization;
using Ktisis.Localization.QRules;

namespace Ktisis.Localization.Loading;

public class QRuleLoadError : QRuleError {
	public readonly string LocaleTechnicalName;
	public readonly string KeyPath;
	public readonly string RawMessage;

	protected QRuleLoadError(
		string errorType,
		string message,
		ref LoadContext context,
		string extraPath = ""
	) : base(FormatMessage(errorType, message, ref context, extraPath)) {
		this.LocaleTechnicalName = context.TechnicalName;
		this.KeyPath = context.JsonPath + extraPath;
		this.RawMessage = message;
	}

	protected static string FormatMessage(string errorType, string message, ref LoadContext loadContext, string extraPath) {
		return $"QRules {errorType} Error in language '{loadContext.TechnicalName}' at '{loadContext.JsonPath + extraPath}': {message}";
	}
}

public class QRuleSyntaxError : QRuleLoadError {
	public QRuleSyntaxError(
		string message,
		ref LoadContext context,
		string extraPath = ""
	) : base("Syntax", message, ref context, extraPath) {}
}

public class QRuleSemanticError : QRuleLoadError {
	public QRuleSemanticError(
		string message,
		ref LoadContext context,
		string extraPath = ""
	) : base("Semantic", message, ref context, extraPath) { }
}
