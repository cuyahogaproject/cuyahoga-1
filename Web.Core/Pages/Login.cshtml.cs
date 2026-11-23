using Cuyahoga.Web.Core.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cuyahoga.Web.Core.Pages
{
	/// <summary>
	/// The login page of the site administration (Login.aspx of the WebForms site): logs in and goes back to ReturnUrl.
	/// </summary>
	public class LoginModel : PageModel
	{
		[BindProperty(SupportsGet = true)]
		public string ReturnUrl { get; set; }

		public string Error { get; private set; }

		public void OnGet()
		{
		}

		public async Task<IActionResult> OnPostAsync(string username, string password)
		{
			string error = await CuyahogaAuthentication.SignInAsync(HttpContext, username ?? String.Empty,
				password ?? String.Empty, persistent: false);
			if (error != null)
			{
				Error = "Invalid username or password.";
				return Page();
			}
			return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "~/");
		}
	}
}
