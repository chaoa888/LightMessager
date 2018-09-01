/*
 *  2017-08-01 10:53:45
 *  本文件由生成工具自动生成，请勿随意修改内容除非你很清楚自己在做什么！
 */
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
		/// 是否存在指定的
		/// </summary>
		/// <param name="Id"></param>
		/// <returns>是否存在，true为存在</returns>
		public static bool Exists(int Id)
		{
			var sql = new StringBuilder();
			sql.Append("SELECT COUNT(1) FROM MessageQueue");
			sql.Append(" WHERE Id=@Id ");
			var ret = false;
			using (var conn = GetOpenConnection())
			{
				ret = conn.ExecuteScalar<int>(sql.ToString(), new { @Id=Id }) > 0;
			}

			return ret;
		}

        public static bool Exists(string knuthHash)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT COUNT(1) FROM MessageQueue");
            sql.Append(" WHERE KnuthHash=@KnuthHash and CanBeRemoved=@CanBeRemoved ");
            var ret = false;
            using (var conn = GetOpenConnection())
            {
                ret = conn.ExecuteScalar<int>(sql.ToString(), new { @KnuthHash = knuthHash, @CanBeRemoved = false }) > 0;
            }

            return ret;
        }

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="model">实体</param>
        /// <returns>新插入数据的id</returns>
        public static int Insert(MessageQueue model, SqlConnection conn = null, SqlTransaction transaction = null)
        {
            var sql = new StringBuilder();
            sql.Append("INSERT INTO MessageQueue(KnuthHash, MsgContent, CanBeRemoved, ExecuteCount, LastExecuteTime, CreatedTime)");
            sql.Append(" OUTPUT INSERTED.Id ");
            sql.Append("VALUES(@KnuthHash, @MsgContent, @CanBeRemoved, @ExecuteCount, @LastExecuteTime, @CreatedTime)");
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
        /// 删除指定的
        /// </summary>
		/// <param name="Id"></param>
        /// <returns>是否成功，true为成功</returns>
        public static bool Delete(int Id)
        {
            var sql = new StringBuilder();
            sql.Append("DELETE FROM MessageQueue ");
            sql.Append(" WHERE Id=@Id ");
            var ret = false;
            using (var conn = GetOpenConnection())
            {
                ret = conn.Execute(sql.ToString(), new { @Id=Id }) > 0;
            }

            return ret;
        }

		/// <summary>
        /// 批量删除指定的
        /// </summary>
        /// <param name="ids"> id列表</param>
        /// <returns>是否成功，true为成功</returns>
        public static bool Delete(List<int> ids)
        {
            var sql = new StringBuilder();
            sql.Append("DELETE FROM MessageQueue ");
            sql.Append(" WHERE Id IN @ids");
            var ret = false;
            using (var conn = GetOpenConnection())
            {
                ret = conn.Execute(sql.ToString(), new { @ids = ids.ToArray() }) > 0;
            }

            return ret;
        }

		/// <summary>
        /// 更新
        /// </summary>
        /// <param name="model">实体</param>
        /// <returns>是否成功，true为成功</returns>
        public static bool Update(MessageQueue model, SqlConnection conn = null, SqlTransaction transaction = null)
        {
            var sql = new StringBuilder();
            sql.Append("UPDATE MessageQueue");
            sql.Append(" SET CanBeRemoved=@CanBeRemoved, ExecuteCount=ExecuteCount+1, LastExecuteTime=@LastExecuteTime");
            sql.Append(" WHERE KnuthHash=@KnuthHash");
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
        /// 获取指定的
        /// </summary>
        /// <param name="Id"></param>
        /// <returns>MessageQueue实体</returns>
        public static MessageQueue GetModel(int Id)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT TOP 1 Id, KnuthHash, MsgContent, CanBeRemoved, ExecuteCount, LastExecuteTime, CreatedTime FROM MessageQueue ");
            sql.Append(" WHERE Id=@Id ");
            MessageQueue ret = null;
            using (var conn = GetOpenConnection())
            {
                ret = conn.QueryFirst<MessageQueue>(sql.ToString(), new { @Id=Id });
            }

            return ret;
        }

        /// <summary>
        /// 获取指定的
        /// </summary>
		/// <param name="msgHash"></param>
        /// <returns>MessageQueue实体</returns>
        public static MessageQueue GetModelBy(string knuthHash)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT TOP 1 Id, KnuthHash, MsgContent, CanBeRemoved, ExecuteCount, LastExecuteTime, CreatedTime FROM MessageQueue ");
            sql.Append(" WHERE KnuthHash=@KnuthHash");
            MessageQueue ret = null;
            using (var conn = GetOpenConnection())
            {
                ret = conn.QueryFirstOrDefault<MessageQueue>(sql.ToString(), new { @KnuthHash = knuthHash });
            }

            return ret;
        }

        /// <summary>
        /// 批量获取
        /// </summary>
        /// <param name="where">查询条件</param>
        /// <param name="top">取出前top数的数据</param>
        /// <returns>MessageQueue列表</returns>
        public static List<MessageQueue> GetList(string where = "", int top = 100)
        {
            var sql = new StringBuilder();
            sql.Append("SELECT ");
            sql.Append(" TOP " + top.ToString());
            sql.Append(" Id, KnuthHash, MsgContent, CanBeRemoved, ExecuteCount, LastExecuteTime, CreatedTime ");
            sql.Append(" FROM MessageQueue ");
            if (!string.IsNullOrWhiteSpace(where))
            {
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
        protected static int GetRecordCount(string where = "")
        {
            var sql = new StringBuilder();
            sql.Append("SELECT COUNT(1) FROM MessageQueue ");
            if (!string.IsNullOrWhiteSpace(where))
            {
                if (!where.Trim().StartsWith("where", StringComparison.CurrentCultureIgnoreCase))
                {
                    sql.Append(" WHERE " + where);
                }
                else
                {
                    sql.Append(" ");
                    sql.Append(where);
                    sql.Append(" ");
                }
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
