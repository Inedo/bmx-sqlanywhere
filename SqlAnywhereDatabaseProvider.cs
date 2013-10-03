using System;
using System.Data;
using System.IO;
using System.Text;
using iAnywhere.Data.SQLAnywhere;
using Inedo.BuildMaster.Diagnostics;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Extensibility.Providers.Database;
using Inedo.BuildMaster.Web;

namespace Inedo.BuildMasterExtensions.SqlAnywhere
{
    [ProviderProperties("SQL Anywhere", "Connects to a SQL Anywhere database and provides change script functionality.")]
    [CustomEditor(typeof(SqlAnywhereDatabaseProviderEditor))]
    public sealed class SqlAnywhereDatabaseProvider : DatabaseProviderBase, IChangeScriptProvider
    {
        private static string initErrorMessage;

        static SqlAnywhereDatabaseProvider()
        {
            if (!string.IsNullOrEmpty(initErrorMessage))
                throw new InvalidOperationException(initErrorMessage);

            BadUI.WindowBlocker.ShowWindow += WindowBlocker_ShowWindow;

            try
            {
                BadUI.WindowBlocker.DisableWindowCreation();

                try
                {
                    new SAConnection();
                }
                finally
                {
                    BadUI.WindowBlocker.EnableWindowCreation();
                }
            }
            finally
            {
                BadUI.WindowBlocker.ShowWindow -= WindowBlocker_ShowWindow;
            }

            if (!string.IsNullOrEmpty(initErrorMessage))
                throw new InvalidOperationException(initErrorMessage);
        }

        public SqlAnywhereDatabaseProvider()
        {
            var dblgen12_dll = IntPtr.Size == 4
                ? Properties.Resources.dblgen12_dll_x86
                : Properties.Resources.dblgen12_dll_x64;

            var path = Path.Combine(Path.GetTempPath(), "dblgen12.dll");

            Tracer.Debug("Ensuring " + path);

            if (!File.Exists(path))
                File.WriteAllBytes(path, dblgen12_dll);
        }

        public override bool IsAvailable()
        {
            return true;
        }
        public override void ValidateConnection()
        {
            this.ExecuteQuery("select 1");
        }
        public override string ToString()
        {
            BadUI.WindowBlocker.DisableWindowCreation();
            try
            {
                var csb = new SAConnectionStringBuilder(this.ConnectionString);
                var toString = new StringBuilder();
                if (!string.IsNullOrEmpty(csb.DatabaseName))
                    toString.Append("SQL Anywhere database \"" + csb.DatabaseName + "\"");
                else
                    toString.Append("SQL Anywhere");

                if (!string.IsNullOrEmpty(csb.Host))
                    toString.Append(" on host \"" + csb.Host + "\"");

                return toString.ToString();
            }
            catch
            {
                return "SQL Anywhere";
            }
            finally
            {
                BadUI.WindowBlocker.EnableWindowCreation();
            }
        }
        public override void ExecuteQueries(string[] queries)
        {
            BadUI.WindowBlocker.DisableWindowCreation();
            try
            {
                using (var cmd = CreateCommand(string.Empty))
                {
                    try
                    {
                        cmd.Connection.Open();
                        foreach (var sqlCommand in queries)
                        {
                            cmd.CommandText = sqlCommand;
                            cmd.ExecuteNonQuery();
                        }
                    }
                    finally
                    {
                        cmd.Connection.Close();
                    }
                }
            }
            finally
            {
                BadUI.WindowBlocker.EnableWindowCreation();
            }
        }
        public override void ExecuteQuery(string query)
        {
            ExecuteQueries(new string[] { query });
        }

        public void InitializeDatabase()
        {
            if (this.IsDatabaseInitialized())
                throw new InvalidOperationException("The database has already been initialized.");

            this.ExecuteQuery(Properties.Resources.Initialize);
        }
        public bool IsDatabaseInitialized()
        {
            this.ValidateConnection();

            var tables = this.ExecuteDataTable("select 1 from sysobjects where type='U' AND name='__buildmaster_dbschemachanges'");
            return tables.Rows.Count != 0;
        }
        public ChangeScript[] GetChangeHistory()
        {
            this.ValidateInitialization();

            var tables = this.ExecuteDataTable("select * from __buildmaster_dbschemachanges");
            var scripts = new ChangeScript[tables.Rows.Count];
            for (int i = 0; i < tables.Rows.Count; i++)
                scripts[i] = new SqlAnywhereChangeScript(tables.Rows[i]);

            return scripts;
        }
        public long GetSchemaVersion()
        {
            this.ValidateInitialization();

            return (long)this.ExecuteDataTable(
                "select coalesce(max(numeric_release_number),0) from __buildmaster_dbschemachanges"
                ).Rows[0][0];
        }
        public ExecutionResult ExecuteChangeScript(long numericReleaseNumber, int scriptId, string scriptName, string scriptText)
        {
            this.ValidateInitialization();

            var tables = this.ExecuteDataTable("select * from __buildmaster_dbschemachanges");
            if (tables.Select("Script_Id=" + scriptId.ToString()).Length > 0)
                return new ExecutionResult(ExecutionResult.Results.Skipped, scriptName + " already executed.");

            Exception ex = null;
            try { this.ExecuteQuery(scriptText); }
            catch (Exception _ex) { ex = _ex; }

            this.ExecuteQuery(string.Format(
                "insert into __buildmaster_dbschemachanges "
                + " (numeric_release_number, script_id, script_name, executed_date, success_indicator) "
                + "values "
                + "({0}, {1}, '{2}', now(), '{3}')",
                numericReleaseNumber,
                scriptId,
                scriptName.Replace("'", "''"),
                ex == null ? "Y" : "N"));

            if (ex == null)
                return new ExecutionResult(ExecutionResult.Results.Success, scriptName + " executed successfully.");
            else
                return new ExecutionResult(ExecutionResult.Results.Failed, scriptName + " execution failed:" + ex.Message);
        }

        private IDbConnection CreateConnection()
        {
            var conStr = new SAConnectionStringBuilder(this.ConnectionString) { Pooling = false };
            return new SAConnection(conStr.ToString());
        }
        private IDbCommand CreateCommand(string cmdText)
        {
            return new SACommand
            {
                CommandTimeout = 0,
                CommandText = cmdText,
                Connection = (SAConnection)this.CreateConnection()
            };
        }
        private DataTable ExecuteDataTable(string sqlCommand)
        {
            BadUI.WindowBlocker.DisableWindowCreation();
            try
            {
                var dt = new DataTable();
                using (var cmd = this.CreateCommand(string.Empty))
                {
                    try
                    {
                        cmd.Connection.Open();
                        cmd.CommandText = sqlCommand;
                        dt.Load(cmd.ExecuteReader());
                        return dt;
                    }
                    finally
                    {
                        cmd.Connection.Close();
                    }
                }
            }
            finally
            {
                BadUI.WindowBlocker.EnableWindowCreation();
            }
        }
        private void ValidateInitialization()
        {
            if (!this.IsDatabaseInitialized())
                throw new InvalidOperationException("The database has not been initialized.");
        }

        private static void WindowBlocker_ShowWindow(object sender, BadUI.ShowWindowEventArgs e)
        {
            initErrorMessage = string.Format("{0}: {1}", e.Title, e.Message);
        }
    }
}
