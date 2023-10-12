using Microsoft.Extensions.DependencyInjection;
using Quartz;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Diagnostics;
using System.Data;
using System.Linq;
using Microsoft.Extensions.Options;
using Yoyo.IServices.Response;

namespace Yoyo.Jobs
{
    public class DailyUpdateDividend : IJob
    {
        private readonly IServiceProvider ServiceProvider;

        public DailyUpdateDividend(IServiceProvider service)
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
                    IServices.IMember.ITeams Team = service.ServiceProvider.GetRequiredService<IServices.IMember.ITeams>();
                    List<IServices.Utils.TaskSettings> Settings = service.ServiceProvider.GetRequiredService<IOptionsMonitor<List<IServices.Utils.TaskSettings>>>().CurrentValue;

                    String TableName = $"yoyo_member_dividend_{DateTime.Now.ToString("yyyyMMdd")}";

                    StringBuilder Sql = new StringBuilder();
                    //==============================创建基础数据表==============================//
                    Sql.AppendLine("DROP TABLE IF EXISTS `yoyo_member_ext_tmp`;");
                    Sql.AppendLine("CREATE TABLE `yoyo_member_ext_tmp` (");
                    Sql.AppendLine("  `Id` bigint(20) NOT NULL,");
                    Sql.AppendLine("  `UserID` bigint(20) NOT NULL,");
                    Sql.AppendLine("  `ParentId` bigint(20) NOT NULL,");
                    Sql.AppendLine("  `teamStart` int(11) NOT NULL,");
                    Sql.AppendLine("  `teamCount` int(11) NOT NULL,");
                    Sql.AppendLine("  `authCount` int(11) NOT NULL,");
                    Sql.AppendLine("  `teamCandyH` int(11) NOT NULL,");
                    Sql.AppendLine("  `bigCandyH` int(11) NOT NULL,");
                    Sql.AppendLine("  `littleCandyH` int(11) NOT NULL,");
                    Sql.AppendLine("  PRIMARY KEY (`UserID`),");
                    Sql.AppendLine("	KEY `FK_Id` (`Id`) USING BTREE,");
                    Sql.AppendLine("  KEY `FK_ParentId` (`ParentId`) USING BTREE,");
                    Sql.AppendLine("  KEY `FK_teamCandyH` (`teamCandyH`) USING BTREE");
                    Sql.AppendLine(") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
                    Sql.AppendLine("");
                    //==============================拷贝基础数据==============================//
                    Sql.AppendLine("TRUNCATE TABLE yoyo_member_ext_tmp;");
                    Sql.AppendLine("INSERT INTO yoyo_member_ext_tmp SELECT ");
                    Sql.AppendLine("A.Id,A.UserID,R.ParentId,0 AS teamStart,A.teamCount,A.authCount,A.teamCandyH,0 AS bigCandyH,0 AS littleCandyH FROM (");
                    Sql.AppendLine("SELECT (@i:=@i+1) AS Id,ext.userId AS UserID,ext.authCount,ext.teamCount,ext.teamCandyH FROM user_ext AS ext,(SELECT @i:=0) AS Ids ORDER BY ext.teamCandyH DESC) AS A");
                    Sql.AppendLine("INNER JOIN (SELECT MemberId,ParentId FROM yoyo_member_relation) AS R ON A.userId=R.MemberId");
                    Sql.AppendLine("ORDER BY A.Id;");
                    Sql.AppendLine("");
                    //==============================创建分红基础表==============================//
                    Sql.AppendLine($"DROP TABLE IF EXISTS `{TableName}`;");
                    Sql.AppendLine($"CREATE TABLE `{TableName}` (");
                    Sql.AppendLine("  `UserId` bigint(20) NOT NULL,");
                    Sql.AppendLine("  `teamStart` int(11) NOT NULL DEFAULT '0',");
                    Sql.AppendLine("  `teamCandyH` int(11) NOT NULL DEFAULT '0',");
                    Sql.AppendLine("  `bigCandyH` int(11) NOT NULL DEFAULT '0',");
                    Sql.AppendLine("  `littleCandyH` int(11) NOT NULL DEFAULT '0',");
                    Sql.AppendLine("  PRIMARY KEY (`UserId`)");
                    Sql.AppendLine(") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
                    //==============================写入分红数据==============================//
                    Sql.AppendLine($"TRUNCATE TABLE `{TableName}`;");
                    Sql.AppendLine($"INSERT INTO `{TableName}` SELECT ");
                    Sql.AppendLine("Tmp.UserId,");
                    Sql.AppendLine("(CASE");
                    //Sql.AppendLine("  WHEN Tmp.authCount>=20 AND Tmp.teamCandyH>=1000000 AND IF(Tmp.bigCandyH>Tmp.littleCandyH,Tmp.littleCandyH,Tmp.bigCandyH)>=250000 THEN 5");
                    Sql.AppendLine("  WHEN Tmp.authCount>=20 AND Tmp.teamCandyH>=100000 AND IF(Tmp.bigCandyH>Tmp.littleCandyH,Tmp.littleCandyH,Tmp.bigCandyH)>=25000 THEN 4");
                    Sql.AppendLine("  WHEN Tmp.authCount>=20 AND Tmp.teamCandyH>=8000 AND IF(Tmp.bigCandyH>Tmp.littleCandyH,Tmp.littleCandyH,Tmp.bigCandyH)>=2000 THEN 3");
                    Sql.AppendLine("  WHEN Tmp.authCount>=20 AND Tmp.teamCandyH>=2000 AND IF(Tmp.bigCandyH>Tmp.littleCandyH,Tmp.littleCandyH,Tmp.bigCandyH)>=400 THEN 2");
                    Sql.AppendLine("  WHEN Tmp.authCount>=20 AND Tmp.teamCandyH>=500 THEN 1");
                    Sql.AppendLine("  ELSE 0");
                    Sql.AppendLine("END)AS teamStart,");
                    Sql.AppendLine("Tmp.teamCandyH,");
                    Sql.AppendLine("IF(Tmp.bigCandyH<Tmp.littleCandyH,Tmp.littleCandyH,Tmp.bigCandyH) AS bigCandyH,");
                    Sql.AppendLine("IF(Tmp.bigCandyH>Tmp.littleCandyH,Tmp.littleCandyH,Tmp.bigCandyH) AS littleCandyH");
                    Sql.AppendLine("FROM (");
                    Sql.AppendLine("SELECT ");
                    Sql.AppendLine("A.UserID,A.authCount,A.teamCandyH,");
                    Sql.AppendLine("IFNULL(B.BigCandyH,0) AS bigCandyH,");
                    Sql.AppendLine("IF(A.teamCandyH-IFNULL(B.BigCandyH,0)<0,0,A.teamCandyH-IFNULL(B.BigCandyH,0)) AS littleCandyH");
                    Sql.AppendLine("FROM yoyo_member_ext_tmp AS A LEFT JOIN (");
                    Sql.AppendLine("SELECT A.ParentId AS UserID,SUM(A.teamCandyH) AS BigCandyH FROM (SELECT * FROM yoyo_member_ext_tmp WHERE teamCandyH>0) AS A");
                    Sql.AppendLine("  WHERE (");
                    Sql.AppendLine("    SELECT COUNT(1) FROM (SELECT * FROM yoyo_member_ext_tmp WHERE teamCandyH>0) AS B");
                    Sql.AppendLine("    WHERE B.ParentId=A.ParentId AND B.Id<=A.Id");
                    Sql.AppendLine("   )<2 ");
                    Sql.AppendLine(" GROUP BY A.ParentId) AS B ON A.UserID=B.UserID");
                    Sql.AppendLine(")AS Tmp;");
                    Sql.AppendLine("");
                    //==============================删除基础数据表==============================//
                    Sql.AppendLine("DROP TABLE IF EXISTS `yoyo_member_ext_tmp`;");
                    //==============================更新数据==============================//
                    Sql.AppendLine("");
                    //==============================执行SQL语句==============================//
                    var SqlString = Sql.ToString();
                    SqlContext.Dapper.Execute(SqlString, null, null, 1200);

                    //==============================修改正式表内SQL语句==============================//
                    SqlContext.Dapper.Execute($"UPDATE user_ext AS E INNER JOIN `{TableName}` AS T ON E.userId=T.UserID SET E.bigCandyH=T.bigCandyH,E.littleCandyH=T.littleCandyH,E.teamStart=T.teamStart,E.updateTime=NOW();", null, null, 1200);

                    //==============================取出分红基本数据==============================//
                    int TradeAmount = SqlContext.Dapper.QueryFirstOrDefault<int>($"select IFNULL(sum(num),0) num from `gem_records` where TO_DAYS(now())-TO_DAYS(createdAt)=0 and userId=0");
                    List<TeamInfosDto> StartUsers = SqlContext.Dapper.Query<TeamInfosDto>($"SELECT ue.*, u.`Mobile` FROM `user_ext` AS ue LEFT JOIN `user` AS u ON ue.userId = u.id WHERE ue.`teamStart` >= 1 AND u.candyNum >= 0 ORDER BY ue.`teamStart` DESC;").ToList();

                    Dictionary<int, decimal[]> StarAmounts = new Dictionary<int, decimal[]>
                    {
                        {1,new decimal[]{ 0.075M, 40 } },     //1星10%----注入40个分享排行榜
                        {2,new decimal[]{ 0.20M, 16 } },     //2星20%----注入9个分享排行榜----注入7个守榜
                        {3,new decimal[]{ 0.15M, 3 } },     //3星15%----注入1个分享排行榜----注入2个守榜
                        {4,new decimal[]{ 0.10M, 1 } },     //4星10%----注入1个守榜
                        //{5,new decimal[]{ 0.05M, 1 } }      //5星5%----注入1个空人头
                    };

                    #region 写入每日分红记录
                    String TodayDate = DateTime.Now.ToString("yyyy-MM-dd");
                    await SqlContext.Dapper.ExecuteAsync($"INSERT INTO `yoyo_everyday_dividend` VALUES ('{TodayDate}', {TradeAmount}, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)");
                    #endregion

                    //==============================构建分红SQL语句==============================//
                    for (int i = 1; i <= StarAmounts.Count; i++)
                    {
                        List<TeamInfosDto> TotalUser = new List<TeamInfosDto>();
                        List<TeamInfosDto> Losers = new List<TeamInfosDto>();
                        List<TeamInfosDto> CurrentUser = StartUsers.Where(o => o.TeamStart == i).ToList();    //取出当前星级用户
                        for (int gl = 0; gl < CurrentUser.Count; gl++) //活跃度够的用户
                        {
                            Int32 ActiveDegree = 0;//活跃度

                            List<Int64> UserIds = SqlContext.Dapper.Query<Int64>("SELECT id FROM `user` WHERE `auditState`=2 AND `status`=0 AND `inviterMobile`= @UserMobile;", new { UserMobile = CurrentUser[gl].Mobile }).ToList();
                            if (UserIds.Count < 1) { continue; }
                            StringBuilder QueryActiveSql = new StringBuilder();
                            QueryActiveSql.Append("SELECT COUNT(id) FROM gem_records WHERE gemSource = 1 AND TO_DAYS(createdAt) = TO_DAYS(DATE_ADD(NOW(),INTERVAL -1 DAY)) AND userId IN (");
                            QueryActiveSql.Append(String.Join(",", UserIds));
                            QueryActiveSql.Append(");");
                            ActiveDegree = SqlContext.Dapper.QueryFirstOrDefault<Int32>(QueryActiveSql.ToString());
                            
                            //新矿机
                            StringBuilder QueryActiveSql2 = new StringBuilder();
                            QueryActiveSql2.Append("select count(id) count from (SELECT * FROM gem_records WHERE gemSource = 1001 AND TO_DAYS(createdAt) = TO_DAYS(DATE_ADD(NOW(),INTERVAL -1 DAY)) AND userId IN (");
                            QueryActiveSql2.Append(String.Join(",", UserIds));
                            QueryActiveSql2.Append(") GROUP BY userId) a");

                            var ActiveDegree2 = SqlContext.Dapper.QueryFirstOrDefault<Int32>(QueryActiveSql2.ToString());

                            ActiveDegree += ActiveDegree2;

                            if (ActiveDegree >= 20)
                            {
                                TotalUser.Add(CurrentUser[gl]);
                            }
                            else
                            {
                                Losers.Add(CurrentUser[gl]);
                            }
                        }
                        int PeopleCount = TotalUser.Count + (int)StarAmounts[i][1];      //今日享受分红的人数
                        decimal ThisStarAmount = TradeAmount * StarAmounts[i][0] / PeopleCount; //计算当前星级每人分红金额

                        await SqlContext.Dapper.ExecuteAsync($"UPDATE `yoyo_everyday_dividend` SET `Star{i}`={ThisStarAmount}, `People{i}`={PeopleCount} WHERE (`DividendDate`='{TodayDate}')");

                        List<long> AllUserIds = TotalUser.Select(o => o.UserId).ToList();

                        if (AllUserIds.Count == 0) { continue; }

                        //==============================构建分红SQL语句==============================//
                        StringBuilder AmountSql = new StringBuilder("insert into `gem_records`(`userId`,`num`,`description`,gemSource) values ");
                        foreach (var item in AllUserIds)
                        {
                            AmountSql.Append($"\r\n({item},{ThisStarAmount},'{i}星达人全球交易手续费分红奖励',6),");
                        }
                        String AmountSqlString = AmountSql.ToString().TrimEnd(',');

                        StringBuilder LoserSql = new StringBuilder("INSERT INTO `gem_records` ( `userId`, `num`, `description`, gemSource ) VALUES ");
                        foreach (var item in Losers)
                        {
                            LoserSql.Append($"\r\n({item.UserId},0,'直推活跃人数未满足20人,不享受今日达人分红',6),");
                        }
                        String LoserSqlString = LoserSql.ToString().TrimEnd(',');
                        //==============================执行分红操作==============================//
                        String AmountGiveSqlString = $"update `user` set candyNum = (candyNum + {ThisStarAmount}) where id in ({string.Join(",", AllUserIds)})";
                        using (IDbConnection db = SqlContext.DapperConnection)
                        {
                            db.Open();
                            IDbTransaction Tran = db.BeginTransaction();
                            try
                            {
                                if (ThisStarAmount > 0)
                                {
                                    db.Execute(AmountGiveSqlString, null, Tran);
                                    db.Execute(AmountSqlString, null, Tran);
                                    db.Execute(LoserSqlString, null, Tran);
                                    Tran.Commit();
                                }
                            }
                            catch (Exception ex)
                            {
                                Core.SystemLog.Jobs($"每日分红发生错误,分红语句:{AmountGiveSqlString}\r\n\r\n记录语句:{AmountSqlString}", ex);
                                Tran.Rollback();
                            }
                            finally { if (db.State == ConnectionState.Open) { db.Close(); } }
                        }

                        //星级达人送矿机停掉
                        //     for (int ii = i; ii >= 1; ii--)
                        //     {
                        //         if (AllUserIds.Count == 0) { continue; }

                        //         var iii = -1;
                        //         switch (ii)
                        //         {
                        //             case 5: iii = 105; break;
                        //             case 4: iii = 105; break;
                        //             case 3: iii = 103; break;
                        //             case 2: iii = 101; break;
                        //             case 1: iii = 100; break;
                        //         }

                        //         if (iii <= 0) { return; }
                        //         //==============================取需要给予任务的用户ID==============================//
                        //         List<long> GiveUserIds = SqlContext.Dapper.Query<long>($"select userId as count from minnings where minningId IN ({ii},{iii}) and source=2 and userId in ({string.Join(",", AllUserIds)})").ToList();
                        //         List<long> NoGiveTaskUserIds = AllUserIds.Where(o => !GiveUserIds.Contains(o)).ToList();
                        //         //==============================构建任务赠送SQL语句==============================//
                        //         var effectiveBiginTime = DateTime.Now.Date.AddDays(1).ToLocalTime().ToString("yyyy-MM-dd");
                        //         var effectiveEndTime = DateTime.Now.Date.AddDays(31).ToLocalTime().ToString("yyyy-MM-dd");
                        //         StringBuilder StarTaskSql = new StringBuilder("insert into minnings (userId, minningId, beginTime, endTime, source) values ");
                        //         StringBuilder StarTaskRecordSql = new StringBuilder("insert into notice_infos (userId, content, refId, type,title) values ");
                        //         foreach (var item in NoGiveTaskUserIds)
                        //         {
                        //             StarTaskSql.Append($"\r\n({item}, {iii},'{effectiveBiginTime}' , '{effectiveEndTime}',2),");
                        //             StarTaskRecordSql.Append($"\r\n({item}, '达人奖励任务已发送','minningAward','1','系统赠送'),");
                        //         }
                        //         String TaskSqlString = StarTaskSql.ToString().TrimEnd(',');
                        //         String TaskRecordSqlString = StarTaskRecordSql.ToString().TrimEnd(',');
                        //         if (NoGiveTaskUserIds.Count > 0)
                        //         {
                        //             using (IDbConnection db = SqlContext.DapperConnection)
                        //             {
                        //                 db.Open();
                        //                 IDbTransaction Tran = db.BeginTransaction();
                        //                 try
                        //                 {
                        //                     db.Execute(TaskSqlString, null, Tran);
                        //                     db.Execute(TaskRecordSqlString, null, Tran);
                        //                     Tran.Commit();
                        //                 }
                        //                 catch (Exception ex)
                        //                 {
                        //                     Core.SystemLog.Jobs($"每日赠送任务发生错误:赠送语句:{TaskSqlString}\r\n\r\n记录语句:{TaskRecordSqlString}", ex);
                        //                     Tran.Rollback();
                        //                 }
                        //                 finally { if (db.State == ConnectionState.Open) { db.Close(); } }
                        //             }
                        //         }
                        //         IServices.Utils.TaskSettings TaskSetting = Settings.FirstOrDefault(o => o.TaskLevel == iii);
                        //         if (null == TaskSetting) { continue; }

                        //         //===赠送果核的任务
                        //         foreach (var item in NoGiveTaskUserIds)
                        //         {
                        //             try
                        //             {
                        //                 RspMemberRelation Relation = await Team.GetRelation(item);
                        //                 await Team.UpdateTeamKernel(Relation.MemberId, TaskSetting.TeamCandyH);
                        //             }
                        //             catch (Exception ex)
                        //             {
                        //                 Core.SystemLog.Jobs($"任务赠送团队果核发生错误,用户:{item},任务等级:{TaskSetting.TaskLevel},赠送数量:{TaskSetting.TeamCandyH}", ex);
                        //             }
                        //         }

                        //     }
                    }

                    #region 股权分红

                    try
                    {
                        Decimal EquityTotal = 10000.0000M * 1000;
                        Decimal SingleStock = TradeAmount * 0.2000M / EquityTotal;

                        StringBuilder EquityDividendSql = new StringBuilder();

                        EquityDividendSql.Append("UPDATE `user` AS u, (SELECT UserId, Balance FROM user_account_equity WHERE Balance >= 100) AS e ");
                        EquityDividendSql.Append("SET u.candyNum = u.candyNum + e.Balance * @SingleStock ");
                        EquityDividendSql.Append("WHERE u.id = e.UserId;");

                        EquityDividendSql.Append("INSERT INTO `gem_records` ( `userId`, `num`, `description`, `gemSource` ) ");
                        EquityDividendSql.Append("SELECT UserId, Balance * @SingleStock AS num, CONCAT('股权',TRUNCATE(Balance,0),'份,交易分红：',TRUNCATE(Balance * @SingleStock,4),'糖果') AS description, 90 AS gemSource ");
                        EquityDividendSql.Append("FROM user_account_equity WHERE Balance >= 100;");

                        SqlContext.Dapper.Execute(EquityDividendSql.ToString(), new { SingleStock });
                    }
                    catch (Exception ex)
                    {
                        Core.SystemLog.Jobs($"每日股权分红 执行异常", ex);
                    }

                    #endregion

                    stopwatch.Stop();
                    Core.SystemLog.Jobs($"每日分红 执行完成,执行时间:{stopwatch.Elapsed.TotalSeconds}秒");
                }
                catch (Exception ex)
                {
                    Core.SystemLog.Jobs("每日分红 发生错误", ex);
                }
            }
        }

        /// <summary>
        /// 团队星级
        /// </summary>
        public class TeamStar
        {
            /// <summary>
            /// 用户ID
            /// </summary>
            public long UserId { get; set; }
            /// <summary>
            /// 团队星级
            /// </summary>
            public int TeamStart { get; set; }
            /// <summary>
            /// 直推星级
            /// </summary>
            public int StartLevel { get; set; }
            /// <summary>
            /// 直推星级数量
            /// </summary>
            public int StartCount { get; set; }
        }

        /// <summary>
        /// 团队用户信息表
        /// </summary>
        public class TeamInfosDto
        {
            /// <summary>
            /// ???
            /// </summary>
            public long Id { get; set; }
            /// <summary>
            /// 用户ID
            /// </summary>
            public long UserId { get; set; }
            /// <summary>
            /// 直推等级
            /// </summary>
            /// <value></value>
            public int AuthCount { get; set; } = 0;
            /// <summary>
            /// 大区果核
            /// </summary>
            /// <value></value>
            public int BigCandyH { get; set; } = 0;
            /// <summary>
            /// 小区果核
            /// </summary>
            /// <value></value>
            public int LittleCandyH { get; set; } = 0;
            /// <summary>
            /// 团队果核
            /// </summary>
            /// <value></value>
            public int TeamCandyH { get; set; } = 0;
            /// <summary>
            /// 团队人数
            /// </summary>
            /// <value></value>
            public int TeamCount { get; set; } = 0;
            /// <summary>
            /// 团队等级
            /// </summary>
            /// <value></value>
            public int TeamStart { get; set; } = 0;
            /// <summary>
            /// 用户手机号
            /// </summary>
            public string Mobile { get; set; }
        }
    }
}
