using Cuyahoga.Modules.Articles.Domain;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Http.Extensions;

namespace Cuyahoga.Modules.Articles.Mvc.Pages.Modules.Articles
{
	/// <summary>
	/// The articles of a section, 10 per page (AdminArticles.aspx in the WebForms site).
	/// </summary>
	public class AdminArticlesModel : ModuleAdminPageModel<ArticleModule>
	{
		private const int PageSize = 10;

		public IReadOnlyList<Article> Articles { get; private set; }

		public IReadOnlyList<PagerLink> PagerLinks { get; private set; }

		public void OnGet()
		{
			List<Article> articles = Module.GetAllArticles().Cast<Article>().ToList();
			string url = UriHelper.BuildRelative(Request.PathBase, Request.Path, Request.QueryString);
			var pager = new UrlPager(url, url, articles.Count, PageSize, hideWhenOnePage: false, PagerUrlMode.QueryString);
			Articles = pager.GetPage(articles).ToList();
			PagerLinks = pager.GetLinks();
		}
	}
}
