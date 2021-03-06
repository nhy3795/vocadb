﻿using System;
using System.Linq;
using System.Xml.Linq;
using NLog;
using VocaDb.Model.Domain.Globalization;
using NHibernate;
using NHibernate.Linq;
using VocaDb.Model.DataContracts.Artists;
using VocaDb.Model.DataContracts.UseCases;
using VocaDb.Model.Domain.Artists;
using VocaDb.Model.Domain.Security;
using VocaDb.Model.Service.Helpers;
using System.Drawing;
using VocaDb.Model.Helpers;
using VocaDb.Model.Database.Repositories.NHibernate;
using VocaDb.Model.Service.QueryableExtenders;
using VocaDb.Model.Service.Search.Artists;

namespace VocaDb.Model.Service {

	public class ArtistService : ServiceBase {

		private readonly IEntryUrlParser entryUrlParser;

// ReSharper disable UnusedMember.Local
		private static readonly Logger log = LogManager.GetCurrentClassLogger();
// ReSharper restore UnusedMember.Local

		public PartialFindResult<Artist> Find(ISession session, ArtistQueryParams queryParams) {

			var context = new NHibernateDatabaseContext<Artist>(session, PermissionContext);
			return new ArtistSearch(queryParams.LanguagePreference, context, entryUrlParser).Find(queryParams);

		}

		public ArtistService(ISessionFactory sessionFactory, IUserPermissionContext permissionContext, IEntryLinkFactory entryLinkFactory, IEntryUrlParser entryUrlParser)
			: base(sessionFactory, permissionContext, entryLinkFactory) {
			
			this.entryUrlParser = entryUrlParser;

		}

		public void Archive(ISession session, Artist artist, ArtistDiff diff, ArtistArchiveReason reason, string notes = "") {

			SysLog("Archiving " + artist);

			var agentLoginData = SessionHelper.CreateAgentLoginData(session, PermissionContext);
			var archived = ArchivedArtistVersion.Create(artist, diff, agentLoginData, reason, notes);
			session.Save(archived);

		}

		public void Archive(ISession session, Artist artist, ArtistArchiveReason reason, string notes = "") {

			Archive(session, artist, new ArtistDiff(), reason, notes);

		}

		public void Delete(int id, string notes) {

			UpdateEntity<Artist>(id, (session, a) => {

				EntryPermissionManager.VerifyDelete(PermissionContext, a);

				AuditLog(string.Format("deleting artist {0}{1}", EntryLinkFactory.CreateEntryLink(a), !string.IsNullOrEmpty(notes) ? " " + notes : string.Empty), session);

				NHibernateUtil.Initialize(a.Picture);
				a.Delete();
			          
				Archive(session, a, new ArtistDiff(false), ArtistArchiveReason.Deleted, notes);
               
			}, PermissionToken.Nothing, skipLog: true);

		}

		public PartialFindResult<ArtistContract> FindArtists(ArtistQueryParams queryParams) {

			return FindArtists(a => new ArtistContract(a, PermissionContext.LanguagePreference), queryParams);

		}

		public PartialFindResult<T> FindArtists<T>(Func<Artist, T> fac, ArtistQueryParams queryParams) {

			return HandleQuery(session => {

				var result = Find(session, queryParams);

				return new PartialFindResult<T>(result.Items.Select(fac).ToArray(),
					result.TotalCount, result.Term);

			});

		}

		public string[] FindNames(ArtistSearchTextQuery textQuery, int maxResults) {

			if (textQuery.IsEmpty)
				return new string[] {};

			return HandleQuery(session => {

				var names = session.Query<ArtistName>()
					.Where(a => !a.Artist.Deleted)
					.WhereArtistNameIs(textQuery)
					.Select(n => n.Value)
					.OrderBy(n => n)
					.Distinct()
					.Take(maxResults)
					.ToArray();

				return NameHelper.MoveExactNamesToTop(names, textQuery.Query);

			});

		}

		public EntryForPictureDisplayContract GetArchivedArtistPicture(int archivedVersionId) {

			return HandleQuery(session =>
				EntryForPictureDisplayContract.Create(
				session.Load<ArchivedArtistVersion>(archivedVersionId), LanguagePreference));

		}

		public ArtistContract GetArtist(int id) {

			return HandleQuery(session => new ArtistContract(session.Load<Artist>(id), LanguagePreference));

		}

		public ArtistForEditContract GetArtistForEdit(int id) {

			return
				HandleQuery(session =>
					new ArtistForEditContract(session.Load<Artist>(id), PermissionContext.LanguagePreference));

		}

		public ArtistContract GetArtistWithAdditionalNames(int id) {

			return HandleQuery(session => new ArtistContract(session.Load<Artist>(id), PermissionContext.LanguagePreference));

		}

		/// <summary>
		/// Gets the picture for a <see cref="Artist"/>.
		/// </summary>
		/// <param name="id">Artist Id.</param>
		/// <param name="requestedSize">Requested size. If Empty, original size will be returned.</param>
		/// <returns>Data contract for the picture. Can be null if there is no picture.</returns>
		public EntryForPictureDisplayContract GetArtistPicture(int id, Size requestedSize) {

			return HandleQuery(session => 
				EntryForPictureDisplayContract.Create(session.Load<Artist>(id), PermissionContext.LanguagePreference, requestedSize));

		}

		public ArtistWithArchivedVersionsContract GetArtistWithArchivedVersions(int artistId) {

			return HandleQuery(session => new ArtistWithArchivedVersionsContract(
				session.Load<Artist>(artistId), PermissionContext.LanguagePreference));

		}

		public ArtistForApiContract[] GetArtistsWithYoutubeChannels(ContentLanguagePreference languagePreference) {

			return HandleQuery(session => {

				var contracts = session.Query<ArtistWebLink>()
					.Where(l => !l.Entry.Deleted 
						&& (l.Entry.ArtistType == ArtistType.Producer || l.Entry.ArtistType == ArtistType.Circle || l.Entry.ArtistType == ArtistType.Animator) 
						&& (l.Url.Contains("youtube.com/user/") || l.Url.Contains("youtube.com/channel/")))
					.Select(l => l.Entry)
					.Distinct()
					.ToArray()
					.Select(a => new ArtistForApiContract(a, languagePreference, null, false, ArtistOptionalFields.WebLinks))
					.ToArray();

				return contracts;

			});

		}

		public EntryWithTagUsagesContract GetEntryWithTagUsages(int artistId) {

			return HandleQuery(session => {

				var artist = session.Load<Artist>(artistId);
				return new EntryWithTagUsagesContract(artist, artist.Tags.ActiveUsages, LanguagePreference, PermissionContext);

			});

		}

		public ArchivedArtistVersionDetailsContract GetVersionDetails(int id, int comparedVersionId) {

			return HandleQuery(session =>
				new ArchivedArtistVersionDetailsContract(session.Load<ArchivedArtistVersion>(id),
					comparedVersionId != 0 ? session.Load<ArchivedArtistVersion>(comparedVersionId) : null, PermissionContext.LanguagePreference));

		}

		public XDocument GetVersionXml(int id) {
			return HandleQuery(session => session.Load<ArchivedArtistVersion>(id).Data);
		}

		public void Merge(int sourceId, int targetId) {

			PermissionContext.VerifyPermission(PermissionToken.MergeEntries);

			if (sourceId == targetId)
				throw new ArgumentException("Source and target artists can't be the same", "targetId");

			HandleTransaction(session => {

				var source = session.Load<Artist>(sourceId);
				var target = session.Load<Artist>(targetId);

				AuditLog(string.Format("Merging {0} to {1}", 
					EntryLinkFactory.CreateEntryLink(source), EntryLinkFactory.CreateEntryLink(target)), session);

				NHibernateUtil.Initialize(source.Picture);
				NHibernateUtil.Initialize(target.Picture);

				foreach (var n in source.Names.Names.Where(n => !target.HasName(n))) {
					var name = target.CreateName(n.Value, n.Language);
					session.Save(name);
				}

				foreach (var w in source.WebLinks.Where(w => !target.HasWebLink(w.Url))) {
					var link = target.CreateWebLink(w.Description, w.Url, w.Category);
					session.Save(link);
				}

				var groups = source.Groups.Where(g => !target.HasGroup(g.Parent)).ToArray();
				foreach (var g in groups) {
					g.MoveToMember(target);
					session.Update(g);
				}

				var members = source.Members.Where(m => !m.Member.HasGroup(target)).ToArray();
				foreach (var m in members) {
					m.MoveToGroup(target);
					session.Update(m);
				}

				var albums = source.Albums.Where(a => !target.HasAlbum(a.Album)).ToArray();
				foreach (var a in albums) {
					a.Move(target);
					session.Update(a);
				}

				var songs = source.Songs.Where(s => !target.HasSong(s.Song)).ToArray();
				foreach (var s in songs) {
					s.Move(target);
					session.Update(s);
				}

				var ownerUsers = source.OwnerUsers.Where(s => !target.HasOwnerUser(s.User)).ToArray();
				foreach (var u in ownerUsers) {
					u.Move(target);
					session.Update(u);
				}

				var pictures = source.Pictures.ToArray();
				foreach (var p in pictures) {
					p.Move(target);
					session.Update(p);
				}

				var users = source.Users.ToArray();
				foreach (var u in users) {
					u.Move(target);
					session.Update(u);
				}

				target.Description.CopyIfEmpty(source.Description);

				// Create merge record
				var mergeEntry = new ArtistMergeRecord(source, target);
				session.Save(mergeEntry);

				source.Deleted = true;

				Archive(session, source, ArtistArchiveReason.Deleted, string.Format("Merged to {0}", target));
				Archive(session, target, ArtistArchiveReason.Merged, string.Format("Merged from '{0}'", source));

				NHibernateUtil.Initialize(source.Picture);
				NHibernateUtil.Initialize(target.Picture);

				session.Update(source);
				session.Update(target);

			});

		}

		public void Restore(int artistId) {

			PermissionContext.VerifyPermission(PermissionToken.DeleteEntries);

			HandleTransaction(session => {

				var artist = session.Load<Artist>(artistId);

				NHibernateUtil.Initialize(artist.Picture);
				artist.Deleted = false;

				session.Update(artist);

				Archive(session, artist, new ArtistDiff(false), ArtistArchiveReason.Restored);

				AuditLog("restored " + EntryLinkFactory.CreateEntryLink(artist), session);

			});

		}

	}

	public enum ArtistSortRule {

		/// <summary>
		/// Not sorted (random order)
		/// </summary>
		None,

		/// <summary>
		/// Sort by name (ascending)
		/// </summary>
		Name,

		/// <summary>
		/// Sort by addition date (descending)
		/// </summary>
		AdditionDate,

		/// <summary>
		/// Sort by addition date (ascending)
		/// </summary>
		AdditionDateAsc,

		/// <summary>
		/// Release date (only for voicebanks)
		/// </summary>
		ReleaseDate,

		SongCount,

		SongRating,

		FollowerCount

	}

}
