using System;
using Ktisis.Data.Json;
using Ktisis.Localization.Loading;

namespace Ktisis.Localization.QRules;

public interface QRuleStatement {
	public void Run(ref QRuleContext context);

	public interface Partial {
		/**
		 * <summary>Advance the reader to continue loading the statement, returning it if loading is complete.</summary>
		 * <returns>The loaded statement, if it is complete.</returns>
		 * <param name="reader">The JSON reader.</param>
		 * <param name="context">The loading context</param>
		 * <param name="parseReturn">The result of the last statement parse request.</param>
		 */
		public QRuleStatement? Continue(ref DoubleBufferedJsonReader reader, ref LoadContext context, QRuleStatement? parseReturn);
	}
}
