using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Cuyahoga.Core;
using Cuyahoga.Modules.User;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.Mvc.User
{
	/// <summary>
	/// The forms of a UserProfile section: register, reset the password, and edit the profile and the password of the
	/// logged-in user. Like the WebForms site, the result shows on the page of the post (a confirmation, or what is
	/// wrong).
	/// </summary>
	public class ProfileController : ModuleController<ProfileModule>
	{
		/// <summary>
		/// The e-mail address check of the WebForms validators (the whole value has to match).
		/// </summary>
		private static readonly Regex EmailAddress = new Regex(@"^\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$",
			RegexOptions.CultureInvariant);

		private static readonly ResourceManager Texts =
			new ResourceManager(typeof(ProfileController).Namespace + ".Resources.Strings", typeof(ProfileController).Assembly);

		private readonly IAntiforgery _antiforgery;

		public ProfileController(IAntiforgery antiforgery)
		{
			this._antiforgery = antiforgery;
		}

		[HttpPost]
		[ActionName("Index")]
		public async Task<IActionResult> Post([FromForm] ProfileForm form)
		{
			switch (Module.CurrentAction)
			{
				case ProfileModuleAction.Register:
					Register(form);
					break;
				case ProfileModuleAction.ResetPassword:
					ResetPassword(form);
					break;
				case ProfileModuleAction.EditProfile:
					if (PublicPage.User == null)
					{
						// The page tells that the user has to log in.
						break;
					}
					if (!await this._antiforgery.IsRequestValidAsync(HttpContext))
					{
						return BadRequest();
					}
					if (form.SavePassword != null)
					{
						SavePassword(form);
					}
					else
					{
						SaveProfile(form);
					}
					break;
				default:
					return StatusCode(StatusCodes.Status405MethodNotAllowed);
			}
			HttpContext.Features.Set(form);
			return PublicPageView();
		}

		private void Register(ProfileForm form)
		{
			if (!ValidateUsernameAndEmail(form))
			{
				return;
			}
			if (Module.CheckIfUserExists(form.Username))
			{
				form.Error = String.Format(GetText("USEREXISTS"), form.Username);
				return;
			}
			try
			{
				Module.RegisterUser(form.Username, form.Email);
				form.Confirmation = String.Format(GetText("REGISTERCONFIRMATION"), form.Email);
			}
			catch (EmailException)
			{
				form.Error = GetText("REGISTEREMAILERROR");
			}
			catch (Exception ex)
			{
				form.Error = ex.Message;
			}
			// Like the WebForms site: the form is gone after an attempt, also when it failed.
			form.Done = true;
		}

		private void ResetPassword(ProfileForm form)
		{
			if (!ValidateUsernameAndEmail(form))
			{
				return;
			}
			try
			{
				Module.ResetPassword(form.Username, form.Email);
				form.Confirmation = String.Format(GetText("RESETCONFIRMATION"), form.Email);
				form.Done = true;
			}
			catch (EmailException)
			{
				form.Error = GetText("RESETEMAILERROR");
			}
			catch (Exception ex)
			{
				form.Error = ex.Message;
			}
		}

		private void SaveProfile(ProfileForm form)
		{
			if (!ValidateEmail(form))
			{
				return;
			}
			Core.Domain.User user = PublicPage.User;
			user.FirstName = form.FirstName;
			user.LastName = form.LastName;
			user.Email = form.Email;
			user.Website = form.Website;
			if (Int32.TryParse(form.TimeZone, NumberStyles.Integer, CultureInfo.InvariantCulture, out int timeZone))
			{
				user.TimeZone = timeZone;
			}
			try
			{
				Module.UpdateUser(user);
				form.Confirmation = GetText("EDITPROFILECONFIRMATION");
				form.Done = true;
			}
			catch (Exception ex)
			{
				form.Error = ex.Message;
			}
		}

		private void SavePassword(ProfileForm form)
		{
			// The password button of the WebForms site also showed the messages of the e-mail address (it doesn't
			// stop saving the password).
			ValidateEmail(form);
			Core.Domain.User user = PublicPage.User;
			if (!Core.Domain.User.ValidatePassword(form.NewPassword ?? String.Empty)
				|| !Core.Domain.User.ValidatePassword(form.CurrentPassword ?? String.Empty))
			{
				form.Error = GetText("INVALIDPASSWORD");
			}
			else if (!user.CheckPassword(form.CurrentPassword))
			{
				form.Error = GetText("EDITPASSWORDCURRENTERROR");
			}
			else if (form.NewPassword != form.NewPasswordConfirmation)
			{
				form.Error = GetText("EDITPASSWORDCONFIRMERROR");
			}
			else
			{
				try
				{
					user.Password = Core.Domain.User.HashPassword(form.NewPassword);
					Module.UpdateUser(user);
					form.Confirmation = GetText("EDITPASSWORDCONFIRMATION");
					form.Done = true;
				}
				catch (Exception ex)
				{
					form.Error = ex.Message;
				}
			}
		}

		private static bool ValidateUsernameAndEmail(ProfileForm form)
		{
			form.UsernameMissing = String.IsNullOrWhiteSpace(form.Username);
			return ValidateEmail(form) && !form.UsernameMissing;
		}

		private static bool ValidateEmail(ProfileForm form)
		{
			form.EmailMissing = String.IsNullOrWhiteSpace(form.Email);
			form.EmailInvalid = !form.EmailMissing && !EmailAddress.IsMatch(form.Email);
			return !form.EmailMissing && !form.EmailInvalid;
		}

		/// <summary>
		/// A text of the module, like ModuleViewComponent.GetText.
		/// </summary>
		private static string GetText(string key)
		{
			return Texts.GetString(key, CultureInfo.CurrentUICulture);
		}
	}

	/// <summary>
	/// The posted fields of the forms of the profile module, and the result.
	/// </summary>
	public class ProfileForm
	{
		public string Username { get; set; }
		public string Email { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string Website { get; set; }
		public string TimeZone { get; set; }
		public string CurrentPassword { get; set; }
		public string NewPassword { get; set; }
		public string NewPasswordConfirmation { get; set; }
		/// <summary>
		/// The password button of the edit page was clicked (the other one saves the profile).
		/// </summary>
		public string SavePassword { get; set; }

		internal bool UsernameMissing { get; set; }
		internal bool EmailMissing { get; set; }
		internal bool EmailInvalid { get; set; }
		/// <summary>
		/// What went wrong, or null.
		/// </summary>
		internal string Error { get; set; }
		/// <summary>
		/// The message after it went well, or null.
		/// </summary>
		internal string Confirmation { get; set; }
		/// <summary>
		/// The form doesn't show any more.
		/// </summary>
		internal bool Done { get; set; }
	}
}
