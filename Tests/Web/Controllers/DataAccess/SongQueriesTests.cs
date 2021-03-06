﻿using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocaDb.Model.Database.Queries;
using VocaDb.Model.DataContracts;
using VocaDb.Model.DataContracts.Artists;
using VocaDb.Model.DataContracts.PVs;
using VocaDb.Model.DataContracts.ReleaseEvents;
using VocaDb.Model.DataContracts.Songs;
using VocaDb.Model.DataContracts.UseCases;
using VocaDb.Model.Domain;
using VocaDb.Model.Domain.Activityfeed;
using VocaDb.Model.Domain.Artists;
using VocaDb.Model.Domain.ExtLinks;
using VocaDb.Model.Domain.Globalization;
using VocaDb.Model.Domain.PVs;
using VocaDb.Model.Domain.ReleaseEvents;
using VocaDb.Model.Domain.Security;
using VocaDb.Model.Domain.Songs;
using VocaDb.Model.Domain.Tags;
using VocaDb.Model.Domain.Users;
using VocaDb.Model.Resources.Messages;
using VocaDb.Model.Service.Helpers;
using VocaDb.Model.Service.VideoServices;
using VocaDb.Tests.TestData;
using VocaDb.Tests.TestSupport;
using VocaDb.Web.Code;
using VocaDb.Model.Service;
using VocaDb.Web.Helpers;

namespace VocaDb.Tests.Web.Controllers.DataAccess {

	/// <summary>
	/// Tests for <see cref="SongQueries"/>.
	/// </summary>
	[TestClass]
	public class SongQueriesTests {

		private EntryAnchorFactory entryLinkFactory;
		private FakeUserMessageMailer mailer;
		private CreateSongContract newSongContract;
		private FakePermissionContext permissionContext;
		private Artist producer;
		private FakePVParser pvParser;
		private FakeSongRepository repository;
		private SongQueries queries;
		private ReleaseEvent releaseEvent;
		private Song song;
		private Tag tag;
		private User user;
		private User user2;
		private User user3;
		private Artist vocalist;
		private Artist vocalist2;

		private SongContract CallCreate() {
			return queries.Create(newSongContract);
		}

		private NewSongCheckResultContract CallFindDuplicates(string[] anyName = null, string[] anyPv = null, int[] artistIds = null, bool getPvInfo = true) {
			
			return queries.FindDuplicates(anyName ?? new string[0], anyPv ?? new string[0], artistIds ?? new int[0], getPvInfo);

		}

		private SongForEditContract EditContract() {
			return new SongForEditContract(song, ContentLanguagePreference.English);
		}

		private void AssertHasArtist(Song song, Artist artist, ArtistRoles? roles = null) {
			Assert.IsTrue(song.Artists.Any(a => a.Artist.Equals(artist)), song + " has " + artist);			
			if (roles.HasValue)
				Assert.IsTrue(song.Artists.Any(a => a.Artist.Equals(artist) && a.Roles == roles), artist + " has roles " + roles);
		}

		private ArtistForSongContract CreateArtistForSongContract(int artistId = 0, string artistName = null, ArtistRoles roles = ArtistRoles.Default) {
			if (artistId != 0)
				return new ArtistForSongContract { Artist = new ArtistContract { Name = artistName, Id = artistId }, Roles = roles };
			else
				return new ArtistForSongContract { Name = artistName, Roles = roles };
		}

		private T Save<T>(T entry) {
			return repository.Save(entry);
		}

		[TestInitialize]
		public void SetUp() {

			producer = CreateEntry.Producer(id: 1, name: "Tripshots");
			vocalist = CreateEntry.Vocalist(id: 39, name: "Hatsune Miku");
			vocalist2 = CreateEntry.Vocalist(id: 40, name: "Kagamine Rin");

			song = CreateEntry.Song(id: 1, name: "Nebula");
			song.LengthSeconds = 39;
			repository = new FakeSongRepository(song);
			Save(song.AddArtist(producer));
			Save(song.AddArtist(vocalist));
			Save(song.CreatePV(new PVContract { Id = 1, Service = PVService.Youtube, PVId = "hoLu7c2XZYU", Name = "Nebula", PVType = PVType.Original }));
			repository.SaveNames(song);

			user = CreateEntry.User(id: 1, name: "Miku");
			user.GroupId = UserGroupId.Trusted;
			user2 = CreateEntry.User(id: 2, name: "Rin", email: "rin@vocadb.net");
			user3 = CreateEntry.User(id: 3, name: "Luka", email: "luka@vocadb.net");
			repository.Add(user, user2);
			repository.Add(producer, vocalist);

			tag = new Tag("vocarock");
			repository.Add(tag, new Tag("vocaloud"));

			releaseEvent = repository.Save(new ReleaseEvent { Name = "Comiket 39" });

			permissionContext = new FakePermissionContext(user);
			entryLinkFactory = new EntryAnchorFactory("http://test.vocadb.net");

			newSongContract = new CreateSongContract {
				SongType = SongType.Original,
				Names = new[] {
					new LocalizedStringContract("Resistance", ContentLanguageSelection.English)
				},
				Artists = new[] {
					new ArtistForSongContract { Artist = new ArtistContract(producer, ContentLanguagePreference.Default) },
					new ArtistForSongContract { Artist = new ArtistContract(vocalist, ContentLanguagePreference.Default) }, 
				},
				PVUrl = "http://test.vocadb.net/"
			};

			pvParser = new FakePVParser();
			pvParser.ResultFunc = (url, getMeta) => 
				VideoUrlParseResult.CreateOk(url, PVService.NicoNicoDouga, "sm393939", 
				getMeta ? VideoTitleParseResult.CreateSuccess("Resistance", "Tripshots", "testimg.jpg", 39) : VideoTitleParseResult.Empty);

			mailer = new FakeUserMessageMailer();

			queries = new SongQueries(repository, permissionContext, entryLinkFactory, pvParser, mailer, 				
				new FakeLanguageDetector(), new FakeUserIconFactory(), new EnumTranslations(), new InMemoryImagePersister(), new FakeObjectCache(), new Model.Utils.Config.VdbConfigManager());

		}

		[TestMethod]
		public void Create() {

			var result = CallCreate();

			Assert.IsNotNull(result, "result");
			Assert.AreEqual("Resistance", result.Name, "Name");

			song = repository.HandleQuery(q => q.Query().FirstOrDefault(a => a.DefaultName == "Resistance"));

			Assert.IsNotNull(song, "Song was saved to repository");
			Assert.AreEqual("Resistance", song.DefaultName, "Name");
			Assert.AreEqual(ContentLanguageSelection.English, song.Names.SortNames.DefaultLanguage, "Default language should be English");
			Assert.AreEqual(2, song.AllArtists.Count, "Artists count");
			VocaDbAssert.ContainsArtists(song.AllArtists, "Tripshots", "Hatsune Miku");
			Assert.AreEqual("Tripshots feat. Hatsune Miku", song.ArtistString.Default, "ArtistString");
			Assert.AreEqual(39, song.LengthSeconds, "Length");	// From PV
			Assert.AreEqual(PVServices.NicoNicoDouga, song.PVServices, "PVServices");

			var archivedVersion = repository.List<ArchivedSongVersion>().FirstOrDefault();

			Assert.IsNotNull(archivedVersion, "Archived version was created");
			Assert.AreEqual(song, archivedVersion.Song, "Archived version song");
			Assert.AreEqual(SongArchiveReason.Created, archivedVersion.Reason, "Archived version reason");

			var activityEntry = repository.List<ActivityEntry>().FirstOrDefault();

			Assert.IsNotNull(activityEntry, "Activity entry was created");
			Assert.AreEqual(song, activityEntry.EntryBase, "Activity entry's entry");
			Assert.AreEqual(EntryEditEvent.Created, activityEntry.EditEvent, "Activity entry event type");

			var pv = repository.List<PVForSong>().FirstOrDefault(p => p.Song.Id == song.Id);

			Assert.IsNotNull(pv, "PV was created");
			Assert.AreEqual(song, pv.Song, "pv.Song");
			Assert.AreEqual("Resistance", pv.Name, "pv.Name");

		}

		[TestMethod]
		public void Create_Notification() {

			repository.Save(user2.AddArtist(producer));

			CallCreate();

			var notification = repository.List<UserMessage>().FirstOrDefault();

			Assert.IsNotNull(notification, "Notification was created");
			Assert.AreEqual(user2, notification.Receiver, "Receiver");

		}

		[TestMethod]
		public void Create_NoNotificationForSelf() {

			repository.Save(user.AddArtist(producer));

			CallCreate();

			Assert.IsFalse(repository.List<UserMessage>().Any(), "No notification was created");

		}

		[TestMethod]
		public void Create_EmailNotification() {
			
			var subscription = repository.Save(user2.AddArtist(producer));
			subscription.EmailNotifications = true;

			CallCreate();

			var notification = repository.List<UserMessage>().First();

			Assert.AreEqual(notification.Subject, mailer.Subject, "Subject");
			Assert.IsNotNull(mailer.Body, "Body");
			Assert.AreEqual(notification.Receiver.Name, mailer.ReceiverName, "ReceiverName");

		}

		[TestMethod]
		public void Create_NotificationForTags() {

			repository.Save(user2.AddTag(tag));
			repository.Save(new TagMapping(tag, "VOCAROCK"));
			pvParser.ResultFunc = (url, meta) => CreateEntry.VideoUrlParseResultWithTitle(tags: new[] { "VOCAROCK" });

			CallCreate();

			var notification = repository.List<UserMessage>().FirstOrDefault();
			Assert.IsNotNull(notification, "Notification was created");
			Assert.AreEqual(user2, notification.User, "Notified user");

		}

		[TestMethod]
		[ExpectedException(typeof(NotAllowedException))]
		public void Create_NoPermission() {

			user.GroupId = UserGroupId.Limited;
			permissionContext.RefreshLoggedUser(repository);

			CallCreate();

		}

		[TestMethod]
		public void Create_Tags() {

			repository.Save(new TagMapping(tag, "VOCAROCK"));
			pvParser.ResultFunc = (url, meta) => CreateEntry.VideoUrlParseResultWithTitle(tags: new[] { "VOCAROCK" });
				
			CallCreate();

			song = repository.HandleQuery(q => q.Query().FirstOrDefault(a => a.DefaultName == "Resistance"));

			Assert.AreEqual(1, song.Tags.Tags.Count(), "Tags.Count");
			Assert.IsTrue(song.Tags.HasTag(tag), "Has vocarock tag");

		}

		[TestMethod]
		public void Create_Tags_IgnoreDuplicates() {

			repository.Save(user2.AddTag(tag));
			repository.Save(new TagMapping(tag, "VOCAROCK"));
			repository.Save(new TagMapping(tag, "rock"));
			pvParser.ResultFunc = (url, meta) => CreateEntry.VideoUrlParseResultWithTitle(tags: new[] { "VOCAROCK", "rock" });

			CallCreate();

			song = repository.HandleQuery(q => q.Query().FirstOrDefault(a => a.DefaultName == "Resistance"));

			Assert.AreEqual(1, song.Tags.Tags.Count(), "Tags.Count");
			Assert.IsTrue(song.Tags.HasTag(tag), "Has vocarock tag");
			Assert.AreEqual(1, song.Tags.GetTagUsage(tag).Count, "Tag vote count");
			var messages = repository.List<UserMessage>().Where(u => u.User.Equals(user2)).ToArray();
			Assert.AreEqual(1, messages.Length, "Notification was sent");
			var message = messages[0];
			Assert.AreEqual(user2, message.Receiver, "Message receiver");
			Assert.AreEqual("New song tagged with vocarock", message.Subject, "Message subject"); // Test subject to make sure it's for one tag

		}

		[TestMethod]
		public void Create_NoPV() {

			newSongContract.PVUrl = null;

			var result = CallCreate();

			Assert.IsNotNull(result, "result");
			Assert.AreEqual(PVServices.Nothing, result.PVServices, "PVServices");

		}

		[TestMethod]
		public void CreateReport() {
			
			queries.CreateReport(song.Id, SongReportType.InvalidInfo, "39.39.39.39", "It's Miku, not Rin", null);

			var report = repository.List<SongReport>().FirstOrDefault();

			Assert.IsNotNull(report, "Report was saved");
			Assert.AreEqual(song.Id, report.EntryBase.Id, "Entry Id");
			Assert.AreEqual(user, report.User, "Report author");
			Assert.AreEqual(SongReportType.InvalidInfo, report.ReportType, "Report type");

		}

		[TestMethod]
		public void CreateReport_Version() {
			
			var version = ArchivedSongVersion.Create(song, new SongDiff(), new AgentLoginData(user), SongArchiveReason.PropertiesUpdated, String.Empty);
			repository.Save(version);
			queries.CreateReport(song.Id, SongReportType.Other, "39.39.39.39", "It's Miku, not Rin", version.Version);

			var report = repository.List<SongReport>().First();

			Assert.AreEqual(version.Version, report.VersionNumber, "Version number");
			Assert.IsNotNull(report.VersionBase, "VersionBase");

			var notification = repository.List<UserMessage>().FirstOrDefault();

			Assert.IsNotNull(notification, "Notification was created");
			Assert.AreEqual(user, notification.Receiver, "Notification receiver");
			Assert.AreEqual(string.Format(EntryReportStrings.EntryVersionReportTitle, song.DefaultName), notification.Subject, "Notification subject");
			Assert.AreEqual(string.Format(EntryReportStrings.EntryVersionReportBody, 
				MarkdownHelper.CreateMarkdownLink(entryLinkFactory.GetFullEntryUrl(song), song.DefaultName), "It's Miku, not Rin"), 
				notification.Message, "Notification message");

		}

		[TestMethod]
		public void CreateReport_Duplicate() {
			
			queries.CreateReport(song.Id, SongReportType.InvalidInfo, "39.39.39.39", "It's Miku, not Rin", null);
			queries.CreateReport(song.Id, SongReportType.Other, "39.39.39.39", "It's Miku, not Rin", null);

			var reports = repository.List<SongReport>();

			Assert.AreEqual(1, reports.Count, "Number of reports");
			Assert.AreEqual(SongReportType.InvalidInfo, reports.First().ReportType, "Report type");

		}

		[TestMethod]
		public void CreateReport_NotLoggedIn() {
			
			permissionContext.LoggedUser = null;
			queries.CreateReport(song.Id, SongReportType.Other, "39.39.39.39", "It's Miku, not Rin", null);

			var report = repository.List<SongReport>().FirstOrDefault();

			Assert.IsNotNull(report, "Report was created");
			Assert.IsNull(report.User, "User is null");
			Assert.AreEqual("39.39.39.39", report.Hostname, "Hostname");

		}

		// Create report, notify the user who created the entry if they're the only editor.
		[TestMethod]
		public void CreateReport_NotifyIfUnambiguous() {

			var editor = user2;
			repository.Save(ArchivedSongVersion.Create(song, new SongDiff(), new AgentLoginData(editor), SongArchiveReason.PropertiesUpdated, String.Empty));
			queries.CreateReport(song.Id, SongReportType.Other, "39.39.39.39", "It's Miku, not Rin", null);

			var report = repository.List<SongReport>().First();
			Assert.AreEqual(song, report.Entry, "Report was created for song");
			Assert.IsNull(report.Version, "Version");

			var notification = repository.List<UserMessage>().FirstOrDefault();
			Assert.IsNotNull(notification, "notification was created");
			Assert.AreEqual(editor, notification.Receiver, "notification was receiver is editor");
			Assert.AreEqual(string.Format(EntryReportStrings.EntryVersionReportTitle, song.DefaultName), notification.Subject, "Notification subject");

		}

		[TestMethod]
		public void CreateReport_DoNotNotifyIfAmbiguous() {
			
			repository.Save(ArchivedSongVersion.Create(song, new SongDiff(), new AgentLoginData(user2), SongArchiveReason.PropertiesUpdated, String.Empty));
			repository.Save(ArchivedSongVersion.Create(song, new SongDiff(), new AgentLoginData(user3), SongArchiveReason.PropertiesUpdated, String.Empty));
			queries.CreateReport(song.Id, SongReportType.Other, "39.39.39.39", "It's Miku, not Rin", null);

			var report = repository.List<SongReport>().First();
			Assert.AreEqual(song, report.Entry, "Report was created for song");
			Assert.IsNull(report.Version, "Version");

			var notification = repository.List<UserMessage>().FirstOrDefault();
			Assert.IsNull(notification, "notification was not created");

		}

		// Create report, with both report type name and notes
		[TestMethod]
		public void CreateReport_Notify_ReportTypeNameAndNotes() {

			var editor = user2;
			repository.Save(ArchivedSongVersion.Create(song, new SongDiff(), new AgentLoginData(editor), SongArchiveReason.PropertiesUpdated, String.Empty));
			queries.CreateReport(song.Id, SongReportType.BrokenPV, "39.39.39.39", "It's Miku, not Rin", null);

			var entryLink = MarkdownHelper.CreateMarkdownLink(entryLinkFactory.GetFullEntryUrl(song), song.DefaultName);

			var notification = repository.List<UserMessage>().FirstOrDefault();
			Assert.IsNotNull(notification, "notification was created");
			Assert.AreEqual(string.Format(EntryReportStrings.EntryVersionReportTitle, song.DefaultName), notification.Subject, "Notification subject");
			Assert.AreEqual(string.Format(EntryReportStrings.EntryVersionReportBody, entryLink, "Broken PV (It's Miku, not Rin)"), notification.Message, "Notification body");

		}


		// Two PVs, no matches, parse song info from the NND PV.
		[TestMethod]
		public void FindDuplicates_NoMatches_ParsePVInfo() {

			// Note: for now only NNDPV will be used for song metadata parsing.
			pvParser.MatchedPVs.Add("http://youtu.be/123456567",
				VideoUrlParseResult.CreateOk("http://youtu.be/123456567", PVService.Youtube, "123456567", 
				VideoTitleParseResult.CreateSuccess("Resistance", "Tripshots", "testimg2.jpg", 33)));

			pvParser.MatchedPVs.Add("http://www.nicovideo.jp/watch/sm3183550",
				VideoUrlParseResult.CreateOk("http://www.nicovideo.jp/watch/sm3183550", PVService.NicoNicoDouga, "sm3183550", 
				VideoTitleParseResult.CreateSuccess("anger", "Tripshots", "testimg.jpg", 39)));

			var result = CallFindDuplicates(new []{ "【初音ミク】anger PV EDIT【VOCALOID3DPV】"}, new []{ "http://youtu.be/123456567", "http://www.nicovideo.jp/watch/sm3183550" });

			Assert.AreEqual("anger", result.Title, "Title"); // Title from PV
			Assert.AreEqual(0, result.Matches.Length, "No matches");

		}

		[TestMethod]
		public void FindDuplicates_MatchName() {

			var result = CallFindDuplicates(new []{ "Nebula"});

			Assert.AreEqual(1, result.Matches.Length, "Matches");
			var match = result.Matches.First();
			Assert.AreEqual(song.Id, match.Entry.Id, "Matched song");
			Assert.AreEqual(SongMatchProperty.Title, match.MatchProperty, "Matched property");

		}

		[TestMethod]
		public void FindDuplicates_MatchNameAndArtist() {

			var producer2 = Save(CreateEntry.Artist(ArtistType.Producer, name: "minato"));
			var song2 = repository.Save(CreateEntry.Song(name: "Nebula"));
			Save(song2.AddArtist(producer2));

			var result = CallFindDuplicates(new []{ "Nebula"}, artistIds: new[] { producer2.Id });

			// 2 songs, the one with both artist and title match appears first
			Assert.AreEqual(2, result.Matches.Length, "Matches");
			var match = result.Matches.First();
			Assert.AreEqual(song2.Id, match.Entry.Id, "Matched song");
			Assert.AreEqual(SongMatchProperty.Title, match.MatchProperty, "Matched property");

		}

		[TestMethod]
		public void FindDuplicates_SkipPVInfo() {

			var result = CallFindDuplicates(new []{ "Anger"}, new []{ "http://www.nicovideo.jp/watch/sm393939" }, getPvInfo: false);

			Assert.IsNull(result.Title, "Title");
			Assert.AreEqual(0, result.Matches.Length, "No matches");

		}

		[TestMethod]
		public void FindDuplicates_MatchPV() {

			pvParser.MatchedPVs.Add("http://youtu.be/hoLu7c2XZYU",
				VideoUrlParseResult.CreateOk("http://youtu.be/hoLu7c2XZYU", PVService.Youtube, "hoLu7c2XZYU", VideoTitleParseResult.Empty));

			var result = CallFindDuplicates(anyPv: new []{ "http://youtu.be/hoLu7c2XZYU"});

			Assert.AreEqual(1, result.Matches.Length, "Matches");
			var match = result.Matches.First();
			Assert.AreEqual(song.Id, match.Entry.Id, "Matched song");
			Assert.AreEqual(SongMatchProperty.PV, match.MatchProperty, "Matched property");

		}

		[TestMethod]
		public void FindDuplicates_CoverInSongTitle_CoverType() {

			pvParser.MatchedPVs.Add("http://www.nicovideo.jp/watch/sm27114783",
				VideoUrlParseResult.CreateOk("http://www.nicovideo.jp/watch/sm27114783", PVService.NicoNicoDouga, "123456567",
				VideoTitleParseResult.CreateSuccess("【GUMI】 光(宇多田ヒカル) 【アレンジカバー】", string.Empty, "testimg2.jpg", 33)));

			var result = CallFindDuplicates(anyPv: new[] { "http://www.nicovideo.jp/watch/sm27114783" });

			Assert.AreEqual(SongType.Cover, result.SongType, "SongType is cover because of the 'cover' in title");

		}

		[TestMethod]
		public void GetRelatedSongs() {

			var matchingArtist = Save(CreateEntry.Song());
			Save(matchingArtist.AddArtist(song.Artists.First().Artist));

			Save(song.AddTag(tag).Result);
			var matchingTag = Save(CreateEntry.Song());
			Save(matchingTag.AddTag(tag));

			Save(user.AddSongToFavorites(song, SongVoteRating.Like));
			var matchingLike = Save(CreateEntry.Song());
			Save(user.AddSongToFavorites(matchingLike, SongVoteRating.Like));

			// Unrelated song
			Save(CreateEntry.Song());

			var result = queries.GetRelatedSongs(song.Id, SongOptionalFields.AdditionalNames);

			Assert.IsNotNull(result, "result");
			Assert.AreEqual(1, result.ArtistMatches.Length, "Number of artist matches");
			Assert.AreEqual(matchingArtist.Id, result.ArtistMatches.First().Id, "Matching artist");
			Assert.AreEqual(1, result.TagMatches.Length, "Number of tag matches");
			Assert.AreEqual(matchingTag.Id, result.TagMatches.First().Id, "Matching tag");
			Assert.AreEqual(1, result.LikeMatches.Length, "Number of like matches");
			Assert.AreEqual(matchingLike.Id, result.LikeMatches.First().Id, "Matching like");

		}

		[TestMethod]
		public void GetSongForEdit() {

			var album = repository.Save(CreateEntry.Album());
			album.OriginalRelease.ReleaseDate = new OptionalDateTime(2007, 8, 31);
			var relEvent = repository.Save(new ReleaseEvent(string.Empty, new DateTime(2007, 8, 31), "Miku's birthday"));
			album.OriginalRelease.ReleaseEvent = relEvent;
			album.AddSong(song, 1, 1);

			var album2 = repository.Save(CreateEntry.Album());
			album2.OriginalRelease.ReleaseDate = new OptionalDateTime(2017, 8, 31);
			album2.AddSong(song, 1, 2);

			var result = queries.GetSongForEdit(song.Id);

			Assert.IsNotNull(result, "result");
			Assert.AreEqual(relEvent.Id, result.AlbumEventId, "AlbumEventId");

		}

		[TestMethod]
		public void Merge_ToEmpty() {

			song.Notes.Original = "Notes";
			var song2 = new Song();
			repository.Save(song2);

			queries.Merge(song.Id, song2.Id);

			Assert.AreEqual("Nebula", song2.Names.AllValues.FirstOrDefault(), "Name");
			Assert.AreEqual(2, song2.AllArtists.Count, "Artists");
			AssertHasArtist(song2, producer);
			AssertHasArtist(song2, vocalist);
			Assert.AreEqual(song.LengthSeconds, song2.LengthSeconds, "LengthSeconds");
			Assert.AreEqual(song.Notes.Original, song2.Notes.Original, "Notes were copied");

			var mergeRecord = repository.List<SongMergeRecord>().FirstOrDefault();
			Assert.IsNotNull(mergeRecord, "Merge record was created");
			Assert.AreEqual(song.Id, mergeRecord.Source, "mergeRecord.Source");
			Assert.AreEqual(song2.Id, mergeRecord.Target.Id, "mergeRecord.Target.Id");

		}

		[TestMethod]
		public void Merge_WithArtists() {

			song.GetArtistLink(producer).Roles = ArtistRoles.Instrumentalist;
			var song2 = CreateEntry.Song();
			repository.Save(song2);
			song2.AddArtist(vocalist);
			song2.AddArtist(vocalist2).Roles = ArtistRoles.Other;

			queries.Merge(song.Id, song2.Id);
			Assert.AreEqual(3, song2.AllArtists.Count, "Artists");
			AssertHasArtist(song2, producer, ArtistRoles.Instrumentalist);
			AssertHasArtist(song2, vocalist2, ArtistRoles.Other);

		}

		[TestMethod]
		[ExpectedException(typeof(NotAllowedException))]
		public void Merge_NoPermissions() {

			user.GroupId = UserGroupId.Regular;
			permissionContext.RefreshLoggedUser(repository);

			var song2 = new Song();
			repository.Save(song2);

			queries.Merge(song.Id, song2.Id);

		}

		[TestMethod]
		public void Update_Names() {
			
			var contract = new SongForEditContract(song, ContentLanguagePreference.English);
			contract.Names.First().Value = "Replaced name";
			contract.UpdateNotes = "Updated song";

			contract = queries.UpdateBasicProperties(contract);

			var songFromRepo = repository.Load(contract.Id);
			Assert.AreEqual("Replaced name", songFromRepo.DefaultName);
			Assert.AreEqual(1, songFromRepo.Version, "Version");
			Assert.AreEqual(2, songFromRepo.AllArtists.Count, "Number of artists");
			Assert.AreEqual(0, songFromRepo.AllAlbums.Count, "No albums");

			var archivedVersion = repository.List<ArchivedSongVersion>().FirstOrDefault();

			Assert.IsNotNull(archivedVersion, "Archived version was created");
			Assert.AreEqual(song, archivedVersion.Song, "Archived version song");
			Assert.AreEqual(SongArchiveReason.PropertiesUpdated, archivedVersion.Reason, "Archived version reason");
			Assert.AreEqual(SongEditableFields.Names, archivedVersion.Diff.ChangedFields.Value, "Changed fields");

			var activityEntry = repository.List<ActivityEntry>().FirstOrDefault();

			Assert.IsNotNull(activityEntry, "Activity entry was created");
			Assert.AreEqual(song, activityEntry.EntryBase, "Activity entry's entry");
			Assert.AreEqual(EntryEditEvent.Updated, activityEntry.EditEvent, "Activity entry event type");

		}

		[TestMethod]
		public void Update_Artists() {
			
			var newSong = CreateEntry.Song(name: "Anger");

			repository.Save(newSong);

			foreach (var name in newSong.Names)
				repository.Save(name);

			var contract = new SongForEditContract(newSong, ContentLanguagePreference.English);
			contract.Artists = new [] {
				CreateArtistForSongContract(artistId: producer.Id), 
				CreateArtistForSongContract(artistId: vocalist.Id),
				CreateArtistForSongContract(artistName: "Goomeh", roles: ArtistRoles.Vocalist),
			};

			contract = queries.UpdateBasicProperties(contract);

			var songFromRepo = repository.Load(contract.Id);

			Assert.AreEqual(3, songFromRepo.AllArtists.Count, "Number of artists");

			AssertHasArtist(songFromRepo, producer);
			AssertHasArtist(songFromRepo, vocalist);
			Assert.AreEqual("Tripshots feat. Hatsune Miku, Goomeh", songFromRepo.ArtistString.Default, "Artist string");

			var archivedVersion = repository.List<ArchivedSongVersion>().FirstOrDefault();

			Assert.IsNotNull(archivedVersion, "Archived version was created");
			Assert.AreEqual(SongEditableFields.Artists, archivedVersion.Diff.ChangedFields.Value, "Changed fields");

		}

		[TestMethod]
		public void Update_Artists_Notify() {
			
			repository.Save(user2.AddArtist(vocalist2));
			repository.Save(vocalist2);

			var contract = new SongForEditContract(song, ContentLanguagePreference.English);
			contract.Artists = contract.Artists.Concat(new [] { CreateArtistForSongContract(vocalist2.Id)}).ToArray();

			queries.UpdateBasicProperties(contract);

			var notification = repository.List<UserMessage>().FirstOrDefault();

			Assert.IsNotNull(notification, "Notification was created");
			Assert.AreEqual(user2, notification.Receiver, "Receiver");

		}

		[TestMethod]
		public void Update_Artists_RemoveDeleted() {
			
			repository.Save(vocalist2);
			repository.Save(song.AddArtist(vocalist2));
			vocalist2.Deleted = true;

			var contract = new SongForEditContract(song, ContentLanguagePreference.English);

			queries.UpdateBasicProperties(contract);

			Assert.IsFalse(song.AllArtists.Any(a => Equals(vocalist2, a.Artist)), "vocalist2 was removed from song");

		}

		[TestMethod]
		public void Update_Lyrics() {

			var contract = EditContract();
			contract.Lyrics = new[] {
				CreateEntry.LyricsForSongContract(cultureCode: OptionalCultureCode.LanguageCode_English, translationType: TranslationType.Original)
			};

			queries.UpdateBasicProperties(contract);

			Assert.AreEqual(1, song.Lyrics.Count, "Lyrics were added");
			var lyrics = song.Lyrics.First();
			Assert.AreEqual("Miku Miku", lyrics.Value, "Lyrics text");

		}

		[TestMethod]
		public void Update_PublishDate_From_PVs() {
			
			var contract = new SongForEditContract(song, ContentLanguagePreference.English);
			contract.PVs = new[] {
				CreateEntry.PVContract(id: 1, pvId: "hoLu7c2XZYU", pvType: PVType.Reprint, publishDate: new DateTime(2015, 3, 9, 10, 0, 0)),
				CreateEntry.PVContract(id: 2, pvId: "mikumikumiku", pvType: PVType.Original, publishDate: new DateTime(2015, 4, 9, 16, 0, 0))
			};

			contract = queries.UpdateBasicProperties(contract);

			var songFromRepo = repository.Load(contract.Id);
			Assert.AreEqual(2, songFromRepo.PVs.PVs.Count, "Number of PVs");
			Assert.AreEqual(new DateTime(2015, 4, 9), songFromRepo.PublishDate.DateTime, "Song publish date was updated");

		}

		[TestMethod]
		public void Update_Weblinks() {

			var contract = new SongForEditContract(song, ContentLanguagePreference.English);
			contract.WebLinks = new[] {
				new WebLinkContract("http://vocadb.net", "VocaDB", WebLinkCategory.Reference)
			};

			contract = queries.UpdateBasicProperties(contract);
			var songFromRepo = repository.Load(contract.Id);
			Assert.AreEqual(1, songFromRepo.WebLinks.Count, "Number of weblinks");
			Assert.AreEqual("http://vocadb.net", songFromRepo.WebLinks[0].Url, "Weblink URL");

		}

		[TestMethod]
		public void Update_Weblinks_SkipWhitespace() {

			var contract = new SongForEditContract(song, ContentLanguagePreference.English);
			contract.WebLinks = new[] {
				new WebLinkContract(" ", "VocaDB", WebLinkCategory.Reference)
			};

			contract = queries.UpdateBasicProperties(contract);
			var songFromRepo = repository.Load(contract.Id);
			Assert.AreEqual(0, songFromRepo.WebLinks.Count, "Number of weblinks");

		}

		[TestMethod]
		public void Update_ReleaseEvent_ExistingEvent() {

			var contract = EditContract();
			contract.ReleaseEvent = new ReleaseEventContract(releaseEvent);

			queries.UpdateBasicProperties(contract);

			Assert.AreSame(releaseEvent, song.ReleaseEvent, "ReleaseEvent");

		}

		[TestMethod]
		public void Update_ReleaseEvent_NewEvent_Standalone() {

			var contract = EditContract();
			contract.ReleaseEvent = new ReleaseEventContract { Name = "Comiket 40" };

			queries.UpdateBasicProperties(contract);

			Assert.IsNotNull(song.ReleaseEvent, "ReleaseEvent");
			Assert.AreSame("Comiket 40", song.ReleaseEvent.Name, "ReleaseEvent.Name");

			Assert.AreEqual(1, song.ReleaseEvent.ArchivedVersionsManager.Versions.Count, "New release event was archived");

		}

		[TestMethod]
		public void Update_ReleaseEvent_NewEvent_SeriesEvent() {

			var series = repository.Save(CreateEntry.EventSeries("Comiket"));
			var contract = EditContract();
			contract.ReleaseEvent = new ReleaseEventContract { Name = "Comiket 40" };

			queries.UpdateBasicProperties(contract);

			Assert.IsNotNull(song.ReleaseEvent, "ReleaseEvent");
			Assert.AreEqual(series, song.ReleaseEvent.Series, "Series");
			Assert.AreEqual(40, song.ReleaseEvent.SeriesNumber, "SeriesNumber");

		}

		[TestMethod]
		public void Update_SendNotificationsForNewPVs() {

			song.PVs.PVs.Clear();
			song.CreateDate = DateTime.Now - TimeSpan.FromDays(30);
			repository.Save(user2.AddArtist(producer));
			var contract = EditContract();
			contract.PVs = new[] { CreateEntry.PVContract(pvType: PVType.Original) };

			queries.UpdateBasicProperties(contract);

			var notifications = repository.List<UserMessage>();
			Assert.AreEqual(1, notifications.Count, "Notification was sent");
			var notification = notifications.First();
			Assert.AreEqual(user2, notification.User, "Notification was sent to user");

		}

		[TestMethod]
		public void Update_DoNotSendNotificationsForReprints() {

			song.PVs.PVs.Clear();
			song.CreateDate = DateTime.Now - TimeSpan.FromDays(30);
			repository.Save(user2.AddArtist(producer));
			var contract = EditContract();
			contract.PVs = new[] { CreateEntry.PVContract(pvType: PVType.Reprint) };

			queries.UpdateBasicProperties(contract);

			Assert.AreEqual(0, repository.Count<UserMessage>(), "No notification was sent");

		}

	}
}
