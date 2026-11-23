using System.Globalization;
using Cuyahoga.Core.Util;
using Cuyahoga.Modules.Articles.Domain;
using Cuyahoga.Web.Mvc;
using ArticleCategory = Cuyahoga.Modules.Articles.Domain.Category;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.Articles.Mvc.Pages.Modules.Articles
{
	/// <summary>
	/// Add, edit and delete an article (EditArticle.aspx in the WebForms site). The dates are shown and entered in the
	/// time zone of the user.
	/// </summary>
	public class EditArticleModel : ModuleAdminPageModel<ArticleModule>
	{
		private Article _article;

		[BindProperty(Name = "txtTitle")]
		public string ArticleTitle { get; set; }

		[BindProperty(Name = "txtSummary")]
		public string Summary { get; set; }

		[BindProperty(Name = "txtContent")]
		public string ArticleContent { get; set; }

		/// <summary>
		/// The selected category, 0 for none.
		/// </summary>
		[BindProperty(Name = "ddlCategory")]
		public int CategoryId { get; set; }

		[BindProperty(Name = "txtCategory")]
		public string NewCategory { get; set; }

		[BindProperty(Name = "chkSyndicate")]
		public bool Syndicate { get; set; } = true;

		[BindProperty(Name = "calDateOnline")]
		public string DateOnline { get; set; }

		[BindProperty(Name = "calDateOffline")]
		public string DateOffline { get; set; }

		public IReadOnlyList<ArticleCategory> Categories { get; private set; }

		public bool IsExistingArticle => this._article != null;

		public bool TitleMissing { get; private set; }

		public IActionResult OnGet()
		{
			if (!LoadArticle())
			{
				return NotFound();
			}
			if (this._article != null)
			{
				ArticleTitle = this._article.Title;
				Summary = this._article.Summary;
				ArticleContent = this._article.Content;
				Syndicate = this._article.Syndicate;
				DateOnline = JsCalendar.Format(TimeZoneUtil.AdjustDateToUserTimeZone(this._article.DateOnline, CurrentUser), true);
				DateOffline = JsCalendar.Format(TimeZoneUtil.AdjustDateToUserTimeZone(this._article.DateOffline, CurrentUser), true);
				CategoryId = this._article.Category?.Id ?? 0;
			}
			BindCategories();
			return Page();
		}

		public IActionResult OnPost()
		{
			if (!LoadArticle())
			{
				return NotFound();
			}
			// An unchecked checkbox isn't posted.
			Syndicate = Request.Form["chkSyndicate"] == "true";
			if (Request.Form.ContainsKey("btnDelete") && this._article != null)
			{
				try
				{
					Module.DeleteArticle(this._article);
					return BackToList();
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}
			else if (Save())
			{
				return BackToList();
			}
			BindCategories();
			return Page();
		}

		/// <summary>
		/// The article of the url (ArticleId) in this section, or null for a new article. False when the article doesn't
		/// exist in this section.
		/// </summary>
		private bool LoadArticle()
		{
			if (!Int32.TryParse(Request.Query["ArticleId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int articleId)
				|| articleId <= 0)
			{
				return true;
			}
			this._article = Module.GetAllArticles().Cast<Article>().FirstOrDefault(article => article.Id == articleId);
			return this._article != null;
		}

		private bool Save()
		{
			TitleMissing = String.IsNullOrEmpty(ArticleTitle);
			if (TitleMissing)
			{
				return false;
			}
			Article article = this._article ?? new Article
			{
				Section = Module.Section,
				CreatedBy = CurrentUser
			};
			try
			{
				article.Title = ArticleTitle;
				article.Summary = !String.IsNullOrEmpty(Summary) ? Summary : null;
				article.Content = (ArticleContent ?? String.Empty).Trim();
				if (CategoryId > 0)
				{
					article.Category = Module.GetCategoryById(CategoryId);
				}
				else if (!String.IsNullOrEmpty(NewCategory))
				{
					article.Category = new ArticleCategory { Title = NewCategory };
				}
				else
				{
					article.Category = null;
				}
				article.Syndicate = Syndicate;
				DateTime? dateOnline = JsCalendar.Parse(DateOnline);
				article.DateOnline = dateOnline != null
					? TimeZoneUtil.AdjustDateToServerTimeZone(dateOnline.Value, CurrentUser)
					: DateTime.Now;
				DateTime? dateOffline = JsCalendar.Parse(DateOffline);
				article.DateOffline = dateOffline != null
					? TimeZoneUtil.AdjustDateToServerTimeZone(dateOffline.Value, CurrentUser)
					: new DateTime(2199, 1, 1);
				article.ModifiedBy = CurrentUser;
				Module.SaveArticle(article);
				return true;
			}
			catch (Exception ex)
			{
				ShowException(ex);
				return false;
			}
		}

		private void BindCategories()
		{
			Categories = Module.GetAvailableCategories().Cast<ArticleCategory>().ToList();
		}

		private IActionResult BackToList()
		{
			return Redirect("AdminArticles.aspx" + BaseQueryString);
		}
	}
}
