// Databases for the e2e tests. Each provider creates an empty database and returns the Cuyahoga settings
// (passed to the site as CUYAHOGA_* environment variables, which override the appSettings in Web.config).
import { execFileSync } from 'node:child_process';

const databaseName = process.env.CUYAHOGA_E2E_DATABASE_NAME ?? 'Cuyahoga_e2e';

const providers = {
  // SQL Server LocalDB, which comes with Visual Studio and SQL Server Express.
  mssql: {
    recreate() {
      const server = process.env.CUYAHOGA_E2E_MSSQL_SERVER ?? '(localdb)\\MSSQLLocalDB';
      execFileSync('sqlcmd', ['-S', server, '-E', '-b', '-Q',
        `IF DB_ID('${databaseName}') IS NOT NULL BEGIN ` +
        `ALTER DATABASE [${databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${databaseName}]; END; ` +
        `CREATE DATABASE [${databaseName}];`], { stdio: 'inherit' });
      return {
        CUYAHOGA_CONNECTIONSTRING: `Server=${siteServerName(server)};Database=${databaseName};Integrated Security=true`,
        CUYAHOGA_NHIBERNATEDRIVER: 'NHibernate.Driver.SqlClientDriver',
        CUYAHOGA_NHIBERNATEDIALECT: 'NHibernate.Dialect.MsSql2005Dialect',
      };
    },
  },
};

/**
 * The server name for the site. A '(localdb)\instance' name makes SqlClient load the native LocalDB API, which
 * fails in IIS Express on this setup ("%1 is not a valid Win32 application"). The named pipe of the instance
 * works everywhere.
 */
function siteServerName(server) {
  const localDb = /^\(localdb\)\\(.+)$/i.exec(server);
  if (!localDb) {
    return server;
  }
  const instance = localDb[1];
  execFileSync('SqlLocalDB', ['start', instance], { stdio: 'ignore' });
  const info = execFileSync('SqlLocalDB', ['info', instance], { encoding: 'utf8' });
  const pipe = /Instance pipe name:\s*(\S+)/i.exec(info);
  if (!pipe) {
    throw new Error(`No pipe name for LocalDB instance ${instance}:\n${info}`);
  }
  return pipe[1];
}

/** The database type of this test run (CUYAHOGA_E2E_DB, default mssql). */
export function databaseType() {
  return process.env.CUYAHOGA_E2E_DB ?? 'mssql';
}

/** Drops and creates the test database and returns the environment variables for the site. */
export function recreateDatabase() {
  const type = databaseType();
  const provider = providers[type];
  if (!provider) {
    throw new Error(`Unknown CUYAHOGA_E2E_DB '${type}'. Supported: ${Object.keys(providers).join(', ')}.`);
  }
  console.log(`[e2e] Creating empty ${type} database ${databaseName}`);
  return provider.recreate();
}
