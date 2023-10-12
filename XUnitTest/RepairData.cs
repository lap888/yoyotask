using CSRedis;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Text;
using Xunit;
using Yoyo.Core.Expand;
using Yoyo.Entity.Models;
using System.Threading.Tasks;
using System.Net.Http;
using System.Security.Cryptography;

namespace XUnitTest
{
    public class RepairData
    {
        private readonly IServiceProvider ServiceProvider;
        public readonly IConfiguration Configuration;

        public RepairData()
        {
            CommServiceProvider commService = new CommServiceProvider();
            Configuration = commService.GetConfiguration();
            ServiceProvider = commService.GetServiceProvider();
        }

        [Fact]
        public async Task ShandwOrder()
        {
            var factory = ServiceProvider.GetRequiredService<IHttpClientFactory>();
            HttpClient client = factory.CreateClient();

            String Url = "https://h5gm2.shandw.com/open/channel/queryPayByChannel";

            Dictionary<String, String> Pairs = new Dictionary<String, String>();
            Pairs.Add("account", "yoyoba888");
            Pairs.Add("channel", "12872");
            Pairs.Add("sec", GetTimeStamp());

            StringBuilder Body = new StringBuilder();
            Pairs.Aggregate(Body, (s, i) => s.Append($"{i.Key}={i.Value}&"));
            Body.Append("key=9c839dc797024c18b434b533f0c127da");

            Pairs.Add("sign", MD5(Body.ToString()));
            Pairs.Add("page", MD5(Body.ToString()));
            Pairs.Add("pageSize", MD5(Body.ToString()));
            Pairs.Add("stime", GetTimeStamp(DateTime.Now.AddDays(-90)));
            Pairs.Add("etime", GetTimeStamp());

            StringBuilder Param = new StringBuilder();
            Pairs.Aggregate(Param, (s, i) => s.Append($"{i.Key}={i.Value}&"));
            Param.Remove(Param.Length - 1, 1);

            var response = await client.GetAsync(Url + "?" + Param.ToString());
            String data = await response.Content.ReadAsStringAsync();
            return;
        }

        [Fact]
        public void Withdraw()
        {
            CSRedisClient RedisCache = new CSRedisClient("49.233.134.249:6005,password=yoyoba,defaultDatabase=2");

            //String TradeNo = "20200518022520136130";
            //Int64 UserId = 637040;
            //Decimal Amount = 40;
            //string[] param = { TradeNo };

            //String TradeNo = "20200518081206166098";
            //Int64 UserId = 128510;
            //Decimal Amount = 10;
            //string[] param = { TradeNo };

            //Int64 Rows = RedisCache.Publish("YoYo_Wallet_Withdraw", new { TradeNo = TradeNo, ActionType = 4, UserId = UserId, Amount = Amount, Desc = param.ToList() }.ToJson());

            return;
        }

        [Fact]
        public void Register()
        {
            CSRedisClient RedisCache = new CSRedisClient("49.233.134.249:6005,password=yoyoba,defaultDatabase=2");
            Yoyo.Entity.SqlContext SqlContext = this.ServiceProvider.GetService<Yoyo.Entity.SqlContext>();

            List<User> ListUsers = SqlContext.User.Where(item => item.Ctime > DateTime.Now.Date && item.Ctime < DateTime.Parse("2020-05-18 08:34:00") && item.CCount == 0).ToList();


            Int64 Rows = 0;
            Int64 ReferrerUid = 0;

            foreach (var item in ListUsers)
            {
                ReferrerUid = SqlContext.User.Where(r => r.Mobile == item.InviterMobile).Select(r => r.Id).FirstOrDefault();
                Rows = RedisCache.Publish("YoYo_Member_Regist", JsonConvert.SerializeObject(new { MemberId = item.Id, ParentId = ReferrerUid }));

                if (item.AuditState == 2)
                {
                    Rows = RedisCache.Publish("YoYo_Member_Certified", JsonConvert.SerializeObject(new { MemberId = item.Id }));
                }
            }
            return;
        }

        /// <summary>
        /// 获取时间戳
        /// </summary>
        /// <returns></returns>
        public static string GetTimeStamp(DateTime? Dt = null)
        {
            if (Dt == null) { Dt = DateTime.Now; }
            TimeSpan ts = (DateTime)Dt - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalMilliseconds).ToString();
        }

        /// <summary>
        /// MD5加密[大写]
        /// </summary>
        /// <param name="value">需要加密的字符串</param>
        /// <param name="IsShort">是否使用16位加密[默认:false]</param>
        /// <returns></returns>
        public static string MD5(string value, bool IsShort = false)
        {
            try
            {
                MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
                byte[] bs = Encoding.UTF8.GetBytes(value);
                bs = md5.ComputeHash(bs);
                string CryptoStr;
                if (IsShort)
                {
                    CryptoStr = BitConverter.ToString(bs, 4, 8).Replace("-", "");
                }
                else
                {
                    StringBuilder s = new StringBuilder();
                    foreach (byte b in bs) { s.Append(b.ToString("x2")); }
                    CryptoStr = s.ToString();
                }
                return CryptoStr.ToUpper();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

    }
}
