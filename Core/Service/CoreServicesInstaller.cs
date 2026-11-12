using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using Cuyahoga.Core.DataAccess;
using Cuyahoga.Core.Service.Email;
using Cuyahoga.Core.Service.Files;
using Cuyahoga.Core.Service.Membership;
using Cuyahoga.Core.Service.SiteStructure;
using Cuyahoga.Core.Util;

namespace Cuyahoga.Core.Service
{
	/// <summary>
	/// Registers the core services and data access components. Components are singletons unless their class says
	/// otherwise (for example [Transient]).
	/// </summary>
	public class CoreServicesInstaller : IWindsorInstaller
	{
		private readonly CuyahogaAppSettings _settings;

		public CoreServicesInstaller(CuyahogaAppSettings settings)
		{
			this._settings = settings;
		}

		public void Install(IWindsorContainer container, IConfigurationStore store)
		{
			container.Register(
				// Site structure and membership
				Component.For<ISiteService>().ImplementedBy<SiteService>().Named("core.siteservice"),
				Component.For<INodeService>().ImplementedBy<NodeService>().Named("core.nodeservice"),
				Component.For<ISectionService>().ImplementedBy<SectionService>().Named("core.sectionservice"),
				Component.For<IModuleTypeService>().ImplementedBy<ModuleTypeService>().Named("core.moduletypeservice"),
				Component.For<ITemplateService>().ImplementedBy<TemplateService>().Named("core.templateservice"),
				Component.For<IAuthenticationService>().ImplementedBy<DefaultAuthenticationService>().Named("core.authenticationservice"),
				Component.For<IUserService>().ImplementedBy<DefaultUserService>().Named("core.userservice"),

				// Files
				Component.For<IFileService>().Named("core.fileservice")
					.UsingFactoryMethod(() => new TransactionalFileService { TempDir = this._settings.TempDir }),

				// Email
				Component.For<IEmailService>().ImplementedBy<DefaultEmailService>().Named("core.emailservice"),
				Component.For<IEmailSender>().Named("core.emailsender").UsingFactoryMethod(() => CreateEmailSender()),
				Component.For<IEmailTemplateEngine>().ImplementedBy<SimpleEmailTemplateEngine>().Named("core.emailtemplateengine"),

				// Data access
				Component.For<ICommonDao>().ImplementedBy<CommonDao>().Named("core.commondao"),
				Component.For<ISiteStructureDao>().ImplementedBy<SiteStructureDao>().Named("core.sitestructuredao"),
				Component.For<IUserDao>().ImplementedBy<UserDao>().Named("core.userdao"));
		}

		private SmtpNet2EmailSender CreateEmailSender()
		{
			SmtpNet2EmailSender sender = new SmtpNet2EmailSender(this._settings.SmtpHost);
			if (this._settings.SmtpPort.HasValue)
			{
				sender.Port = this._settings.SmtpPort.Value;
			}
			sender.SmtpUsername = this._settings.SmtpUsername;
			sender.SmtpPassword = this._settings.SmtpPassword;
			if (this._settings.EmailEncoding != null)
			{
				sender.EmailEncoding = this._settings.EmailEncoding;
			}
			return sender;
		}
	}
}
