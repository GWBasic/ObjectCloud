// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Database;
using ObjectCloud.ORM.DataAccess.WhereConditionals;

namespace ObjectCloud.ORM.DataAccess.SQLite
{
    public abstract class SQLiteConnectorBase : IEmbeddedDatabaseConnector
    {
        public SQLiteConnectorBase()
        {
            ConnectionOpenerCache = new Cache<string, ConnectionOpener>(CreateForCache);
        }

        public abstract void CreateFile(string databaseFilename);

        public virtual DbConnection OpenEmbedded(string databaseFilename)
        {
            return Open(string.Format("Data Source=\"{0}\"", databaseFilename));
        }

        public DbConnection Open(string connectionString)
        {
            return ConnectionOpenerCache[connectionString].Open();
        }

        /// <summary>
        /// Cache of connection openers, indexed by connection string.  Each connection opener will block new connections while there is an existing connection open
        /// </summary>
        private Cache<string, SQLiteConnectorBase.ConnectionOpener> ConnectionOpenerCache;

        /// <summary>
        /// Opens a connection.  Blocks new connections while an existing connection is open
        /// </summary>
        public class ConnectionOpener
        {
            public ConnectionOpener(string connectionString, SQLiteConnectorBase databaseConnector)
            {
                ConnectionString = connectionString;
                DatabaseConnector = databaseConnector;
                Semaphore = new Semaphore(1, 1);
            }

            private string ConnectionString;
            private SQLiteConnectorBase DatabaseConnector;

            private Semaphore Semaphore;

#if DEBUG
			volatile string BlockingCallerStacktrace = null;
            volatile string BlockingCallerThreadName = null;
			
            // https://bugzilla.novell.com/show_bug.cgi?id=545873
			private bool IsMono
			{
				get
				{
					if (null == _IsMono)
						_IsMono = null != Type.GetType("Mono.Runtime");
					
					return _IsMono.Value;
				}
			}
			private bool? _IsMono = null;
#endif
			
            /// <summary>
            /// Opens a connection.  There's a problem here, a Thread currently cannot recusively open a connection.
            /// </summary>
            /// <returns></returns>
            public DbConnection Open()
            {
                if (Semaphore.WaitOne(30000))
                {
                    DbConnection toReturn = DatabaseConnector.OpenInt(ConnectionString);
                    toReturn.Disposed += new EventHandler(toReturn_Disposed);

#if DEBUG
		            // https://bugzilla.novell.com/show_bug.cgi?id=545873
					if (!IsMono)
                    	BlockingCallerStacktrace = Environment.StackTrace;
					else
						BlockingCallerStacktrace = "https://bugzilla.novell.com/show_bug.cgi?id=545873";
                    BlockingCallerThreadName = Thread.CurrentThread.Name;
#endif

                    return toReturn;
                }

#if DEBUG
                // This happens when a thread holds a DbConnection open, or if a Thread tries to recursively create a DbConnection when
                // one is already open.

                // I might consider a way to dispose an old connection and break it...  Could be evil, but it's a way to prevent a long-running
                // transaction from hogging the system.


                if (System.Diagnostics.Debugger.IsAttached)
                    System.Diagnostics.Debugger.Break();
#endif

                // Could not get Semaphore
                throw new Blocked("Timeout getting a lock on " + ConnectionString
#if DEBUG
				    + "\nThread Name: " + BlockingCallerThreadName
                    + "\nStacktrace:\n" + BlockingCallerStacktrace
#endif
                    );
            }

            /// <summary>
            /// Removes the oldest token in the queue after a connection is disposed, unblocking the oldest connection request
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="e"></param>
            void toReturn_Disposed(object sender, EventArgs e)
            {
#if DEBUG
                BlockingCallerStacktrace = null;
                BlockingCallerThreadName = null;
#endif
                ((DbConnection)sender).Disposed -= new EventHandler(toReturn_Disposed);
                Semaphore.Release();
            }
        }

        /// <summary>
        /// Thrown when the database can not be opened because it's blocked by another thread
        /// </summary>
        public class Blocked : ORMException
        {
            internal Blocked(string message) : base(message) { }
        }

        /// <summary>
        /// Creates a ConnectionOpener for the given connection string
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private SQLiteConnectorBase.ConnectionOpener CreateForCache(string key)
        {
            return new ConnectionOpener(key, this);
        }

        /// <summary>
        /// Does the actual opening
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        protected abstract DbConnection OpenInt(string connectionString);

        public abstract DbParameter ConstructParameter(string parameterName, object value);

        public DbParameter[] Build(ComparisonCondition comparisonCondition, out string whereClause)
        {
            StringBuilder whereClauseBuilder = new StringBuilder(" where");

            List<DbParameter> toReturn = Build_Int(whereClauseBuilder, comparisonCondition, "");

            whereClause = whereClauseBuilder.ToString();

            return toReturn.ToArray();
        }

        public List<DbParameter> Build_Int(StringBuilder whereClauseBuilder, ComparisonCondition comparisonCondition, string argumentPostfix)
        {
            List<DbParameter> toReturn = new List<DbParameter>();

            if (comparisonCondition.Not)
                whereClauseBuilder.Append(" not(");

            if ((null != comparisonCondition.InContents) || null != comparisonCondition.LikeComparison)
            {
                if (comparisonCondition.Lhs is Column)
                    whereClauseBuilder.AppendFormat(" {0}", ((Column)comparisonCondition.Lhs).Name);
                else
                {
                    whereClauseBuilder.Append(" @l" + argumentPostfix);

                    if (comparisonCondition.Lhs is IID)
                        toReturn.Add(ConstructParameter("@l" + argumentPostfix, ((IID)comparisonCondition.Lhs).Value));
                    else
                        toReturn.Add(ConstructParameter("@l" + argumentPostfix, comparisonCondition.Lhs));
                }

                if (null != comparisonCondition.InContents)
                {
                    List<string> clauseContents = new List<string>();
                    string argumentMidfix = "";
                    foreach (object item in comparisonCondition.InContents)
                    {
                        string argumentId = "@r" + argumentMidfix + argumentPostfix;
                        clauseContents.Add(argumentId);

                        if (item is IID)
                            toReturn.Add(ConstructParameter(argumentId, ((IID)item).Value));
                        else
                            toReturn.Add(ConstructParameter(argumentId, item));

                        argumentMidfix = argumentMidfix + "i";
                    }

                    whereClauseBuilder.AppendFormat(" in ({0})", StringGenerator.GenerateCommaSeperatedList(clauseContents));
                }
                else if (null != comparisonCondition.LikeComparison)
                {
                    string argumentId = "@i" + argumentPostfix;
                    whereClauseBuilder.AppendFormat(" like {0}", argumentId);
                    toReturn.Add(ConstructParameter(argumentId, comparisonCondition.LikeComparison));
                }
            }
            else if (null != comparisonCondition.ComparisonOperator)
            {
                if (comparisonCondition.Lhs is Column)
                    whereClauseBuilder.AppendFormat(" {0}", ((Column)comparisonCondition.Lhs).Name);
                else
                {
                    whereClauseBuilder.Append(" @l" + argumentPostfix);

                    if (comparisonCondition.Lhs is IID)
                        toReturn.Add(ConstructParameter("@l" + argumentPostfix, ((IID)comparisonCondition.Lhs).Value));
                    else
                        toReturn.Add(ConstructParameter("@l" + argumentPostfix, comparisonCondition.Lhs));
                }

                whereClauseBuilder.AppendFormat(" {0}", comparisonOperatorToSqlOperator[comparisonCondition.ComparisonOperator.Value]);

                if (comparisonCondition.Rhs is Column)
                    whereClauseBuilder.AppendFormat(" {0}", ((Column)comparisonCondition.Rhs).Name);
                else
                {
                    whereClauseBuilder.Append(" @r" + argumentPostfix);

                    if (comparisonCondition.Rhs is IID)
                        toReturn.Add(ConstructParameter("@r" + argumentPostfix, ((IID)comparisonCondition.Rhs).Value));
                    else
                        toReturn.Add(ConstructParameter("@r" + argumentPostfix, comparisonCondition.Rhs));
                }
            }
            else
            {
                whereClauseBuilder.Append("(");
                toReturn.AddRange(
                    Build_Int(whereClauseBuilder, (ComparisonCondition)comparisonCondition.Lhs, argumentPostfix + "r"));
                whereClauseBuilder.Append(")");

                whereClauseBuilder.AppendFormat(" {0} ", booleanOperatorToSqlOperator[comparisonCondition.BooleanOperator.Value]);
                whereClauseBuilder.Append("(");
                toReturn.AddRange(
                    Build_Int(whereClauseBuilder, (ComparisonCondition)comparisonCondition.Rhs, argumentPostfix + "l"));
                whereClauseBuilder.Append(")");
            }

            if (comparisonCondition.Not)
                whereClauseBuilder.Append(")");

            return toReturn;
        }

        private static readonly Dictionary<ComparisonOperator, string> comparisonOperatorToSqlOperator;

        private static readonly Dictionary<BooleanOperator, string> booleanOperatorToSqlOperator;

        static SQLiteConnectorBase()
        {
            comparisonOperatorToSqlOperator = new Dictionary<ComparisonOperator, string>(5);
            comparisonOperatorToSqlOperator[ComparisonOperator.Equals] = "=";
            comparisonOperatorToSqlOperator[ComparisonOperator.GreaterThen] = ">";
            comparisonOperatorToSqlOperator[ComparisonOperator.GreaterThenEquals] = ">=";
            comparisonOperatorToSqlOperator[ComparisonOperator.LessThen] = "<";
            comparisonOperatorToSqlOperator[ComparisonOperator.LessThenEquals] = "<=";

            booleanOperatorToSqlOperator = new Dictionary<BooleanOperator,string>(3);
            booleanOperatorToSqlOperator[BooleanOperator.And] = "and";
            booleanOperatorToSqlOperator[BooleanOperator.Or] = "or";
            booleanOperatorToSqlOperator[BooleanOperator.Xor] = "xor";
        }
    }
}
