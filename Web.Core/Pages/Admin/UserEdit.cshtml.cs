using System.Collections;
using System.Text.RegularExpressions;
using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.Membership;
using Cuyahoga.Core.Util;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	public class UserEditModel : AdminPageModel
	{
		// The email check of the WebForms page (RegularExpressionValidator).
		private static readonly Regex EmailPattern = new Regex(@"^\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$");

		private readonly IUserService _userService;
		private readonly ICommonDao _commonDao;
		private readonly CuyahogaAppSettings _settings;
		private User _user;

		public UserEditModel(IUserService userService, ICommonDao commonDao, CuyahogaAppSettings settings)
		{
			this._userService = userService;
			this._commonDao = commonDao;
			this._settings = settings;
		}

		[BindProperty(SupportsGet = true)]
		public int UserId { get; set; }

		[BindProperty] public string Username { get; set; }
		[BindProperty] public string Firstname { get; set; }
		[BindProperty] public string Lastname { get; set; }
		[BindProperty] public string Email { get; set; }
		[BindProperty] public string Website { get; set; }
		[BindProperty] public bool Active { get; set; }
		[BindProperty] public int TimeZone { get; set; }
		[BindProperty] public string Password1 { get; set; }
		[BindProperty] public string Password2 { get; set; }
		[BindProperty] public List<int> RoleIds { get; set; } = new List<int>();

		public bool IsExistingUser
		{
			get { return UserId > 0; }
		}

		/// <summary>
		/// The roles that can be assigned (all roles except the anonymous ones), by permission level.
		/// </summary>
		public List<Role> Roles { get; private set; }

		public IDictionary TimeZones
		{
			get { return TimeZoneUtil.GetTimeZones(); }
		}

		public string UsernameError { get; private set; }
		public string EmailError { get; private set; }
		public string PasswordError { get; private set; }

		public IActionResult OnGet()
		{
			if (!LoadUser())
			{
				return NotFound();
			}
			Username = this._user.UserName;
			Firstname = this._user.FirstName;
			Lastname = this._user.LastName;
			Email = this._user.Email;
			Website = this._user.Website;
			Active = this._user.IsActive;
			TimeZone = this._user.TimeZone;
			RoleIds = Roles.Where(role => this._user.IsInRole(role)).Select(role => role.Id).ToList();
			return Page();
		}

		public IActionResult OnPost()
		{
			if (!LoadUser())
			{
				return NotFound();
			}
			if (IsExistingUser)
			{
				// The username can't be changed.
				Username = this._user.UserName;
			}
			if (Request.Form.ContainsKey("btnCancel"))
			{
				return Redirect("Users.aspx");
			}
			if (Request.Form.ContainsKey("btnDelete"))
			{
				return Delete();
			}
			return Save();
		}

		private bool LoadUser()
		{
			Title = "Edit user";
			this._user = UserId == -1 ? new User() : this._userService.GetUserById(UserId);
			Roles = this._commonDao.GetAll(typeof(Role), "PermissionLevel").Cast<Role>()
				.Where(role => role.PermissionLevel != (int)AccessLevel.Anonymous)
				.ToList();
			return this._user != null;
		}

		private bool Validate()
		{
			if (!IsExistingUser && String.IsNullOrEmpty(Username))
			{
				UsernameError = "Username is required";
			}
			if (String.IsNullOrEmpty(Email))
			{
				EmailError = "Email is required";
			}
			else if (!EmailPattern.IsMatch(Email))
			{
				EmailError = "Invalid email";
			}
			// Like the CompareValidator: only when the first password is entered.
			if (!String.IsNullOrEmpty(Password1) && Password1 != Password2)
			{
				PasswordError = "Both passwords must be the same";
			}
			return UsernameError == null && EmailError == null && PasswordError == null;
		}

		private IActionResult Save()
		{
			if (!Validate())
			{
				return Page();
			}

			string password = null;
			if (!String.IsNullOrEmpty(Password1) && !String.IsNullOrEmpty(Password2))
			{
				try
				{
					// Qualified: User is also the ClaimsPrincipal property of the page.
					password = Cuyahoga.Core.Domain.User.HashPassword(Password1);
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
					return Page();
				}
			}
			if (!IsExistingUser && password == null)
			{
				ShowError("Password is required");
				return Page();
			}

			List<Role> selectedRoles = Roles.Where(role => RoleIds.Contains(role.Id)).ToList();
			// The superuser always keeps the Administrator role.
			if (Username == this._settings.SuperUser && !selectedRoles.Any(role => role.Name == this._settings.AdministratorRole))
			{
				ShowError(String.Format("The user '{0}' has to have the '{1}' role.", Username, this._settings.AdministratorRole));
				return Page();
			}

			this._user.UserName = Username;
			// Like the WebForms page: empty names don't replace the current ones.
			if (!String.IsNullOrEmpty(Firstname))
			{
				this._user.FirstName = Firstname;
			}
			if (!String.IsNullOrEmpty(Lastname))
			{
				this._user.LastName = Lastname;
			}
			this._user.Email = Email;
			this._user.Website = Website;
			this._user.IsActive = Active;
			this._user.TimeZone = TimeZone;
			if (password != null)
			{
				this._user.Password = password;
			}
			this._user.Roles.Clear();
			foreach (Role role in selectedRoles)
			{
				this._user.Roles.Add(role);
			}

			try
			{
				if (this._user.Id == -1)
				{
					this._commonDao.SaveObject(this._user);
					return Redirect("UserEdit.aspx?UserId=" + this._user.Id + "&message=" + Uri.EscapeDataString("User created successfully"));
				}
				this._commonDao.UpdateObject(this._user);
				ShowMessage("User saved");
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
			}
			return Page();
		}

		private IActionResult Delete()
		{
			if (this._user.Id > 0)
			{
				if (this._user.Id == CurrentUser.Id)
				{
					ShowError("You can't delete yourself.");
				}
				else if (this._user.UserName == this._settings.SuperUser)
				{
					ShowError("You can't delete the superuser.");
				}
				else
				{
					try
					{
						this._userService.DeleteUser(this._user);
						return Redirect("Users.aspx");
					}
					catch (Exception ex)
					{
						ShowError(ex.Message);
					}
				}
			}
			return Page();
		}
	}
}
