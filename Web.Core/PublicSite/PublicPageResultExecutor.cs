using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Cuyahoga.Web.Core.PublicSite
{
	/// <summary>
	/// Renders a public page (<see cref="PublicPageResult"/>): the sections first (Views/PublicPage/Index.cshtml),
	/// then the template as the layout.
	/// </summary>
	public class PublicPageResultExecutor : IActionResultExecutor<PublicPageResult>
	{
		private readonly IModelMetadataProvider _metadataProvider;

		public PublicPageResultExecutor(IModelMetadataProvider metadataProvider)
		{
			this._metadataProvider = metadataProvider;
		}

		public Task ExecuteAsync(ActionContext context, PublicPageResult result)
		{
			PublicPage page = context.HttpContext.GetPublicPage()
				?? throw new InvalidOperationException("The request has no public page.");
			page.Assets.RegisterStylesheet("maincss",
				PublicUrls.GetApplicationPath(context.HttpContext.Request) + page.Template.BasePath + "/Css/" + page.Template.Css);
			var view = new ViewResult
			{
				ViewName = "/Views/PublicPage/Index.cshtml",
				ViewData = new ViewDataDictionary<PublicPageModel>(this._metadataProvider, context.ModelState)
				{
					Model = new PublicPageModel(page, TemplateLayouts.GetLayoutPath(page.Template))
				}
			};
			return view.ExecuteResultAsync(context);
		}
	}
}
