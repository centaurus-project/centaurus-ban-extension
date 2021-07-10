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

        public void SetTillDate(DateTime currentDate, int singleBanPeiod, int boostFactor)
        {
            var banPeriod = GetBanPeriod(singleBanPeiod, boostFactor, BanCounts);
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

        public bool IsOnProbation(DateTime currentDate, int singleBanPeiod, int boostFactor)
        {
            return BanCounts > 0 && GetProbationEndDate(singleBanPeiod, boostFactor) > currentDate;
        }

        public DateTime GetProbationEndDate(int singleBanPeiod, int boostFactor)
        {
            if (BanCounts == 0)
                return DateTime.MinValue;

            int probationPeriod = GetProbationPeriod(singleBanPeiod, boostFactor, BanCounts);

            try
            {
                return Till + new TimeSpan(0, 0, probationPeriod);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateTime.MaxValue;
            }
        }

        public static int GetProbationPeriod(int singleBanPeiod, int boostFactor, int banCounts)
        {
            return GetPeriod(singleBanPeiod, boostFactor, banCounts - 1);
        }

        public static int GetBanPeriod(int singleBanPeiod, int boostFactor, int banCounts)
        {
            return GetPeriod(singleBanPeiod, boostFactor, banCounts);
        }

        static int GetPeriod(int singleBanPeiod, int boostFactor, int banCounts)
        {
            int period;
            try
            {
                checked
                {
                    period = (int)(Math.Pow(boostFactor, banCounts) * singleBanPeiod);
                }
            }
            catch (OverflowException)
            {
                period = int.MaxValue;
            }
            return period;
        }
    }
}
