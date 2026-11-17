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
3. Start this project (http://localhost:5080). `LegacySiteUrl` in `appsettings.Development.json` points to the
   WebForms site.

The e2e tests (`e2e`) run both sites: the WebForms site on port 8082 and this site in front of it on port 8081.

## Migrated pages

| Page | Since |
|---|---|
| `/Admin/Users.aspx`, `/Admin/UserEdit.aspx`, `/Admin/Roles.aspx`, `/Admin/RoleEdit.aspx` | step 3 |

The pages keep their urls and the markup of the WebForms pages (`Pages/Shared/_AdminLayout.cshtml` is
`AdminTemplate.ascx`), so links between old and new pages keep working and the admin looks the same.

## Login

Users log in and out on the WebForms site (Forms authentication). This site can't read that cookie, so the admin pages
ask the WebForms site who is logged in: `LegacyAuthenticationHandler` calls `CurrentUser.axd` with the cookies of the
browser and loads that user. Only pages that need a login make this call; forwarded requests don't. A renewed Forms
cookie in the answer (sliding expiration) is passed on to the browser.

## Notes

- Both sites must use the same database. NHibernate's second-level cache is off, because an in-memory cache in one
  site would serve data that the other site has changed.
- The WebForms site builds absolute urls from the `X-Forwarded-Host` and `X-Forwarded-Proto` headers that the
  forwarder sets. Make sure the WebForms site is only reachable through this site.
- The Cuyahoga core services live in a Windsor container (modules are registered while the site runs) and are
  available for constructor injection (`AddCuyahogaCoreServices`).
