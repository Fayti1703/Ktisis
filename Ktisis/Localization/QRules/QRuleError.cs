using System;

namespace Ktisis.Localization.QRules;

abstract public class QRuleError : Exception {
	protected QRuleError(string message) : base(message) {}
}


public class QRuleRuntimeError : QRuleError {
	public QRuleRuntimeError(string message) : base(message) {}
}

public class QRuleInternalError : QRuleError {
	public QRuleInternalError(string message) : base(message) {}
}
