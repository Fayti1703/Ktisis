using System;
using System.Diagnostics;
using System.Text.Json;
using Ktisis.Data.Json;
using Ktisis.Localization.Loading;

namespace Ktisis.Localization.QRules;

[QRuleStatement("set")]
public class SetVariableStatement : QRuleStatement {
	private readonly string variableName;
	private readonly QRuleStatement valueStatement;

	public SetVariableStatement(string variableName, QRuleStatement valueStatement) {
		this.variableName = variableName;
		this.valueStatement = valueStatement;
	}

	public void Run(ref QRuleContext context) {
		this.valueStatement.Run(ref context);
		context.SetVariable(this.variableName, context.ConsumeValue()!);
	}

	public bool ProducesValue => false;

	public class Partial : QRuleStatement.Partial {
		private string? variableName;
		private QRuleStatement? valueStatement;

		public QRuleStatement? Continue(ref DoubleBufferedJsonReader reader, ref LoadContext context, QRuleStatement? parseReturn) {
			if(parseReturn != null) {
				if(!parseReturn.ProducesValue)
					throw new QRuleSemanticError("Value statement must produce a value.", ref context);
				context.ExitElement();
				this.valueStatement = parseReturn;
			}

			do {
				if(reader.Reader.TokenType == JsonTokenType.EndObject) {
					if(this.variableName == null) throw new QRuleSyntaxError("Missing variable name (`var`) in `set` statement.", ref context);
					if(this.valueStatement == null) throw new QRuleSyntaxError("Missing value statement (`to`) in `set` statement.", ref context);
					return new SetVariableStatement(this.variableName!, this.valueStatement!);
				}

				Debug.Assert(reader.Reader.TokenType == JsonTokenType.PropertyName);
				string propertyName = reader.Reader.GetString()!;
				reader.Read();
				switch(propertyName) {
					case "var":
						if(reader.Reader.TokenType != JsonTokenType.String)
							throw new QRuleSyntaxError("Variable name to set must be a string.", ref context, ".var");
						this.variableName = reader.Reader.GetString();
						break;
					case "to":
						context.EnterProperty("to");
						return null;
					default:
						reader.SkipIt();
						break;
				}
			} while(reader.Read());

			throw new Exception();
		}
	}

}
