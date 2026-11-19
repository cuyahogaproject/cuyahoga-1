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

The pages keep their urls and the markup of the WebForms pages (`Pages/Shared/_AdminLayout.cshtml` is
`AdminTemplate.ascx`), so links between old and new pages keep working and the admin looks the same.

## Login

Users log in and out on the WebForms site (Forms authentication). This site can't read that cookie, so the admin pages
ask the WebForms site who is logged in: `LegacyAuthenticationHandler` calls `CurrentUser.axd` with the cookies of the
browser and loads that user. Only pages that need a login make this call; forwarded requests don't. A renewed Forms
cookie in the answer (sliding expiration) is passed on to the browser.

## Template placeholders

Templates are WebForms user controls, which this site can't load. Pages that list the placeholders of a template ask
the WebForms site (`TemplatePlaceholders.axd`, administrators only) through `TemplatePlaceholderProvider`. Both calls go
through `LegacySiteClient`, which sends the cookies of the browser along.

## Modules

This site references the module assemblies (the netstandard2.0 module classes, without their WebForms pages) and
activates the modules that are activated in the database before every page (`ModuleActivation`), so modules that are
activated on the WebForms site are picked up too. A module that can't be loaded is logged and skipped.

## The folder of the WebForms site

Some things live in the folder of the WebForms site: the full-text index (`~/index`) and uploaded files (`~/files`).
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
