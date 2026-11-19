using System;
using System.Web;
using System.Web.UI;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.UI;
using Cuyahoga.Web.Util;

namespace Cuyahoga.Web.HttpHandlers
{
	/// <summary>
	/// Returns the placeholders of a template (TemplatePlaceholders.axd?TemplateId=1), one per line (text/plain).
	/// Administrators only.
	/// </summary>
	/// <remarks>
	/// Templates are user controls. The ASP.NET Core site (Web.Core) can't load them, so its admin pages ask this site,
	/// which loads the template like the WebForms admin pages did (<see cref="BaseTemplate.Containers"/>).
	/// </remarks>
	public class TemplatePlaceholdersHandler : IHttpHandler
	{
		public bool IsReusable
		{
			get { return true; }
		}

		public void ProcessRequest(HttpContext context)
		{
			context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
			User user = context.User != null && context.User.Identity.IsAuthenticated ? context.User.Identity as User : null;
			if (user == null || !user.HasPermission(AccessLevel.Administrator))
			{
				context.Response.StatusCode = 403;
				return;
			}
			int templateId;
			Template template = Int32.TryParse(context.Request.QueryString["TemplateId"], out templateId)
				? IoC.Resolve<ITemplateService>().GetTemplateById(templateId)
				: null;
			if (template == null)
			{
				context.Response.StatusCode = 404;
				return;
			}

			BaseTemplate templateControl = (BaseTemplate)new Page().LoadControl(UrlHelper.GetApplicationPath() + template.Path);
			context.Response.ContentType = "text/plain";
			foreach (string placeholder in templateControl.Containers.Keys)
			{
				context.Response.Write(placeholder + "\n");
			}
		}
	}
}
