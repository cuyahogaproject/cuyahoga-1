using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Search;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Core.Util;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;

namespace Cuyahoga.Web.Mvc
{
	/// <summary>
	/// Base class of the admin pages of a module (ModuleAdminBasePage in the WebForms site): the node and the section
	/// from the url (NodeId and SectionId), the module of the section, the message box, and instant indexing when the
	/// module is searchable. The site only lets users in who may edit the section.
	/// </summary>
	/// <remarks>
	/// Admin pages of modules are Razor Pages in the project of the module, under Pages/Modules/{Module}/, with the url
	/// of the WebForms page (@page "/Modules/{Module}/Edit.aspx"). They get the module admin layout of the site.
	/// </remarks>
	public abstract class ModuleAdminPageModel : PageModel
	{
		/// <summary>
		/// The node of the url.
		/// </summary>
		public Node Node { get; private set; }

		/// <summary>
		/// The section of the url.
		/// </summary>
		public Section Section { get; private set; }

		/// <summary>
		/// The module of the section.
		/// </summary>
		public ModuleBase SectionModule { get; private set; }

		/// <summary>
		/// The title of the page (browser title), or null.
		/// </summary>
		public string Title { get; protected set; }

		/// <summary>
		/// The text of the message box, or null when there is no message.
		/// </summary>
		public string Message { get; private set; }

		public bool MessageIsError { get; private set; }

		/// <summary>
		/// The logged-in user.
		/// </summary>
		public User CurrentUser => HttpContext.GetCuyahogaUser();

		/// <summary>
		/// The query string with the node and the section (?NodeId=1&amp;SectionId=2), for links to the other admin
		/// pages of the module.
		/// </summary>
		public string BaseQueryString => $"?NodeId={Node.Id}&SectionId={Section.Id}";

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

		/// <summary>
		/// The message of the exception and of its inner exceptions.
		/// </summary>
		protected void ShowException(Exception exception)
		{
			var messages = new List<string>();
			for (Exception current = exception; current != null; current = current.InnerException)
			{
				messages.Add(current.Message);
			}
			ShowError(String.Join(" ", messages));
		}

		public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context,
			PageHandlerExecutionDelegate next)
		{
			IServiceProvider services = HttpContext.RequestServices;
			if (!Int32.TryParse(Request.Query["NodeId"], out int nodeId)
				|| !Int32.TryParse(Request.Query["SectionId"], out int sectionId))
			{
				context.Result = NotFound();
				return;
			}
			Node = LoadOrNull(services.GetRequiredService<INodeService>().GetNodeById(nodeId));
			Section = LoadOrNull(services.GetRequiredService<ISectionService>().GetSectionById(sectionId));
			if (Node == null || Section == null)
			{
				context.Result = NotFound();
				return;
			}
			SectionModule = services.GetRequiredService<ModuleLoader>().GetModuleFromSection(Section);
			if (SectionModule == null)
			{
				context.Result = NotFound();
				return;
			}
			CuyahogaAppSettings settings = services.GetRequiredService<CuyahogaAppSettings>();
			if (SectionModule is ISearchable searchable && settings.InstantIndexing)
			{
				string indexDir = ApplicationPaths.MapPath(settings.SearchIndexDir);
				searchable.ContentCreated += (sender, e) => UpdateIndex(indexDir, builder => builder.AddContent(e.SearchContent));
				searchable.ContentUpdated += (sender, e) => UpdateIndex(indexDir, builder => builder.UpdateContent(e.SearchContent));
				searchable.ContentDeleted += (sender, e) => UpdateIndex(indexDir, builder => builder.DeleteContent(e.SearchContent));
			}
			await next();
			if (HttpMethods.IsPost(Request.Method))
			{
				// The content may have changed: the public pages show it right away.
				services.GetService<ISectionOutputCache>()?.Remove(SectionModule);
			}
		}

		private static void UpdateIndex(string indexDir, Action<IndexBuilder> update)
		{
			var builder = new IndexBuilder(indexDir, false);
			try
			{
				update(builder);
			}
			finally
			{
				builder.Close();
			}
		}

		/// <summary>
		/// The services return a proxy that is loaded on first use: null when the id doesn't exist.
		/// </summary>
		private static T LoadOrNull<T>(T entity) where T : class
		{
			try
			{
				NHibernateUtil.Initialize(entity);
				return entity;
			}
			catch (ObjectNotFoundException)
			{
				return null;
			}
		}
	}

	/// <summary>
	/// An admin page of a module of type TModule.
	/// </summary>
	public abstract class ModuleAdminPageModel<TModule> : ModuleAdminPageModel where TModule : ModuleBase
	{
		/// <summary>
		/// The module of the section.
		/// </summary>
		protected TModule Module => (TModule)SectionModule;
	}
}
