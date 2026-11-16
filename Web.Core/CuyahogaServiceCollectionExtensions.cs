using Castle.Windsor;
using Cuyahoga.Core.Persistence;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Service.Email;
using Cuyahoga.Core.Service.Files;
using Cuyahoga.Core.Service.Membership;
using Cuyahoga.Core.Service.SiteStructure;
using Microsoft.Extensions.DependencyInjection;

namespace Cuyahoga.Web.Core
{
	public static class CuyahogaServiceCollectionExtensions
	{
		/// <summary>
		/// Makes the core services of the Windsor container available for constructor injection. Every resolve goes to
		/// Windsor, so the lifestyles of the Windsor registrations (and its transaction interceptor) apply.
		/// </summary>
		public static IServiceCollection AddCuyahogaCoreServices(this IServiceCollection services, IWindsorContainer container)
		{
			// The ASP.NET Core container disposes the Windsor container when the site stops.
			services.AddSingleton(_ => container);

			Forward<ISiteService>(services, container);
			Forward<INodeService>(services, container);
			Forward<ISectionService>(services, container);
			Forward<IModuleTypeService>(services, container);
			Forward<ITemplateService>(services, container);
			Forward<IAuthenticationService>(services, container);
			Forward<IUserService>(services, container);
			Forward<IFileService>(services, container);
			Forward<IEmailService>(services, container);
			Forward<ISessionManager>(services, container);
			Forward<ModuleLoader>(services, container);
			return services;
		}

		private static void Forward<TService>(IServiceCollection services, IWindsorContainer container) where TService : class
		{
			services.AddTransient(_ => container.Resolve<TService>());
		}
	}
}
