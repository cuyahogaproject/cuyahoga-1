using System.Globalization;
using Cuyahoga.Modules.Articles.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.Articles.Mvc.Pages.Modules.Articles
{
	/// <summary>
	/// The comments of an article, and deleting them (AdminComments.aspx in the WebForms site).
	/// </summary>
	public class AdminCommentsModel : ModuleAdminPageModel<ArticleModule>
	{
		private Article _article;

		public IReadOnlyList<Comment> Comments { get; private set; }

		public IActionResult OnGet()
		{
			if (!LoadArticle())
			{
				return NotFound();
			}
			BindComments();
			return Page();
		}

		public IActionResult OnPost([FromForm] int? deleteCommentId)
		{
			if (!LoadArticle())
			{
				return NotFound();
			}
			if (Request.Form.ContainsKey("btnBack"))
			{
				return Redirect("AdminArticles.aspx" + BaseQueryString);
			}
			if (deleteCommentId != null)
			{
				try
				{
					Comment comment = this._article.Comments.Cast<Comment>().FirstOrDefault(c => c.Id == deleteCommentId);
					if (comment != null)
					{
						this._article.Comments.Remove(comment);
						Module.DeleteComment(comment);
					}
					else
					{
						ShowError("Could not find comment.");
					}
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}
			BindComments();
			return Page();
		}

		/// <summary>
		/// The article of the url (ArticleId) in this section.
		/// </summary>
		private bool LoadArticle()
		{
			if (!Int32.TryParse(Request.Query["ArticleId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int articleId))
			{
				return false;
			}
			this._article = Module.GetAllArticles().Cast<Article>().FirstOrDefault(article => article.Id == articleId);
			return this._article != null;
		}

		private void BindComments()
		{
			Comments = this._article.Comments.Cast<Comment>().ToList();
		}
	}
}
