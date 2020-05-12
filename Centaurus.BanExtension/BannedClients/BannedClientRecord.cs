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
            int banPeriod;
            try
            {
                checked
                {
                    banPeriod = (int)(Math.Pow(singleBanPeiod, BanCounts) * banPeriodMultiplier);
                }
            }
            catch (OverflowException)
            {
                banPeriod = int.MaxValue;
            }
            DateTime newValue;
            try
            {
                newValue = currentDate + new TimeSpan(0, 0, banPeriod);
            }
            catch (ArgumentOutOfRangeException)
            {
                newValue = DateTime.MaxValue;
            }
            if (newValue > Till)
                Till = newValue;
        }

        public bool IsOnProbation(DateTime currentDate, int singleBanPeiod, int banPeriodMultiplier)
        {
            return BanCounts > 0 && GetProbationEnd(singleBanPeiod, banPeriodMultiplier) > currentDate;
        }

        public DateTime GetProbationEnd(int singleBanPeiod, int banPeriodMultiplier)
        {
            if (BanCounts == 0)
                return DateTime.MinValue;

            int banPeriod;
            try
            {
                checked
                {
                    banPeriod = (int)(Math.Pow(singleBanPeiod, BanCounts) * Math.Sqrt(banPeriodMultiplier));
                }
            }
            catch (OverflowException)
            {
                banPeriod = int.MaxValue;
            }

            try
            {
                return Till + new TimeSpan(0, 0, banPeriod);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateTime.MaxValue;
            }
        }
    }
}
