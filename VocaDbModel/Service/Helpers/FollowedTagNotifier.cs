﻿using System.Collections.Generic;
using System.Linq;
using VocaDb.Model.Database.Repositories;
using VocaDb.Model.Domain;
using VocaDb.Model.Domain.Tags;
using VocaDb.Model.Domain.Users;
using VocaDb.Model.Helpers;
using VocaDb.Model.Service.Translations;

namespace VocaDb.Model.Service.Helpers {

	public class FollowedTagNotifier {

		private string CreateMessageBody(Tag[] followedArtists, User user, IEntryWithNames entry, IEntryLinkFactory entryLinkFactory, bool markdown, 
			string entryTypeName) {

			var entryName = entry.Names.SortNames[user.DefaultLanguageSelection];
			var url = entryLinkFactory.GetFullEntryUrl(entry);

			string entryLink;
			if (markdown) {
				entryLink = MarkdownHelper.CreateMarkdownLink(url, entryName);
			} else {
				entryLink = string.Format("{0} ( {1} )", entryName, url);
			}

			string msg;

			if (followedArtists.Length == 1) {

				var artistName = followedArtists.First().TranslatedName[user.DefaultLanguageSelection];
				msg = string.Format("A new {0}, '{1}', tagged with {2} was just added.",
					entryTypeName, entryLink, artistName);

			} else {

				msg = string.Format("A new {0}, '{1}', tagged with multiple tags you're following was just added.",
					entryTypeName, entryLink);

			}

			msg += "\nYou're receiving this notification because you're following the tag(s).";
			return msg;

		}

		/// <summary>
		/// Sends notifications
		/// </summary>
		/// <param name="ctx">Repository context. Cannot be null.</param>
		/// <param name="entry">Entry that was created. Cannot be null.</param>
		/// <param name="artists">List of artists for the entry. Cannot be null.</param>
		/// <param name="creator">User who created the entry. The creator will be excluded from all notifications. Cannot be null.</param>
		/// <param name="entryLinkFactory">Factory for creating links to entries. Cannot be null.</param>
		public void SendNotifications(IDatabaseContext ctx, IEntryWithNames entry,
			IEnumerable<Tag> tags, int[] ignoreUsers, IEntryLinkFactory entryLinkFactory,
			IEnumTranslations enumTranslations) {

			ParamIs.NotNull(() => ctx);
			ParamIs.NotNull(() => entry);
			ParamIs.NotNull(() => tags);
			ParamIs.NotNull(() => ignoreUsers);
			ParamIs.NotNull(() => entryLinkFactory);

			var coll = tags.Distinct().ToArray();
			var tagIds = coll.Select(a => a.Id).ToArray();

			// Get users with less than maximum number of unread messages, following any of the tags
			var usersWithTags = ctx.OfType<TagForUser>()
				.Query()
				.Where(afu =>
					tagIds.Contains(afu.Tag.Id)
					&& afu.User.Active
					&& !ignoreUsers.Contains(afu.User.Id)
					&& afu.User.ReceivedMessages.Count(m => m.Inbox == UserInboxType.Notifications && !m.Read) < afu.User.Options.UnreadNotificationsToKeep)
				.Select(afu => new {
					UserId = afu.User.Id,
					TagId = afu.Tag.Id
				})
				.ToArray()
				.GroupBy(afu => afu.UserId)
				.ToDictionary(afu => afu.Key, afu => afu.Select(a => a.TagId));

			var userIds = usersWithTags.Keys;

			if (!userIds.Any())
				return;

			var entryTypeNames = enumTranslations.Translations<EntryType>();
			var users = ctx.OfType<User>().Query().Where(u => userIds.Contains(u.Id)).ToArray();

			foreach (var user in users) {

				var tagIdsForUser = new HashSet<int>(usersWithTags[user.Id]);
				var followedTags = coll.Where(a => tagIdsForUser.Contains(a.Id)).ToArray();

				if (followedTags.Length == 0)
					continue;

				string title;

				var entryTypeName = entryTypeNames.GetName(entry.EntryType, CultureHelper.GetCultureOrDefault(user.LanguageOrLastLoginCulture)).ToLowerInvariant();
				var msg = CreateMessageBody(followedTags, user, entry, entryLinkFactory, true, entryTypeName);

				if (followedTags.Length == 1) {

					var artistName = followedTags.First().TranslatedName[user.DefaultLanguageSelection];
					title = string.Format("New {0} tagged with {1}", entryTypeName, artistName);

				} else {

					title = string.Format("New {0}", entryTypeName);

				}

				var notification = user.CreateNotification(title, msg);
				ctx.Save(notification);

			}

		}

	}

}
