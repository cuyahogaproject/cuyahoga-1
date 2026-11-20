using System.Globalization;
using Castle.Windsor;
using Cuyahoga.Core.Persistence;
using Cuyahoga.Core.Util;
using Cuyahoga.Web.Core;
using Cuyahoga.Web.Core.Persistence;
using Cuyahoga.Web.Core.PublicSite;
using Cuyahoga.Web.Core.Security;
using Cuyahoga.Web.Core.Services;
using Cuyahoga.Web.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Yarp.ReverseProxy.Transforms;

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
// endpoints with the Administrator policy and public page urls (PublicPageTransformer) authenticate, so other forwarded
// requests don't make that extra call.
builder.Services.AddAuthentication()
	.AddScheme<AuthenticationSchemeOptions, LegacyAuthenticationHandler>(LegacyAuthenticationHandler.SchemeName, null);
builder.Services.AddSingleton<IAuthorizationHandler, AdministratorRequirement>();
builder.Services.AddAuthorization(options =>
	options.AddPolicy(AdministratorRequirement.PolicyName, policy => policy
		.AddAuthenticationSchemes(LegacyAuthenticationHandler.SchemeName)
		.RequireAuthenticatedUser()
		.AddRequirements(new AdministratorRequirement())));

// The migrated pages keep the urls of the WebForms pages (/Admin/Users.aspx). The admin home page is also the default
// document of the admin folder (/Admin/), like Default.aspx was in IIS.
builder.Services.AddRazorPages(options =>
{
	options.Conventions.AuthorizeFolder("/Admin", AdministratorRequirement.PolicyName);
	options.Conventions.AddPageRoute("/Admin/Default", "Admin/");
});

// The public pages (PublicSite): rendered here when their template and modules have moved to this site, otherwise
// forwarded to the WebForms site (PublicPageTransformer).
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<InstallationState>();
builder.Services.AddSingleton<ModuleViewRegistry>();
builder.Services.AddSingleton<TemplateLayouts>();
builder.Services.AddScoped<IPageLookup, ServicePageLookup>();
builder.Services.AddScoped<PageResolver>();
builder.Services.AddScoped<SectionRenderer>();
builder.Services.AddScoped<PublicPageTransformer>();

builder.Services.AddHttpForwarder();
// Request size limits are up to the WebForms site (maxRequestLength) for the requests that it handles.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = null);

var app = builder.Build();

app.UseMiddleware<NHibernateSessionMiddleware>();
// The admin folder without trailing slash goes to the folder (like IIS does), so the relative links of the admin pages
// work. The routes ignore the trailing slash, so this can't be left to the admin home page route.
app.Use(async (context, next) =>
{
	if (String.Equals(context.Request.Path.Value, "/admin", StringComparison.OrdinalIgnoreCase))
	{
		context.Response.Redirect(context.Request.PathBase + context.Request.Path + "/" + context.Request.QueryString, permanent: true);
		return;
	}
	await next(context);
});
app.UseRouting();
// Before authentication, which opens the first NHibernate session of the request.
app.UseMiddleware<ModuleActivationMiddleware>();
// A public page renders in the culture of its node.
app.Use(async (context, next) =>
{
	PublicPage page = context.GetPublicPage();
	if (page != null)
	{
		CultureInfo culture = CultureInfo.CreateSpecificCulture(page.ActiveNode.Culture);
		CultureInfo.CurrentCulture = culture;
		CultureInfo.CurrentUICulture = culture;
	}
	await next(context);
});
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
// Every other url can be a public page; the transformer decides (before the forwarder below gets the request).
app.MapDynamicControllerRoute<PublicPageTransformer>("{**path}");

// Everything that this site doesn't handle goes to the WebForms site. The forwarder sets the X-Forwarded-Host and
// X-Forwarded-Proto headers, which the WebForms site uses to build absolute urls (and to find the Cuyahoga site of
// the request) for the address that the browser used. Redirects that IIS itself makes (for example from a folder url
// without trailing slash) have the address of the WebForms site, so they're changed to the address of this site.
string legacySiteRootUrl = legacySiteUrl.TrimEnd('/') + "/";
app.MapForwarder("/{**catch-all}", legacySiteUrl, transforms => transforms.AddResponseTransform(context =>
{
	string location = context.HttpContext.Response.Headers.Location;
	if (location != null && location.StartsWith(legacySiteRootUrl, StringComparison.OrdinalIgnoreCase))
	{
		HttpRequest request = context.HttpContext.Request;
		context.HttpContext.Response.Headers.Location =
			$"{request.Scheme}://{request.Host}{request.PathBase}/{location.Substring(legacySiteRootUrl.Length)}";
	}
	return ValueTask.CompletedTask;
})).WithOrder(int.MaxValue);

app.Run();
