using System.Text;
using System.Collections.Generic;
using Ktisis.Localization.QRules;

namespace Ktisis.Localization; 

public class LocaleData {
	private readonly Dictionary<string, QRuleStatement> _translationData;
	private readonly HashSet<string> warnedKeys = new();

	public LocaleMetaData MetaData { get; }

	public LocaleData(LocaleMetaData metaData, Dictionary<string, QRuleStatement> translationData) {
		this._translationData = translationData;
		this.MetaData = metaData;
	}

	public string Translate(string key, Dictionary<string, string>? parameters = null) {
		/* TODO: Implementing some form of fallback system might be good here. */
		if(!this._translationData.TryGetValue(key, out QRuleStatement? statement)) {
			if(this.warnedKeys.Add(key))
				Logger.Warning("Unassigned translation key '{0}' for locale '{1}'", key, this.MetaData.TechnicalName);
			return key;
		}

		QRuleContext context = new(key, parameters, this.MetaData);
		statement.Run(ref context);
		return context.ConsumeValue();
	}

	public bool HasTranslationFor(string key) {
		return this._translationData.ContainsKey(key);
	}
}
