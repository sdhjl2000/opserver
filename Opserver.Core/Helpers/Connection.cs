﻿using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Profiling;
using StackExchange.Profiling.Data;

namespace StackExchange.Opserver.Helpers
{
    public class Connection
    {
        /// <summary>
        /// Gets an open READ UNCOMMITTED connection using the specified connection string, optionally timing out on the initial connect
        /// </summary>
        /// <param name="connectionString">The connection string to use for the connection</param>
        /// <param name="connectionTimeout">Milliseconds to wait to connect, optional</param>
        /// <returns>A READ UNCOMMITTED connection to the specified connection string</returns>
        public static DbConnection GetOpen(string connectionString, int? connectionTimeout = null)
        {
            var conn = new ProfiledDbConnection(new SqlConnection(connectionString), MiniProfiler.Current);
            Action<DbConnection> setReadUncommitted = c =>
                {
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.CommandText = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED";
                        cmd.ExecuteNonQueryAsync();
                    }
                };

            if (connectionTimeout.GetValueOrDefault(0) == 0)
            {
                conn.OpenAsync();
                setReadUncommitted(conn);
            }
            else
            {
                // In the case of remote monitoring, the timeout will be at the NIC level, not responding to traffic,
                // in that scenario, connection timeouts don't really do much, because they're never reached, the timeout happens
                // before their timer starts.  Because of that, we need to spin up our own overall timeout
                using (MiniProfiler.Current.Step("Opening Connection, Timeout: " + conn.ConnectionTimeout))
                try
                {
                    conn.Open();
                }
                catch (SqlException e)
                {
                    var csb = new SqlConnectionStringBuilder(connectionString);
                    var sqlException = string.Format("Error opening connection to {0} at {1} timeout was: {2} ms", csb.InitialCatalog, csb.DataSource, connectionTimeout.ToComma());
                    throw new Exception(sqlException, e);
                }
                setReadUncommitted(conn);
                if (conn.State == ConnectionState.Connecting)
                {
                    var b = new SqlConnectionStringBuilder { ConnectionString = connectionString };

                    throw new TimeoutException("Timeout expired connecting to " + b.InitialCatalog + " on " +
                                                b.DataSource + " on in the alloted " +
                                                connectionTimeout.Value.ToComma() + " ms");
                }
            }
            return conn;
        }

        /// <summary>
        /// Gets an open READ UNCOMMITTED connection using the specified connection string, optionally timing out on the initial connect
        /// </summary>
        /// <param name="connectionString">The connection string to use for the connection</param>
        /// <param name="connectionTimeout">Milliseconds to wait to connect, optional</param>
        /// <returns>A READ UNCOMMITTED connection to the specified connection string</returns>
        public static async Task<DbConnection> GetOpenAsync(string connectionString, int? connectionTimeout = null)
        {
            var conn = new ProfiledDbConnection(new SqlConnection(connectionString), MiniProfiler.Current);

            if (connectionTimeout.GetValueOrDefault(0) == 0)
            {
                await conn.OpenAsync();
                await SetReadUncommitted(conn);
            }
            else
            {
                // In the case of remote monitoring, the timeout will be at the NIC level, not responding to traffic,
                // in that scenario, connection timeouts don't really do much, because they're never reached, the timeout happens
                // before their timer starts.  Because of that, we need to spin up our own overall timeout
                using (MiniProfiler.Current.Step("Opening Connection, Timeout: " + conn.ConnectionTimeout))
                using (var tokenSource = new CancellationTokenSource())
                {
                    tokenSource.CancelAfter(connectionTimeout.Value);
                    try
                    {
                        await conn.OpenAsync(tokenSource.Token); // Throwing Null Refs
                    }
                    catch (SqlException e)
                    {
                        var csb = new SqlConnectionStringBuilder(connectionString);
                        var sqlException = string.Format("Error opening connection to {0} at {1} timeout was: {2} ms", csb.InitialCatalog, csb.DataSource, connectionTimeout.ToComma());
                        throw new Exception(sqlException, e);
                    }
                    await SetReadUncommitted(conn);
                    if (conn.State == ConnectionState.Connecting)
                    {
                        tokenSource.Cancel();
                        var b = new SqlConnectionStringBuilder {ConnectionString = connectionString};

                        throw new TimeoutException("Timeout expired connecting to " + b.InitialCatalog + " on " +
                                                    b.DataSource + " on in the alloted " +
                                                    connectionTimeout.Value.ToComma() + " ms");
                    }
                }
            }
            return conn;
        }

        private static async Task<int> SetReadUncommitted(DbConnection conn)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED";
                await cmd.ExecuteNonQueryAsync();
                return 1;
            }
        }
    }
}