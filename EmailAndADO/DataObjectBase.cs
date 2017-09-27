using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace EmailAndADO
{
    /// <summary>
    /// Clase base para los objectos DataObject (DO) de la capa DAL
    /// Contiene todos los procedimientos necesarios coomunes para interactuar con la BD
    /// </summary>
    public class DataObjectBase
    {
        protected int ErrorNumber = 0;
        protected int ErrorCode = 0;
        protected SqlDataReader reader;
        protected SqlConnection connection;
        private const int kSQLCommandTimeoutValue = 180;
        private const int kDeadlockRetryAttempts = 3;

        protected string GetConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings[dbParameters.ConnectionName].ConnectionString; }
        }

        protected void CloseConnection()
        {
            try { reader.Close(); }
            catch { }

            try { connection.Close(); }
            catch { }
        }

        //Logs internos de la clase para ingresar en la base de datos toda accion de los usuarios        
        #region InnerLogs

        /// <summary>
        /// Inserta un registro de mensaje de error en el Log
        /// Este método es llamado cuando ocurre un error en el DAL
        /// </summary>
        /// <param description="ErrorMessage">String con el detalle del error que ocurrió en la DAL</param>
        /// <param description="User_ID">Identificador único del usuario loggeado cuando ocurrió el error</param>
        public void InsertLog(string ErrorMessage, int idOrigin = 0)
        {
            CloseConnection();

            new LogDO().InsertErrorLog(ErrorMessage, idOrigin);
        }

        /// <summary>
        /// Inserta un registro de mensaje de error en el Log
        /// Este método es llamado cuando ocurre un error en el DAL
        /// </summary>
        /// <param description="ErrorMessage">String con el detalle del error que ocurrió en la DAL</param>
        /// <param description="User_ID">Identificador único del usuario loggeado cuando ocurrió el error</param>
        public void InsertDeadLockLog(int attempts, string StoredProcedure, string ErrorNumberName)
        {
            CloseConnection();

            if (attempts < kDeadlockRetryAttempts)
            {
                string tolog = string.Format("<{0}> tuvo un <{1}>, reintentando por <{2}> vez... SqlException Number: <{3}>.  SqlException Code: <{4}>", StoredProcedure, ErrorNumberName, attempts, ErrorNumber, ErrorCode);
                new LogDO().InsertDeadlockManagedLog(tolog, 5555);
            
                //Espera un segundo para reitentar el procedimiento...
                System.Threading.Thread.Sleep(1000);
            }
            else
            {
                string tolog = string.Format("<{0}> tuvo un <{1}>, se reintentó <{2}> veces sin éxito... SqlException Number: <{3}>.  SqlException Code: <{4}>", StoredProcedure, ErrorNumberName, attempts, ErrorNumber, ErrorCode);
                new LogDO().InsertErrorLog(tolog, 5555);

                attempts = 0;
            }
        }

        /// <summary>
        /// Inserta un registro de mensaje de transaccion en el curLog
        /// </summary>
        /// <param description="TransactionMessage">String con el detalle de la transaccion</param>
        /// <param description="idOrigin">Id del registro actualizado en la BD</param>
        /// <param description="UserInfo">Info del usuario loggeado cuando ocurrió el error</param>
        public void InsertTransactionLog(string TransactionMessage, int idOrigin)
        {
            CloseConnection();

            new LogDO().InsertDBTransactionLog(TransactionMessage, idOrigin);
        }

        /// <summary>
        /// Inserta un registro de mensaje de seleccion de registros en el curLog
        /// </summary>
        /// <param description="TransactionMessage">String con el detalle de la transaccion</param>
        /// <param description="User_ID">Identificador único del usuario loggeado cuando ocurrió el error</param>
        public void InsertSelectReturnedNullLog(string SelectMessage)
        {
            CloseConnection();

            new LogDO().InsertDBSelectValueReturnNullLog(SelectMessage);
        }

        #endregion InnerLogs

        private void DealWithSqlException(SqlException ex, string ProcedureName, ref int attempts, ref bool getOut)
        {
            ErrorNumber = ex.Number;
            ErrorCode = ex.ErrorCode;

            if (SQLExceptionNumbersReserved.ListSQLExceptionsNumbersToRepeatTransaction.Contains(ErrorNumber)
                || (ex.Message.Contains("eadlock")))
            {//Es DeadLock o Transport-Level error (para repetir)
                attempts++;

                InsertDeadLockLog(attempts, ProcedureName, SQLExceptionNumbersReserved.SQLNumberName(ErrorNumber));
            }
            else
            {
                getOut = true;

                if (ErrorNumber != SQLExceptionNumbersReserved.kSQLErrorNumberNoConnection) //Codigo: 121 (No hay conexión a la BD)
                {
                    if (ex.InnerException != null)
                    { InsertLog(ProcedureName + "; Msg:" + ex.Message + "; SQL: " + ex.InnerException.Message + ". SqlException Number: " + ErrorNumber.ToString() + ". SqlException Code: " + ErrorCode.ToString()); }
                    else
                    { InsertLog(ProcedureName + "; Msg:" + ex.Message + ". SqlException Number: " + ErrorNumber.ToString() + ". SqlException Code: " + ErrorCode.ToString()); }
                }
            }
        }

        private void DealWithException(Exception e, string ProcedureName, ref bool getOut)
        {
            getOut = true;

            ErrorNumber = -1;

            if (e.InnerException != null)
            { InsertLog(ProcedureName + "; Msg:" + e.Message + "; SQL: " + e.InnerException.Message); }
            else
            { InsertLog(ProcedureName + "; Msg:" + e.Message); }
        }

        /// <summary>
        /// Retorna la fecha y hora de BD
        /// </summary>
        public DateTime GetDateTimeNowFromFunction()
        {
            ErrorNumber = 0;
            DateTime toReturn = new DateTime(1900, 1, 1);

            int attempts = 0;
            bool getOut = false;

            while ((attempts < kDeadlockRetryAttempts) 
                && (!getOut))
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(GetConnectionString))
                    {
                        SqlCommand cmd = new SqlCommand("SELECT GETDATE()", conn);
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandTimeout = kSQLCommandTimeoutValue;

                        //Popula el objeto local llamando al Stored Procedure que retorna un object
                        conn.Open();
                        object oAux = cmd.ExecuteScalar();

                        //Si el sproc. retorna null, pero no hubo error este procedimiento retorna un cero 0
                        if ((oAux == null) || (oAux is DBNull))
                        { InsertSelectReturnedNullLog("Funcion: GETDATE() tuvo retorno NULL; controlado en el sistema valor en cero"); }
                        else
                        { toReturn = Convert.ToDateTime(oAux); }

                        getOut = true;
                    }
                }
                catch (SqlException ex)
                { DealWithSqlException(ex, "GetDateTimeNowFromFunction", ref attempts, ref getOut); }
                catch (Exception e)
                { DealWithException(e, "GetDateTimeNowFromFunction", ref getOut); }
            }

            if (toReturn.Year < 2000)
            { toReturn = DateTime.Now; }

            return toReturn;
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una lista de parámetros
        /// </summary>
        /// <param description="StoredProcedure">Nombre del SQL Stored Procedure</param>
        /// <param description="ParameterList">Lista de parametros a enviar a SQL</param>
        /// <param description="hasParameters">True si la lista enviada tiene parametros. False si la lista es NULL.</param>
        /// <param description="UserInfo">Informacion del Usuario</param>
        /// <returns>Retorna el id del registro isertado. Retorna 0 si no aplica. Al Error: NULL..</returns>
        private object getValue(string StoredProcedure, List<dbParameter> ParameterList, bool hasParameters)
        {
            ErrorNumber = 0;
            object oAux = (object)-1;

            int attempts = 0;
            bool getOut = false;

            while ((attempts < kDeadlockRetryAttempts)
                && (!getOut))
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(GetConnectionString))
                    {
                        SqlCommand cmd = new SqlCommand(StoredProcedure, conn);
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = kSQLCommandTimeoutValue;

                        if (hasParameters)
                        {
                            foreach (dbParameter currentParameter in ParameterList)
                            {
                                string paramName = currentParameter.Name;
                                cmd.Parameters.AddWithValue(paramName, currentParameter.Value);
                            }
                        }

                        //Popula el objeto local llamando al Stored Procedure que retorna un object
                        conn.Open();
                        oAux = cmd.ExecuteScalar();

                        //Si el sproc. retorna null, pero no hubo error este procedimiento retorna un cero 0
                        if ((oAux == null) || (oAux is DBNull))
                        {
                            InsertSelectReturnedNullLog("Stored procedure: " + StoredProcedure + " tuvo retorno NULL; controlado en el sistema valor en cero");
                            oAux = (object)0;
                        }

                        getOut = true;
                    }
                }
                catch (SqlException ex)
                { DealWithSqlException(ex, StoredProcedure, ref attempts, ref getOut); }
                catch (Exception e)
                { DealWithException(e, StoredProcedure, ref getOut); }
            }

            return oAux;
        }

        //Procedimientos que retornan un valor tipo "object"; derivados de getValue()
        #region getValue()

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una lista de parámetros
        /// </summary>
        /// <param description="StoredProcedure">Nombre del SQL Stored Procedure</param>
        /// <param description="ParameterList">Lista de parametros a enviar a SQL</param>
        /// <param description="UserInfo">Informacion del Usuario</param>
        /// <returns>Retorna el id del registro isertado. Retorna 0 si no aplica. Al Error: NULL..</returns>
        protected object getValue(string StoredProcedure, List<dbParameter> ParameterList)
        {
            //Envia un parametro en "true", que indica que el Stored Procedure espera parametros.
            return this.getValue(StoredProcedure, ParameterList, true);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una llave única
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="PrimaryKeyParameter">Parámetro único a enviar en el SP</param>
        /// <returns>Objecto obtenido de la BD</returns>
        protected object getValue(string StoredProcedure, dbParameter PrimaryKeyParameter)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(PrimaryKeyParameter);

            return this.getValue(StoredProcedure, parameterList, true);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una llave única
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="PrimaryKeyName">Nombre del parametro de llave única que recibe el SP</param>
        /// <param description="PrimaryKeyValue">Valor de la llave que recibe el SP</param>
        /// <returns>Un objeto resultado</returns>
        protected object getValue(string StoredProcedure, string PrimaryKeyName, int PrimaryKeyValue)
        {
            return getValue(StoredProcedure, new dbParameter(PrimaryKeyName, PrimaryKeyValue));
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Objecto obtenido de la BD</returns>
        protected object getValue(string StoredProcedure)
        {
            return this.getValue(StoredProcedure, null, false);
        }

        #endregion getValue()

        //Procedimientos que retornan un valor tipo "int"; que reciben de SQL con un getValue()
        #region getValueAsInt

        /// <summary>
        /// Ejecuta un SQL Stored Procedure que recibe una lista de parametros y retorna un entero
        /// </summary>
        /// <param description="StoredProcedure">Nombre del SQL Stored Procedure</param>
        /// <param description="ParameterList">Lista de Parametros que recibe el SQL Stored Procedure</param>
        /// <param description="hasParameters">True si la lista enviada tiene parametros. False si la lista es NULL.</param>
        /// <param description="UserInfo">Informacion del usuario</param>
        /// <returns>Entero obtenido de la BD. Retorna 0 si la consulta no retorna valores, y al Error: -1 </returns>
        private int getValueAsInt(string StoredProcedure, List<dbParameter> ParameterList, bool hasParameters)
        {
            object result = this.getValue(StoredProcedure, ParameterList, hasParameters);
            int valueToReturn = -1;

            if (result != null)
            { valueToReturn = Convert.ToInt32(result); }

            return valueToReturn;
        }

        /// <summary>
        /// Ejecuta un SQL Stored Procedure que recibe una lista de parametros y retorna un entero
        /// </summary>
        /// <param description="StoredProcedure">Nombre del SQL Stored Procedure</param>
        /// <param description="ParameterList">Lista de Parametros que recibe el SQL Stored Procedure</param>
        /// <param description="UserInfo">Informacion del usuario</param>
        /// <returns>Entero obtenido de la BD. Retorna 0 si la consulta no retorna valores, y al Error: -1 </returns>
        protected int getValueAsInt(string StoredProcedure, List<dbParameter> ParameterList)
        {
            return this.getValueAsInt(StoredProcedure, ParameterList, true);
        }
        /// <summary>
        /// Ejecuta un SQL Stored Procedure que recibe un parametro entero y retorna un entero
        /// </summary>
        /// <param description="StoredProcedure">Nombre del SQL Stored Procedure</param>
        /// <param description="TableKeyParameter">Parámetro único a enviar en el SP</param>
        /// <param description="UserInfo">Informacion del usuario</param>
        /// <returns>Entero obtenido de la BD. Retorna 0 si la consulta no retorna valores, y al Error: -1 </returns>
        protected int getValueAsInt(string StoredProcedure, dbParameter TableKeyParameter)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(TableKeyParameter);

            return this.getValueAsInt(StoredProcedure, parameterList, true);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una llave única
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="PrimaryKeyName">Nombre del parametro de llave única que recibe el SP</param>
        /// <param description="PrimaryKeyValue">Valor de la llave que recibe el SP</param>
        /// <returns>Entero obtenido de la BD</returns>
        protected int getValueAsInt(string StoredProcedure, string PrimaryKeyName, int PrimaryKeyValue)
        {
            return getValueAsInt(StoredProcedure, new dbParameter(PrimaryKeyName, PrimaryKeyValue));
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Entero obtenido de la BD</returns>
        protected int getValueAsInt(string StoredProcedure)
        {
            return this.getValueAsInt(StoredProcedure, null, false);
        }

        #endregion getValueAsInt

        //Procedimientos que retornan un valor tipo "bool"; que reciben de SQL con un getValue()
        #region getValueAsBool

        /// <summary>
        /// Ejecuta un stored procedure para recibir un bool, a partir de una lista de parámetros
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="ParameterList">Parámetros a enviar al SP</param>
        /// <param description="DefaultValueToReturn">Valor que retorna si la consulta no retorna nada o hay error</param>
        /// <param description="hasParameters">Espera true si el Stored Procedure espera parametros, false si la lista es NULL</param>
        /// <returns>Boolean obtenido de la BD</returns>
        private bool getValueAsBool(string StoredProcedure, List<dbParameter> ParameterList, bool DefaultValueToReturn, bool hasParameters)
        {
            object result = getValue(StoredProcedure, ParameterList, hasParameters);
            bool valueToReturn = DefaultValueToReturn;

            if (result != null)
            { valueToReturn = Convert.ToBoolean(result); }

            return valueToReturn;
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un bool, a partir de una lista de parámetros
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="PrimaryKeyParameter">Parámetro único a enviar en el SP</param>
        /// <param description="DefaultValueToReturn">Valor que retorna si la consulta no retorna nada o hay error</param>
        /// <returns>Boolean obtenido de la BD</returns>
        protected bool getValueAsBool(string StoredProcedure, dbParameter PrimaryKeyParameter, bool DefaultValueToReturn)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(PrimaryKeyParameter);

            return this.getValueAsBool(StoredProcedure, parameterList, DefaultValueToReturn, true);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una llave única
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="PrimaryKeyName">Nombre del parametro de llave única que recibe el SP</param>
        /// <param description="PrimaryKeyValue">Valor de la llave que recibe el SP</param>
        /// <returns>Boolean obtenido de la BD</returns>
        protected bool getValueAsBool(string StoredProcedure, string PrimaryKeyName, int PrimaryKeyValue, bool DefaultValueToReturn)
        {
            return getValueAsBool(StoredProcedure, new dbParameter(PrimaryKeyName, PrimaryKeyValue), DefaultValueToReturn);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Boolean obtenido de la BD</returns>
        protected bool getValueAsBool(string StoredProcedure, bool DefaultValueToReturn)
        {
            return this.getValueAsBool(StoredProcedure, null, DefaultValueToReturn, false);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Boolean obtenido de la BD</returns>
        protected bool getValueAsBool(string StoredProcedure)
        {
            return this.getValueAsBool(StoredProcedure, null, false, false);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una llave única
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="PrimaryKeyParameter">Parámetro único a enviar en el SP</param>
        /// <returns>Boolean obtenido de la BD</returns>
        protected bool getValueAsBool(string StoredProcedure, dbParameter PrimaryKeyParameter)
        {
            return getValueAsBool(StoredProcedure, PrimaryKeyParameter, false);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una lista de parámetros
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="ParameterList">Parámetros a enviar al SP</param>
        /// <returns>Boolean obtenido de la BD</returns>
        protected bool getValueAsBool(string StoredProcedure, List<dbParameter> ParameterList)
        {
            return this.getValueAsBool(StoredProcedure, ParameterList, false, true);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una lista de parámetros
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="ParameterList">Parámetros a enviar al SP</param>
        /// <returns>Boolean obtenido de la BD</returns>
        protected bool getValueAsBool(string StoredProcedure, List<dbParameter> ParameterList, bool DefaultValueToReturn)
        {
            return this.getValueAsBool(StoredProcedure, ParameterList, DefaultValueToReturn, true);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una llave única
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="PrimaryKeyName">Nombre del parametro de llave única que recibe el SP</param>
        /// <param description="PrimaryKeyValue">Valor de la llave que recibe el SP</param>
        /// <returns>Boolean obtenido de la BD</returns>
        protected bool getValueAsBool(string StoredProcedure, string PrimaryKeyName, int PrimaryKeyValue)
        {
            return getValueAsBool(StoredProcedure, PrimaryKeyName, PrimaryKeyValue, false);
        }

        #endregion getValueAsBool

        //Procedimientos que retornan un valor tipo "string"; que reciben de SQL con un getValue()
        #region getValueAsString

        /// <summary>
        /// Ejecuta un stored procedure que recibe una lista de parametros y retorna una cadena tipo "string"
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="ParameterList"></param>
        /// <param description="DefaultValue">Cadena que retorna el procedimiento si la consulta SQL no retorna valores</param>
        /// <param description="hasParameters">True si la lista enviada tiene parametros. False si la lista es NULL.</param>
        /// <param description="UserInfo">Informacion del usuario</param>
        /// <returns>Cadena tipo "string" obtenida de SQL</returns>
        private string getValueAsString(string StoredProcedure, List<dbParameter> ParameterList, string DefaultValue, bool hasParameters)
        {
            object result = this.getValue(StoredProcedure, ParameterList, hasParameters);

            string valueToReturn = DefaultValue;

            if (result != null)
            {
                valueToReturn = Convert.ToString(result);

                if (valueToReturn.Equals("0")) // Si la consulta viene vacia
                { valueToReturn = DefaultValue; }
            }

            return valueToReturn;
        }

        /// <summary>
        /// Ejecuta un stored procedure que recibe una lista de parametros y retorna una cadena tipo "string"
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="ParameterList"></param>
        /// <param description="DefaultValue">Cadena que retorna el procedimiento si la consulta SQL no retorna valores</param>
        /// <param description="UserInfo">Informacion del usuario</param>
        /// <returns>Cadena tipo "string" obtenida de SQL</returns>
        protected string getValueAsString(string StoredProcedure, List<dbParameter> ParameterList, string DefaultValue)
        {
            return this.getValueAsString(StoredProcedure, ParameterList, DefaultValue, true);
        }

        /// <summary>
        /// Ejecuta un stored procedure que recibe un parametro y retorna una cadena tipo "string"
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="PrimaryKeyParameter">Parámetro único a enviar en el SP</param>
        /// <param description="DefaultValue">Cadena que retorna el procedimiento si la consulta SQL no retorna valores</param>
        /// <param description="UserInfo">Informacion del usuario</param>
        /// <returns>Cadena obtenido de la BD</returns>
        protected string getValueAsString(string StoredProcedure, dbParameter PrimaryKeyParameter, string DefaultValue)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(PrimaryKeyParameter);

            return this.getValueAsString(StoredProcedure, parameterList, DefaultValue, true);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Cadena obtenido de la BD</returns>
        protected string getValueAsString(string StoredProcedure, string DefaultValue)
        {
            return this.getValueAsString(StoredProcedure, null, DefaultValue, false);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una llave única
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="PrimaryKeyParameter">Parámetro único a enviar en el SP</param>
        /// <returns>Cadena obtenido de la BD</returns>
        protected string getValueAsString(string StoredProcedure, dbParameter PrimaryKeyParameter)
        {
            return this.getValueAsString(StoredProcedure, PrimaryKeyParameter, "");
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una lista de parámetros
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="ParameterList">Parámetros a enviar al SP</param>
        /// <returns>Cadena obtenido de la BD</returns>
        protected string getValueAsString(string StoredProcedure, List<dbParameter> ParameterList)
        {
            return this.getValueAsString(StoredProcedure, ParameterList, "", true);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Cadena obtenido de la BD</returns>
        protected string getValueAsString(string StoredProcedure)
        {
            return this.getValueAsString(StoredProcedure, null, "", false);
        }

        #endregion getValueAsString

        //Procedimientos que retornan un valor tipo "DateTime"; que reciben de SQL con un getValue()
        #region getValueAsDateTime

        /// <summary>
        /// Ejecuta un stored procedure que recibe una lista de parametros y retorna un DateTime
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="ParameterList"></param>
        /// <param description="DefaultValue">DateTime que retorna el procedimiento si la consulta SQL no retorna valores</param>
        /// <param description="hasParameters">True si la lista enviada tiene parametros. False si la lista es NULL.</param>
        /// <param description="UserInfo">Informacion del usuario</param>
        /// <returns>DateTime obtenida de SQL</returns>
        private DateTime getValueAsDateTime(string StoredProcedure, List<dbParameter> ParameterList, DateTime DefaultValue, bool hasParameters)
        {
            object result = this.getValue(StoredProcedure, ParameterList, hasParameters);

            DateTime valueToReturn = new DateTime();

            if (result != null)// Si la consulta viene vacia
            {
                string resutlAsString = result.ToString();

                if (string.IsNullOrEmpty(resutlAsString) || resutlAsString.Equals("0") || resutlAsString.Equals("-1") || resutlAsString.Equals("N/A"))
                { valueToReturn = DefaultValue; }
                else
                { valueToReturn = Convert.ToDateTime(result); }
            }

            return valueToReturn;
        }

        /// <summary>
        /// Ejecuta un stored procedure que recibe una lista de parametros y retorna un DateTime
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="ParameterList"></param>
        /// <param description="DefaultValue">DateTime que retorna el procedimiento si la consulta SQL no retorna valores</param>
        /// <param description="UserInfo">Informacion del usuario</param>
        /// <returns>DateTime obtenida de SQL</returns>
        protected DateTime getValueAsDateTime(string StoredProcedure, List<dbParameter> ParameterList, DateTime DefaultValue)
        {
            return this.getValueAsDateTime(StoredProcedure, ParameterList, DefaultValue, true);
        }

        /// <summary>
        /// Ejecuta un stored procedure que recibe un parametro y retorna un DateTime
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="PrimaryKeyParameter">Parámetro único a enviar en el SP</param>
        /// <param description="DefaultValue">DateTime que retorna el procedimiento si la consulta SQL no retorna valores</param>
        /// <param description="UserInfo">Informacion del usuario</param>
        /// <returns>DateTime obtenido de la BD</returns>
        protected DateTime getValueAsDateTime(string StoredProcedure, dbParameter PrimaryKeyParameter, DateTime DefaultValue)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(PrimaryKeyParameter);

            return this.getValueAsDateTime(StoredProcedure, parameterList, DefaultValue, true);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>DateTime obtenido de la BD</returns>
        protected DateTime getValueAsDateTime(string StoredProcedure, DateTime DefaultValue)
        {
            return this.getValueAsDateTime(StoredProcedure, null, DefaultValue, false);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una llave única
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="PrimaryKeyParameter">Parámetro único a enviar en el SP</param>
        /// <returns>DateTime obtenido de la BD</returns>
        protected DateTime getValueAsDateTime(string StoredProcedure, dbParameter PrimaryKeyParameter)
        {
            return this.getValueAsDateTime(StoredProcedure, PrimaryKeyParameter, new DateTime(1900, 01, 01, 00, 00, 00, 00));
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único, a partir de una lista de parámetros
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="ParameterList">Parámetros a enviar al SP</param>
        /// <returns>DateTime obtenido de la BD</returns>
        protected DateTime getValueAsDateTime(string StoredProcedure, List<dbParameter> ParameterList)
        {
            return this.getValueAsDateTime(StoredProcedure, ParameterList, new DateTime(1900,01,01,00,00,00,00), true);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Cadena obtenido de la BD</returns>
        protected DateTime getValueAsDateTime(string StoredProcedure)
        {
            return this.getValueAsDateTime(StoredProcedure, null, new DateTime(1900, 01, 01, 00, 00, 00, 00), false);
        }

        #endregion getValueAsDateTime

        //Procedimientos que retornan un valor tipo "decimal"; que reciben de SQL con un getValue()
        #region getValueAsDecimal

        /// <summary>
        /// Ejecuta un SQL Stored Procedure que recibe una lista de parametros y retorna un decimal
        /// </summary>
        /// <param description="StoredProcedure">Nombre del SQL Stored Procedure</param>
        /// <param description="ParameterList">Lista de Parametros que recibe el SQL Stored Procedure</param>
        /// <param description="hasParameters">True si la lista enviada tiene parametros. False si la lista es NULL.</param>
        /// <param description="UserInfo">Informacion del usuario</param>
        /// <returns>Decimal obtenido de la BD. Retorna 0 si la consulta no retorna valores, y al Error: -1 </returns>
        private decimal getValueAsDecimal(string StoredProcedure, List<dbParameter> ParameterList, bool hasParameters)
        {
            object result = getValue(StoredProcedure, ParameterList, hasParameters);
            decimal valueToReturn = -1;

            if (result != null)
            { valueToReturn = Convert.ToDecimal(result); }

            return valueToReturn;
        }

        /// <summary>
        /// Ejecuta un SQL Stored Procedure que recibe una lista de parametros y retorna un decimal
        /// </summary>
        /// <param description="StoredProcedure">Nombre del SQL Stored Procedure</param>
        /// <param description="ParameterList">Lista de Parametros que recibe el SQL Stored Procedure</param>
        /// <param description="UserInfo">Informacion del usuario</param>
        /// <returns>Decimal obtenido de la BD. Retorna 0 si la consulta no retorna valores, y al Error: -1 </returns>
        protected decimal getValueAsDecimal(string StoredProcedure, List<dbParameter> ParameterList)
        {
            return this.getValueAsDecimal(StoredProcedure, ParameterList, true);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un decimal, a partir de un unico parametro
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="PrimaryKeyParameter">Parámetro único a enviar en el SP</param>
        /// <param description="UserInfo">Informacion del usuario</param>
        /// <returns>Decimal obtenido de la BD. Retorna 0 si la consulta no retorna valores, y al Error: -1 </returns>
        protected decimal getValueAsDecimal(string StoredProcedure, dbParameter PrimaryKeyParameter)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(PrimaryKeyParameter);

            return this.getValueAsDecimal(StoredProcedure, parameterList, true);
        }

        /// <summary>
        /// Ejecuta un stored procedure para recibir un valor único
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Decimal obtenido de la BD</returns>
        protected decimal getValueAsDecimal(string StoredProcedure)
        {
            return this.getValueAsDecimal(StoredProcedure, null, false);
        }

        #endregion getValueAsDecimal


        #region ExecuteProcedure_getValue

        /// <summary>
        /// Ejecuta un Insert o un Update, dependiendo del procedimiento almacenado en la variable "StoredProcedure". 
        /// Si no se encuentra registro, retorna "0" en resultID. Si hubo error, retorna "-1"
        /// </summary>
        /// <param description="StoredProcedure">Nombre del SQL stored procedure.</param>
        /// <param description="ParameterList">Lista de parámetros a enviar en el SP</param>
        /// <returns>Retorna el ID del registro modificado. Si hubo error: -1; si la consulta no retorna nada: 0.</returns>
        protected int ExecuteProcedureFeedback(string StoredProcedure, List<dbParameter> ParameterList)
        {
            int ResultID = -1;

            ResultID = this.getValueAsInt(StoredProcedure, ParameterList, true);

            if (ResultID >= 0)
            { InsertTransactionLog(SysSprocs.getSprocAction(StoredProcedure), ResultID); }

            return ResultID;
        }

        /// <summary>
        /// Ejecuta un Insert o un Update, dependiendo del procedimiento almacenado en la variable "StoredProcedure"
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="ParameterList">Lista de parámetros a enviar en el SP</param>
        /// <returns>True si la transacción fue exitosa</returns>
        protected bool ExecuteProcedure(string StoredProcedure, List<dbParameter> ParameterList)
        {
            bool resultToReturn = false;

            if (ExecuteProcedureFeedback(StoredProcedure, ParameterList) >= 0)
            { resultToReturn = true; }

            return resultToReturn;
        }

        /// <summary>
        /// Ejecuta un Insert o un Update, dependiendo del procedimiento almacenado en la variable "StoredProcedure" pero no inserta el Log de la transaccion
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="ParameterList">Lista de parámetros a enviar en el SP</param>
        /// <returns>True si la transacción fue exitosa</returns>
        protected bool ExecuteProcedureNoLog(string StoredProcedure, List<dbParameter> ParameterList)
        {
            return (getValueAsInt(StoredProcedure, ParameterList) >= 0);
        }

        /// <summary>
        /// Ejecuta un Insert o un Update, dependiendo del procedimiento almacenado en la variable "StoredProcedure"
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>True si la transacción fue exitosa</returns>
        protected bool ExecuteProcedure(string StoredProcedure)
        {
            List<dbParameter> parameterList = new List<dbParameter>();

            bool resultToReturn = false;

            if (ExecuteProcedureFeedback(StoredProcedure, parameterList) >= 0)
            { resultToReturn = true; }

            return resultToReturn;
        }

        /// <summary>
        /// Ejecuta un Insert o un Update, dependiendo del procedimiento almacenado en la variable "StoredProcedure"
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="UniqueParameter">Parámetro único a enviar en el SP</param>
        /// <returns>True si la transacción fue exitosa</returns>
        protected bool ExecuteProcedure(string StoredProcedure, dbParameter UniqueParameter)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(UniqueParameter);

            return ExecuteProcedure(StoredProcedure, parameterList);
        }

        #endregion ExecuteProcedure_getValue

        /// <summary>
        /// Método privado que  se encarga de ejecutar un Select. Recordar cerrar conexion con reader.Close();.
        /// </summary>
        /// <param description="hasParameters">Verdadero si el stored procedure recibe parámetros</param>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="ParameterList">Lista de parámetros a enviar en el SP</param>
        /// <returns>Reader abierto con los datos del Select.</returns>
        private void ExecuteSelect(bool hasParameters, string StoredProcedure, List<dbParameter> ParameterList)
        {
            ErrorNumber = 0;

            int attempts = 0;
            bool getOut = false;

            while ((attempts < kDeadlockRetryAttempts)
                && (!getOut))
            {
                try
                {
                    connection = new SqlConnection(GetConnectionString);

                    SqlCommand cmd = new SqlCommand(StoredProcedure, connection);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = kSQLCommandTimeoutValue;

                    if (hasParameters)
                    {
                        foreach (dbParameter currentParameter in ParameterList)
                        {
                            string paramName = currentParameter.Name;
                            cmd.Parameters.AddWithValue(paramName, currentParameter.Value);
                        }
                    }

                    //Popula el DataReader local llamando al Stored Procedure
                    connection.Open();
                    reader = cmd.ExecuteReader();

                    getOut = true;
                }
                catch (SqlException ex)
                { DealWithSqlException(ex, StoredProcedure, ref attempts, ref getOut); }
                catch (Exception e)
                { DealWithException(e, StoredProcedure, ref getOut); }
            }
        }

        //Procedimientos que retornan un valor tipo "SqlDataReader"; que utilizan ExecuteSelect()
        #region LoadOpenReader()

        /// <summary>
        /// Método privado que  se encarga de ejecutar un Select. Recordar cerrar conexion con reader.Close();.
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Reader abierto con los datos del Select</returns>
        protected void LoadOpenReader(string StoredProcedure)
        {
            ExecuteSelect(false, StoredProcedure, null);
        }

        /// <summary>
        /// Método privado que  se encarga de ejecutar un Select. Recordar cerrar conexion con reader.Close();.
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="ParameterList">Lista de parámetros a enviar en el SP</param>
        /// <returns>Reader abierto con los datos del Select</returns>
        protected void LoadOpenReader(string StoredProcedure, List<dbParameter> ParameterList)
        {
            ExecuteSelect(true, StoredProcedure, ParameterList);
        }

        /// <summary>
        /// Método privado que  se encarga de ejecutar un Select. Recordar cerrar conexion con reader.Close();.
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <param description="Parameter">Parámetro único a enviar en el SP</param>
        /// <returns>Reader abierto con los datos del Select</returns>
        protected void LoadOpenReader(string StoredProcedure, dbParameter UniqueParameter)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(UniqueParameter);

            ExecuteSelect(true, StoredProcedure, parameterList);
        }

        #endregion LoadOpenReader()

        //Procedimientos que utilizan ExecuteSelect() para retornar una lista de ComboItems
        #region getComboItemList()

        /// <summary>
        /// Sirve para popular una lista de "ComboItems" con un objecto DataReader abierto que contiene los datos a leer.
        /// </summary>
        /// <param description="reader">DataReader inicializado con los datos a leer</param>
        /// <returns>Una lista de ComboItems con los datos de la BD</returns>
        private List<ComboItem> ReaderToComboList()
        {
            List<ComboItem> ListCombo = new List<ComboItem>();

            try
            {
                while (reader.Read())
                {
                    ComboItem Combo = new ComboItem();
                    Combo.ID = reader.GetValue(0).ToString();
                    Combo.Text = reader.GetValue(1).ToString();
                    ListCombo.Add(Combo);
                }

                CloseConnection();
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                { InsertLog("Msg: " + e.Message + "; SQL: " + e.InnerException.Message); }
                else
                { InsertLog("Msg: " + e.Message); }

                ListCombo = null;
            }

            return ListCombo;
        }

        /// <summary>
        /// Sirve para popular una lista de "ComboItems" con un objecto DataReader abierto que contiene los datos a leer.
        /// </summary>
        /// <param description="reader">DataReader inicializado con los datos a leer</param>
        /// <returns>Una lista de ComboItems con los datos de la BD</returns>
        private List<ComboItemWithString> ReaderToComboListWithString()
        {
            List<ComboItemWithString> ListCombo = new List<ComboItemWithString>();

            try
            {
                while (reader.Read())
                {
                    ComboItemWithString Combo = new ComboItemWithString();
                    Combo.ID = reader.GetValue(0).ToString();
                    Combo.Text = reader.GetValue(1).ToString();
                    Combo.Cadena = reader.GetValue(2).ToString();

                    ListCombo.Add(Combo);
                }

                CloseConnection();
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                { InsertLog("Msg: " + e.Message + "; SQL: " + e.InnerException.Message); }
                else
                { InsertLog("Msg: " + e.Message); }

                ListCombo = null;
            }

            return ListCombo;
        }

        /// <summary>
        /// Retorna una lista de ComboItems a partir de un Stored Procedure que no recibe parámetros
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Lista de ComboItems</returns>
        protected List<ComboItem> getComboItemList(string StoredProcedure, dbParameter ParameterToSearch)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(ParameterToSearch);

            ExecuteSelect(true, StoredProcedure, parameterList);

            return ReaderToComboList();
        }

        /// <summary>
        /// Retorna una lista de ComboItems a partir de un Stored Procedure que no recibe parámetros
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Lista de ComboItems</returns>
        protected List<ComboItemWithString> getComboItemListWithString(string StoredProcedure, dbParameter ParameterToSearch)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(ParameterToSearch);

            ExecuteSelect(true, StoredProcedure, parameterList);

            return ReaderToComboListWithString();
        }

        /// <summary>
        /// Retorna una lista de ComboItems a partir de un Stored Procedure que no recibe parámetros
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Lista de ComboItems</returns>
        protected List<ComboItemWithString> getComboItemListWithString(string StoredProcedure)
        {
            ExecuteSelect(false, StoredProcedure, new List<dbParameter>());

            return ReaderToComboListWithString();
        }

        /// <summary>
        /// Retorna una lista de ComboItems que se popula recibiendo un identificador de una tabla
        /// </summary>
        /// <param description="PrimaryKey_Name">Nombre del parámetro a enviar al Stored Procedure</param>
        /// <param description="PrimaryKey_Value">Valor del parámetro a enviar al Stored Procedure</param>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Lista con los datos populados por medio del SP</returns>
        protected List<ComboItem> getComboItemListForID(string StoredProcedure, string PrimaryKeyName, int PrimaryKeyValue)
        {
            return getComboItemList(StoredProcedure, new dbParameter(PrimaryKeyName, PrimaryKeyValue));
        }

        /// <summary>
        /// Retorna una lista de ComboItems a partir de un Stored Procedure que no recibe parámetros
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Lista de ComboItems</returns>
        protected List<ComboItem> getComboItemList(string StoredProcedure)
        {
            ExecuteSelect(false, StoredProcedure, null);

            return ReaderToComboList();
        }

        /// <summary>
        /// Retorna una lista de ComboItems a partir de un Stored Procedure que no recibe parámetros
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        /// <returns>Lista de ComboItems</returns>
        protected List<ComboItem> getComboItemList(string StoredProcedure, List<dbParameter> ParameterList)
        {
            ExecuteSelect(true, StoredProcedure, ParameterList);

            return ReaderToComboList();
        }

        #endregion getComboItemList()

        /// <summary>
        /// Sirve para popular una lista de "Int" con un objecto DataReader abierto que contiene los datos a leer.
        /// </summary>
        /// <param description="reader">DataReader inicializado con los datos a leer</param>
        /// <returns>Una lista de Int con los datos de la BD</returns>
        private List<int> ReaderToIntList()
        {
            List<int> ListInts = new List<int>();

            try
            {
                while (reader.Read())
                { ListInts.Add(reader.GetInt32(0)); }

                CloseConnection();
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                { InsertLog("Msg: " + e.Message + "; SQL: " + e.InnerException.Message); }
                else
                { InsertLog("Msg: " + e.Message); }

                ListInts = null;
            }

            return ListInts;
        }

        /// <summary>
        /// Sirve para popular una lista de "String" con un objecto DataReader abierto que contiene los datos a leer.
        /// </summary>
        private List<string> ReaderToStringList()
        {
            List<string> ListStrings = new List<string>();

            try
            {
                while (reader.Read())
                { ListStrings.Add(reader.GetString(0)); }

                CloseConnection();
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                { InsertLog("Msg: " + e.Message + "; SQL: " + e.InnerException.Message); }
                else
                { InsertLog("Msg: " + e.Message); }

                ListStrings = null;
            }

            return ListStrings;
        }

        /// <summary>
        /// Retorna una lista de Enteros a partir de un Stored Procedure
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        protected List<int> getIntList(string StoredProcedure, List<dbParameter> ParameterList)
        {
            ExecuteSelect(true, StoredProcedure, ParameterList);

            return ReaderToIntList();
        }

        /// <summary>
        /// Retorna una lista de Enteros a partir de un Stored Procedure
        /// </summary>
        /// <param description="StoredProcedure">Nombre del stored procedure</param>
        protected List<string> getStringList(string StoredProcedure, List<dbParameter> ParameterList)
        {
            ExecuteSelect(true, StoredProcedure, ParameterList);

            return ReaderToStringList();
        }

        protected byte[] GetBytesFromReader(int indexColumn)
        {
            byte[] values = new byte[]{0};

            try
            {
                if (!reader.IsDBNull(indexColumn))
                {
                    long size = reader.GetBytes(indexColumn, 0, null, 0, 0);  //get the length of data
                    values = new byte[size];

                    int bufferSize = 1024;
                    long bytesRead = 0;
                    int curPos = 0;

                    while (bytesRead < size)
                    {
                        bytesRead += reader.GetBytes(indexColumn, curPos, values, curPos, bufferSize);
                        curPos += bufferSize;
                    }
                }
            }
            catch (Exception e)
            {
                InsertLog("Error al leer BYTE[] de SQL: " + e.Message);
                values = new byte[] { 0 };
            }

            return values;
        }
    }
}