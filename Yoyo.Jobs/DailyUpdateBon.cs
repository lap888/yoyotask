using Microsoft.Extensions.DependencyInjection;
using Quartz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Linq;
using CSRedis;
using Yoyo.Entity.Models;
using System.Data;

namespace Yoyo.Jobs
{
    public class DailyUpdateBon : IJob
    {
        private readonly String AccountTableName = "user_account_shard";
        private readonly String RecordTableName = "user_account_shard_record";
        private readonly String CacheLockKey = "ShardAccount:";
        private readonly IServiceProvider ServiceProvider;

        public DailyUpdateBon(IServiceProvider service)
        {
            this.ServiceProvider = service;
        }
        public async Task Execute(IJobExecutionContext context)
        {
            using (var service = this.ServiceProvider.CreateScope())
            {
                try
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    Entity.SqlContext SqlContext = service.ServiceProvider.GetRequiredService<Entity.SqlContext>();
                    CSRedis.CSRedisClient RedisCache = service.ServiceProvider.GetRequiredService<CSRedis.CSRedisClient>();

                    IEnumerable<UserAccountShard> Accounts = await SqlContext.Dapper.QueryAsync<UserAccountShard>("SELECT * FROM user_account_shard WHERE Balance > 0");

                    foreach (var item in Accounts)
                    {
                        await ChangeAmount(item.UserId, -item.Balance, 3, false, item.Balance.ToString());
                    }

                    #region 邀请排行榜
                    //List<long> UserIds = (await SqlContext.Dapper.QueryAsync<long>("SELECT u.id FROM (SELECT * FROM yoyo_member_invite_ranking WHERE  Phase = DATE_FORMAT(NOW(), '%m') AND InviteTotal >= 100 ORDER BY InviteTotal DESC LIMIT 50) AS rank INNER JOIN `user` AS u ON rank.UserId = u.id LIMIT 50")).ToList();
                    //RedisCache.Del("UserBon");
                    //foreach (var item in UserIds)
                    //{
                    //    var Index = UserIds.FindIndex(o => o == item) + 1;
                    //    if (Index == 0) { continue; }
                    //    Decimal BonRate = 1.00M;
                    //    if (Index == 1) { BonRate = 2.00M; }
                    //    if (Index == 2 || Index == 3) { BonRate = 1.80M; }
                    //    if (Index >= 4 && Index <= 10) { BonRate = 1.50M; }
                    //    if (Index >= 11 && Index <= 20) { BonRate = 1.30M; }
                    //    if (Index >= 21 && Index <= 50) { BonRate = 1.10M; }
                    //    RedisCache.HSet("UserBon", item.ToString(), BonRate);
                    //}
                    //stopwatch.Stop();
                    //Core.SystemLog.Jobs($"每日更新邀请排行榜加成 执行完成,执行时间:{stopwatch.Elapsed.TotalSeconds}秒");
                    #endregion
                }
                catch (Exception ex)
                {
                    Core.SystemLog.Jobs("每日更新邀请排行榜加成 发生错误", ex);
                }
            }
        }

        /// <summary>
        /// 积分余额变更
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="Amount"></param>
        /// <param name="useFrozen">使用冻结金额，账户金额增加时，此参数无效</param>
        /// <param name="modifyType">账户变更类型</param>
        /// <param name="Desc">描述</param>
        /// <returns></returns>
        private async Task<Boolean> ChangeAmount(long userId, decimal Amount, Int32 modifyType, bool useFrozen, params string[] Desc)
        {
            if (Amount == 0) { return false; }   //账户无变动，直接返回成功
            if (Amount > 0 && useFrozen) { useFrozen = false; } //账户增加时，无法使用冻结金额

            using (var service = this.ServiceProvider.CreateScope())
            {
                Entity.SqlContext SqlContext = service.ServiceProvider.GetRequiredService<Entity.SqlContext>();
                CSRedisClient RedisCache = service.ServiceProvider.GetRequiredService<CSRedisClient>();

                CSRedisClientLock CacheLock = null;
                UserAccountShard UserAccount;
                Int64 AccountId;
                String Field = String.Empty, EditSQl = String.Empty, RecordSql = String.Empty, PostChangeSql = String.Empty;
                try
                {
                    CacheLock = RedisCache.Lock($"{CacheLockKey}InitEquity_{userId}", 30);
                    if (CacheLock == null) { return false; }

                    #region 验证账户信息
                    String SelectSql = $"SELECT * FROM `{AccountTableName}` WHERE `UserId` = {userId} LIMIT 1";
                    UserAccount = await SqlContext.Dapper.QueryFirstOrDefaultAsync<UserAccountShard>(SelectSql);
                    if (UserAccount == null)
                    {
                        String InsertSql = $"INSERT INTO `{AccountTableName}` (`UserId`, `Revenue`, `Expenses`, `Balance`, `Frozen`, `ModifyTime`) VALUES ({userId}, '0', '0', '0', '0', NOW())";
                        Int32 rows = await SqlContext.Dapper.ExecuteAsync(InsertSql);
                        if (rows < 1)
                        {
                            return false;
                        }
                        UserAccount = await SqlContext.Dapper.QueryFirstOrDefaultAsync<UserAccountShard>(SelectSql);
                    }
                    if (Amount < 0)
                    {
                        if (useFrozen)
                        {
                            if (UserAccount.Frozen < Math.Abs(Amount) || UserAccount.Balance < Math.Abs(Amount)) { return false; }
                        }
                        else
                        {
                            if (UserAccount.Balance < Math.Abs(Amount)) { return false; }
                            if ((UserAccount.Balance - UserAccount.Frozen) < Math.Abs(Amount)) { return false; }
                        }
                    }
                    #endregion

                    AccountId = UserAccount.AccountId;
                    Field = Amount > 0 ? "Revenue" : "Expenses";

                    EditSQl = $"UPDATE `{AccountTableName}` SET `Balance`=`Balance`+{Amount},{(useFrozen ? $"`Frozen`=`Frozen`+{Amount}," : "")}`{Field}`=`{Field}`+{Math.Abs(Amount)},`ModifyTime`=NOW() WHERE `AccountId`={AccountId} {(useFrozen ? $"AND (`Frozen`+{Amount})>=0;" : $"AND(`Balance`-`Frozen`+{Amount}) >= 0;")}";

                    PostChangeSql = $"IFNULL((SELECT `PostChange` FROM `{RecordTableName}` WHERE `AccountId`={AccountId} ORDER BY `RecordId` DESC LIMIT 1),0)";
                    StringBuilder TempRecordSql = new StringBuilder($"INSERT INTO `{RecordTableName}` ");
                    TempRecordSql.Append("( `AccountId`, `PreChange`, `Incurred`, `PostChange`, `ModifyType`, `ModifyDesc`, `ModifyTime` ) ");
                    TempRecordSql.Append($"SELECT {AccountId} AS `AccountId`, ");
                    TempRecordSql.Append($"{PostChangeSql} AS `PreChange`, ");
                    TempRecordSql.Append($"{Amount} AS `Incurred`, ");
                    TempRecordSql.Append($"{PostChangeSql}+{Amount} AS `PostChange`, ");
                    TempRecordSql.Append($"{(int)modifyType} AS `ModifyType`, ");
                    TempRecordSql.Append($"'{String.Join(',', Desc)}' AS `ModifyDesc`, ");
                    TempRecordSql.Append($"'{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}' AS`ModifyTime`");
                    RecordSql = TempRecordSql.ToString();

                    #region 修改账务
                    using (IDbConnection db = SqlContext.DapperConnection)
                    {
                        db.Open();
                        using (IDbTransaction Tran = db.BeginTransaction())
                        {
                            try
                            {
                                Int32 EditRow = db.Execute(EditSQl, null, Tran);
                                Int32 RecordId = db.Execute(RecordSql, null, Tran);
                                if (EditRow == RecordId && EditRow == 1)
                                {
                                    Tran.Commit();
                                    return true;
                                }
                                Tran.Rollback();
                                return false;
                            }
                            catch (Exception ex)
                            {
                                Tran.Rollback();
                                Yoyo.Core.SystemLog.Debug($"股权账户变更发生错误\r\n修改语句：\r\n{EditSQl}\r\n记录语句：{RecordSql}", ex);
                                return false;
                            }
                            finally { if (db.State == ConnectionState.Open) { db.Close(); } }
                        }
                    }
                    #endregion
                }
                catch (Exception ex)
                {
                    Yoyo.Core.SystemLog.Debug($"股权变更发生错误\r\n修改语句：\r\n{EditSQl}\r\n记录语句：{RecordSql}", ex);
                    return false;
                }
                finally
                {
                    if (null != CacheLock) { CacheLock.Unlock(); }
                }

            }


        }

        public class UserAccountShard
        {
            public long AccountId { get; set; }
            public long UserId { get; set; }
            public decimal Revenue { get; set; }
            public decimal Expenses { get; set; }
            public decimal Balance { get; set; }
            public decimal Frozen { get; set; }
            public DateTime ModifyTime { get; set; }
        }
    }
}
