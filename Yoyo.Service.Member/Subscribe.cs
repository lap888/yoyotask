using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SqlServer.Server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yoyo.Core.Expand;
using Yoyo.Entity.Enums;
using Yoyo.Entity.Models;
using Yoyo.IServices.ICityPartner;
using Yoyo.IServices.Models;
using Yoyo.IServices.Response;

namespace Yoyo.Service.Member
{
    public class Subscribe : IServices.IMember.ISubscribe
    {
        private readonly IServiceProvider ServiceProvider;
        private readonly FraudUsers Frauds;

        public Subscribe(IServiceProvider serviceProvider, IOptionsMonitor<FraudUsers> monitor)
        {
            this.ServiceProvider = serviceProvider;
            this.Frauds = monitor.CurrentValue;
        }

        /// <summary>
        /// 消息订阅用户注册事件
        /// </summary>
        /// <param name="Msg">消息主体</param>
        /// <returns></returns>
        public async Task SubscribeMemberRegist(String Msg)
        {
            using (var service = this.ServiceProvider.CreateScope())
            {
                try
                {
                    IServices.IMember.ITeams Team = service.ServiceProvider.GetRequiredService<IServices.IMember.ITeams>();
                    IServices.Request.ReqTeamSubscribeInfo info = Msg.JsonTo<IServices.Request.ReqTeamSubscribeInfo>();
                    RspMemberRelation Relation = await Team.SetRelation(info.MemberId, info.ParentId);
                    await Team.UpdateTeamPersonnel(info.MemberId, 1);
                }
                catch (Exception ex)
                {
                    Core.SystemLog.Debug($"消息订阅用户注册事件 发生错误\r\n消息内容===>PUBLISH YoYo_Member_Regist \"{Msg}\"", ex);
                }
            }
        }

        /// <summary>
        /// 消息订阅用户认证事件
        /// </summary>
        /// <param name="Msg">消息主体</param>
        /// <returns></returns>
        public async Task SubscribeMemberCertified(String Msg)
        {
            using (var service = this.ServiceProvider.CreateScope())
            {
                try
                {
                    IServices.IMember.ITeams Team = service.ServiceProvider.GetRequiredService<IServices.IMember.ITeams>();
                    Entity.SqlContext SqlContext = service.ServiceProvider.GetRequiredService<Entity.SqlContext>();
                    CSRedis.CSRedisClient Redis = service.ServiceProvider.GetRequiredService<CSRedis.CSRedisClient>();
                    IServices.Request.ReqTeamSubscribeInfo info = Msg.JsonTo<IServices.Request.ReqTeamSubscribeInfo>();
                    RspMemberRelation Relation = await Team.GetRelation(info.MemberId);
                    await Team.UpdateTeamDirectPersonnel(Relation.ParentId, Entity.Utils.MemberAuthStatus.CERTIFIED);
                    await Team.UpdateTeamKernel(Relation.MemberId, 6);

                    #region 拉新分红
                    try
                    {
                        int ModifyType = 10, CityModify = 11;
                        var UserInfo = new User();//SqlContext.User.FirstOrDefault(o => o.Id == Relation.MemberId && o.MonthlyTradeCount == 0);
                        var BaseTaskSql = "SELECT COUNT(1) AS Total FROM minnings WHERE userId={0} AND `status`=1 AND TO_DAYS(beginTime)<=TO_DAYS(Now()) AND TO_DAYS(endTime)>=TO_DAYS(Now()) AND minningId>=101";
                        if (UserInfo != null)
                        {
                            #region 直推奖励
                            //if (Relation != null)
                            //{
                            //    var ParentId = Relation.ParentId;
                            //    if (ParentId == 0) { ParentId = 2; }
                            //    ChangeWalletAmount(SqlContext.DapperConnection, ParentId, ModifyType, 0.3M, UserInfo.Name, UserInfo.Id.ToString(), "邀请新用户");
                            //}
                            #endregion

                            #region 上代奖励
                            //RspMemberRelation UpRelation = await Team.GetRelation(Relation.ParentId);
                            //if (UpRelation != null)
                            //{
                            //    var ParentId = UpRelation.ParentId;
                            //    if (SqlContext.Dapper.QueryFirstOrDefault<int>(String.Format(BaseTaskSql, ParentId)) > 0 && ParentId > 0)
                            //    {
                            //        ChangeWalletAmount(SqlContext.DapperConnection, ParentId, ModifyType, 0.1M, UserInfo.Name, UserInfo.Id.ToString(), "间推收益");
                            //    }
                            //}
                            #endregion

                            #region 隔代奖励
                            //RspMemberRelation UpUpRelation = await Team.GetRelation(UpRelation.ParentId);
                            //if (UpUpRelation != null)
                            //{
                            //    var ParentId = UpUpRelation.ParentId;
                            //    if (ParentId == 0) { ParentId = 20; }
                            //    if (SqlContext.Dapper.QueryFirstOrDefault<int>(String.Format(BaseTaskSql, ParentId)) > 0)
                            //    {
                            //        ChangeWalletAmount(SqlContext.DapperConnection, ParentId, ModifyType, 0.05M, UserInfo.Name, UserInfo.Id.ToString(), "隔代收益");
                            //    }
                            //    else
                            //    {
                            //        //====悟道隔代收益
                            //        ChangeWalletAmount(SqlContext.DapperConnection, 20, ModifyType, 0.05M, UserInfo.Name, UserInfo.Id.ToString(), "隔代收益");
                            //    }
                            //}
                            #endregion

                            #region 城主奖励
                            // decimal CityMoney = 0.05M;
                            // var UserCity = SqlContext.Dapper.QueryFirstOrDefault<UserLocations>($"SELECT UserId,City,CityCode FROM user_locations WHERE UserId={UserInfo.Id}");
                            // if (UserCity != null)
                            // {
                            //     var CityInfo = SqlContext.Dapper.QueryFirstOrDefault<CityMaster>($"SELECT UserId,CityCode,StartTime,EndTime FROM `yoyo_city_master` WHERE CityCode='{UserCity.CityCode}'");
                            //     if (CityInfo != null)
                            //     {
                            //         if (CityInfo.StartTime < (new DateTime(2020, 06, 01))) { CityMoney = CityMoney * 2; }
                            //         ChangeWalletAmount(SqlContext.DapperConnection, CityInfo.UserId, CityModify, CityMoney, UserInfo.Name, UserInfo.Id.ToString(), UserCity.City, "合伙人收益");

                            //         #region 更新城主 现金账户
                            //         SqlContext.Dapper.Execute("UPDATE city_earnings SET Cash = Cash + @CityMoney WHERE CityNo = CityNo;", new { CityMoney, CityNo = CityInfo.CityCode });
                            //         #endregion
                            //     }
                            // }
                            #endregion

                            // UserInfo.MonthlyTradeCount = 1;
                            // SqlContext.User.Update(UserInfo);
                            // SqlContext.SaveChanges();
                        }
                    }
                    catch (Exception ex)
                    {
                        Core.SystemLog.Debug($"用户认证分红 发生错误\r\n消息内容===>PUBLISH YoYo_Member_Certified \"{Msg}\"", ex);
                    }
                    #endregion

                    // Int32 Phase = DateTime.Now.Month;   //当前期
                    // Entity.Models.MemberInviteRanking InviteUser = await SqlContext.MemberInviteRanking.FirstOrDefaultAsync(o => o.Phase == Phase && o.UserId == Relation.ParentId);

                    #region 用户邀请排行榜
                    // try
                    // {
                    //     if (InviteUser == null)
                    //     {
                    //         InviteUser = SqlContext.MemberInviteRanking.Add(new Entity.Models.MemberInviteRanking
                    //         {
                    //             Phase = Phase,
                    //             UserId = Relation.ParentId,
                    //             InviteTotal = 1,
                    //             InviteToday = 1,
                    //             InviteDate = DateTime.Now.Date
                    //         }).Entity;
                    //     }
                    //     else
                    //     {
                    //         if (InviteUser.InviteDate != DateTime.Now.Date) { InviteUser.InviteToday = 0; } //不是当日，本日累计清零
                    //         InviteUser.InviteToday += 1;
                    //         InviteUser.InviteTotal += 1;
                    //         InviteUser.InviteDate = DateTime.Now.Date;
                    //         InviteUser = SqlContext.MemberInviteRanking.Update(InviteUser).Entity;
                    //     }
                    //     SqlContext.SaveChanges();
                    // }
                    // catch (Exception ex) { Core.SystemLog.Debug($"邀请用户排行榜 发生错误\r\n消息内容===>PUBLISH YoYo_Member_Certified \"{Msg}\"", ex); }
                    #endregion

                    #region 推送基础任务
                    // await SubscribeTask(new IServices.Utils.DailyTask { UserId = InviteUser.UserId, TaskType = IServices.Utils.TaskType.USER_AUTH, CarryOut = 1 });
                    #endregion

                    //============停掉平台收单
                    #region 平台收单功能

                    //#region 验证交易时间
                    //if (DateTime.Now > DateTime.Now.Date.AddHours(23) || DateTime.Now < DateTime.Now.Date.AddHours(8)) { return; }
                    //#endregion

                    //#region 验证用户是否有效
                    //if (SqlContext.User.FirstOrDefault(o => o.Id == info.MemberId && o.CCount == 0) == null) { return; }
                    //#endregion

                    //#region 平台基础人数
                    //IServices.Utils.SystemTrade SysTradeInfo;
                    //long InUser = Redis.IncrBy($"System_Order:{DateTime.Now.ToString("yyyyMMdd")}", 1) + 1;
                    //String TradeInfo = $"System_Order:TradeInfo";
                    //if (Redis.Exists(TradeInfo))
                    //{
                    //    SysTradeInfo = Redis.Get<IServices.Utils.SystemTrade>(TradeInfo);
                    //}
                    //else
                    //{
                    //    SysTradeInfo = new IServices.Utils.SystemTrade();
                    //    Redis.Set(TradeInfo, SysTradeInfo);
                    //}
                    //if (InUser % SysTradeInfo.BaseUser != 0) { return; }
                    //#endregion

                    //#region 生成订单号
                    //var orderdate = DateTime.Now.ToString("ddHHmmssffffff");
                    //var ordercode = Guid.NewGuid().GetHashCode();
                    //var num = 20 - orderdate.Length;
                    //if (ordercode < 0) { ordercode = -ordercode; }
                    //var orderlast = ordercode.ToString().Length > num ? ordercode.ToString().Substring(0, num) : ordercode.ToString().PadLeft(num, '0');
                    //var SysOrderNo = $"{orderdate}{orderlast}";
                    //#endregion

                    //#region 发布订单
                    //var OrderPrice = SysTradeInfo.BuyAmount * SysTradeInfo.BuyPrice;      //收购价格
                    //var OrderId = SqlContext.Dapper.ExecuteScalar<long>($"insert into coin_trade(tradeNumber,buyerUid,buyerAlipay,amount,price,totalPrice,fee,trendSide,status)values('{SysOrderNo}',1,'SYSTEM',{SysTradeInfo.BuyAmount},{SysTradeInfo.BuyPrice},{OrderPrice},0,'BUY',1);SELECT @@IDENTITY");
                    //SqlContext.Dapper.Execute($"INSERT INTO `coin_trade_location` (`TradeId`, `Buyer_Location_X`, `Buyer_Location_Y`, `Buyer_Province`, `Buyer_City`, `Buyer_Area`) VALUES ({OrderId}, 0, 0, '','', '')");
                    //#endregion

                    #endregion

                }
                catch (Exception ex)
                {
                    Core.SystemLog.Debug($"消息订阅用户认证事件 发生错误\r\n消息内容===>PUBLISH YoYo_Member_Certified \"{Msg}\"", ex);
                }
            }
        }

        /// <summary>
        /// 消息订阅任务开启事件
        /// </summary>
        /// <param name="Msg"></param>
        /// <returns></returns>
        public async Task SubscribeTaskAction(String Msg)
        {
            using (var service = this.ServiceProvider.CreateScope())
            {
                try
                {
                    IServices.Request.ReqTeamSubscribeInfo info = Msg.JsonTo<IServices.Request.ReqTeamSubscribeInfo>();

                    //==============================查找任务配置==============================//
                    List<IServices.Utils.TaskSettings2> Settings = service.ServiceProvider.GetRequiredService<IOptionsMonitor<List<IServices.Utils.TaskSettings2>>>().CurrentValue;
                    IServices.Utils.TaskSettings2 TaskSetting = Settings.FirstOrDefault(o => o.TaskLevel == info.TaskLevel);
                    if (null == TaskSetting)
                    {
                        Core.SystemLog.Warn($"消息订阅任务开启事件,未找到任务配置.\r\n{Msg}");
                        return;
                    }
                    //==============================查找任务配置==============================//
                    // if (!info.RenewTask)
                    // {
                    //     IServices.IMember.ITeams Team = service.ServiceProvider.GetRequiredService<IServices.IMember.ITeams>();
                    //     RspMemberRelation Relation = await Team.GetRelation(info.MemberId);
                    //     await Team.UpdateTeamKernel(Relation.MemberId, TaskSetting.TeamCandyH);
                    // }
                    IServices.IMember.ITeams Team = service.ServiceProvider.GetRequiredService<IServices.IMember.ITeams>();
                    RspMemberRelation Relation = await Team.GetRelation(info.MemberId);
                    await Team.UpdateTeamKernel(Relation.MemberId, TaskSetting.TeamCandyH);
                    //==========停止任务赠送活跃贡献值
                    //if (info.Devote > 0) { await SubscribeDevote(info.MemberId, info.Devote); }
                }
                catch (Exception ex)
                {
                    Core.SystemLog.Debug($"消息订阅任务开启事件 发生错误\r\n消息内容===>PUBLISH YoYo_Member_TaskAction \"{Msg}\"", ex);
                }

            }
        }

        /// <summary>
        /// 广告点击方法订阅
        /// </summary>
        /// <param name="Msg"></param>
        /// <returns></returns>
        public async Task SubscribeClickAd(String Msg)
        {
            IServices.Utils.ClickAd info = Msg.JsonTo<IServices.Utils.ClickAd>();
            if (null == info) { return; }
            if (info.AdId == 0 || String.IsNullOrWhiteSpace(info.Code) || String.IsNullOrWhiteSpace(info.UserCode)) { return; }
            using (var service = this.ServiceProvider.CreateScope())
            {
                try
                {
                    Entity.SqlContext SqlContext = service.ServiceProvider.GetRequiredService<Entity.SqlContext>();
                    CSRedis.CSRedisClient Redis = service.ServiceProvider.GetRequiredService<CSRedis.CSRedisClient>();
                    IPlugins.IWeChatPlugin WeChat = service.ServiceProvider.GetRequiredService<IPlugins.IWeChatPlugin>();
                    IServices.Utils.ClickCandyP Settings = service.ServiceProvider.GetRequiredService<IOptionsMonitor<IServices.Utils.ClickCandyP>>().CurrentValue;
                    String OpenIdCacheKey = "ClickOpenID:{0}";
                    String OpenId = String.Empty;
                    Decimal CandyP = new Random(Guid.NewGuid().GetHashCode()).Next(Settings.OneMin, Settings.OneMax) * Settings.OneUnit;
                    Int64 UserId = 0;

                    #region 判断是否给予果皮
                    if (CandyP == 0) { return; }
                    #endregion

                    #region 获取用户OPENID并验证是否初次点击
                    OpenId = await WeChat.GetOpenId(info.Code);
                    if (String.IsNullOrWhiteSpace(OpenId)) { return; }
                    if (Redis.Exists(String.Format(OpenIdCacheKey, OpenId))) { return; }
                    if ((await SqlContext.AdClick.FirstOrDefaultAsync(o => o.ClickId == OpenId && o.ClickDate == DateTime.Now.Date)) != null)
                    {
                        Redis.Set(String.Format(OpenIdCacheKey, OpenId), Msg, (int)(DateTime.Now.AddDays(1).Date - DateTime.Now).TotalSeconds);
                        return;
                    }
                    #endregion

                    #region 验证广告是否存在
                    String AdCacheKey = $"BannerInfo:{info.AdId}";
                    if (!Redis.Exists(AdCacheKey)) { return; }
                    #endregion

                    #region 检测广告果皮是否超标
                    Decimal MaxAdCandyP = Settings.AdMax;
                    Decimal ADCandyP = SqlContext.AdClick.Where(o => o.AdId == info.AdId && o.ClickDate == DateTime.Now.Date).Sum(e => e.CandyP);
                    if (ADCandyP >= MaxAdCandyP) { return; }
                    CandyP = (ADCandyP + CandyP) > MaxAdCandyP ? MaxAdCandyP - ADCandyP : CandyP;
                    #endregion

                    #region 校验邀请码是否有效
                    Entity.Models.User UserInfo = await SqlContext.User.FirstOrDefaultAsync(o => o.Rcode == info.UserCode || o.Mobile == info.UserCode);
                    if (null == UserInfo) { return; }
                    if (UserInfo.AuditState != 2 || UserInfo.Status != 0) { return; }   //未实名或封禁，无效
                    UserId = UserInfo.Id;
                    if (UserId == 0) { return; }
                    #endregion

                    #region 校验用户获取果皮是否超标
                    Decimal UserCandyP = Settings.UserMax;
                    Decimal GiveCandyP = SqlContext.AdClick.Where(o => o.UserId == UserId && o.ClickDate == DateTime.Now.Date).Sum(e => e.CandyP);
                    if (GiveCandyP >= UserCandyP) { return; }
                    CandyP = (CandyP + GiveCandyP) > UserCandyP ? UserCandyP - GiveCandyP : CandyP;
                    #endregion

                    #region 写入当日首次点击
                    if (Redis.Exists(String.Format(OpenIdCacheKey, OpenId))) { return; }
                    Redis.Set(String.Format(OpenIdCacheKey, OpenId), Msg, (int)(DateTime.Now.AddDays(1).Date - DateTime.Now).TotalSeconds);
                    var AdClick = SqlContext.AdClick.Add(new Entity.Models.AdClick { AdId = info.AdId, UserId = UserId, ClickId = OpenId, CandyP = CandyP, ClickDate = DateTime.Now.Date, ClickTime = DateTime.Now }).Entity;
                    SqlContext.SaveChanges();
                    #endregion

                    #region 给予用户果皮
                    Boolean SendTask = false;
                    using (IDbConnection Db = SqlContext.DapperConnection)
                    {
                        Db.Open();
                        IDbTransaction Tran = Db.BeginTransaction();
                        try
                        {
                            var Row = Db.Execute($"insert into `user_candyp`(`userId`,`candyP`,`content`,`source`,`createdAt`,`updatedAt`) values({UserId},{CandyP},'分享广告赠送{CandyP}果皮',4,now(),now());", null, Tran);
                            var Row1 = Db.Execute($"update `user` set `candyP`=(`candyP`+{CandyP}) where `id`={UserId};", null, Tran);
                            if (Row == Row1 && Row == 1)
                            {
                                Tran.Commit();
                                Redis.Set($"ClickHaveCandyP:{info.AdId}", MaxAdCandyP - (ADCandyP + GiveCandyP), (int)(DateTime.Now.Date.AddDays(1) - DateTime.Now).TotalSeconds);
                                SendTask = true;
                            }
                            else
                            {
                                Tran.Rollback();
                            }
                        }
                        catch { Tran.Rollback(); }
                        finally { if (Db.State == ConnectionState.Open) { Db.Close(); } }
                    }
                    #endregion

                    #region 推送基础任务
                    if (SendTask)
                    {
                        await SubscribeTask(new IServices.Utils.DailyTask { UserId = UserId, TaskType = IServices.Utils.TaskType.SHARE_AD, CarryOut = 1 });
                    }
                    #endregion
                }
                catch (Exception ex)
                {
                    Core.SystemLog.Error("广告点击发生错误", ex);
                    return;
                }
            }
        }

        /// <summary>
        /// 每日基础任务
        /// </summary>
        /// <param name="Msg"></param>
        /// <returns></returns>
        public async Task SubscribeTask(String Msg)
        {
            IServices.Utils.DailyTask info = Msg.JsonTo<IServices.Utils.DailyTask>();
            if (null == info) { return; }
            await SubscribeTask(info);
        }

        /// <summary>
        /// 更新用户活跃时间
        /// </summary>
        /// <param name="Msg"></param>
        /// <returns></returns>
        public async Task SubscribeActive(String Msg)
        {
            IServices.Utils.UserActive info = Msg.JsonTo<IServices.Utils.UserActive>();

            if (info == null) { return; }
            using (var service = this.ServiceProvider.CreateScope())
            {
                try
                {
                    Entity.SqlContext SqlContext = service.ServiceProvider.GetRequiredService<Entity.SqlContext>();

                    var model = await SqlContext.MemberActive.Where(item => item.UserId == info.UserId).FirstOrDefaultAsync();

                    if (model == null)
                    {
                        SqlContext.MemberActive.Add(new Entity.Models.MemberActive()
                        {
                            UserId = info.UserId,
                            JPushId = info.JPushId,
                            ActiveTime = DateTime.Now,
                            Remark = info.Remark
                        });
                    }
                    else
                    {
                        model.JPushId = info.JPushId;
                        model.ActiveTime = DateTime.Now;
                        model.Remark = info.Remark;
                        SqlContext.MemberActive.Update(model);
                    }

                    SqlContext.SaveChanges();
                }
                catch (Exception ex)
                {
                    Core.SystemLog.Error($"更新用户活跃时间 参数:\r\n{Msg}\r\n", ex);
                }
            }
        }

        /// <summary>
        /// 订阅系统交易订单
        /// </summary>
        /// <param name="Msg"></param>
        /// <returns></returns>
        public async Task SubscribeTradeSystem(String Msg)
        {
            if (String.IsNullOrWhiteSpace(Msg)) { return; }
            PublishOrder info = Msg.JsonTo<PublishOrder>();
            if (null == info) { return; }
            using (var service = this.ServiceProvider.CreateScope())
            {
                try
                {
                    Entity.SqlContext SqlContext = service.ServiceProvider.GetRequiredService<Entity.SqlContext>();
                    CSRedis.CSRedisClient Redis = service.ServiceProvider.GetRequiredService<CSRedis.CSRedisClient>();
                    IPlugins.IAlipayPlugin AliPay = service.ServiceProvider.GetRequiredService<IPlugins.IAlipayPlugin>();

                    #region 系统缓存控制
                    String CacheKey = $"System_Order_Lock:{info.OrderNum}";
                    if (Redis.Exists(CacheKey)) { return; }
                    Redis.Set(CacheKey, Msg, 30);
                    #endregion

                    CoinTrade OrderInfo = await SqlContext.Dapper.QueryFirstAsync<CoinTrade>($"SELECT id,tradeNumber,buyerUid,buyerAlipay,IFNULL(sellerUid,0) AS sellerUid,sellerAlipay,amount,price,totalPrice,fee,pictureUrl,`status` FROM coin_trade WHERE id='{info.OrderNum}'");
                    if (OrderInfo == null) { return; }
                    List<IServices.Utils.SystemUserLevel> SystemLevels = service.ServiceProvider.GetRequiredService<IOptionsMonitor<List<IServices.Utils.SystemUserLevel>>>().CurrentValue;

                    #region 完成用户的订单
                    bool UserOrder = false;

                    //====计算买家获得的果皮
                    decimal BuyerCandyP = 0;
                    var UserLevel = SqlContext.User.FirstOrDefault(o => o.Id == OrderInfo.BuyerUid);
                    if (null == UserLevel) { return; }
                    IServices.Utils.SystemUserLevel SysLevel = SystemLevels.FirstOrDefault(o => o.Level.ToLower().Equals(UserLevel.Level.ToLower()));
                    if (null == SysLevel) { return; }
                    BuyerCandyP = OrderInfo.Amount * SysLevel.BuyRate;

                    using (IDbConnection Db = SqlContext.DapperConnection)
                    {
                        Db.Open();
                        IDbTransaction Tran = Db.BeginTransaction();
                        try
                        {
                            //果皮扣除记录
                            var systemUserId = 0;
                            var totalAmount = OrderInfo.Amount + OrderInfo.Fee;
                            //减掉卖家用户的冻结账户中的冻结余额并添加糖果流水
                            Db.Execute($"update user set `candyP`=(`candyP`-{totalAmount}),`freezeCandyNum`=(`freezeCandyNum`-{totalAmount}) where id={OrderInfo.SellerUid}", null, Tran);
                            Db.Execute($"insert into `user_candyp`(`userId`,`candyP`,`content`,`source`,`createdAt`,`updatedAt`) values({OrderInfo.SellerUid},-{totalAmount},'卖掉{OrderInfo.Amount}糖果,扣除{totalAmount}果皮',4,now(),now())", null, Tran);
                            Db.Execute($"insert into `gem_records`(`userId`,`num`,`description`,gemSource) values({OrderInfo.SellerUid},-{totalAmount},'卖掉{OrderInfo.Amount}糖果,手续费{OrderInfo.Fee}',3)", null, Tran);
                            //增加买家账户中的余额并添加糖果流水
                            Db.Execute($"update user set `candyP`=(`candyP`+{BuyerCandyP}),`candyNum`=(`candyNum`+{OrderInfo.Amount}) where id={OrderInfo.BuyerUid}", null, Tran);
                            Db.Execute($"insert into `gem_records`(`userId`,`num`,`description`,gemSource) values({OrderInfo.BuyerUid},{OrderInfo.Amount},'购买{OrderInfo.Amount}个糖果',5)", null, Tran);
                            Db.Execute($"insert into `user_candyp`(`userId`,`candyP`,`content`,`source`,`createdAt`,`updatedAt`) values({OrderInfo.BuyerUid},{BuyerCandyP},'买进{OrderInfo.Amount}糖果,赠送{BuyerCandyP}果皮',4,now(),now())", null, Tran);

                            //将手续费划入系统
                            Db.Execute($"update user set `candyNum`=(`candyNum`+{OrderInfo.Fee}) where id={systemUserId}", null, Tran);
                            var insertSql3 = $"insert into `gem_records`(`userId`,`num`,`description`,gemSource) values({systemUserId},{OrderInfo.Fee},'用户{OrderInfo.BuyerUid}购买{OrderInfo.Amount}个糖果,手续费{OrderInfo.Fee}',5)";
                            Db.Execute(insertSql3, null, Tran);
                            //更新订单信息
                            Db.Execute($"update coin_trade set status=4,pictureUrl='/system/Transfer.jpg' where id = {OrderInfo.Id}", null, Tran);

                            Tran.Commit();
                            UserOrder = true;
                        }
                        catch
                        {
                            Tran.Rollback();
                            return;
                        }
                        finally { if (Db.State == ConnectionState.Open) { Db.Close(); } }
                    }
                    #endregion

                    #region 向用户付款
                    if (UserOrder)
                    {
                        String PayId = SqlContext.Dapper.ExecuteScalar<String>("INSERT INTO `yoyo_pay_record` (`UserId`, `Channel`, `Currency`, `Amount`, `Fee`, `ActionType`, `Custom`, `PayStatus`, `ChannelUID`, `CreateTime`) VALUES (@UserId, @Channel, @Currency,@Amount, @Fee,@ActionType, @Custom, @PayStatus, @ChannelUID, NOW());SELECT @@IDENTITY", new
                        {
                            UserId = OrderInfo.SellerUid,
                            Channel = 2,
                            Currency = 1,
                            Amount = OrderInfo.TotalPrice,
                            Fee = 0,
                            ActionType = 2,
                            ChannelUID = info.AlipayUid,
                            PayStatus = 0,
                            Custom = OrderInfo.TradeNumber
                        });
                        if (String.IsNullOrWhiteSpace(PayId))
                        {
                            Core.SystemLog.Error($"系统收单，生成付款信息发生错误:\r\n{Msg}\r\n付款订单参数:\r\n{PayId.ToJson()}");
                            return;
                        }
                        IPlugins.Request.ReqAlipayTransfer Transfer = new IPlugins.Request.ReqAlipayTransfer
                        {
                            ProductCode = "TRANS_ACCOUNT_NO_PWD",
                            BizScene = "DIRECT_TRANSFER",
                            IdentityType = "ALIPAY_USER_ID",
                            OrderTitle = $"哟哟吧系统付款",
                            Remark = $"订单编号:{OrderInfo.TradeNumber}",
                            OutBizNo = PayId,
                            Identity = info.AlipayUid,
                            TransAmount = (int)(OrderInfo.TotalPrice * 100.00M) * 0.01M
                        };
                        var result = await AliPay.Execute(Transfer);
                        if (result.IsError)
                        {
                            Core.SystemLog.Error($"系统收单,付款时支付宝发生错误,请求参数:{Transfer.ToJson()}返回参数:\r\n{result.ToJson()}");
                            return;
                        }
                        if (!result.Result.Status.ToUpper().Equals("SUCCESS") && !result.Result.Status.ToUpper().Equals("DEALING"))
                        {
                            Core.SystemLog.Error($"系统收单,付款时支付宝发生错误:\r\n{result.ToJson()}");
                            return;
                        }
                        SqlContext.Dapper.Execute($"UPDATE `yoyo_pay_record` SET `PayStatus`=1,`ModifyTime`=NOW() WHERE `PayId`={PayId}");
                    }

                    #endregion
                }
                catch (Exception ex) { Core.SystemLog.Error($"系统收单发生错误\r\n{Msg}", ex); }
            }
        }

        public async Task SubscribeWalletWithdraw(String Msg)
        {
            if (String.IsNullOrWhiteSpace(Msg)) { return; }
            WalletWithdraw info = Msg.JsonTo<WalletWithdraw>();
            if (null == info) { return; }
            using (var service = this.ServiceProvider.CreateScope())
            {
                try
                {
                    Entity.SqlContext SqlContext = service.ServiceProvider.GetRequiredService<Entity.SqlContext>();
                    CSRedis.CSRedisClient Redis = service.ServiceProvider.GetRequiredService<CSRedis.CSRedisClient>();
                    IPlugins.IAlipayPlugin AliPay = service.ServiceProvider.GetRequiredService<IPlugins.IAlipayPlugin>();

                    #region 系统缓存控制
                    String CacheKey = $"Wallet_Withdraw_Lock:{info.UserId}";
                    if (Redis.Exists(CacheKey)) { return; }
                    Redis.Set(CacheKey, Msg, 30);
                    #endregion

                    var UserInfo = await SqlContext.User.FirstOrDefaultAsync(o => o.Id == info.UserId);

                    if (UserInfo == null)
                    {
                        Core.SystemLog.Error($"用户提现,用户不存在.\r\n{Msg}");
                        return;
                    }

                    if (String.IsNullOrWhiteSpace(UserInfo.AlipayUid))
                    {
                        Core.SystemLog.Error($"用户提现,用户未做二次认证.\r\n{Msg}");
                        return;
                    }

                    #region 向用户付款
                    String PayId = SqlContext.Dapper.ExecuteScalar<String>("INSERT INTO `yoyo_pay_record` (`UserId`, `Channel`, `Currency`, `Amount`, `Fee`, `ActionType`, `Custom`, `PayStatus`, `ChannelUID`, `CreateTime`) VALUES (@UserId, @Channel, @Currency,@Amount, @Fee,@ActionType, @Custom, @PayStatus, @ChannelUID, NOW());SELECT @@IDENTITY", new
                    {
                        UserId = info.UserId,
                        Channel = 2,
                        Currency = 1,
                        Amount = info.Amount,
                        Fee = 0,
                        ActionType = info.ActionType,
                        ChannelUID = UserInfo.AlipayUid,
                        PayStatus = 0,
                        Custom = String.Join(",", info.Desc.ToArray())
                    });
                    if (String.IsNullOrWhiteSpace(PayId))
                    {
                        Core.SystemLog.Error($"用户提现，生成付款信息发生错误:\r\n{Msg}\r\n付款订单参数:\r\n{PayId.ToJson()}");
                        return;
                    }
                    IPlugins.Request.ReqAlipayTransfer Transfer = new IPlugins.Request.ReqAlipayTransfer
                    {
                        ProductCode = "TRANS_ACCOUNT_NO_PWD",
                        BizScene = "DIRECT_TRANSFER",
                        IdentityType = "ALIPAY_USER_ID",
                        OrderTitle = $"哟哟吧-付款",
                        Remark = $"钱包提现:{info.TradeNo}",
                        OutBizNo = PayId,
                        Identity = UserInfo.AlipayUid,
                        TransAmount = (int)(info.Amount * 100.00M) * 0.01M
                    };
                    var result = await AliPay.Execute(Transfer);
                    if (result.IsError)
                    {
                        Core.SystemLog.Error($"用户提现,付款时支付宝发生错误,请求参数:{Transfer.ToJson()}返回参数:\r\n{result.ToJson()}");
                        return;
                    }
                    if (!result.Result.Status.ToUpper().Equals("SUCCESS") && !result.Result.Status.ToUpper().Equals("DEALING"))
                    {
                        Core.SystemLog.Error($"系统收单,付款时支付宝发生错误:\r\n{result.ToJson()}");
                        return;
                    }
                    SqlContext.Dapper.Execute($"UPDATE `yoyo_pay_record` SET `PayStatus`=1,`ModifyTime`=NOW() WHERE `PayId`={PayId}");
                    #endregion
                }
                catch (Exception ex) { Core.SystemLog.Error($"用户提现发生错误\r\n{Msg}", ex); }
            }
        }

        #region 私有提现处理方案
        public class WalletWithdraw
        {
            public int ActionType { get; set; }

            public string TradeNo { get; set; }

            public long UserId { get; set; }

            public decimal Amount { get; set; }

            public List<string> Desc { get; set; }
        }
        #endregion

        #region 私有活跃贡献解决方案
        private async Task SubscribeDevote(long UserId, decimal Devote)
        {
            using (var service = this.ServiceProvider.CreateScope())
            {
                try
                {
                    List<IServices.Utils.SystemUserLevel> SystemLevels = service.ServiceProvider.GetRequiredService<IOptionsMonitor<List<IServices.Utils.SystemUserLevel>>>().CurrentValue;
                    Entity.SqlContext SqlContext = service.ServiceProvider.GetRequiredService<Entity.SqlContext>();
                    Entity.Models.User UserInfo = await SqlContext.User.Where(o => o.Id == UserId && o.Status == 0 && o.AuditState == 2).FirstOrDefaultAsync();
                    if (UserInfo == null) { return; }
                    Entity.Models.MemberDevote DevoteInfo = await SqlContext.MemberDevote.FirstOrDefaultAsync(o => o.UserId == UserId);
                    if (null == DevoteInfo)
                    {
                        DevoteInfo = SqlContext.MemberDevote.Add(new Entity.Models.MemberDevote
                        {
                            UserId = UserId,
                            DevoteDate = DateTime.Now.Date,
                            Devote = Devote
                        }).Entity;
                    }
                    else
                    {
                        DevoteInfo.Devote += Devote;
                    }
                    SqlContext.SaveChanges();

                    Decimal UserDevote = UserInfo.Golds == null ? 0 : UserInfo.Golds.Value;
                    UserDevote += SqlContext.MemberDevote.Where(o => o.UserId == UserId).Select(f => f.Devote).Sum();

                    #region 开始重算等级
                    string UserLevel = String.Empty;
                    foreach (var item in SystemLevels.OrderBy(o => o.Claim))
                    {
                        if (UserDevote >= item.Claim) { UserLevel = item.Level; }
                    }
                    if (String.IsNullOrWhiteSpace(UserLevel)) { return; }
                    #endregion
                    if (!UserInfo.Level.ToLower().Equals(UserLevel.ToLower()))
                    {
                        UserInfo.Level = UserLevel.ToLower();
                        SqlContext.SaveChanges();
                    }
                }
                catch { }
            }

        }
        #endregion

        #region 私有基础任务处理方案       
        private async Task SubscribeTask(IServices.Utils.DailyTask info)
        {
            if (info.UserId == 0 || info.CarryOut == 0) { return; }
            String CacheKey = $"UserDoSysTask:{info.UserId}";
            using (var service = this.ServiceProvider.CreateScope())
            {
                try
                {
                    Entity.SqlContext SqlContext = service.ServiceProvider.GetRequiredService<Entity.SqlContext>();
                    CSRedis.CSRedisClient Redis = service.ServiceProvider.GetRequiredService<CSRedis.CSRedisClient>();

                    #region 活跃贡献值
                    //==========停止活跃贡献值
                    // if (info.Devote > 0) { await SubscribeDevote(info.UserId, info.Devote); }
                    #endregion

                    #region 买单排行榜
                    if (info.Devote > 0 && info.TaskType == IServices.Utils.TaskType.BUYER)
                    {
                        await Duplicate(info.UserId, info.Devote);
                    }
                    #endregion

                    // if (Redis.Exists(CacheKey)) { return; }

                    // #region 判断用户是否合法
                    // Entity.Models.User ThisUser = await SqlContext.User.Where(o => o.Id == info.UserId && o.Status == 0 && o.AuditState == 2).FirstOrDefaultAsync();
                    // if (ThisUser == null) { return; }
                    // #endregion

                    // #region 取出系统内所有任务
                    // List<Entity.Models.SystemTask> SysTasks = await SqlContext.SystemTask.Where(o => o.Status == 1).ToListAsync();
                    // #endregion

                    // #region 取出用户的所有任务
                    // List<Entity.Models.MemberDailyTask> UserTasks = await SqlContext.MemberDailyTask.Where(o => o.UserId == info.UserId).ToListAsync();
                    // #endregion

                    // #region 为用户补充新的任务
                    // bool SaveUserTask = false;
                    // foreach (var item in SysTasks)
                    // {
                    //     if (UserTasks.FirstOrDefault(o => o.TaskId == item.Id) == null)
                    //     {
                    //         UserTasks.Add(SqlContext.MemberDailyTask.Add(new Entity.Models.MemberDailyTask
                    //         {
                    //             UserId = info.UserId,
                    //             TaskId = item.Id,
                    //             Carry = 0,
                    //             Completed = 0,
                    //             CompleteDate = DateTime.Now.Date

                    //         }).Entity);
                    //         SaveUserTask = true;
                    //     }
                    // }
                    // if (SaveUserTask)
                    // {
                    //     SqlContext.SaveChanges();
                    // }
                    // #endregion

                    // #region 用户任务数据初始化
                    // bool SaveUpdateTask = false;
                    // foreach (var item in UserTasks)
                    // {
                    //     if (item.CompleteDate != DateTime.Now.Date)
                    //     {
                    //         item.CompleteDate = DateTime.Now.Date;
                    //         item.Completed = 0;
                    //         item.Carry = 0;
                    //         SqlContext.MemberDailyTask.Update(item);
                    //         SaveUpdateTask = true;
                    //     }
                    // }
                    // if (SaveUpdateTask)
                    // {
                    //     SqlContext.SaveChanges();
                    //     UserTasks = await SqlContext.MemberDailyTask.Where(o => o.UserId == info.UserId).ToListAsync();
                    // }
                    // #endregion

                    // #region 取出当前需要的任务
                    // List<Entity.Models.SystemTask> ThisTaskType = SysTasks.Where(o => o.TaskType == (int)info.TaskType).OrderBy(o => o.Aims).ToList();
                    // if (ThisTaskType.Count == 0) { return; }
                    // Entity.Models.MemberDailyTask UserNowTask = null;
                    // foreach (var item in ThisTaskType)
                    // {
                    //     var UserTask = UserTasks.FirstOrDefault(o => o.TaskId == item.Id && o.Completed == 0);
                    //     if (UserTask == null) { continue; }
                    //     if ((UserTask.Carry + info.CarryOut) < item.Aims)  //==任务未完成，计数
                    //     {
                    //         UserTask.Carry += info.CarryOut;
                    //         SqlContext.MemberDailyTask.Update(UserTask);
                    //         SqlContext.SaveChanges();
                    //     }
                    //     else    //已经完成任务
                    //     {
                    //         UserTask.Carry += info.CarryOut;
                    //         UserTask.Completed = 1;
                    //         UserNowTask = SqlContext.MemberDailyTask.Update(UserTask).Entity;
                    //         SqlContext.SaveChanges();

                    //         #region 给予用户糖果
                    //         using (IDbConnection Db = SqlContext.DapperConnection)
                    //         {
                    //             Db.Open();
                    //             IDbTransaction Tran = Db.BeginTransaction();
                    //             try
                    //             {
                    //                 //var Row = Db.Execute($"INSERT INTO `gem_records` (`userId`,`num`,`description`,`gemSource`) VALUES({UserTask.UserId},{item.Reward},'完成【{item.TaskTitle}】任务',9);", null, Tran);
                    //                 //var Row1 = Db.Execute($"update `user` set `candyNum`=(`candyNum`+{item.Reward}) where `id`={UserTask.UserId};", null, Tran);
                    //                 var Row = Db.Execute($"INSERT INTO `user_candyp` (`userId`,`candyP`,`content`,`source`,`createdAt`,`updatedAt`) VALUES({UserTask.UserId},{item.Reward},'完成【{item.TaskTitle}】任务',9,NOW(),NOW());", null, Tran);
                    //                 var Row1 = Db.Execute($"update `user` set `candyP`=(`candyP`+{item.Reward}) where `id`={UserTask.UserId};", null, Tran);
                    //                 if (Row == Row1 && Row == 1)
                    //                 {
                    //                     Tran.Commit();
                    //                 }
                    //                 else
                    //                 {
                    //                     Tran.Rollback();
                    //                     return;
                    //                 }
                    //             }
                    //             catch { Tran.Rollback(); }
                    //             finally { if (Db.State == ConnectionState.Open) { Db.Close(); } }
                    //         }
                    //         #endregion

                    //         #region 给予用户活跃贡献值
                    //         if (item.Devote > 0) { await SubscribeDevote(info.UserId, item.Devote); }
                    //         #endregion
                    //     }
                    // }
                    // #endregion

                }
                catch (Exception ex)
                {
                    Core.SystemLog.Error($"每日任务发生错误 参数:\r\n{info.ToJson()}\r\n", ex);
                }
            }
        }
        #endregion

        #region 买单排行榜
        /// <summary>
        /// 买单排行榜
        /// </summary>
        /// <param name="UserId"></param>
        /// <param name="Number"></param>
        /// <returns></returns>
        private async Task Duplicate(long UserId, decimal Number)
        {
            // if (Number < 6)
            // {
            //     return;
            // }
            // else
            // {
            //     Number = 1;

            // }
            DateTime ToDay = DateTime.Now.Date;
            using (var service = this.ServiceProvider.CreateScope())
            {
                try
                {
                    Entity.SqlContext SqlContext = service.ServiceProvider.GetRequiredService<Entity.SqlContext>();
                    Entity.Models.MemberDuplicate Dupli = await SqlContext.MemberDuplicate.FirstOrDefaultAsync(o => o.UserId == UserId && o.Date == ToDay);
                    if (null == Dupli)
                    {
                        Dupli = SqlContext.MemberDuplicate.Add(new Entity.Models.MemberDuplicate
                        {
                            UserId = UserId,
                            Date = ToDay,
                            Duplicate = Number

                        }).Entity;
                    }
                    else
                    {
                        Dupli.Duplicate += Number;
                    }
                    SqlContext.SaveChanges();
                }
                catch (Exception ex)
                {
                    Core.SystemLog.Error($"用户买单计入排行榜发生错误,用户ID:{UserId},购买数量:{Number}", ex);
                }
            }
        }
        #endregion

        #region 钱包金额处理
        /// <summary>
        /// 
        /// </summary>
        /// <param name="DataBase">数据库</param>
        /// <param name="UserId">用户ID</param>
        /// <param name="ModifyType">变更类型</param>
        /// <param name="Amount">金额</param>
        /// <param name="Desc">描述</param>
        private void ChangeWalletAmount(IDbConnection DataBase, long UserId, int ModifyType, decimal Amount, params string[] Desc)
        {
            String EditSQl, RecordSql, PostChangeSql;

            long AccountId;

            AccountId = DataBase.QueryFirst<long>($"SELECT AccountId FROM user_account_wallet WHERE UserId={UserId}");

            EditSQl = $"UPDATE `user_account_wallet` SET `Balance`=`Balance`+{Amount},`Revenue`=`Revenue`+{Math.Abs(Amount)},`ModifyTime`=NOW() WHERE `AccountId`={AccountId}";

            PostChangeSql = $"IFNULL((SELECT `PostChange` FROM `user_account_wallet_record` WHERE `AccountId`={AccountId} ORDER BY `RecordId` DESC LIMIT 1),0)";
            StringBuilder TempRecordSql = new StringBuilder($"INSERT INTO `user_account_wallet_record` ");
            TempRecordSql.Append("( `AccountId`, `PreChange`, `Incurred`, `PostChange`, `ModifyType`, `ModifyDesc`, `ModifyTime` ) ");
            TempRecordSql.Append($"SELECT {AccountId} AS `AccountId`, ");
            TempRecordSql.Append($"{PostChangeSql} AS `PreChange`, ");
            TempRecordSql.Append($"{Amount} AS `Incurred`, ");
            TempRecordSql.Append($"{PostChangeSql}+{Amount} AS `PostChange`, ");
            TempRecordSql.Append($"{ModifyType} AS `ModifyType`, ");
            TempRecordSql.Append($"'{String.Join(',', Desc)}' AS `ModifyDesc`, ");
            TempRecordSql.Append($"'{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}' AS`ModifyTime`");
            RecordSql = TempRecordSql.ToString();

            #region 修改账务
            using (IDbConnection db = DataBase)
            {
                db.Open();
                IDbTransaction Tran = db.BeginTransaction();
                try
                {
                    Int32 EditRow = db.Execute(EditSQl, null, Tran);
                    Int32 RecordId = db.Execute(RecordSql, null, Tran);
                    if (EditRow == RecordId && EditRow == 1) { Tran.Commit(); return; }
                    Tran.Rollback();
                    Core.SystemLog.Debug($"钱包账户余额变更发生错误\r\n修改语句：\r\n{EditSQl}\r\n记录语句：{RecordSql}");
                }
                catch (Exception ex)
                {
                    Tran.Rollback();
                    Core.SystemLog.Debug($"钱包账户余额变更发生错误\r\n修改语句：\r\n{EditSQl}\r\n记录语句：{RecordSql}", ex);
                }
                finally { if (db.State == ConnectionState.Open) { db.Close(); } }
            }
            #endregion
        }
        #endregion

        #region 城主模型
        public class CityMaster
        {
            public int CityId { get; set; }

            public long UserId { get; set; }

            public string CityCode { get; set; }

            public DateTime StartTime { get; set; }

            public DateTime EndTime { get; set; }
        }

        public class UserLocations
        {
            public long UserId { get; set; }
            public string City { get; set; }
            public string CityCode { get; set; }
        }
        #endregion

        #region 私有订单处理方案

        public class PublishOrder
        {
            public string OrderNum { get; set; }

            public string AlipayUid { get; set; }
        }

        public class CoinTrade
        {
            public int Id { get; set; }
            public string TradeNumber { get; set; }
            public int BuyerUid { get; set; }
            public string BuyerAlipay { get; set; }
            public int SellerUid { get; set; }
            public string SellerAlipay { get; set; }
            public decimal Amount { get; set; }
            public decimal Price { get; set; }
            public decimal TotalPrice { get; set; }
            public decimal Fee { get; set; }
            public string PictureUrl { get; set; }
            public int Status { get; set; }
        }
        #endregion

        #region 幸运宝箱 分红记录
        public async Task BoxDividend(String Msg)
        {
            if (String.IsNullOrWhiteSpace(Msg)) { return; }
            BoxDividendModel info = Msg.JsonTo<BoxDividendModel>();
            using (var service = this.ServiceProvider.CreateScope())
            {
                Entity.SqlContext SqlContext = service.ServiceProvider.GetRequiredService<Entity.SqlContext>();
                CSRedis.CSRedisClient Redis = service.ServiceProvider.GetRequiredService<CSRedis.CSRedisClient>();
                String CacheKey = $"Box_Dividend_Lock:{info.RecordId}";
                if (Redis.Exists(CacheKey)) { return; }
                await Redis.SetAsync(CacheKey, Msg, 30);

                #region 写入分红记录
                StringBuilder InsertSql = new StringBuilder();
                InsertSql.Append("INSERT INTO `gem_records`(`userId`, `num`, `createdAt`, `updatedAt`, `description`, `gemSource`) ");
                InsertSql.Append("SELECT UserId AS userId, SUM(BuyTotal) * @SingleValue AS num, NOW() AS createdAt, NOW() AS updatedAt, CONCAT('幸运夺宝[',SUM(BuyTotal),'把钥匙]分得',ROUND(SUM(BuyTotal) * @SingleValue, 4),'糖果') AS description, 26 AS gemSource ");
                InsertSql.Append("FROM yoyo_box_record WHERE Period = @Period AND UserId != @UserId AND Id < @RecordId GROUP BY UserId;");
                DynamicParameters InsertParam = new DynamicParameters();
                InsertParam.Add("UserId", info.MemberId, DbType.Int64);
                InsertParam.Add("Period", info.Period, DbType.Int32);
                InsertParam.Add("RecordId", info.RecordId, DbType.Int64);
                InsertParam.Add("SingleValue", info.SingleValue, DbType.Decimal);
                #endregion

                #region 写入账户
                StringBuilder UpdateSql = new StringBuilder();
                UpdateSql.Append("UPDATE `user` AS u, (SELECT UserId, ROUND(SUM(BuyTotal) * @SingleValue, 4) AS price FROM yoyo_box_record WHERE Period = @Period AND UserId != @UserId AND Id < @RecordId GROUP BY UserId) AS r ");
                UpdateSql.Append("SET u.candyNum = u.candyNum + r.price WHERE u.id = r.UserId;");
                DynamicParameters UpdateParam = new DynamicParameters();
                UpdateParam.Add("UserId", info.MemberId, DbType.Int64);
                UpdateParam.Add("Period", info.Period, DbType.Int32);
                UpdateParam.Add("RecordId", info.RecordId, DbType.Int64);
                UpdateParam.Add("SingleValue", info.SingleValue, DbType.Decimal);
                #endregion

                SqlContext.Dapper.Open();
                using (IDbTransaction transaction = SqlContext.Dapper.BeginTransaction())
                {
                    try
                    {
                        SqlContext.Dapper.Execute(InsertSql.ToString(), InsertParam, transaction);
                        SqlContext.Dapper.Execute(UpdateSql.ToString(), UpdateParam, transaction);
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Core.SystemLog.Error($"宝箱分红发生错误\r\n{Msg}", ex);
                    }
                }
                SqlContext.Dapper.Close();
            }
        }

        public class BoxRecord
        {
            public Int64 UserId { get; set; }
            public Int64 BuyTotal { get; set; }
        }

        public class BoxDividendModel
        {
            public Int64 MemberId { get; set; }
            public Int64 Period { get; set; }
            public Int64 RecordId { get; set; }
            public Decimal SingleValue { get; set; }
        }

        #endregion

        #region 城市合伙人 分红
        /// <summary>
        /// 城市分红
        /// </summary>
        /// <param name="Msg"></param>
        /// <returns></returns>
        public async Task SubscribeCityDividend(String Msg)
        {
            if (String.IsNullOrWhiteSpace(Msg)) { return; }
            DividendModel info = Msg.JsonTo<DividendModel>();
            if (null == info) { return; }
            using (var service = this.ServiceProvider.CreateScope())
            {
                ICityDividend CityDividendSub = service.ServiceProvider.GetRequiredService<ICityDividend>();

                switch (info.DividendType)
                {
                    case DividendType.YoBang:
                        await CityDividendSub.YoBangDividend(info);
                        break;
                    case DividendType.Video:
                        await CityDividendSub.VideoDividend(info);
                        break;
                    case DividendType.Shandw:
                        await CityDividendSub.ShandwDividend(info);
                        break;
                    case DividendType.Mall:
                        await CityDividendSub.MallDividend(info);
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion
    }
}
