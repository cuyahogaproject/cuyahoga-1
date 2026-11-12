// Databases for the e2e tests. Each provider creates an empty database and returns the Cuyahoga settings
// (passed to the site as CUYAHOGA_* environment variables, which override the appSettings in Web.config).
import { execFileSync } from 'node:child_process';
import mysql from 'mysql2/promise';
import pg from 'pg';

const databaseName = process.env.CUYAHOGA_E2E_DATABASE_NAME ?? 'cuyahoga_e2e';

function setting(name, defaultValue) {
  const value = process.env[name] ?? defaultValue;
  if (value === undefined) {
    throw new Error(`${name} is not set (see e2e/.env.example).`);
  }
  return value;
}

const providers = {
  // SQL Server LocalDB, which comes with Visual Studio and SQL Server Express.
  async mssql() {
    const server = setting('CUYAHOGA_E2E_MSSQL_SERVER', '(localdb)\\MSSQLLocalDB');
    execFileSync('sqlcmd', ['-S', server, '-E', '-b', '-Q',
      `IF DB_ID('${databaseName}') IS NOT NULL BEGIN ` +
      `ALTER DATABASE [${databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${databaseName}]; END; ` +
      `CREATE DATABASE [${databaseName}];`], { stdio: 'inherit' });
    return {
      CUYAHOGA_CONNECTIONSTRING: `Server=${sqlServerName(server)};Database=${databaseName};Integrated Security=true`,
      CUYAHOGA_NHIBERNATEDRIVER: 'NHibernate.Driver.SqlClientDriver',
      CUYAHOGA_NHIBERNATEDIALECT: 'NHibernate.Dialect.MsSql2005Dialect',
    };
  },

  async mysql() {
    // MySQL55Dialect: Cuyahoga's MySQL tables use DATETIME (whole seconds) for the version timestamps; the
    // MySQL57+ dialects assume fractional seconds, so optimistic locking would never match.
    const host = setting('CUYAHOGA_E2E_MYSQL_HOST', 'localhost');
    const port = setting('CUYAHOGA_E2E_MYSQL_PORT', '3306');
    const user = setting('CUYAHOGA_E2E_MYSQL_USER');
    const password = setting('CUYAHOGA_E2E_MYSQL_PASSWORD');
    const connection = await mysql.createConnection({ host, port: Number(port), user, password });
    try {
      await connection.query(`DROP DATABASE IF EXISTS \`${databaseName}\``);
      await connection.query(`CREATE DATABASE \`${databaseName}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci`);
    } finally {
      await connection.end();
    }
    return {
      CUYAHOGA_CONNECTIONSTRING: `Server=${host};Port=${port};Database=${databaseName};User ID=${user};Password=${password};Allow User Variables=true`,
      CUYAHOGA_NHIBERNATEDRIVER: 'Cuyahoga.Core.Persistence.MySqlConnectorDriver, Cuyahoga.Core',
      CUYAHOGA_NHIBERNATEDIALECT: 'NHibernate.Dialect.MySQL55Dialect',
    };
  },

  async postgres() {
    const host = setting('CUYAHOGA_E2E_POSTGRES_HOST', 'localhost');
    const port = setting('CUYAHOGA_E2E_POSTGRES_PORT', '5432');
    const user = setting('CUYAHOGA_E2E_POSTGRES_USER');
    const password = setting('CUYAHOGA_E2E_POSTGRES_PASSWORD');
    const client = new pg.Client({ host, port: Number(port), user, password, database: 'postgres' });
    await client.connect();
    try {
      // WITH (FORCE) closes connections that a previous test site may have left open.
      await client.query(`DROP DATABASE IF EXISTS "${databaseName}" WITH (FORCE)`);
      await client.query(`CREATE DATABASE "${databaseName}" ENCODING 'UTF8'`);
    } finally {
      await client.end();
    }
    return {
      CUYAHOGA_CONNECTIONSTRING: `Host=${host};Port=${port};Database=${databaseName};Username=${user};Password=${password}`,
      CUYAHOGA_NHIBERNATEDRIVER: 'NHibernate.Driver.NpgsqlDriver',
      CUYAHOGA_NHIBERNATEDIALECT: 'NHibernate.Dialect.PostgreSQL83Dialect',
    };
  },
};

/**
 * The SQL Server name for the site. A '(localdb)\instance' name makes SqlClient load the native LocalDB API, which
 * fails in IIS Express on this setup ("%1 is not a valid Win32 application"). The named pipe of the instance
 * works everywhere.
 */
function sqlServerName(server) {
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
export async function recreateDatabase() {
  const type = databaseType();
  const provider = providers[type];
  if (!provider) {
    throw new Error(`Unknown CUYAHOGA_E2E_DB '${type}'. Supported: ${Object.keys(providers).join(', ')}.`);
  }
  console.log(`[e2e] Creating empty ${type} database ${databaseName}`);
  return provider();
}
