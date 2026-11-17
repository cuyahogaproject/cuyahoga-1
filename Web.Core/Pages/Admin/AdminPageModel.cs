using Cuyahoga.Core.Domain;
using Cuyahoga.Web.Core.Security;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	/// <summary>
	/// Base class of the site administration pages (AdminBasePage in the WebForms site): the page title and the
	/// message box of the admin layout. Only administrators get here (see Program.cs).
	/// </summary>
	public abstract class AdminPageModel : PageModel
	{
		/// <summary>
		/// The title of the page (browser title and heading).
		/// </summary>
		public string Title { get; protected set; }

		/// <summary>
		/// The text of the message box, or null when there is no message.
		/// </summary>
		public string Message { get; private set; }

		public bool MessageIsError { get; private set; }

		/// <summary>
		/// The logged-in administrator.
		/// </summary>
		public User CurrentUser
		{
			get { return LegacyAuthenticationHandler.GetUser(HttpContext); }
		}

		protected void ShowMessage(string message)
		{
			Message = message;
			MessageIsError = false;
		}

		protected void ShowError(string errorText)
		{
			Message = "An error occured: " + errorText;
			MessageIsError = true;
		}

		public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
		{
			// Pages that redirect after a change pass the message in the url.
			string message = Request.Query["message"];
			if (!String.IsNullOrEmpty(message))
			{
				ShowMessage(message);
			}
		}
	}
}
