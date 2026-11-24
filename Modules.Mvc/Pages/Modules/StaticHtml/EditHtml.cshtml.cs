using Cuyahoga.Modules.StaticHtml;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.Mvc.Pages.Modules.StaticHtml
{
	/// <summary>
	/// Edit the html of a StaticHtml section (EditHtml.aspx in the WebForms site).
	/// </summary>
	public class EditHtmlModel : ModuleAdminPageModel<StaticHtmlModule>
	{
		[BindProperty(Name = "txtEditor")]
		public string Html { get; set; }

		public void OnGet()
		{
			Html = Module.GetContent()?.Content ?? String.Empty;
		}

		public void OnPost()
		{
			try
			{
				var currentUser = CurrentUser;
				StaticHtmlContent content = Module.GetContent();
				if (content == null)
				{
					content = new StaticHtmlContent
					{
						Section = Module.Section,
						CreatedBy = currentUser
					};
				}
				content.ModifiedBy = currentUser;
				content.Content = (Html ?? String.Empty).Trim();
				Module.SaveContent(content);
				ShowMessage("Content saved.");
			}
			catch (Exception ex)
			{
				ShowException(ex);
			}
		}
	}
}
