using System;

namespace Ktisis.Localization.QRules;

public interface QRuleStatement {
	public void Run(ref QRuleContext context);
}
