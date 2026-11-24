# Cuyahoga.Web.Core

The Cuyahoga site: ASP.NET Core (.NET 10), MVC for the public pages and Razor Pages for the site administration and the
installer. It replaced the WebForms site (`Web`, removed): the pages kept their urls (`/Admin/NodeEdit.aspx`,
`/page1.aspx`, `/12/section.aspx/...`), their markup and their look.

## Running it

1. Put the connection string in `appsettings.local.json` (git-ignored), for example:

   ```json
   { "Cuyahoga": { "ConnectionString": "...", "NHibernateDriver": "...", "NHibernateDialect": "..." } }
   ```

   The `Cuyahoga` settings have the names of the former `Cuyahoga.*` appSettings of `Web.config` (without the prefix);
   the `CUYAHOGA_*` environment variables override them.
2. Build the solution (`dotnet build Cuyahoga.sln`): the module projects deploy their files into `Modules`.
3. Start this project (http://localhost:5080). On an empty database the installer starts (`/Install/Install.aspx`).

The e2e tests (`e2e`) start this site on port 8081 with an empty database and an empty data folder (`e2e/.site-data`).

## Files

| Path | What | Served |
|---|---|---|
| `wwwroot` | admin and template stylesheets and images, `Images`, `Support` (CKEditor, file manager, calendar) | yes |
| `Templates` | the templates: Razor layouts (compiled) | no |
| `Install` | the install scripts of Cuyahoga.Core and the built-in modules | no |
| `Modules` | the files of the modules (install scripts, e-mail templates, images), deployed by their builds | only `Modules/*/Images` |
| data folder (`DataPath`, default `App_Data`) | `files` (Downloads), `index` (search index), `UserFiles` (files of the editor) | only `/UserFiles` |

`SitePaths` maps the virtual paths of Cuyahoga (`ApplicationPaths.MapPath("~/...")`) to these folders: `~/Admin`,
`~/Images`, `~/Support` and `~/Templates` to `wwwroot`, `~/Modules` and `~/Install` to the site, everything else to
the data folder. The site needs write access to the data folder and to `wwwroot/Templates` (the template stylesheets
can be edited in the site administration).

## Site administration

Razor Pages in `Pages/Admin` (administrators only, policy `Administrator`) with `_AdminLayout` (`AdminTemplate.ascx`),
and the admin pages of the modules in the projects of the modules (see below). The pages kept the urls, markup and
element ids of the WebForms pages. `TemplateEditor.aspx` only edits template stylesheets; the templates themselves are
Razor layouts, part of the code. `TemplatePreview.aspx` renders the layout of a template with a button in each
placeholder (`TemplateRenderer`), and the placeholders of a template are the `<cuyahoga-placeholder>` elements of its
layout, in their order (a new section goes into `maincontent` when the template has it).

## Login

`Login.aspx` and the login form of the User module (`UserLogin.axd`) log in with a cookie with the id of the user
(`CuyahogaAuthentication`; 30 minutes, sliding; persistent with "Remember login"). Every request loads the user; a user
that was deleted or disabled is logged out.

## Installer

`/Install/Install.aspx`, `Upgrade.aspx` and `Sorry.aspx` (Razor Pages). `InstallerMiddleware` sends page requests to
the installer while the database isn't installed, and during an upgrade (a newer Cuyahoga.Core than the database) the
admin pages to the upgrade page (administrators only) and the public pages to `Sorry.aspx`. The steps after installing
the database are only for the browser that installed it (a protected cookie). Modules are installed, upgraded and
activated in `/Admin/Modules.aspx`.

## Public pages

`PublicPageTransformer` (a dynamic route for every url) turns a public url (`PublicUrlParser`: `{shortdescription}.aspx`,
`{id}/view.aspx`, `{id}/section.aspx/...`, `Default.aspx?NodeId=...`) into a page (`PageResolver`): GET and HEAD, and
POST to a section url whose module controller has an [HttpPost] Index action. Urls that don't give a page get the error
page (`Error.aspx` markup): 404, 403 (visitors go to the login page first, `RedirectToLoginWhenAccessDenied`), 503 for
an unknown site; external links redirect. Unhandled errors also show the error page.

`PublicPageController` renders the sections first (`Views/PublicPage/Index.cshtml`, `SectionRenderer`: module views,
the output cache per section, the section block with the title and edit links), and the template last, as the layout,
so the head of the page has the stylesheets and scripts that modules register.

- **Templates** are Razor layouts in `Templates/`, with the path of the former user control
  (`Templates/AnotherRed/Cuyahoga.ascx` -> `Templates/AnotherRed/Cuyahoga.cshtml`), so the database doesn't change.
  They use `<cuyahoga-head-assets />`, `<cuyahoga-placeholder id="..." />` and the navigation view components
  (`<vc:hierarchical-menu />`, `<vc:navigation-level-zero-one />`, `<vc:navigation-level-two />`). Their stylesheets
  and images are in `wwwroot/Templates`.
- **Module views** derive from `ModuleViewComponent<TModule>` (`Cuyahoga.Web.Mvc`) and return their views by full path
  (`~/Modules/{Module}/...`). `GetText` reads the texts of the module from `{namespace}.Resources.Strings` (resx files
  in the .Mvc project of the module). The views of the built-in modules are in `Cuyahoga.Modules.Mvc`, the ones of the
  other modules in their own project (`Cuyahoga.Modules.Downloads.Mvc`, `.Articles.Mvc`, `.RemoteContent.Mvc`).
  `UrlPager` pages a list like the Pager control of the WebForms site (`.../page/2` or `...?page=2`).
- **Module controllers** derive from `ModuleController<TModule>` and are named after the module
  (`DownloadsController`). When the section in the url has a module with a controller, its Index action gets the
  request and returns any result: a file download, a redirect after a posted form, or `PublicPageView()` for the
  page. The module of a section is one instance per request (`IPublicPageModules`), shared by the controller and the
  view. Forms of modules post to their section url, with their own `<form>`. `InvalidateCache()` clears the cached
  output of the section.
- **Module admin pages** are Razor Pages in the project of the module (`Pages/Modules/{Module}/`), with the url and
  the element ids of the WebForms page (`@page "/Modules/Articles/EditArticle.aspx"`). They derive from
  `ModuleAdminPageModel<TModule>` (`Cuyahoga.Web.Mvc`: the node, section and module of the url, the message box,
  instant indexing of searchable modules) and get `_ModuleAdminLayout`. Only users who may edit the section get in
  (policy `SectionEditor` on the `/Modules` folder). `JsCalendar` writes the date fields of the WebForms Calendar
  control. After a post, the cached output of the section is removed.
- Forms that work on any page (a search box, the language list) use GET to a url: the search boxes go to
  `{search section}/Search?q=...`, the language list to the current page with `?culture=...`. A module view can send
  the visitor elsewhere with `PublicPage.Redirect(url)`; views that do that, or set cookies, have `[NoSectionCache]`.
- **Other urls**: the RSS feeds of sections (`{id}/feed.aspx`, `FeedController`), the XML sitemap (`sitemap.*` in any
  folder, `SitemapController`), the server side of the CKEditor file manager (`Support/fileman/asp_net/main.ashx`,
  `FilemanController`; without thumbnails), and the old profile pages (`Profile.aspx/...`, redirected to the
  UserProfile section).
- **Offline pages**: a page is offline when it or a page above it is offline (the home page: the whole site).
  Visitors get the offline template of the site without sections; administrators see the page as usual.
- The output cache of sections is in memory. Posted forms and urls with a query string are never served from it.

## Differences with the WebForms site

- 404 and 403 for things that don't exist or aren't allowed, instead of the error page.
- An article opens only in its own section and only while online (or archived, when the section shows the archive);
  a category lists the articles of its section only. Files, feeds and articles of another section aren't found in the
  module admin pages.
- Article titles and summaries, and the titles, summaries and paths of search results, are encoded (text).
- The RSS feed of a section is only for users who may view the section.
- The file manager has no thumbnails and doesn't resize uploaded images.
- The login cookie is new: after the switch, everyone logs in once more.

## Moving an existing site

The WebForms site and this site used to run side by side (this site in front). With only this site:

- Settings: the `Cuyahoga.*` appSettings of `Web.config` go into `appsettings.json` / `appsettings.local.json` (section
  `Cuyahoga`, without the prefix) or `CUYAHOGA_*` environment variables.
- Data: copy `files`, `index` and `UserFiles` of the WebForms folder into the data folder (or point `DataPath` to the
  folder that has them).
- Custom templates need a Razor layout in `Templates/{base path}` and their stylesheets and images in
  `wwwroot/Templates/{base path}`.
- In IIS, the site runs with the ASP.NET Core Module (publish this project: `dotnet publish -c Release`).

## Notes

- The Cuyahoga core services live in a Windsor container (modules are registered while the site runs) and are
  available for constructor injection (`AddCuyahogaCoreServices`). The modules that are activated in the database are
  activated before every page (`ModuleActivation`); a module that can't be loaded is logged and skipped.
- NHibernate's second-level cache is off.
- Why a url isn't a public page is logged at the Debug level (`Logging__LogLevel__Cuyahoga.Web.Core.PublicSite=Debug`).
