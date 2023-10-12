using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Yoyo.Core.Expand;
using CSRedis;
using Yoyo.Entity.Models;

namespace Yoyo.Jobs
{
    public class DailyInviteRanking : IJob
    {
        private readonly String AccountTableName = "user_account_equity";
        private readonly String RecordTableName = "user_account_equity_record";
        private readonly String CacheLockKey = "EquityAccount:";

        private readonly IServiceProvider ServiceProvider;


        public DailyInviteRanking(IServiceProvider service)
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

                    #region 分享排行榜分红
                    //======================= 取昨日交易分红信息 =======================//
                    Entity.Models.EverydayDividend Dividend = await SqlContext.EverydayDividend.FirstOrDefaultAsync(o => o.DividendDate == DateTime.Now.Date.AddDays(-1));
                    if (null != Dividend)
                    {

                        #region 分享排行榜分红
                        List<Entity.Models.User> ShareUsers = (await SqlContext.Dapper.QueryAsync<Entity.Models.User>($"SELECT t.UserId AS `id`,t.ShareCount AS `monthlyTradeCount` FROM (SELECT UserId,ClickDate,COUNT(1) AS `ShareCount` FROM yoyo_ad_click WHERE ClickDate='{DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd")}' GROUP BY UserId,ClickDate ORDER BY ShareCount DESC LIMIT 50) AS t WHERE t.ShareCount>=50")).ToList();
                        if (ShareUsers.Count > 0)
                        {
                            StringBuilder ShareGiveCandy = new StringBuilder();
                            StringBuilder ShareRecordCandy = new StringBuilder("insert into `gem_records`(`userId`,`num`,`description`,gemSource) values ");

                            #region 上期开始分红
                            Boolean ShareUseRecord = false;
                            //===============取第一名
                            List<Entity.Models.User> ShareFirstUser = ShareUsers.Take(1).ToList();
                            if (ShareFirstUser.Count > 0)
                            {
                                ShareGiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {Dividend.Star3}) where id in ({string.Join(",", ShareFirstUser.Select(o => o.Id).ToList())});");
                            }
                            foreach (var item in ShareFirstUser)
                            {
                                String ChineseNumber = (ShareUsers.FindIndex(o => o.Id == item.Id) + 1).ToChinese();
                                ShareRecordCandy.Append($"\r\n({item.Id},{Dividend.Star3},'分享达人[榜{ChineseNumber}]全球交易手续费分红奖励',7),");
                                ShareUseRecord = true;
                            }

                            //===============取第二至第十名
                            List<Entity.Models.User> ShareSecondUser = ShareUsers.Skip(1).Take(9).ToList();
                            if (ShareSecondUser.Count > 0)
                            {
                                ShareGiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {Dividend.Star2}) where id in ({string.Join(",", ShareSecondUser.Select(o => o.Id).ToList())});");
                            }
                            foreach (var item in ShareSecondUser)
                            {
                                String ChineseNumber = (ShareUsers.FindIndex(o => o.Id == item.Id) + 1).ToChinese();
                                ShareRecordCandy.Append($"\r\n({item.Id},{Dividend.Star2},'分享达人[榜{ChineseNumber}]全球交易手续费分红奖励',7),");
                                ShareUseRecord = true;
                            }

                            //===============取第十一至第五十名
                            List<Entity.Models.User> ShareThirdUser = ShareUsers.Skip(10).Take(40).ToList();
                            if (ShareThirdUser.Count > 0)
                            {
                                ShareGiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {Dividend.Star1}) where id in ({string.Join(",", ShareThirdUser.Select(o => o.Id).ToList())});");
                            }
                            foreach (var item in ShareThirdUser)
                            {
                                String ChineseNumber = (ShareUsers.FindIndex(o => o.Id == item.Id) + 1).ToChinese();
                                ShareRecordCandy.Append($"\r\n({item.Id},{Dividend.Star1},'分享达人[榜{ChineseNumber}]全球交易手续费分红奖励',7),");
                                ShareUseRecord = true;
                            }
                            #endregion

                            #region 执行分红
                            String ShareRecordCandySqlString = ShareRecordCandy.ToString().TrimEnd(',');
                            String ShareeGiveCandySqlString = ShareGiveCandy.ToString();
                            if (ShareUseRecord)
                            {
                                using (IDbConnection db = SqlContext.DapperConnection)
                                {
                                    db.Open();
                                    IDbTransaction Tran = db.BeginTransaction();
                                    try
                                    {
                                        int UpdateRows = db.Execute(ShareeGiveCandySqlString, null, Tran);
                                        int InsertRows = db.Execute(ShareRecordCandySqlString, null, Tran);
                                        Tran.Commit();
                                    }
                                    catch (Exception ex)
                                    {
                                        Yoyo.Core.SystemLog.Debug("", ex);
                                        Tran.Rollback();
                                    }
                                    finally { if (db.State == ConnectionState.Open) { db.Close(); } }
                                }
                            }
                            #endregion
                        }

                        #endregion


                        //    //==============停掉本期分红==============
                        //    #region 当期分红
                        //    //Int32 Phase = DateTime.Now.Month;   //当前期
                        //    //if (DateTime.Now.Date.Day == 1)
                        //    //{
                        //    //    Phase = DateTime.Now.AddMonths(-1).Month;
                        //    //}
                        //    //List<Entity.Models.MemberInviteRanking> InviteUsers = await SqlContext.MemberInviteRanking.Where(o => o.Phase == Phase && o.InviteTotal >= 100).OrderByDescending(o => o.InviteTotal).Take(50).ToListAsync();

                        //    //if (InviteUsers.Count > 0)
                        //    //{
                        //    //    StringBuilder GiveCandy = new StringBuilder();
                        //    //    StringBuilder RecordCandy = new StringBuilder("insert into `gem_records`(`userId`,`num`,`description`,gemSource) values ");

                        //    //    #region 开始分红
                        //    //    Boolean UseRecord = false;
                        //    //    //===============取第一名
                        //    //    List<Yoyo.Entity.Models.MemberInviteRanking> FirstUser = InviteUsers.Take(1).ToList();
                        //    //    if (FirstUser.Count > 0)
                        //    //    {
                        //    //        GiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {Dividend.Star5}) where id in ({string.Join(",", FirstUser.Select(o => o.UserId).ToList())});");
                        //    //    }
                        //    //    foreach (var item in FirstUser)
                        //    //    {
                        //    //        String ChineseNumber = (InviteUsers.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                        //    //        RecordCandy.Append($"\r\n({item.UserId},{Dividend.Star5},'直推达人[榜{ChineseNumber}]全球交易手续费分红奖励',7),");
                        //    //        UseRecord = true;
                        //    //    }

                        //    //    //===============取第二、第三名
                        //    //    List<Yoyo.Entity.Models.MemberInviteRanking> SecondUser = InviteUsers.Skip(1).Take(2).ToList();
                        //    //    if (SecondUser.Count > 0)
                        //    //    {
                        //    //        GiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {Dividend.Star4}) where id in ({string.Join(",", SecondUser.Select(o => o.UserId).ToList())});");
                        //    //    }
                        //    //    foreach (var item in SecondUser)
                        //    //    {
                        //    //        String ChineseNumber = (InviteUsers.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                        //    //        RecordCandy.Append($"\r\n({item.UserId},{Dividend.Star4},'直推达人[榜{ChineseNumber}]全球交易手续费分红奖励',7),");
                        //    //        UseRecord = true;
                        //    //    }

                        //    //    //===============取第四至第十名
                        //    //    List<Yoyo.Entity.Models.MemberInviteRanking> ThirdUser = InviteUsers.Skip(3).Take(7).ToList();
                        //    //    if (ThirdUser.Count > 0)
                        //    //    {
                        //    //        GiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {Dividend.Star3}) where id in ({string.Join(",", ThirdUser.Select(o => o.UserId).ToList())});");
                        //    //    }
                        //    //    foreach (var item in ThirdUser)
                        //    //    {
                        //    //        String ChineseNumber = (InviteUsers.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                        //    //        RecordCandy.Append($"\r\n({item.UserId},{Dividend.Star3},'直推达人[榜{ChineseNumber}]全球交易手续费分红奖励',7),");
                        //    //        UseRecord = true;
                        //    //    }

                        //    //    //===============取第十一至第二十名
                        //    //    List<Yoyo.Entity.Models.MemberInviteRanking> TourthUser = InviteUsers.Skip(10).Take(10).ToList();
                        //    //    if (TourthUser.Count > 0)
                        //    //    {
                        //    //        GiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {Dividend.Star2}) where id in ({string.Join(",", TourthUser.Select(o => o.UserId).ToList())});");
                        //    //    }
                        //    //    foreach (var item in TourthUser)
                        //    //    {
                        //    //        String ChineseNumber = (InviteUsers.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                        //    //        RecordCandy.Append($"\r\n({item.UserId},{Dividend.Star2},'直推达人[榜{ChineseNumber}]全球交易手续费分红奖励',7),");
                        //    //        UseRecord = true;
                        //    //    }

                        //    //    //===============取第二十一至第五十名
                        //    //    List<Yoyo.Entity.Models.MemberInviteRanking> FifthUser = InviteUsers.Skip(20).Take(30).ToList();
                        //    //    if (FifthUser.Count > 0)
                        //    //    {
                        //    //        GiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {Dividend.Star1}) where id in ({string.Join(",", FifthUser.Select(o => o.UserId).ToList())});");
                        //    //    }
                        //    //    foreach (var item in FifthUser)
                        //    //    {
                        //    //        String ChineseNumber = (InviteUsers.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                        //    //        RecordCandy.Append($"\r\n({item.UserId},{Dividend.Star1},'直推达人[榜{ChineseNumber}]全球交易手续费分红奖励',7),");
                        //    //        UseRecord = true;
                        //    //    }
                        //    //    #endregion

                        //    //    #region 执行分红
                        //    //    String RecordCandySqlString = RecordCandy.ToString().TrimEnd(',');
                        //    //    String GiveCandySqlString = GiveCandy.ToString();
                        //    //    if (UseRecord)
                        //    //    {
                        //    //        using (IDbConnection db = SqlContext.DapperConnection)
                        //    //        {
                        //    //            db.Open();
                        //    //            IDbTransaction Tran = db.BeginTransaction();
                        //    //            try
                        //    //            {
                        //    //                int UpdateRows = db.Execute(GiveCandySqlString, null, Tran);
                        //    //                int InsertRows = db.Execute(RecordCandySqlString, null, Tran);
                        //    //                Tran.Commit();
                        //    //            }
                        //    //            catch (Exception ex)
                        //    //            {
                        //    //                Tran.Rollback();
                        //    //            }
                        //    //            finally { if (db.State == ConnectionState.Open) { db.Close(); } }
                        //    //        }
                        //    //    }
                        //    //    #endregion
                        //    //}
                        //    #endregion

                        //    #region 上期分红
                        //    //Int32 BeforePhase = DateTime.Now.AddMonths(-1).Month;   //上期
                        //    //List<Entity.Models.MemberInviteRanking> BeforeInviteUsers = await SqlContext.MemberInviteRanking.Where(o => o.Phase == BeforePhase && o.InviteTotal >= 100).OrderByDescending(o => o.InviteTotal).Take(10).ToListAsync();

                        //    //if (BeforeInviteUsers.Count > 0)
                        //    //{

                        //    //    StringBuilder BeforeGiveCandy = new StringBuilder();
                        //    //    StringBuilder BeforeRecordCandy = new StringBuilder("insert into `gem_records`(`userId`,`num`,`description`,gemSource) values ");

                        //    //    #region 上期开始分红
                        //    //    Boolean BeforeUseRecord = false;
                        //    //    //===============取第一名
                        //    //    List<Yoyo.Entity.Models.MemberInviteRanking> BeforeFirstUser = BeforeInviteUsers.Take(1).ToList();
                        //    //    if (BeforeFirstUser.Count > 0)
                        //    //    {
                        //    //        BeforeGiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {Dividend.Star4}) where id in ({string.Join(",", BeforeFirstUser.Select(o => o.UserId).ToList())});");
                        //    //    }
                        //    //    foreach (var item in BeforeFirstUser)
                        //    //    {
                        //    //        String ChineseNumber = (BeforeInviteUsers.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                        //    //        BeforeRecordCandy.Append($"\r\n({item.UserId},{Dividend.Star4},'守榜达人[榜{ChineseNumber}]全球交易手续费分红奖励',7),");
                        //    //        BeforeUseRecord = true;
                        //    //    }

                        //    //    //===============取第二、第三名
                        //    //    List<Yoyo.Entity.Models.MemberInviteRanking> BeforeSecondUser = BeforeInviteUsers.Skip(1).Take(2).ToList();
                        //    //    if (BeforeSecondUser.Count > 0)
                        //    //    {
                        //    //        BeforeGiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {Dividend.Star3}) where id in ({string.Join(",", BeforeSecondUser.Select(o => o.UserId).ToList())});");
                        //    //    }
                        //    //    foreach (var item in BeforeSecondUser)
                        //    //    {
                        //    //        String ChineseNumber = (BeforeInviteUsers.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                        //    //        BeforeRecordCandy.Append($"\r\n({item.UserId},{Dividend.Star3},'守榜达人[榜{ChineseNumber}]全球交易手续费分红奖励',7),");
                        //    //        BeforeUseRecord = true;
                        //    //    }

                        //    //    //===============取第四至第十名
                        //    //    List<Yoyo.Entity.Models.MemberInviteRanking> BeforeThirdUser = BeforeInviteUsers.Skip(3).Take(7).ToList();
                        //    //    if (BeforeThirdUser.Count > 0)
                        //    //    {
                        //    //        BeforeGiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {Dividend.Star2}) where id in ({string.Join(",", BeforeThirdUser.Select(o => o.UserId).ToList())});");
                        //    //    }
                        //    //    foreach (var item in BeforeThirdUser)
                        //    //    {
                        //    //        String ChineseNumber = (BeforeInviteUsers.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                        //    //        BeforeRecordCandy.Append($"\r\n({item.UserId},{Dividend.Star2},'守榜达人[榜{ChineseNumber}]全球交易手续费分红奖励',7),");
                        //    //        BeforeUseRecord = true;
                        //    //    }
                        //    //    #endregion

                        //    //    #region 执行上期分红
                        //    //    String BeforeRecordCandySqlString = BeforeRecordCandy.ToString().TrimEnd(',');
                        //    //    String BeforeGiveCandySqlString = BeforeGiveCandy.ToString();
                        //    //    if (BeforeUseRecord)
                        //    //    {
                        //    //        using (IDbConnection db = SqlContext.DapperConnection)
                        //    //        {
                        //    //            db.Open();
                        //    //            IDbTransaction Tran = db.BeginTransaction();
                        //    //            try
                        //    //            {
                        //    //                int UpdateRows = db.Execute(BeforeGiveCandySqlString, null, Tran);
                        //    //                int InsertRows = db.Execute(BeforeRecordCandySqlString, null, Tran);
                        //    //                Tran.Commit();
                        //    //            }
                        //    //            catch (Exception ex)
                        //    //            {
                        //    //                Tran.Rollback();
                        //    //            }
                        //    //            finally { if (db.State == ConnectionState.Open) { db.Close(); } }
                        //    //        }
                        //    //    }
                        //    //    #endregion
                        //    //}
                        //    #endregion
                    }
                    #endregion

                    //============停掉收购排行榜分红============
                    #region 收购排行榜分红
                    List<Entity.Models.MemberDuplicate> UserDuplicates = await SqlContext.MemberDuplicate
                    .Where(o => o.Date == DateTime.Now.Date.AddDays(-1) && o.Duplicate > 1)
                    .OrderByDescending(o => o.Duplicate)
                    .Take(50).ToListAsync();

                    #region 糖果分红   已注释
                    //if (UserDuplicates.Count > 0)
                    //{
                    //    StringBuilder GiveCandy = new StringBuilder();
                    //    StringBuilder RecordCandy = new StringBuilder("insert into `gem_records`(`userId`,`num`,`description`,gemSource) values ");

                    //    #region 开始分红
                    //    Boolean UseRecord = false;
                    //    //===============取第一名
                    //    List<Entity.Models.MemberDuplicate> FirstUser = UserDuplicates.Take(1).ToList();
                    //    foreach (var item in FirstUser)
                    //    {
                    //        GiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {item.Duplicate * 0.05M}) where id={item.UserId};");
                    //        String ChineseNumber = (UserDuplicates.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                    //        RecordCandy.Append($"\r\n({item.UserId},{item.Duplicate * 0.05M},'复投达人[榜{ChineseNumber}]奖励',15),");
                    //        UseRecord = true;
                    //    }

                    //    //===============取第二、第三名
                    //    List<Entity.Models.MemberDuplicate> SecondUser = UserDuplicates.Skip(1).Take(2).ToList();
                    //    foreach (var item in SecondUser)
                    //    {
                    //        GiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {item.Duplicate * 0.04M}) where id={item.UserId};");
                    //        String ChineseNumber = (UserDuplicates.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                    //        RecordCandy.Append($"\r\n({item.UserId},{item.Duplicate * 0.04M},'复投达人[榜{ChineseNumber}]奖励',15),");
                    //        UseRecord = true;
                    //    }

                    //    //===============取第四至第十名
                    //    List<Entity.Models.MemberDuplicate> ThirdUser = UserDuplicates.Skip(3).Take(7).ToList();
                    //    foreach (var item in ThirdUser)
                    //    {
                    //        GiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {item.Duplicate * 0.03M}) where id={item.UserId};");
                    //        String ChineseNumber = (UserDuplicates.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                    //        RecordCandy.Append($"\r\n({item.UserId},{item.Duplicate * 0.03M},'复投达人[榜{ChineseNumber}]奖励',15),");
                    //        UseRecord = true;
                    //    }

                    //    //===============取第十一至第二十名
                    //    List<Entity.Models.MemberDuplicate> TourthUser = UserDuplicates.Skip(10).Take(10).ToList();
                    //    foreach (var item in TourthUser)
                    //    {
                    //        GiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {item.Duplicate * 0.02M}) where id={item.UserId};");
                    //        String ChineseNumber = (UserDuplicates.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                    //        RecordCandy.Append($"\r\n({item.UserId},{item.Duplicate * 0.02M},'复投达人[榜{ChineseNumber}]奖励',15),");
                    //        UseRecord = true;
                    //    }

                    //    //===============取第二十一至第五十名
                    //    List<Entity.Models.MemberDuplicate> FifthUser = UserDuplicates.Skip(20).Take(30).ToList();
                    //    foreach (var item in FifthUser)
                    //    {
                    //        GiveCandy.AppendLine($"update `user` set candyNum = (candyNum + {item.Duplicate * 0.01M}) where id={item.UserId};");
                    //        String ChineseNumber = (UserDuplicates.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                    //        RecordCandy.Append($"\r\n({item.UserId},{item.Duplicate * 0.01M},'复投达人[榜{ChineseNumber}]奖励',15),");
                    //        UseRecord = true;
                    //    }
                    //    #endregion

                    //    #region 执行分红
                    //    String RecordCandySqlString = RecordCandy.ToString().TrimEnd(',');
                    //    String GiveCandySqlString = GiveCandy.ToString();
                    //    if (UseRecord)
                    //    {
                    //        using (IDbConnection db = SqlContext.DapperConnection)
                    //        {
                    //            db.Open();
                    //            IDbTransaction Tran = db.BeginTransaction();
                    //            try
                    //            {
                    //                int UpdateRows = db.Execute(GiveCandySqlString, null, Tran);
                    //                int InsertRows = db.Execute(RecordCandySqlString, null, Tran);
                    //                Tran.Commit();
                    //            }
                    //            catch (Exception ex)
                    //            {
                    //                Tran.Rollback();
                    //            }
                    //            finally { if (db.State == ConnectionState.Open) { db.Close(); } }
                    //        }
                    //    }
                    //    #endregion

                    //}
                    #endregion

                    #region 股权奖励 前三名

                    //MemberDuplicate ListOne = UserDuplicates.Take(1).FirstOrDefault();//第一
                    //if (ListOne != null)
                    //{
                    //    await ChangeWalletAmount(ListOne.UserId, 30, 4, false, "榜一", "30");
                    //}
                    //MemberDuplicate ListTwo = UserDuplicates.Skip(1).Take(1).FirstOrDefault();//第二
                    //if (ListTwo != null)
                    //{
                    //    await ChangeWalletAmount(ListTwo.UserId, 20, 4, false, "榜二", "20");
                    //}
                    //MemberDuplicate ListThree = UserDuplicates.Skip(2).Take(1).FirstOrDefault();//第三
                    //if (ListThree != null)
                    //{
                    //    await ChangeWalletAmount(ListThree.UserId, 10, 4, false, "榜三", "10");
                    //}
                    //List<MemberDuplicate> WinUsers = UserDuplicates.Skip(3).Take(12).ToList();
                    //foreach (var item in WinUsers)
                    //{
                    //    String ChineseNumber = (UserDuplicates.FindIndex(o => o.UserId == item.UserId) + 1).ToChinese();
                    //    await ChangeWalletAmount(item.UserId, 5, 4, false, $"榜{ChineseNumber}", "5");
                    //}
                    #endregion

                    #region 果皮分红 第1至30名  已注释
                    //List<MemberDuplicate> WinUsers = UserDuplicates.Take(30).ToList();
                    //StringBuilder GivePeel = new StringBuilder();
                    //StringBuilder RecordPeel = new StringBuilder("INSERT INTO `user_candyp`(`userId`, `candyP`, `content`, `source`, `createdAt`, `updatedAt`) VALUES ");
                    //foreach (var item in WinUsers)
                    //{
                    //    GivePeel.Append($"UPDATE `user` SET `candyP` = (candyP + {item.Duplicate * 0.20M}) WHERE `id` = {item.UserId};");
                    //    RecordPeel.Append($"\r\n({item.UserId},{item.Duplicate * 0.02M},'收购达人奖励{item.Duplicate * 0.02M}果皮',7,NOW(),NOW()),");
                    //}

                    //String GivePeelSql = GivePeel.ToString();
                    //String RecordPeelSql = RecordPeel.ToString().TrimEnd(',');

                    //if (WinUsers.Count > 0)
                    //{
                    //    using (IDbConnection db = SqlContext.DapperConnection)
                    //    {
                    //        db.Open();
                    //        using (IDbTransaction Tran = db.BeginTransaction())
                    //        {
                    //            try
                    //            {
                    //                int UpdateRows = db.Execute(GivePeelSql, null, Tran);
                    //                int InsertRows = db.Execute(RecordPeelSql, null, Tran);
                    //                Tran.Commit();
                    //            }
                    //            catch (Exception ex)
                    //            {
                    //                Yoyo.Core.SystemLog.Debug("收购达人分红果皮失败", ex);
                    //                Tran.Rollback();
                    //            }
                    //            finally { if (db.State == ConnectionState.Open) { db.Close(); } }
                    //        }
                    //    }
                    //}
                    #endregion

                    #endregion

                    stopwatch.Stop();
                    Core.SystemLog.Jobs($"每日排行榜分红 执行完成,执行时间:{stopwatch.Elapsed.TotalSeconds}秒");
                }
                catch (Exception ex)
                {
                    Core.SystemLog.Jobs("每日排行榜分红 发生错误", ex);
                }
            }


        }



        /// <summary>
        /// 股权账户余额变更
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="Amount"></param>
        /// <param name="useFrozen">使用冻结金额，账户金额增加时，此参数无效</param>
        /// <param name="modifyType">账户变更类型</param>
        /// <param name="Desc">描述</param>
        /// <returns></returns>
        private async Task<Boolean> ChangeWalletAmount(long userId, decimal Amount, Int32 modifyType, bool useFrozen, params string[] Desc)
        {
            if (Amount == 0) { return false; }   //账户无变动，直接返回成功
            if (Amount > 0 && useFrozen) { useFrozen = false; } //账户增加时，无法使用冻结金额

            using (var service = this.ServiceProvider.CreateScope())
            {
                Entity.SqlContext SqlContext = service.ServiceProvider.GetRequiredService<Entity.SqlContext>();
                CSRedisClient RedisCache = service.ServiceProvider.GetRequiredService<CSRedisClient>();

                CSRedisClientLock CacheLock = null;
                UserAccountEquity UserAccount;
                Int64 AccountId;
                String Field = String.Empty, EditSQl = String.Empty, RecordSql = String.Empty, PostChangeSql = String.Empty;
                try
                {
                    CacheLock = RedisCache.Lock($"{CacheLockKey}InitEquity_{userId}", 30);
                    if (CacheLock == null) { return false; }

                    #region 验证账户信息
                    String SelectSql = $"SELECT * FROM `{AccountTableName}` WHERE `UserId` = {userId} LIMIT 1";
                    UserAccount = await SqlContext.Dapper.QueryFirstOrDefaultAsync<UserAccountEquity>(SelectSql);
                    if (UserAccount == null)
                    {
                        String InsertSql = $"INSERT INTO `{AccountTableName}` (`UserId`, `Revenue`, `Expenses`, `Balance`, `Frozen`, `ModifyTime`) VALUES ({userId}, '0', '0', '0', '0', NOW())";
                        Int32 rows = await SqlContext.Dapper.ExecuteAsync(InsertSql);
                        if (rows < 1)
                        {
                            return false;
                        }
                        UserAccount = await SqlContext.Dapper.QueryFirstOrDefaultAsync<UserAccountEquity>(SelectSql);
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

    }
}
