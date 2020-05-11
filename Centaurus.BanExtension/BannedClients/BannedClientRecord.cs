using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.BanExtension
{

    public class BannedClientRecord
    {
        [BsonId]
        public string Source { get; set; }

        public int BanCounts { get; set; }

        public DateTime Till { get; set; }

        public bool IsBanActive(DateTime currentDate)
        {
            return Till >= currentDate;
        }

        public void SetTillDate(DateTime currentDate, int singleBanPeiod, int banPeriodMultiplier)
        {
            var newValue = CalcTillDate(currentDate, singleBanPeiod, banPeriodMultiplier, BanCounts);
            if (newValue > Till)
                Till = newValue;
        }

        public static DateTime CalcTillDate(DateTime currentDate, int singleBanPeiod, int banPeriodMultiplier, int banCounts)
        {
            int banPeriod;
            try
            {
                checked
                {
                    banPeriod = (int)Math.Pow(singleBanPeiod * banPeriodMultiplier, banCounts);
                }
            }
            catch (OverflowException)
            {
                banPeriod = int.MaxValue;
            }
            return currentDate + new TimeSpan(0, 0, banPeriod);
        }
    }
}
