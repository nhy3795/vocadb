﻿using System;
using System.ComponentModel.DataAnnotations;
using VocaDb.Model;
using VocaDb.Model.DataContracts;
using VocaDb.Model.DataContracts.ReleaseEvents;
using VocaDb.Model.DataContracts.Songs;
using VocaDb.Model.Domain;
using VocaDb.Model.Domain.Images;
using VocaDb.Web.Code;

namespace VocaDb.Web.Models.Event {

	[PropertyModelBinder]
	public class EventEdit : IEntryImageInformation {

		EntryType IEntryImageInformation.EntryType => EntryType.ReleaseEvent;
		string IEntryImageInformation.Mime => PictureMime;

		public EventEdit() {
			Description = SeriesSuffix = string.Empty;
		}

		public EventEdit(ReleaseEventSeriesContract seriesContract)
			: this() {

			Series = seriesContract;

		}

		public EventEdit(ReleaseEventDetailsContract contract)
			: this() {

			ParamIs.NotNull(() => contract);

			CustomName = contract.CustomName = contract.CustomName;
			Date = contract.Date;
			Description = contract.Description;
			Id = contract.Id;
			Name = OldName = contract.Name;
			Series = contract.Series;
			SeriesNumber = contract.SeriesNumber;
			SeriesSuffix = contract.SeriesSuffix;
			SongList = contract.SongList;
			Venue = contract.Venue;
			WebLinks = contract.WebLinks;

			CopyNonEditableProperties(contract);

		}

		public ReleaseEventSeriesContract[] AllSeries { get; set; }

		public bool CustomName { get; set; }

		[FromJson]
		public DateTime? Date { get; set; }

		[StringLength(400)]
		public string Description { get; set; }

		public int Id { get; set; }

		[StringLength(50)]
		public string Name { get; set; }

		public string OldName { get; set; }

		public string PictureMime { get; set; }

		[FromJson]
		public ReleaseEventSeriesContract Series { get; set; }

		[Display(Name = "Series suffix")]
		public string SeriesSuffix { get; set; }

		[Display(Name = "Series number")]
		public int SeriesNumber { get; set; }

		[FromJson]
		public SongListBaseContract SongList { get; set; }

		public string UrlSlug { get; set; }

		public string Venue { get; set; }

		public int Version { get; set; }

		[FromJson]
		public WebLinkContract[] WebLinks { get; set; }

		public void CopyNonEditableProperties(ReleaseEventDetailsContract contract) {

			ParamIs.NotNull(() => contract);

			OldName = contract.Name;
			PictureMime = contract.PictureMime;
			UrlSlug = contract.UrlSlug;
			Version = contract.Version;

		}

		public ReleaseEventDetailsContract ToContract() {

			return new ReleaseEventDetailsContract {
				CustomName = this.CustomName,
				Date = this.Date,
				Description = this.Description ?? string.Empty,
				Id = this.Id,
				Name = this.Name,
				Series = this.Series, 
				SeriesNumber = this.SeriesNumber,
				SeriesSuffix = this.SeriesSuffix ?? string.Empty,
				SongList = SongList,
				Venue = Venue,
				WebLinks = this.WebLinks
			};

		}

	}

}