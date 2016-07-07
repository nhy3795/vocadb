﻿using System.Linq;
using VocaDb.Model.Domain.Globalization;
using VocaDb.Model.Domain.ReleaseEvents;

namespace VocaDb.Model.DataContracts.ReleaseEvents {

	public class ReleaseEventSeriesDetailsContract : ReleaseEventSeriesWithEventsContract {

		public ReleaseEventSeriesDetailsContract() { }

		public ReleaseEventSeriesDetailsContract(ReleaseEventSeries series, ContentLanguagePreference languagePreference)
			: base(series, languagePreference) {

			Aliases = series.Aliases.Select(a => a.Name).ToArray();

		}

		public string[] Aliases { get; set; }

	}
}
