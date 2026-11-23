using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Util;
using Cuyahoga.Modules.LanguageSwitcher;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Cuyahoga.Modules.Mvc.LanguageSwitcher
{
	/// <summary>
	/// Links (text or flags) or a dropdown to go to the home pages of the other languages of the site
	/// (LanguageSwitcher.ascx in the WebForms site), and optionally a redirect from the start page of the site to the
	/// home page of the language of the browser.
	/// </summary>
	/// <remarks>
	/// The dropdown of the WebForms site posted back; here it is a form that sends the culture to the current page
	/// (?culture=nl-NL), which remembers it (cookie CuyahogaCulture, for the start page) and goes to the home page of
	/// that culture. That works wherever the section is, also in a template. Never cached: the view redirects.
	/// </remarks>
	[NoSectionCache]
	public class LanguageSwitcherViewComponent : ModuleViewComponent<LanguageSwitcherModule>
	{
		public const string CultureCookie = "CuyahogaCulture";
		public const string CultureParameter = "culture";

		protected override Task<IViewComponentResult> RenderAsync()
		{
			Dictionary<string, Node> cultureNodes = Module.GetCultureRootNodesBySite(PublicPage.Site);
			string currentCulture = PublicPage.RootNode.Culture;

			string selectedCulture = Request.Query[CultureParameter].FirstOrDefault();
			if (selectedCulture != null && cultureNodes.TryGetValue(selectedCulture, out Node selectedRootNode))
			{
				HttpContext.Response.Cookies.Append(CultureCookie, selectedCulture, new CookieOptions { Path = "/" });
				PublicPage.Redirect(PublicUrls.GetUrlFromNode(Request, selectedRootNode));
			}
			else if (Module.RedirectToUserLanguage && IsStartPage())
			{
				RedirectToUserLanguage(cultureNodes, currentCulture);
			}

			string imagesPath = PublicUrls.GetApplicationPath(Request) + "Modules/LanguageSwitcher/Images/";
			var model = new LanguageSwitcherModel
			{
				DisplayMode = Module.DisplayMode,
				FormUrl = Request.PathBase + Request.Path,
				GoImageUrl = imagesPath + "go.gif",
				Languages = cultureNodes
					.Select(cultureNode => new LanguageSwitcherModel.Language
					{
						Culture = cultureNode.Key,
						Text = Globalization.GetNativeLanguageTextFromCulture(cultureNode.Key),
						Url = PublicUrls.GetUrlFromNode(Request, cultureNode.Value),
						FlagUrl = imagesPath + "flags/" + Globalization.GetCountryFromCulture(cultureNode.Key).ToLower() + ".png",
						// The links are for the other languages; the dropdown has all, with the current one selected.
						IsCurrent = cultureNode.Value.Culture == currentCulture,
						IsSelected = cultureNode.Key == PublicPage.ActiveNode.Culture
					})
					.ToList()
			};
			return Task.FromResult<IViewComponentResult>(View("~/Modules/LanguageSwitcher/LanguageSwitcher.cshtml", model));
		}

		/// <summary>
		/// The start page of the site: no particular page in the url.
		/// </summary>
		private bool IsStartPage()
		{
			string path = Request.Path.Value ?? String.Empty;
			return (path.Length <= 1 || path.Equals("/Default.aspx", StringComparison.OrdinalIgnoreCase))
				&& !Request.Query.ContainsKey("NodeId") && !Request.Query.ContainsKey("SectionId")
				&& !Request.Query.ContainsKey("ShortDescription");
		}

		/// <summary>
		/// Goes to the home page of the language of the browser, when the site has one (in another culture than the
		/// current one).
		/// </summary>
		private void RedirectToUserLanguage(Dictionary<string, Node> cultureNodes, string currentCulture)
		{
			string acceptLanguage = Request.Headers[HeaderNames.AcceptLanguage].FirstOrDefault();
			if (acceptLanguage == null || acceptLanguage.Length < 2)
			{
				return;
			}
			string preferredLanguage = acceptLanguage.Substring(0, 2);
			foreach (KeyValuePair<string, Node> cultureNode in cultureNodes)
			{
				if (cultureNode.Key != currentCulture
					&& Globalization.GetLanguageFromCulture(cultureNode.Key) == preferredLanguage)
				{
					PublicPage.Redirect(PublicUrls.GetUrlFromNode(Request, cultureNode.Value));
					return;
				}
			}
		}
	}

	public class LanguageSwitcherModel
	{
		public DisplayMode DisplayMode { get; init; }
		/// <summary>
		/// The url of the current page, for the form of the dropdown.
		/// </summary>
		public string FormUrl { get; init; }
		public string GoImageUrl { get; init; }
		public IReadOnlyList<Language> Languages { get; init; }

		public class Language
		{
			public string Culture { get; init; }
			public string Text { get; init; }
			public string Url { get; init; }
			public string FlagUrl { get; init; }
			public bool IsCurrent { get; init; }
			public bool IsSelected { get; init; }
		}
	}
}
