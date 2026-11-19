using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Domain;

namespace Cuyahoga.Web.Core.Pages.Admin
{
	public class TemplatesModel : AdminPageModel
	{
		private readonly ICommonDao _commonDao;

		public TemplatesModel(ICommonDao commonDao)
		{
			this._commonDao = commonDao;
		}

		public List<Template> Templates { get; private set; }

		public void OnGet()
		{
			Title = "Templates";
			Templates = this._commonDao.GetAll(typeof(Template), "BasePath", "Name").Cast<Template>().ToList();
		}
	}
}
