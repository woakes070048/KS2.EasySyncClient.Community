/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using System.Data;

#if __MonoCS__
using System.IO;
using Mono.Data.Sqlite;
#else
using System.Data.SQLite;
using Alphaleonis.Win32.Filesystem;
#endif

namespace KS2.EasySync.Core
{
    public class SQLiteHelper
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

#if __MonoCS__
        private Mono.Data.Sqlite.SqliteConnection _SqlConnection;
        private Mono.Data.Sqlite.SqliteCommand _SqlCommand;
        private Mono.Data.Sqlite.SqliteTransaction _SqlTransaction;
        private List<Mono.Data.Sqlite.SqliteParameter> _OutputParameters;
#else
        private System.Data.SQLite.SQLiteConnection _SqlConnection;
        private System.Data.SQLite.SQLiteCommand _SqlCommand;
        private System.Data.SQLite.SQLiteTransaction _SqlTransaction;
        private List<System.Data.SQLite.SQLiteParameter> _OutputParameters;
#endif
        
        private string _TransactionName;
        private string _CallingEntity;
        private List<SQLiteHelperParams> _CallingEntityParam;
        private Exception _LastException = null;

        /// <summary>
        /// Class Constructor
        /// </summary>
        /// <param name="pCallingEntity">The method that called the function (for logging purpose)</param>
        /// <param name="pCallingEntityParam">The list of the parameters of the method tht called the function (for logging purpose)</param>
        /// <param name="ConnectionString">An optional connectiong string to be used instead of the default one</param>
        public SQLiteHelper(string pCallingEntity, List<SQLiteHelperParams> pCallingEntityParam)
        {
            _CallingEntity = pCallingEntity;
            _CallingEntityParam = pCallingEntityParam;

#if __MonoCS__
			_SqlConnection = new Mono.Data.Sqlite.SqliteConnection(Globals._GlbConnectionString);
            _SqlCommand = new Mono.Data.Sqlite.SqliteCommand(String.Empty);
            _OutputParameters = new List<Mono.Data.Sqlite.SqliteParameter>();
#else
            _SqlConnection = new System.Data.SQLite.SQLiteConnection(Globals.GlbConnectionString);
            _SqlCommand = new System.Data.SQLite.SQLiteCommand(String.Empty);
            _OutputParameters = new List<System.Data.SQLite.SQLiteParameter>();
#endif
        }

        private void LogFault(string pMainMessage, Exception pex, bool pLogQueryDetails)
        {
            this._LastException = pex;

            logger.Error("-----");

            if (pex == null) logger.Error(String.Format("Method {0}. {1}", _CallingEntity, pMainMessage));
            else logger.Error(String.Format("Method {0}. {1}. Exception : {2}", _CallingEntity, pMainMessage, pex.Message));

            if (_CallingEntityParam != null)
            {
                for (int i = 0; i < _CallingEntityParam.Count; i++)
                {
                    logger.Error(String.Format("Method {0}. Param {1} - Type {2} - Value {3}", _CallingEntity, _CallingEntityParam[i]._ParamName, _CallingEntityParam[i]._ParamType, _CallingEntityParam[i]._ParamValue));
                }
            }
            if (pLogQueryDetails)
            {
                if (_SqlCommand != null)
                {
                    for (int i = 0; i < _SqlCommand.Parameters.Count; i++)
                    {
                        if (_SqlCommand.Parameters[i].Direction == ParameterDirection.Input)
                        {
                            logger.Error(String.Format("Method {0}. Query Param {1} - Type {2} - Value {3}", _CallingEntity, _SqlCommand.Parameters[i].ParameterName, _SqlCommand.Parameters[i].DbType.ToString(), _SqlCommand.Parameters[i].Value == null ? "null" : _SqlCommand.Parameters[i].Value.ToString()));
                        }
                    }
                }
            }
            logger.Error("-----");
        }

        public void SetCommandTimeOut(Int32 newTimeOutSec)
        {
            _SqlCommand.CommandTimeout = newTimeOutSec;
        }

        public bool InitConnection()
        {
            try
            {
                _SqlConnection.Open();
                _SqlCommand.Connection = _SqlConnection;
                return true;
            }
            catch (Exception ex)
            {
                LogFault("Database connection failed.", ex, false);
                return false;
            }
        }

        public bool TransactionInit()
        {
            if (_SqlConnection.State != System.Data.ConnectionState.Open || _SqlCommand.Connection == null)
            {
                throw new Exception("Connection not initialised");
            }
            _TransactionName = Guid.NewGuid().ToString();

            try
            {
                _SqlTransaction = _SqlConnection.BeginTransaction(IsolationLevel.Snapshot); //http://msdn.microsoft.com/fr-fr/library/ms173763.aspx
            }
            catch (Exception ex)
            {
                LogFault("Transaction Init failed.", ex, false);
                return false;
            }
            _SqlCommand.Transaction = _SqlTransaction;

            return true;
        }

        public bool TransactionCommit()
        {
            if (_SqlConnection.State != System.Data.ConnectionState.Open || _SqlCommand.Connection == null || _SqlTransaction == null)
            {
                throw new Exception("Connection or transaction not initialised");
            }

            try
            {
                _SqlTransaction.Commit();
            }
            catch (Exception ex)
            {
                LogFault("Transaction Commit failed.", ex, false);
            }

            _SqlCommand.Transaction = null;

            try
            {
                _SqlTransaction.Dispose();
            }
            catch
            {
            }
            _SqlTransaction = null;

            return true;
        }

        public bool TransactionRollBack()
        {
            if (_SqlConnection.State != System.Data.ConnectionState.Open || _SqlCommand.Connection == null || _SqlTransaction == null)
            {
                throw new Exception("Connection or transaction not initialised");
            }

            try
            {
                _SqlTransaction.Rollback();
            }
            catch (Exception ex)
            {
                LogFault("Transaction Rollback failed.", ex, false);
            }

            _SqlCommand.Transaction = null;

            try
            {
                _SqlTransaction.Dispose();
            }
            catch
            {
            }

            _SqlTransaction = null;

            return true;
        }

        public void SetCommandText(string pCommandText)
        {
            if (_SqlConnection.State != System.Data.ConnectionState.Open || _SqlCommand.Connection == null)
            {
                throw new Exception("Connection not initialised");
            }

            _SqlCommand.Parameters.Clear();
#if __MonoCS__
            _OutputParameters = new List<Mono.Data.Sqlite.SqliteParameter>();
#else
            _OutputParameters = new List<System.Data.SQLite.SQLiteParameter>();
#endif
            _SqlCommand.CommandText = pCommandText;
        }

        public void AppendCommandText(string pCommandText)
        {
            if (_SqlConnection.State != System.Data.ConnectionState.Open || _SqlCommand.Connection == null)
            {
                throw new Exception("Connection not initialised");
            }
            _SqlCommand.CommandText += pCommandText;
        }


        public void SetCommandParameter(String Name, DbType Type, int? Length, object pValue)
        {
            if (_SqlConnection.State != System.Data.ConnectionState.Open || _SqlCommand.Connection == null)
            {
                throw new Exception("Connection not initialised");
            }

#if __MonoCS__
            Mono.Data.Sqlite.SqliteParameter oTempSqlParameter;
            if (Length.HasValue) oTempSqlParameter = new SqliteParameter(Name, Type,Length.Value);
			else oTempSqlParameter = new SqliteParameter(Name, Type);
#else
            System.Data.SQLite.SQLiteParameter oTempSqlParameter;
            if (Length.HasValue) oTempSqlParameter = new SQLiteParameter(Name, Type,Length.Value);
            else oTempSqlParameter = new SQLiteParameter(Name, Type);
#endif
            oTempSqlParameter.Value = pValue;
            _SqlCommand.Parameters.Add(oTempSqlParameter);
        }


        public void SetCommandParameterOut(string pParameterName, System.Data.DbType pDBType)
        {
            if (_SqlConnection.State != System.Data.ConnectionState.Open || _SqlCommand.Connection == null)
            {
                throw new Exception("Connection not initialised");
            }

#if __MonoCS__
			Mono.Data.Sqlite.SqliteParameter oTempSqlParameter = new SqliteParameter(pParameterName, pDBType);
#else
            System.Data.SQLite.SQLiteParameter oTempSqlParameter = new SQLiteParameter(pParameterName, pDBType);
#endif
            oTempSqlParameter.Direction = System.Data.ParameterDirection.Output;
            _SqlCommand.Parameters.Add(oTempSqlParameter);
            _OutputParameters.Add(oTempSqlParameter);
        }

        public object GetOutputParameterValue(string ParameterName)
        {
            return _OutputParameters.Find((x) => x.ParameterName.Equals(ParameterName)).Value;
        }

        public bool ExecuteScalar(ref object pQueryResult, bool bKillConnectionOnError = false)
        {
            if (_SqlConnection.State != System.Data.ConnectionState.Open || _SqlCommand.Connection == null)
            {
                throw new Exception("Connection not initialised");
            }

            try
            {
                pQueryResult = _SqlCommand.ExecuteScalar();
                return true;
            }
            catch (Exception ex)
            {
                pQueryResult = null;

                LogFault(String.Format("Query execution failed {0}.", _SqlCommand.CommandText), ex, true);
                if (bKillConnectionOnError) Dispose();

                return false;
            }
        }

#if __MonoCS__
        public Mono.Data.Sqlite.SqliteDataReader ExecuteReader(bool bKillConnectionOnError = false)
        {
            if (_SqlConnection.State != System.Data.ConnectionState.Open || _SqlCommand.Connection == null)
            {
                throw new Exception("Connection not initialised");
            }

            try
            {
                return _SqlCommand.ExecuteReader();
            }
            catch (Exception ex)
            {
                LogFault(String.Format("Query execution failed {0}.", _SqlCommand.CommandText), ex, true);
                if (bKillConnectionOnError) Dispose();

                return null;
            }
        }
#else
        public System.Data.SQLite.SQLiteDataReader ExecuteReader(bool bKillConnectionOnError = false)
        {
            if (_SqlConnection.State != System.Data.ConnectionState.Open || _SqlCommand.Connection == null)
            {
                throw new Exception("Connection not initialised");
            }

            try
            {
                return _SqlCommand.ExecuteReader();
            }
            catch (Exception ex)
            {
                LogFault(String.Format("Query execution failed {0}.", _SqlCommand.CommandText), ex, true);
                if (bKillConnectionOnError) Dispose();

                return null;
            }
        }
#endif


        public bool ExecuteNonQuery(bool bKillConnectionOnError = false)
        {
            if (_SqlConnection.State != System.Data.ConnectionState.Open || _SqlCommand.Connection == null)
            {
                throw new Exception("Connection not initialised");
            }

            try
            {
                _SqlCommand.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                LogFault(String.Format("Query execution failed {0}.", _SqlCommand.CommandText), ex, true);
                if (bKillConnectionOnError) Dispose();

                return false;
            }
        }

        /// <summary>
        /// Log that the query didn't return any row. This is usefull when the query should have returned rows and that we need to log this fact
        /// </summary>
        /// <param name="bKillConnectionOnError"></param>
        public void LogEmptyResult(bool bKillConnectionOnError = false)
        {
            LogFault(String.Format(" Query execution returned no result {0}.", _SqlCommand.CommandText), null, true);
            if (bKillConnectionOnError) Dispose();
        }

        public Exception RetrieveLastException()
        {
            return _LastException;
        }

        ~SQLiteHelper()
        {
            try
            {
                if (_SqlCommand != null) _SqlCommand.Dispose();
                if (_SqlConnection != null) _SqlConnection.Dispose();
            }
            catch { }
        }

        public void Dispose()
        {
            try 
            {
                if (_SqlTransaction != null)
                {
                    _SqlTransaction.Commit();
                    _SqlTransaction.Dispose();
                }
                if (_SqlCommand != null) _SqlCommand.Dispose();
                if (_SqlConnection != null) _SqlConnection.Dispose();
            }
            catch { }
        }
    }

    public class SQLiteHelperParams
    {
        public string _ParamName;
        public string _ParamType;
        public string _ParamValue;

        public SQLiteHelperParams(string pParamName, string pParamType, string pParamValue)
        {
            this._ParamName = pParamName;
            this._ParamType = pParamType;
            this._ParamValue = pParamValue;
        }
    }
}