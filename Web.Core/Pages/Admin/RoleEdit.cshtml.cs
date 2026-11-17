using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Util;
using Microsoft.AspNetCore.Mvc;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	public class RoleEditModel : AdminPageModel
	{
		private readonly ICommonDao _commonDao;
		private readonly CuyahogaAppSettings _settings;
		private Role _role;

		public RoleEditModel(ICommonDao commonDao, CuyahogaAppSettings settings)
		{
			this._commonDao = commonDao;
			this._settings = settings;
		}

		[BindProperty(SupportsGet = true)]
		public int RoleId { get; set; }

		[BindProperty]
		public string Name { get; set; }

		/// <summary>
		/// The checked permissions (AccessLevel values).
		/// </summary>
		[BindProperty]
		public List<int> Permissions { get; set; } = new List<int>();

		public bool IsExistingRole
		{
			get { return RoleId > 0; }
		}

		public string NameError { get; private set; }

		public IActionResult OnGet()
		{
			if (!LoadRole())
			{
				return NotFound();
			}
			Name = this._role.Name;
			Permissions = this._role.Permissions?.Select(p => (int)p).ToList() ?? new List<int>();
			return Page();
		}

		public IActionResult OnPost()
		{
			if (!LoadRole())
			{
				return NotFound();
			}
			if (Request.Form.ContainsKey("btnCancel"))
			{
				return Redirect("Roles.aspx");
			}
			if (Request.Form.ContainsKey("btnDelete"))
			{
				return Delete();
			}
			return Save();
		}

		private bool LoadRole()
		{
			Title = "Edit role";
			this._role = RoleId == -1 ? new Role() : (Role)this._commonDao.GetObjectById(typeof(Role), RoleId, true);
			return this._role != null;
		}

		private IActionResult Save()
		{
			if (String.IsNullOrEmpty(Name))
			{
				NameError = "Name is required";
				return Page();
			}
			this._role.Name = Name;
			// No permissions checked: an existing role keeps its permissions.
			int permissionLevel = Permissions.Sum();
			if (permissionLevel > 0)
			{
				this._role.PermissionLevel = permissionLevel;
			}
			if (this._role.PermissionLevel == -1)
			{
				ShowError("Please select one or more Permission(s)");
				return Page();
			}
			try
			{
				if (this._role.Id == -1)
				{
					this._commonDao.SaveObject(this._role);
					return Redirect("Roles.aspx");
				}
				this._commonDao.UpdateObject(this._role);
				ShowMessage("Role saved");
			}
			catch (Exception ex)
			{
				ShowError(ex.Message);
			}
			return Page();
		}

		private IActionResult Delete()
		{
			if (this._role.Id > 0)
			{
				// The Administrator role and the Anonymous role (PermissionLevel 1) can't be deleted.
				if (this._role.Name == this._settings.AdministratorRole || this._role.PermissionLevel == 1)
				{
					ShowError("You can't delete the Administrator Role or the Anonymous Role.");
					return Page();
				}
				try
				{
					this._commonDao.DeleteObject(this._role);
					return Redirect("Roles.aspx");
				}
				catch (Exception ex)
				{
					ShowError(ex.Message);
				}
			}
			return Page();
		}
	}
}
