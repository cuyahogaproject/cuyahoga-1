using System.Globalization;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Util;
using Cuyahoga.Modules.Articles.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.Articles.Mvc
{
	/// <summary>
	/// An Articles section: the list of articles (also of a category, or the archive), or one article with its
	/// comments and the comment form (Articles.ascx and ArticleDetails.ascx in the WebForms site).
	/// </summary>
	public class ArticlesViewComponent : ModuleViewComponent<ArticleModule>
	{
		protected override Task<IViewComponentResult> RenderAsync()
		{
			IViewComponentResult result = Module.CurrentAction == ArticleModuleAction.Details
				? View("~/Modules/Articles/ArticleDetails.cshtml", CreateDetailsModel())
				: View("~/Modules/Articles/Articles.cshtml", CreateListModel());
			return Task.FromResult(result);
		}

		/// <summary>
		/// The comment form shows for anonymous comments, or for logged-in users when comments are allowed.
		/// </summary>
		internal static bool CanComment(ArticleModule module, User user)
		{
			return module.AllowAnonymousComments || (user != null && module.AllowComments);
		}

		private string SectionUrl => PublicUrls.GetUrlFromSection(Request, Module.Section);

		private ArticleListModel CreateListModel()
		{
			// No RSS icon on an article.
			DisplaySyndicationIcon = Module.AllowSyndication && Module.CurrentArticleId == -1;
			if (Module.CurrentAction == ArticleModuleAction.Category)
			{
				Module.DisplayTitle = String.Format("{0} {1}", GetText("CATEGORY"),
					Module.GetCategoryById(Module.CurrentCategoryId).Title);
			}
			if (Module.IsArchive)
			{
				Module.DisplayTitle += String.Format(" ({0}) ", GetText("ARCHIVE"));
			}

			// A category has articles of all sections of the site; only the ones of this section show here (articles
			// only open in their own section).
			List<Article> articles = Module.GetArticleList().Cast<Article>()
				.Where(article => article.Section.Id == Module.Section.Id)
				.ToList();
			var pager = new PathInfoPager(UriHelper.BuildRelative(Request.PathBase, Request.Path, Request.QueryString),
				SectionUrl, articles.Count, Module.NumberOfArticlesInList, hideWhenOnePage: true);
			DisplayType displayType = GetDisplayType();
			return new ArticleListModel
			{
				Articles = pager.GetPage(articles)
					.Select(article => new ArticleListModel.Item
					{
						Url = GetArticleUrl(article),
						Title = article.Title,
						Summary = article.Summary,
						Content = article.Content,
						Info = CreateArticleInfo(article)
					})
					.ToList(),
				ShowSummary = displayType == DisplayType.HeadersAndSummary,
				ShowContent = displayType == DisplayType.FullContent,
				ReadMoreText = GetText("READMORE"),
				PagerLinks = pager.GetLinks(),
				ArchiveLinkUrl = Module.ShowArchive ? (Module.IsArchive ? SectionUrl : SectionUrl + "/archive") : null,
				ArchiveLinkText = Module.IsArchive ? GetText("CURRENT") : GetText("ARCHIVE")
			};
		}

		private ArticleDetailsModel CreateDetailsModel()
		{
			Article article = Module.GetArticleById(Module.CurrentArticleId);
			User user = PublicPage.User;
			bool canComment = CanComment(Module, user);
			return new ArticleDetailsModel
			{
				Title = article.Title,
				Content = article.Content,
				Info = CreateArticleInfo(article),
				CommentsText = GetText("COMMENTS"),
				ByText = GetText("BY"),
				Comments = Module.AllowComments && article.Comments.Count > 0
					? article.Comments.Cast<Comment>().Select(CreateCommentItem).ToList()
					: null,
				ShowCommentForm = canComment,
				ShowAnonymousFields = canComment && user == null,
				UseAntiforgery = user != null,
				Form = HttpContext.Features.Get<CommentForm>() ?? new CommentForm(),
				PostUrl = GetArticleUrl(article),
				NameText = GetText("NAME"),
				NameRequiredText = GetText("NAMEREQUIRED"),
				WebsiteText = GetText("WEBSITE"),
				CommentText = GetText("COMMENT"),
				CommentRequiredText = GetText("COMMENTREQUIRED"),
				SaveCommentText = GetText("BTNSAVECOMMENT"),
				BackUrl = SectionUrl,
				BackText = GetText("BACK")
			};
		}

		/// <summary>
		/// The line under an article (date, author, category, comments), or null when it shows nothing.
		/// </summary>
		private ArticleInfo CreateArticleInfo(Article article)
		{
			if (!(Module.AllowComments || Module.ShowAuthor || Module.ShowCategory || Module.ShowDateTime))
			{
				return null;
			}
			var info = new ArticleInfo();
			if (Module.ShowDateTime)
			{
				info.DateOnline = TimeZoneUtil.AdjustDateToUserTimeZone(article.DateOnline, PublicPage.User).ToString();
			}
			if (Module.ShowAuthor)
			{
				info.PublishedByText = GetText("PUBLISHED") + " " + GetText("BY");
				info.AuthorName = article.CreatedBy.FullName;
				info.AuthorUrl = Module.GetProfileUrl(article.CreatedBy.Id);
			}
			if (Module.ShowCategory)
			{
				info.CategoryText = GetText("CATEGORY");
				if (article.Category != null)
				{
					info.CategoryTitle = article.Category.Title;
					info.CategoryUrl = SectionUrl + "/category/" + article.Category.Id.ToString(CultureInfo.InvariantCulture);
				}
			}
			if (Module.AllowComments)
			{
				info.CommentsUrl = GetArticleUrl(article) + "#comments";
				info.CommentsText = GetText("COMMENTS") + " " + article.Comments.Count.ToString(CultureInfo.CurrentCulture);
			}
			return info;
		}

		private ArticleDetailsModel.CommentItem CreateCommentItem(Comment comment)
		{
			var item = new ArticleDetailsModel.CommentItem
			{
				TextHtml = comment.CommentTextHtml,
				UpdateTimestamp = TimeZoneUtil.AdjustDateToUserTimeZone(comment.UpdateTimestamp, PublicPage.User).ToString()
			};
			if (comment.User != null)
			{
				// A registered user: a link to the profile when the user has a website (like the WebForms site does).
				item.Name = comment.User.FullName;
				item.IsLink = !String.IsNullOrEmpty(comment.User.Website);
				item.Url = item.IsLink ? Module.GetProfileUrl(comment.User.Id) : null;
			}
			else
			{
				// Older comments may have unsafe website addresses, so check again.
				item.Name = comment.Name;
				item.IsLink = !String.IsNullOrEmpty(comment.Website) && Text.IsSafeUrl(comment.Website);
				item.Url = item.IsLink ? comment.Website : null;
				item.OpensInNewWindow = item.IsLink;
			}
			return item;
		}

		private string GetArticleUrl(Article article)
		{
			return SectionUrl + "/" + article.Id.ToString(CultureInfo.InvariantCulture);
		}

		private DisplayType GetDisplayType()
		{
			return Enum.TryParse(Module.Section.Settings.GetValueOrDefault("DISPLAY_TYPE"), out DisplayType displayType)
				? displayType
				: DisplayType.HeadersOnly;
		}
	}

	public class ArticleListModel
	{
		public IReadOnlyList<Item> Articles { get; init; }
		public bool ShowSummary { get; init; }
		public bool ShowContent { get; init; }
		public string ReadMoreText { get; init; }
		public IReadOnlyList<PagerLink> PagerLinks { get; init; }
		/// <summary>
		/// The link to the archive or back to the current articles, or null.
		/// </summary>
		public string ArchiveLinkUrl { get; init; }
		public string ArchiveLinkText { get; init; }

		public class Item
		{
			public string Url { get; init; }
			/// <summary>
			/// Title and summary are text (encoded; the WebForms site didn't), the content is html.
			/// </summary>
			public string Title { get; init; }
			public string Summary { get; init; }
			public string Content { get; init; }
			public ArticleInfo Info { get; init; }
		}
	}

	public class ArticleDetailsModel
	{
		public string Title { get; init; }
		public string Content { get; init; }
		public ArticleInfo Info { get; init; }
		public string CommentsText { get; init; }
		public string ByText { get; init; }
		/// <summary>
		/// The comments, or null when they don't show.
		/// </summary>
		public IReadOnlyList<CommentItem> Comments { get; init; }
		public bool ShowCommentForm { get; init; }
		/// <summary>
		/// Name and website, for comments without login.
		/// </summary>
		public bool ShowAnonymousFields { get; init; }
		public bool UseAntiforgery { get; init; }
		public CommentForm Form { get; init; }
		public string PostUrl { get; init; }
		public string NameText { get; init; }
		public string NameRequiredText { get; init; }
		public string WebsiteText { get; init; }
		public string CommentText { get; init; }
		public string CommentRequiredText { get; init; }
		public string SaveCommentText { get; init; }
		public string BackUrl { get; init; }
		public string BackText { get; init; }

		public bool NameMissing => Form.NameMissing;
		public bool CommentMissing => Form.CommentMissing;
		public string Error => Form.Error;

		public class CommentItem
		{
			/// <summary>
			/// The comment as html (encoded, with line breaks).
			/// </summary>
			public string TextHtml { get; set; }
			public string Name { get; set; }
			/// <summary>
			/// The name is a link (possibly without url, like the WebForms site renders it).
			/// </summary>
			public bool IsLink { get; set; }
			public string Url { get; set; }
			public bool OpensInNewWindow { get; set; }
			public string UpdateTimestamp { get; set; }
		}
	}

	/// <summary>
	/// The line under an article. The parts that don't show are null.
	/// </summary>
	public class ArticleInfo
	{
		public string DateOnline { get; set; }
		public string PublishedByText { get; set; }
		public string AuthorName { get; set; }
		/// <summary>
		/// The profile of the author, or null (the name is still a link, without url).
		/// </summary>
		public string AuthorUrl { get; set; }
		public string CategoryText { get; set; }
		public string CategoryTitle { get; set; }
		public string CategoryUrl { get; set; }
		public string CommentsUrl { get; set; }
		public string CommentsText { get; set; }
	}
}
