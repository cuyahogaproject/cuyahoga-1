using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	public class RolesModel : AdminPageModel
	{
		private readonly ICommonDao _commonDao;

		public RolesModel(ICommonDao commonDao)
		{
			this._commonDao = commonDao;
		}

		public List<Role> Roles { get; private set; }

		public void OnGet()
		{
			Title = "Roles";
			Roles = this._commonDao.GetAll(typeof(Role), "PermissionLevel").Cast<Role>().ToList();
		}

		/// <summary>
		/// The administrator and anonymous roles can't be edited.
		/// </summary>
		public static bool IsSystemRole(Role role)
		{
			return role.Name == "Administrator" || role.Name == "Anonymous user";
		}
	}
}
