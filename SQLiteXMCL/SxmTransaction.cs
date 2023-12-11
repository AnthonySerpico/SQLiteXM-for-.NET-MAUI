using System.Collections;

namespace SQLiteXM
{
    public class SxmTransaction : IDisposable
	{
		private bool interruptSynchronize = false;
		private SxmConnection connection;
		private bool disposed = false;

		public SxmTransaction (SxmConnection connection)
		{
			try
			{
				if (connection.lockConnection () == false) 
				{
					throw new SxmException (new ErrorMessage("lockDB", connection.DatabaseName));
				}
				this.connection = connection; 
			}
			catch (SxmException ex)
			{
				if (connection != null) 
					connection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
				throw;
			}
			catch(System.Exception ex)
			{
				if (connection != null) 
					connection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
				throw new SxmException (ex);
			}
		}

		public SxmTransaction (string? databaseName = null)
		{
			bool transient = true;

			try
			{
				connection = new SxmConnection(databaseName, transient);
				if (connection.lockConnection () == false) 
				{
					if (databaseName == null)
						databaseName = SxmConnection.ImplicitDatabaseName;

                    throw new SxmException (new ErrorMessage("lockDB", databaseName!));
				}
			}
			catch (SxmException ex)
			{
				if (connection != null) 
				{
					connection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
					connection.releaseConnection ();
				}
				throw;
			}
			catch(System.Exception ex)
			{
				if (connection != null) 
				{
					connection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
					connection.releaseConnection ();
				}
				throw new SxmException (ex);
			}
		}

		public void Dispose()
		{ 
			Dispose(true); // Called from user code.
			GC.SuppressFinalize(this);           
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposed == true)
				return;

			if (disposing == true) {/* Called from user code. Release managed and unmanaged resources. */} 

			finalizeTransaction ();
            disposed = true;
		}

		~SxmTransaction()
		{
			Dispose (false); // Called from runtime.
		}

		public Dictionary<string, object?> executeInsert (string command, List<object> ParameterValues)
		{
			long recordID = -1;
			string? synchID = default(string);

			InsertDefinition? insertDefinition = SqlStatements.insertStatements [command] as InsertDefinition;
			if (insertDefinition == null)
				throw new SxmException ( new ErrorMessage("unknownSQLStatement", command));

			executeNonQueryTrans (insertDefinition.InsertSQL, ParameterValues);

			try
			{
				if (insertDefinition.TableName.Length != 0) 
				{
					executeQueryDirect ("select last_insert_rowid() as rowID", null);
                    Dictionary<string, object?>? nextRow = connection.getNextRow<Dictionary<string, object?>> ();

					if (nextRow != default && nextRow.Count > 0)
						if (nextRow.ContainsKey("rowID") == true)
						{
							recordID = (long)nextRow["rowID"]!;
							synchID = getSynchID(insertDefinition.TableName, recordID);
						}

					if (synchID == null || synchID.Length == 0)
						synchID = Guid.NewGuid ().ToString ();

                    List<object> synchIDPV = new List<object>();
					synchIDPV.Add (synchID);
					synchIDPV.Add (recordID);
					executeNonQuery (String.Format ("UPDATE {0} SET systemSynchID = @p0 WHERE id = @p1", insertDefinition.TableName), synchIDPV);
					synchIDPV.RemoveAt (1);

					executeNonQuery (String.Format ("UPDATE _systemCloudSynch SET action='insert' WHERE systemSynchID = @p0 "), synchIDPV);
				}
			}
			#pragma warning disable 0168
            catch (SxmException ex)
			#pragma warning restore 0168
            {
                throw;
			}
			catch (System.Exception ex)
			{
				throw new SxmException (ex);
			}

            Dictionary<string, object?> ir = new Dictionary<string, object?>();
			ir.Add("id", recordID);
            ir.Add("synchId", synchID);
			return ir;

        }

		private string? getSynchID (string tableName, long recordID)
		{
			string? systemSynchID = default(string);

			try
			{
                List<object> parameterList = new List<object>();
				parameterList.Add (recordID);

				executeQueryDirect (String.Format ("SELECT systemSynchID FROM {0} WHERE id = @p0 LIMIT 1", tableName), parameterList);
                Dictionary<string, object?>? row = connection.getNextRow<Dictionary<string, object?>> ();

				if (row != null && row.Count > 0) 
					if (row.ContainsKey ("systemSynchID") == true)
						systemSynchID = (string?)row ["systemSynchID"];

			}
			#pragma warning disable 0168
			catch (Exception doNothing) { /* If an error occurs reading the record, then do nothing. Assume synch ID does not exist. */ }
			#pragma warning restore 0168

			return systemSynchID;
		}

		public void executeQuery (string command, List<object> ParameterValues)
		{
			connection.executeQuery (SqlStatements.selectStatements [command].SelectSQL, ParameterValues);
		}
        public void executeUpdate(string command, List<object> ParameterValues)
        {
            executeNonQuery(SqlStatements.updateStatements[command].UpdateSQL, ParameterValues);
        }
        public void executeDelete(string command, List<object> ParameterValues)
        {
            executeNonQuery(SqlStatements.deleteStatements[command].DeleteSQL, ParameterValues);
        }

        public void executeQueryDirect(string sqlStatement, List<object> ParameterValues)
        {
            connection.executeQuery(sqlStatement, ParameterValues);
        }
        public void executeUpdateDirect (string sqlStatement, List<object> ParameterValues)
		{
			executeNonQuery (sqlStatement, ParameterValues);
		}
		public void executeDeleteDirect (string sqlStatement, List<object> ParameterValues)
		{
			executeNonQuery (sqlStatement, ParameterValues);
		}

		public void executeSystemUpdateDirect (string sqlStatement, List<object> ParameterValues)
		{
			executeNonQueryTrans (sqlStatement, ParameterValues);
		}

		public void executeTableStatement (string sqlStatement)
		{
			executeNonQueryTrans (sqlStatement);
		}

		public void executeAlterTable (string sqlStatement)
		{
			executeNonQueryTrans (sqlStatement);
		}

		public void executeIndex (string sqlStatement)
		{
			executeNonQueryTrans (sqlStatement);
		}

		public void executeCreateTrigger (string sqlStatement)
		{
			executeNonQueryTrans (sqlStatement);
		}

		public void executeNonQuery (string sqlStatement, List<object> ParameterValues = null)
		{
			executeNonQueryTrans (sqlStatement, ParameterValues);
			interruptSynchronize = true;
//			SxmInit.interruptSynchronize (connection.DatabaseName);
		}

		public void executeNonQueryTrans (string sqlStatement, List<object>? ParameterValues = null)
		{
			connection.beginTransaction ();
			connection.executeNonQuery (sqlStatement, ParameterValues);
		}

		public void attachDatabase ()
		{
			ArrayList databaseNames = DatabaseDescriptor.getDatabaseNames ();

			foreach (string databaseName in databaseNames)
				attachDatabase (databaseName);
		}

		// Silent when attempting to attach to the current connection.
		public void attachDatabase (string databaseName)
		{
			if (connection.DatabaseName.Equals (databaseName) == false) 
			{
				DatabaseDescriptor? databaseDescriptor = DatabaseDescriptor.getDescriptor (databaseName);
				if (databaseDescriptor == null) 
					throw new SxmException (new ErrorMessage("noDBDescriptorExists", databaseName));

				try
				{
					string databaseFolderPath = Environment.GetFolderPath (databaseDescriptor.DatabaseFolder);
					string dbFullyQualifiedPath = Path.Combine (databaseFolderPath, databaseName);

					if (File.Exists (dbFullyQualifiedPath) == true)
						connection.executeNonQuery (String.Format ("ATTACH DATABASE '{0}' as {1}", dbFullyQualifiedPath, databaseName), null as List<object>);
					else
						throw new SxmException (new ErrorMessage("noDatabaseExists", databaseName));
				}
				#pragma warning disable 0168
                catch (SxmException ex)
				#pragma warning restore 0168
                {
                    throw;
				}
				catch (System.Exception ex) 
				{
					throw new SxmException (ex);
				}
			}
		}

		// Detach all attached databases. Detaching all databases is normally associated with cleanup, no-throw.
		public void detachDatabase ()
		{
			try
			{
				connection.executeQuery ("PRAGMA database_list", null as List<object>);

				while (nextRow () == true) 
				{
					try
					{
						string? dbName = (string?)getValue ("name");
						if (dbName?.ToLower().Equals("main") == false && dbName.ToLower().Equals("temp") == false)
							detachDatabase (dbName);
					}
					#pragma warning disable 0168
					catch (System.Exception notUsed) // Keep trying to detach all databases.
					#pragma warning restore 0168
					{
					}
				}
			}
			#pragma warning disable 0168
			catch (System.Exception notUsed) 
			#pragma warning restore 0168
			{
			}
		}

		// Silent when attempting to detach to the current connection.
		public void detachDatabase (string databaseName)
		{
			if (connection.DatabaseName.Equals (databaseName) == false) 
			{
				DatabaseDescriptor? databaseDescriptor = DatabaseDescriptor.getDescriptor (databaseName);
				if (databaseDescriptor == null)
					throw new SxmException (new ErrorMessage("noDBDescriptorExists", databaseName));

				try
				{
					string databaseFolderPath = Environment.GetFolderPath (databaseDescriptor.DatabaseFolder);
					string dbFullyQualifiedPath = Path.Combine (databaseFolderPath, databaseName);
					if (File.Exists (dbFullyQualifiedPath) == true)
						connection.executeNonQuery (String.Format ("DETACH DATABASE '{0}'", databaseName), null as List<object>);
					else
						throw new SxmException (new ErrorMessage("noDatabaseExists", databaseName));
				}
				#pragma warning disable 0168
                catch (SxmException ex)
				#pragma warning restore 0168
                {
                    throw;
				}
				catch (System.Exception ex) 
				{
					throw new SxmException (ex);
				}
			}
		}

		// Returns error code for SqliteException, otherwise throw the exception.
		public SQLiteErrorCode commitTransaction ()
		{
			SQLiteErrorCode ec = connection.finishTransaction (SQLiteXM.Defines.commitTransaction);
			if (interruptSynchronize == true) 
			{
				//SxmInit.interruptSynchronize (connection.DatabaseName);
				interruptSynchronize = false;
			}
			return ec;
		}

		public void rollbackTransaction ()
		{
			connection.finishTransaction (SQLiteXM.Defines.rollbackTransaction);
			interruptSynchronize = false;
		}

		// No-throw guarantee.
		protected void finalizeTransaction ()
		{
			try 
			{
			    connection.releaseConnection ();
			} 
			catch (System.Exception ex) // I don't think there is any way to get here, but just in case.
			{
                try
                {
                    connection.logger.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
                }
                catch (Exception) { }
			}
			finally
			{
                connection = null;
			}
		}

		public bool hasRows ()
		{
			return connection.hasRows ();
		}

		public object? getValue (string fieldName)
		{
			return connection.getValue (fieldName);
		}

		public object? getValue (int fieldOrdinal)
		{
			return connection.getValue (fieldOrdinal);
		}

		public string? getFieldName (int fieldOrdinal)
		{
			return connection.getFieldName (fieldOrdinal);
		}

		public string[] getFieldNames ()
		{
			return connection.getFieldNames ();
		}

		public T? getNextRow<T> () where T : IDictionary<string, object?>, new()
        {
			return connection.getNextRow<T> ();
		}

		public List<T> getAllRows<T>() where T : IDictionary<string, object?>, new()
		{
			List<T> allRows = new List<T> ();
			T? row;

			while ((row = getNextRow<T> ()) != null)
				allRows.Add (row);

			return allRows;
		}

		public int getColumnCount ()
		{
			return connection.getColumnCount ();
		}

		public bool nextRow ()
		{
			return connection.nextRow ();
		}

		public Type? getType (string fieldName)
		{
			return connection.getType (fieldName);
		}
	}
}

