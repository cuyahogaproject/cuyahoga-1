using Castle.Windsor;
using Cuyahoga.Core.Persistence;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Core;
using Cuyahoga.Web.Core.Persistence;
using Cuyahoga.Web.Core.Security;
using Cuyahoga.Web.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

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
CuyahogaAppSettings settings = CuyahogaContainer.LoadSettings(builder.Configuration);
builder.Services.AddSingleton(settings);
var httpContextAccessor = new HttpContextAccessor();
builder.Services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);
ContextStorage.Current = new HttpContextItemsStorage(httpContextAccessor);
IWindsorContainer container = CuyahogaContainer.Create(settings, httpContextAccessor);
// Modules resolve services through the static IoC wrapper.
IoC.Initialize(container);
builder.Services.AddCuyahogaCoreServices(container);

// Virtual paths (~/index, ~/files) are in the folder of the WebForms site (LegacySitePath, absolute or relative to
// this site). Without it, the things that need these folders (updating the search index) fail with an error.
string legacySitePath = builder.Configuration["LegacySitePath"];
if (!String.IsNullOrWhiteSpace(legacySitePath))
{
	string legacySiteRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, legacySitePath));
	ApplicationPaths.MapPath = virtualPath =>
		Path.Combine(legacySiteRoot, virtualPath.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar));
}

// The modules of the database, activated before every page of this site.
builder.Services.AddSingleton<ModuleActivation>();
builder.Services.AddScoped<SearchIndexUpdater>();

// Questions for the WebForms site, asked with the cookies of the browser: who is logged in, template placeholders.
builder.Services.AddHttpClient(LegacySiteClient.HttpClientName, client =>
		client.BaseAddress = new Uri(legacySiteUrl.TrimEnd('/') + "/"))
	.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
builder.Services.AddScoped<LegacySiteClient>();
builder.Services.AddScoped<TemplatePlaceholderProvider>();

// Users log in on the WebForms site. The migrated admin pages ask it who is logged in (CurrentUser.axd). Only
// endpoints with the Administrator policy authenticate, so forwarded requests don't make that extra call.
builder.Services.AddAuthentication()
	.AddScheme<AuthenticationSchemeOptions, LegacyAuthenticationHandler>(LegacyAuthenticationHandler.SchemeName, null);
builder.Services.AddSingleton<IAuthorizationHandler, AdministratorRequirement>();
builder.Services.AddAuthorization(options =>
	options.AddPolicy(AdministratorRequirement.PolicyName, policy => policy
		.AddAuthenticationSchemes(LegacyAuthenticationHandler.SchemeName)
		.RequireAuthenticatedUser()
		.AddRequirements(new AdministratorRequirement())));

// The migrated pages keep the urls of the WebForms pages (/Admin/Users.aspx).
builder.Services.AddRazorPages(options =>
	options.Conventions.AuthorizeFolder("/Admin", AdministratorRequirement.PolicyName));

builder.Services.AddHttpForwarder();
// Request size limits are up to the WebForms site (maxRequestLength) for the requests that it handles.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = null);

var app = builder.Build();

app.UseMiddleware<NHibernateSessionMiddleware>();
app.UseRouting();
// Before authentication, which opens the first NHibernate session of the request.
app.UseMiddleware<ModuleActivationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Everything that this site doesn't handle goes to the WebForms site. The forwarder sets the X-Forwarded-Host and
// X-Forwarded-Proto headers, which the WebForms site uses to build absolute urls (and to find the Cuyahoga site of
// the request) for the address that the browser used.
app.MapForwarder("/{**catch-all}", legacySiteUrl).WithOrder(int.MaxValue);

app.Run();
