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
using Yoyo.Core.Expand;

namespace Yoyo.Jobs
{
    public class DealWithTradeOrder : IJob
    {
        private readonly IServiceProvider ServiceProvider;

        public DealWithTradeOrder(IServiceProvider service)
        {
            this.ServiceProvider = service;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            using (var service = this.ServiceProvider.CreateScope())
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                Entity.SqlContext SqlContext = service.ServiceProvider.GetRequiredService<Entity.SqlContext>();
                CSRedis.CSRedisClient RedisCache = service.ServiceProvider.GetRequiredService<CSRedis.CSRedisClient>();
                List<IServices.Utils.SystemUserLevel> SystemLevels = service.ServiceProvider.GetRequiredService<IOptionsMonitor<List<IServices.Utils.SystemUserLevel>>>().CurrentValue;

                #region 停止虚拟挂单
                //if (DateTime.Now.Hour >= 9 && DateTime.Now.Hour <= 22)
                //{
                //    if (!RedisCache.Exists("System:AddOrder"))
                //    {
                //        var Bei = 1;
                //        if ((new Random().Next(0, 10)) >= 3) { Bei = 2; }
                //        AddOrderInfo Order = new AddOrderInfo
                //        {
                //            UserId = 505838,     //用户ID
                //            Amount = Bei * 250,     //购买数量
                //            Price = 3.2M + ((new Random().Next(0, 9)) * 0.01M),    //单价
                //            AliPay = Guid.NewGuid().ToString("N")     //用户支付宝
                //        };

                //        var orderNum = NewGuid20();
                //        var insertSql = $"insert into coin_trade(tradeNumber,buyerUid,buyerAlipay,amount,price,totalPrice,fee,trendSide,status)values('{orderNum}',{Order.UserId},'{Order.AliPay}',{Order.Amount},{Order.Price},{Order.Amount * Order.Price},0,'BUY',1)";
                //        SqlContext.Dapper.Execute(insertSql);
                //        RedisCache.Set("System:AddOrder", Order.ToJson(), (new Random().Next(600, 900)));
                //    }
                //}
                #endregion

                //======关闭卖方超时(买房未支付)订单--退回卖方糖果======//

                List<long> UserIds = (await SqlContext.Dapper.QueryAsync<long>("SELECT sellerUid FROM coin_trade WHERE dealEndTime < NOW() AND `status`= 2 AND buyerBan = 0")).ToList();
                StringBuilder CloseSellerOrder = new StringBuilder();
                CloseSellerOrder.AppendLine("UPDATE coin_trade AS T INNER JOIN `user` AS U ON T.sellerUid=U.id");
                CloseSellerOrder.AppendLine("SET T.buyerBan = 1, T.`status`= 6, U.candyNum = U.candyNum + T.amount + T.fee, U.freezeCandyNum = U.freezeCandyNum - T.amount - T.fee");
                CloseSellerOrder.AppendLine("WHERE T.dealEndTime < NOW() AND T.`status`= 2 AND T.buyerBan = 0");
                String CloseSellerOrderSql = CloseSellerOrder.ToString();
                await SqlContext.Dapper.ExecuteAsync(CloseSellerOrderSql);
                stopwatch.Stop();
                Core.SystemLog.Jobs($"关闭卖方超时订单--退回卖方糖果 执行完成,执行时间:{stopwatch.Elapsed.TotalSeconds}秒");

                //======关闭买方超时(卖方未确认)订单--强制发放糖果======//
                stopwatch.Restart();

                List<OrderInfo> Orders = (await SqlContext.Dapper.QueryAsync<OrderInfo>("SELECT id AS Id,tradeNumber AS TradeNumber,buyerUid AS BuyerUid,sellerUid AS SellerUid,IFNULL(amount,0) AS Amount,IFNULL(fee,0) AS Fee FROM coin_trade WHERE `status`=3 AND paidEndTime<NOW() AND sellerBan<>1 AND sellerUid IS NOT NULL")).ToList();

                List<BuyerLevel> BuyerLevels = (await SqlContext.Dapper.QueryAsync<BuyerLevel>($"SELECT `id`,`level` FROM `user` WHERE `id` IN ({String.Join(",", Orders.Select(o => o.BuyerUid).ToList())})")).ToList();

                //基础SQL语句
                String UserBase = "UPDATE `user` SET candyNum=candyNum+{0},candyP=candyP+{1},freezeCandyNum=freezeCandyNum+{2},utime=NOW() WHERE id={3};";
                String CandyPBase = "INSERT INTO `user_candyp` (`userId`,`candyP`,`content`,`source`,`createdAt`,`updatedAt`) VALUES ({0},{1},'{2}',{3},NOW(),NOW());";
                String CandyHBase = "INSERT INTO `gem_records` (`userId`,`num`,`description`,`gemSource`) VALUES ({0},{1},'{2}',{3});";

                using (IDbConnection Db = SqlContext.DapperConnection)
                {
                    Db.Open();
                    IDbTransaction Tran = Db.BeginTransaction();
                    try
                    {
                        foreach (var item in Orders)
                        {
                            decimal BuyerCandyP = 0;
                            //====计算买家获得的果皮
                            BuyerLevel UserLevel = BuyerLevels.FirstOrDefault(o => o.Id == item.BuyerUid);
                            if (null == UserLevel) { continue; }
                            IServices.Utils.SystemUserLevel SysLevel = SystemLevels.FirstOrDefault(o => o.Level.ToLower().Equals(UserLevel.Level.ToLower()));
                            if (null == SysLevel) { continue; }
                            if (item.Amount > 500)
                            {
                                BuyerCandyP = item.Amount * 4;
                            }
                            else
                            {
                                BuyerCandyP = item.Amount * SysLevel.BuyRate;

                            }
                            decimal TotalCandy = item.Amount + item.Fee;
                            decimal TotalPeel = TotalCandy * 2;
                            //====构建交易SQL语句
                            StringBuilder TradeSql = new StringBuilder();
                            //扣除卖家冻结的糖果及果皮，并增加交易记录
                            TradeSql.AppendLine(String.Format(UserBase, 0, -TotalPeel, -TotalCandy, item.SellerUid));
                            TradeSql.AppendLine(String.Format(CandyPBase, item.SellerUid, -TotalPeel, $"卖掉{item.Amount}糖果,扣除{TotalPeel}果皮", 4));
                            TradeSql.AppendLine(String.Format(CandyHBase, item.SellerUid, -TotalCandy, $"卖掉{item.Amount}糖果,手续费{item.Fee}", 3));
                            //买家增加糖果及果皮，并增加交易记录
                            TradeSql.AppendLine(String.Format(UserBase, item.Amount, BuyerCandyP, 0, item.BuyerUid));
                            TradeSql.AppendLine(String.Format(CandyPBase, item.BuyerUid, BuyerCandyP, $"买进{item.Amount}糖果,赠送{BuyerCandyP}果皮", 4));
                            TradeSql.AppendLine(String.Format(CandyHBase, item.BuyerUid, item.Amount, $"购买{item.Amount}个糖果", 5));
                            //手续费划入系统账户
                            TradeSql.AppendLine(String.Format(UserBase, item.Fee, 0, 0, 0));
                            TradeSql.AppendLine(String.Format(CandyHBase, 0, item.Fee, $"用户{item.BuyerUid}购买{item.Amount}个糖果,手续费{item.Fee}", 5));
                            //修改订单状态
                            TradeSql.AppendLine($"UPDATE coin_trade SET `status`=4,sellerBan=1 WHERE id={item.Id};");
                            //卖方超时，扣除20%糖果惩罚
                            TradeSql.AppendLine(String.Format(UserBase, -(item.Amount * 0.20M), -(item.Amount * 0.20M), 0, item.SellerUid));
                            TradeSql.AppendLine(String.Format(CandyHBase, item.SellerUid, -(item.Amount * 0.20M), $"卖方发送糖果超时，扣掉{item.Amount * 0.20M}糖果.", 4));
                            TradeSql.AppendLine(String.Format(CandyPBase, item.SellerUid, -(item.Amount * 0.20M), $"卖方发送糖果超时，扣掉{item.Amount * 0.20M}果皮.", 4));

                            String ActionSql = TradeSql.ToString();
                            await Db.ExecuteAsync(ActionSql, null, Tran);
                        }
                        Tran.Commit();
                    }
                    catch (Exception ex)
                    {
                        Tran.Rollback();
                        Core.SystemLog.Jobs($"关闭买方超时订单--强制发放糖果 发生异常", ex);
                    }
                    finally { if (Db.State == ConnectionState.Open) { Db.Close(); } }
                }

                stopwatch.Stop();
                if (Orders.Count > 0)
                {
                    Core.SystemLog.Jobs($"处理超时交易订单 执行完成,关闭{Orders.Count}笔订单,执行时间:{stopwatch.Elapsed.TotalSeconds}秒");
                }
            }
        }

        private string NewGuid20()
        {

            var orderdate = DateTime.Now.ToString("ddHHmmssffffff");
            var ordercode = Guid.NewGuid().GetHashCode();
            var num = 20 - orderdate.Length;
            if (ordercode < 0) { ordercode = -ordercode; }
            var orderlast = ordercode.ToString().Length > num ? ordercode.ToString().Substring(0, num) : ordercode.ToString().PadLeft(num, '0');
            return $"{orderdate}{orderlast}";
        }

        public class AddOrderInfo
        {
            public long UserId { get; set; }

            public string AliPay { get; set; }

            public decimal Amount { get; set; }

            public decimal Price { get; set; }
        }

        public class BuyerLevel
        {
            public long Id { get; set; }

            public string Level { get; set; }
        }

        public class OrderInfo
        {
            public long Id { get; set; }

            public string TradeNumber { get; set; }

            public long BuyerUid { get; set; }

            public long SellerUid { get; set; }

            public decimal Amount { get; set; }

            public decimal Fee { get; set; }
        }
    }
}
