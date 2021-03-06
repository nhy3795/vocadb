﻿using System;
using VocaDb.Model.DataContracts.Songs;
using VocaDb.Model.Domain;
using VocaDb.Model.Domain.Images;
using VocaDb.Model.Domain.ReleaseEvents;
using VocaDb.Model.Helpers;

namespace VocaDb.Model.DataContracts.ReleaseEvents {

	public class ReleaseEventContract : IReleaseEvent, IEntryImageInformation {

		EntryType IEntryImageInformation.EntryType => EntryType.ReleaseEvent;
		string IEntryImageInformation.Mime => PictureMime;

		public ReleaseEventContract() {
			Description = string.Empty;
		}

		public ReleaseEventContract(ReleaseEvent ev, bool includeSeries = false)
			: this() {

			ParamIs.NotNull(() => ev);

			CustomName = ev.CustomName;
			Date = ev.Date;
			Description = ev.Description;
			Id = ev.Id;
			Name = ev.Name;
			PictureMime = ev.PictureMime;
			SongList = ObjectHelper.Convert(ev.SongList, s => new SongListBaseContract(s));
			UrlSlug = ev.UrlSlug;
			Venue = ev.Venue;
			Version = ev.Version;

			if (includeSeries && ev.Series != null)
				Series = new ReleaseEventSeriesContract(ev.Series);

		}

		public bool CustomName { get; set; }

		public DateTime? Date { get; set; }

		public string Description { get; set; }

		public int Id { get; set; }

		public string Name { get; set; }

		public string PictureMime { get; set; }

		public ReleaseEventSeriesContract Series { get; set; }

		public SongListBaseContract SongList { get; set; }

		public string UrlSlug { get; set; }

		public string Venue { get; set; }

		public int Version { get; set; }

	}

}
