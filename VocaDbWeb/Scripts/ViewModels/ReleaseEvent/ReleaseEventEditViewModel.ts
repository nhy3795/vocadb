﻿
module vdb.viewModels.releaseEvents {

	import dc = vdb.dataContracts;

	export class ReleaseEventEditViewModel {

		constructor(contract: dc.ReleaseEventContract) {

			this.date = ko.observable(contract.date ? moment(contract.date).toDate() : null);
			this.dateStr = ko.computed(() => (this.date() ? this.date().toISOString() : null));

			this.series = new BasicEntryLinkViewModel(contract.series, null);
			this.isSeriesEvent = ko.observable(!this.series.isEmpty());

			this.isSeriesEventStr = ko.computed<string>({
				read: () => this.isSeriesEvent() ? "true" : "false",
				write: (val) => this.isSeriesEvent(val === "true")
			});

			this.isSeriesEvent.subscribe(val => {
				if (!val)
					this.series.clear();
			});

			this.songList = new BasicEntryLinkViewModel(contract.songList, null);
			this.webLinks = new WebLinksEditViewModel(contract.webLinks);

		}

		public customName = ko.observable(false);

		public date: KnockoutObservable<Date>;

		public dateStr: KnockoutComputed<string>;

		public description = ko.observable<string>();

		public isSeriesEvent: KnockoutObservable<boolean>;

		public isSeriesEventStr: KnockoutComputed<string>;

		public series: BasicEntryLinkViewModel<models.IEntryWithIdAndName>;

		public songList: BasicEntryLinkViewModel<dc.SongListBaseContract>;

		public submit = () => {
			this.submitting(true);
			return true;
		}

		public submitting = ko.observable(false);

        public webLinks: WebLinksEditViewModel;

	}

}