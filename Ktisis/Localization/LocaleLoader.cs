#nullable  enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Ktisis.Localization.Loading;

namespace Ktisis.Localization;

public static class LocaleLoader {

	/* Being lenient for backwards compatibility, but you will get an annoyed look if you put in C/C++-style comments in your JSON. */
	internal static readonly JsonReaderOptions readerOptions = new() {
		AllowTrailingCommas = true,
		CommentHandling = JsonCommentHandling.Skip
	};

	private static Stream GetLocaleFileStream(string technicalName) {
		Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
			typeof(LocaleLoader),
			"Data." + technicalName + ".json"
		);
		if (stream == null)
			throw new Exception($"Cannot find data file '{technicalName}'");
		return stream;
	}

	public static LocaleMetaData LoadMeta(string technicalName) {
		using Stream stream = GetLocaleFileStream(technicalName);
		return LocaleMetaLoader.LoadMetaDataFromStream(technicalName, stream);
	}

	public static LocaleData LoadData(string technicalName) => _LoadData(technicalName, (LocaleMetaData?) null);
	public static LocaleData LoadData(LocaleMetaData metaData) => _LoadData(metaData.TechnicalName, metaData);

	internal static LocaleData _LoadData(string technicalName, LocaleMetaData? meta) {
		using Stream stream = GetLocaleFileStream(technicalName);
		return LocaleDataLoader.LoadDataFromStream(technicalName, meta, stream);
	}


}
