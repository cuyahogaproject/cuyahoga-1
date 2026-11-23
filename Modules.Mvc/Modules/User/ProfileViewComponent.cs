using System.Collections;
using System.Globalization;
using Cuyahoga.Core.Util;
using Cuyahoga.Modules.User;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Modules.Mvc.User
{
	/// <summary>
	/// A UserProfile section: the profile of a user ({section url}/ViewProfile/{id}), registering, resetting the
	/// password, and editing the profile of the logged-in user (the default). ViewProfile.ascx, Register.ascx,
	/// ResetPassword.ascx and EditProfile.ascx in the WebForms site. The forms post to <see cref="ProfileController"/>.
	/// </summary>
	public class ProfileViewComponent : ModuleViewComponent<ProfileModule>
	{
		protected override Task<IViewComponentResult> RenderAsync()
		{
			ProfileForm form = HttpContext.Features.Get<ProfileForm>() ?? new ProfileForm();
			Core.Domain.User user = PublicPage.User;
			var model = new ProfileModel
			{
				Text = GetText,
				Form = form,
				FormUrl = PublicUrls.GetUrlFromSection(Request, Module.Section) + Module.ModulePathInfo,
				UseAntiforgery = user != null
			};
			string view;
			switch (Module.CurrentAction)
			{
				case ProfileModuleAction.ViewProfile:
					view = "ViewProfile";
					model.Profile = Module.CurrentUserId > 0 ? Module.GetUserById(Module.CurrentUserId) : null;
					if (Module.CurrentUserId > 0 && model.Profile == null)
					{
						model.Form.Error = String.Format(GetText("USERNOTFOUND"),
							Module.CurrentUserId.ToString(CultureInfo.CurrentCulture));
					}
					break;
				case ProfileModuleAction.Register:
					view = "Register";
					break;
				case ProfileModuleAction.ResetPassword:
					view = "ResetPassword";
					break;
				default:
					view = "EditProfile";
					model.Profile = user;
					if (user == null)
					{
						form.Confirmation = GetText("NOTAUTHENTICATED");
						form.Done = true;
					}
					else if (HttpContext.Features.Get<ProfileForm>() == null)
					{
						// Not posted: the current profile.
						form.FirstName = user.FirstName;
						form.LastName = user.LastName;
						form.Email = user.Email;
						form.Website = user.Website;
						form.TimeZone = user.TimeZone.ToString(CultureInfo.InvariantCulture);
					}
					model.TimeZones = TimeZoneUtil.GetTimeZones().Cast<DictionaryEntry>()
						.Select(entry => new KeyValuePair<string, string>(
							Convert.ToString(entry.Key, CultureInfo.InvariantCulture), (string)entry.Value))
						.ToList();
					break;
			}
			return Task.FromResult<IViewComponentResult>(View($"~/Modules/User/{view}.cshtml", model));
		}
	}

	public class ProfileModel
	{
		/// <summary>
		/// The texts of the module (GetText).
		/// </summary>
		public Func<string, string> Text { get; init; }
		/// <summary>
		/// The posted form and its result (or an empty form).
		/// </summary>
		public ProfileForm Form { get; init; }
		/// <summary>
		/// The url that the form posts to (the url of the page).
		/// </summary>
		public string FormUrl { get; init; }
		public bool UseAntiforgery { get; init; }
		/// <summary>
		/// The user to show (ViewProfile) or to edit (EditProfile), or null.
		/// </summary>
		public Core.Domain.User Profile { get; set; }
		public IReadOnlyList<KeyValuePair<string, string>> TimeZones { get; set; }

		public bool UsernameMissing => Form.UsernameMissing;
		public bool EmailMissing => Form.EmailMissing;
		public bool EmailInvalid => Form.EmailInvalid;
		public string Error => Form.Error;
		public string Confirmation => Form.Confirmation;
		public bool Done => Form.Done;
	}
}
