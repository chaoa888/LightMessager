using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dapper;
using System.Data.SqlClient;

namespace LightMessager.DAL
{
    internal partial class MessageQueueHelper : BaseTableHelper
    {
        /// <summary>
        /// 是否存在指定的MessageQueue实体对象
        /// </summary>
        /// <param name="Id">Id</param>
        /// <returns>是否存在，true为存在</returns>
        public static bool Exists(int Id)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT COUNT(1) FROM [MessageQueue]");
            sql.Append(" WHERE [Id]=@Id ");
            var ret = false;
            using (var conn = GetOpenConnection())
            {
                ret = conn.ExecuteScalar<int>(sql.ToString(), new { @Id = Id }) > 0;
            }

            return ret;
        }

        /// <summary>
        /// 添加MessageQueue实体对象
        /// </summary>
        /// <param name="model">MessageQueue实体</param>
        /// <returns>新插入数据的id</returns>
        public static int Insert(MessageQueue model, SqlConnection conn = null, SqlTransaction transaction = null)
        {
            var sql = new StringBuilder();
            sql.Append("INSERT INTO [MessageQueue]([KnuthHash], [MsgContent], [CanBeRemoved], [RetryCount], [LastRetryTime], [CreatedTime])");
            sql.Append(" OUTPUT INSERTED.[Id] ");
            sql.Append("VALUES(@KnuthHash, @MsgContent, @CanBeRemoved, @RetryCount, @LastRetryTime, @CreatedTime)");
            var ret = 0;
            if (conn != null)
            {
                if (transaction == null)
                {
                    throw new ArgumentNullException("transaction");
                }
                ret = conn.ExecuteScalar<int>(sql.ToString(), model, transaction);
            }
            else
            {
                using (var conn1 = GetOpenConnection())
                {
                    ret = conn1.ExecuteScalar<int>(sql.ToString(), model);
                }
            }

            return ret;
        }

        /// <summary>
        /// 删除指定的MessageQueue实体对象
        /// </summary>
        /// <param name="Id">Id</param>
        /// <returns>是否成功，true为成功</returns>
        public static bool Delete(int Id)
        {
            var sql = new StringBuilder();
            sql.Append("DELETE FROM [MessageQueue] ");
            sql.Append(" WHERE [Id]=@Id ");
            var ret = false;
            using (var conn = GetOpenConnection())
            {
                ret = conn.Execute(sql.ToString(), new { @Id = Id }) > 0;
            }

            return ret;
        }

        /// <summary>
        /// 批量删除指定的MessageQueue实体对象
        /// </summary>
        /// <param name="ids">id列表</param>
        /// <returns>是否成功，true为成功</returns>
        public static bool Delete(List<int> ids)
        {
            var sql = new StringBuilder();
            sql.Append("DELETE FROM [MessageQueue] ");
            sql.Append(" WHERE [Id] IN @ids");
            var ret = false;
            using (var conn = GetOpenConnection())
            {
                ret = conn.Execute(sql.ToString(), new { @ids = ids.ToArray() }) > 0;
            }

            return ret;
        }

        /// <summary>
        /// 更新指定的MessageQueue实体对象
        /// </summary>
        /// <param name="model">MessageQueue实体</param>
        /// <param name="fields">需要更新的字段名字</param>
        /// <returns>是否成功，true为成功</returns>
        public static bool Update(MessageQueue model, IList<string> fields = null, SqlConnection conn = null, SqlTransaction transaction = null)
        {
            var sql = new StringBuilder();
            sql.Append("UPDATE [MessageQueue]");
            if (fields == null || fields.Count == 0)
            {
                sql.Append(" SET [KnuthHash]=@KnuthHash, [MsgContent]=@MsgContent, [CanBeRemoved]=@CanBeRemoved, [RetryCount]=@RetryCount, [LastRetryTime]=@LastRetryTime, [CreatedTime]=@CreatedTime");
            }
            else
            {
                sql.Append(" SET ");
                for (int i = 0; i < fields.Count; i++)
                {
                    sql.Append("[" + fields[i] + "]=@" + fields[i] + "");
                    if (i != fields.Count - 1)
                    {
                        sql.Append(",");
                    }
                }
            }
            sql.Append(" WHERE [Id]=@Id");
            var ret = false;
            if (conn != null)
            {
                if (transaction == null)
                {
                    throw new ArgumentNullException("transaction");
                }
                ret = conn.Execute(sql.ToString(), model, transaction) > 0;
            }
            else
            {
                using (var conn1 = GetOpenConnection())
                {
                    ret = conn1.Execute(sql.ToString(), model) > 0;
                }
            }

            return ret;
        }

        /// <summary>
        /// 获取指定的MessageQueue实体对象
        /// </summary>
        /// <param name="id">id</param>
        /// <returns>MessageQueue实体对象</returns>
        public static MessageQueue GetModel(int id)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT TOP 1 [Id], [KnuthHash], [MsgContent], [CanBeRemoved], [RetryCount], [LastRetryTime], [CreatedTime] FROM [MessageQueue] ");
            sql.Append(" WHERE [Id]=@Id ");
            MessageQueue ret = null;
            using (var conn = GetOpenConnection())
            {
                ret = conn.QueryFirstOrDefault<MessageQueue>(sql.ToString(), new { @Id = id });
            }

            return ret;
        }

        /// <summary>
        /// 获取指定的MessageQueue实体对象
        /// </summary>
        /// <param name="Id">Id</param>
        /// <returns>MessageQueue实体对象</returns>
        public static MessageQueue GetModelBy(ulong knuthHash)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT TOP 1 Id, KnuthHash, MsgContent, CanBeRemoved, RetryCount, LastRetryTime, CreatedTime FROM MessageQueue ");
            sql.Append(" WHERE KnuthHash=@KnuthHash");
            MessageQueue ret = null;
            using (var conn = GetOpenConnection())
            {
                ret = conn.QueryFirstOrDefault<MessageQueue>(sql.ToString(), new { @KnuthHash = knuthHash });
            }

            return ret;
        }

        /// <summary>
        /// 批量获取MessageQueue实体对象
        /// </summary>
        /// <param name="where">查询条件（不需要带有where关键字）</param>
        /// <param name="top">取出前top数的数据</param>
        /// <returns>MessageQueue实体对象列表</returns>
        public static List<MessageQueue> GetList(string where = "", int top = 100)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT ");
            sql.Append(" TOP " + top.ToString());
            sql.Append(" [Id], [KnuthHash], [MsgContent], [CanBeRemoved], [RetryCount], [LastRetryTime], [CreatedTime] ");
            sql.Append(" FROM [MessageQueue] ");
            if (!string.IsNullOrWhiteSpace(where))
            {
                if (where.ToLower().Contains("where"))
                {
                    throw new ArgumentException("where子句不需要带where关键字");
                }
                sql.Append(" WHERE " + where);
            }
            object ret = null;
            using (var conn = GetOpenConnection())
            {
                ret = conn.Query<MessageQueue>(sql.ToString()).ToList();
            }

            return (List<MessageQueue>)ret;
        }

        /// <summary>
        /// 获取记录总数
        /// </summary>
        /// <param name="where">查询条件（不需要带有where关键字）</param>
        public static int GetCount(string where = "")
        {
            var sql = new StringBuilder();
            sql.Append("SELECT COUNT(1) FROM [MessageQueue] ");
            if (!string.IsNullOrWhiteSpace(where))
            {
                if (where.ToLower().Contains("where"))
                {
                    throw new ArgumentException("where子句不需要带where关键字");
                }
                sql.Append(" WHERE " + where);
            }
            var ret = -1;
            using (var conn = GetOpenConnection())
            {
                ret = conn.ExecuteScalar<int>(sql.ToString());
            }

            return ret;
        }

        /// <summary>
        /// 分页获取数据列表
        /// </summary>
        public static PageDataView<MessageQueue> GetListByPage(string where = "", string orderBy = "", string columns = " * ", int pageSize = 20, int currentPage = 1)
        {
            return Paged<MessageQueue>("MessageQueue", where, orderBy, columns, pageSize, currentPage);
        }
    }
}
