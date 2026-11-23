# Cuyahoga.Web.Core

The ASP.NET Core (.NET 10) site of Cuyahoga. Pages move here from the WebForms site (`Web`) one by one, starting with
the site administration. Every request that this site doesn't handle is forwarded to the WebForms site
([YARP](https://github.com/dotnet/yarp) direct forwarding), so to the browser it's one site.

```
browser ──> Cuyahoga.Web.Core (Kestrel) ──> Cuyahoga.Web (WebForms, IIS / IIS Express)
                     │                                 │
                     └──────── same database ──────────┘
```

## Running it

1. Start the WebForms site as usual (IIS Express on http://localhost:8080).
2. Put the connection string of that site in `appsettings.local.json` (git-ignored), for example:

   ```json
   { "Cuyahoga": { "ConnectionString": "..." } }
   ```

   The `Cuyahoga` settings have the names of the `Cuyahoga.*` appSettings in `Web/Web.config` (without the prefix)
   and the same `CUYAHOGA_*` environment variables override them.
3. Start this project (http://localhost:5080). `LegacySiteUrl` and `LegacySitePath` in
   `appsettings.Development.json` point to the WebForms site and its folder.

The e2e tests (`e2e`) run both sites: the WebForms site on port 8082 and this site in front of it on port 8081.

## Migrated pages

| Page | Since |
|---|---|
| `/Admin/Users.aspx`, `/Admin/UserEdit.aspx`, `/Admin/Roles.aspx`, `/Admin/RoleEdit.aspx` | step 3 |
| `/Admin/Default.aspx`, `/Admin/SiteEdit.aspx`, `/Admin/SiteAliasEdit.aspx` | step 4a |
| `/Admin/Sections.aspx`, `/Admin/SectionEdit.aspx`, `/Admin/SectionAttachTemplate.aspx`, `/Admin/SectionAttachNode.aspx`, `/Admin/ConnectionEdit.aspx` | step 4b |
| `/Admin/NodeEdit.aspx`, `/Admin/MenuEdit.aspx` | step 4c |
| `/Admin/Templates.aspx`, `/Admin/TemplateEdit.aspx`, `/Admin/TemplateEditor.aspx`, `/Admin/TemplateSection.aspx`, `/Admin/RebuildIndex.aspx` | step 5 |

Two admin pages stay in the WebForms site until the public site moves: `Modules.aspx` (activating a module registers
it in the process that renders the public pages) and `TemplatePreview.aspx` (it renders a WebForms template).

The pages keep their urls and the markup of the WebForms pages (`Pages/Shared/_AdminLayout.cshtml` is
`AdminTemplate.ascx`), so links between old and new pages keep working and the admin looks the same.

`TemplateEditor.aspx` only edits template stylesheets: the templates themselves are Razor layouts of this site now
(see below), part of its code.

## Public pages

The PageEngine of the WebForms site (`Default.aspx` with the url mappings in `Web.config`) lives in `PublicSite`, with
MVC: `PublicPageTransformer` (a dynamic route for every url) decides per request whether this site renders the page:

- the url is a public page (`PublicUrlParser`: `{shortdescription}.aspx`, `{id}/view.aspx`, `{id}/section.aspx/...`,
  `Default.aspx?NodeId=...`, the same forms that the WebForms site rewrites),
- a GET request, or a POST to a section url whose module controller has an [HttpPost] Index action (other posts,
  such as postbacks of WebForms pages, go to the WebForms site),
- the database is installed, and the page exists and the user may view it (`PageResolver`),
- the template of the page has a Razor layout here, and every section on it has a module view here.

Everything else goes to the WebForms site as before: pages with modules that haven't moved, error pages (not found,
forbidden), redirects of external links, the installer.

`PublicPageController` then renders the sections first (`Views/PublicPage/Index.cshtml`, `SectionRenderer`: module
views, the output cache per section, the section block with the title and edit links), and the template last, as the
layout, so the head of the page has the stylesheets and scripts that modules register.

- **Templates** are Razor layouts in `Templates/`, with the path of the user control of the WebForms site
  (`Templates/AnotherRed/Cuyahoga.ascx` -> `Templates/AnotherRed/Cuyahoga.cshtml`), so the database doesn't change.
  They use `<cuyahoga-head-assets />`, `<cuyahoga-placeholder id="..." />` and `<vc:hierarchical-menu />`. The
  stylesheets and images stay in the WebForms site. Moved: Another Red and Offline.
- **Module views** derive from `ModuleViewComponent<TModule>` (`Cuyahoga.Web.Mvc`) and return their views by full path
  (`~/Modules/{Module}/...`). `GetText` reads the texts of the module from `{namespace}.Resources.Strings`. The views of
  the built-in modules are in `Cuyahoga.Modules.Mvc`, the ones of the other modules in their own project
  (`Cuyahoga.Modules.Downloads.Mvc`, `Cuyahoga.Modules.Articles.Mvc`, `Cuyahoga.Modules.RemoteContent.Mvc`). Moved:
  all modules (StaticHtml, Sitemap, Search, SearchInput, LanguageSwitcher, User, UserProfile, Downloads, Articles,
  RemoteContent). Their admin pages are still WebForms pages.
  `UrlPager` pages a list like the Pager control of the WebForms site (`.../page/2` or `...?page=2`).
- **Module controllers** derive from `ModuleController<TModule>` and are named after the module
  (`DownloadsController`). When the section in the url ({id}/section.aspx/...) has a module with a controller, its
  Index action gets the request and returns any result: a file download, a redirect after a posted form, or
  `PublicPageView()` for the page. The module of a section is one instance per request (`IPublicPageModules`),
  shared by the controller and the view. Forms of modules post to their section url, with their own `<form>` (the
  templates have no form around the page). `InvalidateCache()` clears the cached output of the section.
- Forms that work on any page (a search box, the language list) use GET to a url instead of a postback: the search
  boxes go to `{search section}/Search?q=...`, the language list to the current page with `?culture=...`. A module view
  can send the visitor elsewhere with `PublicPage.Redirect(url)` (the page then answers with a redirect); views that do
  that, or set cookies, have `[NoSectionCache]`.
- **Login**: the login form of the User module posts to `UserLogin.axd` of the WebForms site (only that site can create
  its Forms cookie), which logs in or out and goes back to the page (`?loginerror=...` when the login failed, so the
  form is empty again: the WebForms site kept the username).
- `sitemap.*` urls in any folder are the XML sitemap of the WebForms site (its handler comes before the page urls).
- **RSS feeds** of sections with a syndicatable module (`{id}/feed.aspx`, with the path info of the module) come from
  `FeedController`, the same XML as `Rss.aspx`, cached with the cache duration of the section.
- The reasons why a public page url goes to the WebForms site are logged at the Debug level
  (`Logging__LogLevel__Cuyahoga.Web.Core.PublicSite=Debug`).
- **Offline pages**: a page is offline when it or a page above it is offline (the home page: the whole site).
  Visitors get the offline template of the site without sections (or the template of the page without sections when
  the site has no offline template); administrators see the page as usual.
- **Differences with the WebForms site**, besides 404/403 instead of the error page: an article opens only in its
  own section and only while online (or archived, when the section shows the archive), so a category lists the
  articles of its section only; article titles and summaries are encoded (they are text, the content stays html),
  like the titles, summaries and paths of search results; the RSS feed of a section is only for users who may view
  the section (the WebForms site gave it to anyone).
- The output cache of sections is in this process: changes on the WebForms site (module admin pages) don't clear it,
  so cached sections show changes after their cache duration. Posted forms and urls with a query string (a search)
  are never served from the cache (the WebForms site served the results of the first search url for all queries).

## Login

Users log in and out on the WebForms site (Forms authentication). This site can't read that cookie, so the admin pages
ask the WebForms site who is logged in: `LegacyAuthenticationHandler` calls `CurrentUser.axd` with the cookies of the
browser and loads that user. Only admin pages and public page urls make this call (and only with the Forms cookie);
other forwarded requests don't. A renewed Forms
cookie in the answer (sliding expiration) is passed on to the browser.

## Template placeholders

The templates in the database are still WebForms user controls for the pages that the WebForms site renders, which this
site can't load. Pages that list the placeholders of a template ask
the WebForms site (`TemplatePlaceholders.axd`, administrators only) through `TemplatePlaceholderProvider`. Both calls go
through `LegacySiteClient`, which sends the cookies of the browser along.

## Modules

This site references the module assemblies (the netstandard2.0 module classes, without their WebForms pages) and
activates the modules that are activated in the database before every page (`ModuleActivation`), so modules that are
activated on the WebForms site are picked up too. A module that can't be loaded is logged and skipped.

## The folder of the WebForms site

Some things live in the folder of the WebForms site: the full-text index (`~/index`), uploaded files (`~/files`) and
the template files, which the template pages list, and the template stylesheets, which they edit (so this site needs
write access to `Templates`).
`LegacySitePath` (absolute, or relative to this site) tells this site where that folder is, for
`ApplicationPaths.MapPath`. Both sites write to the same full-text index; Lucene's lock keeps it consistent, but when
both write at the same moment, one of them fails after waiting for the lock.

## Notes

- Both sites must use the same database. NHibernate's second-level cache is off, because an in-memory cache in one
  site would serve data that the other site has changed.
- The WebForms site builds absolute urls from the `X-Forwarded-Host` and `X-Forwarded-Proto` headers that the
  forwarder sets. Make sure the WebForms site is only reachable through this site.
- The Cuyahoga core services live in a Windsor container (modules are registered while the site runs) and are
  available for constructor injection (`AddCuyahogaCoreServices`).
