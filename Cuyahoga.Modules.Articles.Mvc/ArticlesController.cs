using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Util;
using Cuyahoga.Modules.Articles.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NHibernate;

namespace Cuyahoga.Modules.Articles.Mvc
{
	/// <summary>
	/// Requests for an Articles section ({id}/section.aspx/...): the pages of the module (the list, a category, the
	/// archive, an article) and posting a comment on an article.
	/// </summary>
	public class ArticlesController : ModuleController<ArticleModule>
	{
		private readonly IAntiforgery _antiforgery;

		public ArticlesController(IAntiforgery antiforgery)
		{
			this._antiforgery = antiforgery;
		}

		[HttpGet]
		[HttpHead]
		public IActionResult Index()
		{
			// An unknown article or category is a 404 (the WebForms site showed its error page).
			switch (Module.CurrentAction)
			{
				case ArticleModuleAction.Details when GetArticle() == null:
				case ArticleModuleAction.Category when !CategoryExists():
					return NotFound();
				default:
					return PublicPageView();
			}
		}

		/// <summary>
		/// Saves a comment on the article of the url and goes back to the article, or shows the article with what is
		/// missing.
		/// </summary>
		[HttpPost]
		[ActionName("Index")]
		public async Task<IActionResult> SaveComment([FromForm] CommentForm form)
		{
			if (Module.CurrentAction != ArticleModuleAction.Details)
			{
				return StatusCode(StatusCodes.Status405MethodNotAllowed);
			}
			Article article = GetArticle();
			if (article == null)
			{
				return NotFound();
			}
			User user = PublicPage.User;
			if (!ArticlesViewComponent.CanComment(Module, user))
			{
				return StatusCode(StatusCodes.Status403Forbidden);
			}
			// Only logged-in users get a token: the article (with the form) is cached for anonymous users when the
			// section has a cache duration, and a comment form without login has nothing to forge.
			if (user != null && !await this._antiforgery.IsRequestValidAsync(HttpContext))
			{
				return BadRequest();
			}

			form.NameMissing = user == null && String.IsNullOrWhiteSpace(form.Name);
			form.CommentMissing = String.IsNullOrWhiteSpace(form.Comment);
			if (!form.NameMissing && !form.CommentMissing)
			{
				var comment = new Comment
				{
					Article = article,
					CommentText = Comment.CreateCommentText(form.Comment),
					UserIp = HttpContext.Connection.RemoteIpAddress?.ToString()
				};
				if (user != null)
				{
					comment.User = user;
				}
				else
				{
					comment.Name = form.Name;
					// Only store website addresses that are safe to link to.
					if (Text.IsSafeUrl(form.Website))
					{
						comment.Website = form.Website.Trim();
					}
				}
				try
				{
					Module.SaveComment(comment);
					// The comment shows immediately, also when the section is cached.
					InvalidateCache();
					return Redirect(PublicUrls.GetUrlFromSection(Request, Module.Section) + "/" + article.Id);
				}
				catch (Exception ex)
				{
					form.Error = ex.Message;
				}
			}
			// The article again, with the form as it was posted.
			HttpContext.Features.Set(form);
			return PublicPageView();
		}

		/// <summary>
		/// The article of the url, or null when it doesn't exist or doesn't show in this section.
		/// </summary>
		private Article GetArticle()
		{
			if (Module.CurrentArticleId <= 0)
			{
				return null;
			}
			Article article = Module.GetArticleById(Module.CurrentArticleId);
			try
			{
				// GetArticleById returns a proxy that is loaded on first use.
				NHibernateUtil.Initialize(article);
			}
			catch (ObjectNotFoundException)
			{
				return null;
			}
			return IsVisible(article) ? article : null;
		}

		/// <summary>
		/// An article shows in its own section only, once it is online. After it went offline it shows when the section
		/// has an archive (where it is listed). The WebForms site showed any article in any Articles section.
		/// </summary>
		private bool IsVisible(Article article)
		{
			DateTime now = DateTime.Now;
			return article.Section.Id == Module.Section.Id
				&& article.DateOnline <= now
				&& (article.DateOffline > now || Module.ShowArchive);
		}

		private bool CategoryExists()
		{
			try
			{
				NHibernateUtil.Initialize(Module.GetCategoryById(Module.CurrentCategoryId));
				return true;
			}
			catch (ObjectNotFoundException)
			{
				return false;
			}
		}
	}

	/// <summary>
	/// A posted comment (the fields of the comment form), and what is wrong with it.
	/// </summary>
	public class CommentForm
	{
		public string Name { get; set; }
		public string Website { get; set; }
		public string Comment { get; set; }

		internal bool NameMissing { get; set; }
		internal bool CommentMissing { get; set; }
		/// <summary>
		/// Why the comment couldn't be saved.
		/// </summary>
		internal string Error { get; set; }
	}
}
