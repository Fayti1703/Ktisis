using System.Collections.Generic;
using System.Text.Json;
using Ktisis.Data.Json;
using Ktisis.Localization.Loading;

namespace Ktisis.Localization.QRules;

public class CompoundStatement : QRuleStatement {
	private readonly QRuleStatement[] statements;

	public CompoundStatement(QRuleStatement[] statements) {
		this.statements = statements;
	}

	public void Run(ref QRuleContext context) {
		foreach(QRuleStatement statement in this.statements) {
			statement.Run(ref context);
		}
	}

	public class Partial : QRuleStatement.Partial {
		private readonly List<QRuleStatement> statements = new();

		public QRuleStatement? Continue(ref DoubleBufferedJsonReader reader, ref LoadContext context, QRuleStatement? parseReturn) {
			if(parseReturn != null) {
				/* TODO?: Might be worth flattening out directly nested `CompoundStatement`s. */
				this.statements.Add(parseReturn);
				context.ExitElement();
			}

			if(reader.Reader.TokenType == JsonTokenType.EndArray)
				return new CompoundStatement(this.statements.ToArray());

			context.EnterItem(this.statements.Count);
			return null;
		}
	}
}
