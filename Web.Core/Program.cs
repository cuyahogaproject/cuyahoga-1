using Castle.Windsor;
using Cuyahoga.Core.Persistence;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Core;
using Cuyahoga.Web.Core.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Machine-specific settings (for example the connection string with a password) go in the optional, git-ignored
// appsettings.local.json, like Web.local.config of the WebForms site. Environment variables and command line
// arguments still override them.
builder.Configuration
	.AddJsonFile("appsettings.local.json", optional: true)
	.AddEnvironmentVariables()
	.AddCommandLine(args);

string legacySiteUrl = builder.Configuration["LegacySiteUrl"];
if (String.IsNullOrWhiteSpace(legacySiteUrl))
{
	throw new InvalidOperationException("The setting 'LegacySiteUrl' (the url of the WebForms site) is required.");
}

// Cuyahoga core: the unit of work lives per request, the core services come from Windsor.
var httpContextAccessor = new HttpContextAccessor();
builder.Services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);
ContextStorage.Current = new HttpContextItemsStorage(httpContextAccessor);
IWindsorContainer container = CuyahogaContainer.Create(CuyahogaContainer.LoadSettings(builder.Configuration), httpContextAccessor);
// Modules resolve services through the static IoC wrapper.
IoC.Initialize(container);
builder.Services.AddCuyahogaCoreServices(container);

builder.Services.AddHttpForwarder();
// Request size limits are up to the WebForms site (maxRequestLength) for the requests that it handles.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = null);

var app = builder.Build();

app.UseMiddleware<NHibernateSessionMiddleware>();

// Everything that this site doesn't handle goes to the WebForms site. The forwarder sets the X-Forwarded-Host and
// X-Forwarded-Proto headers, which the WebForms site uses to build absolute urls (and to find the Cuyahoga site of
// the request) for the address that the browser used.
app.MapForwarder("/{**catch-all}", legacySiteUrl).WithOrder(int.MaxValue);

app.Run();
