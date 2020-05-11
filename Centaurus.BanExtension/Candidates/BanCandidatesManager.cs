using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.BanExtension
{
    public class BanCandidatesManager
    {
        Dictionary<string, BanCandidate> candidates = new Dictionary<string, BanCandidate>();
        int violationsCountThreshold;
        TimeSpan violationsPeriodThreshold;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="violationsCountThreshold">When candidate riches this number, it should be banned. </param>
        /// <param name="violationsPeriodThreshold">Period in ms. If client doesn't violate messaging within specified period, all previus violations will be cleard.</param>
        public BanCandidatesManager(int violationsCountThreshold, int violationsPeriodThreshold)
        {
            this.violationsCountThreshold = violationsCountThreshold;
            this.violationsPeriodThreshold = new TimeSpan(0, 0, 0, 0, violationsPeriodThreshold);
        }

        /// <summary>
        /// Registers violation and returns ban verdict result
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="pubkey"></param>
        /// <returns></returns>
        public bool RegisterViolation(string ip, string pubkey = null)
        {
            var ipBanCandidate = GetCandidate(ip);
            var pubkeyBanCandidate = GetCandidate(pubkey);
            return IncrementCandidateViolationsCount(ipBanCandidate, pubkeyBanCandidate);
        }

        #region private members

        BanCandidate GetCandidate(string source)
        {
            if (string.IsNullOrEmpty(source))
                return null;
            BanCandidate banCandidate;
            lock (candidates)
            {
                if (!candidates.ContainsKey(source))
                {
                    banCandidate = new BanCandidate(source);
                    candidates[source] = banCandidate;
                }
                else
                    banCandidate = candidates[source];
            }
            return banCandidate;
        }

        bool ShouldBeBanned(BanCandidate banCandidate)
        {
            return banCandidate.ViolationsCount >= violationsCountThreshold;
        }


        bool IncrementCandidateViolationsCount(BanCandidate ipBanCandidate, BanCandidate pubkeyBanCandidate = null)
        {
            bool shouldBeBanned;
            lock (ipBanCandidate)
            {
                ipBanCandidate.IncViolations(violationsPeriodThreshold);
                shouldBeBanned = ShouldBeBanned(ipBanCandidate);
            }

            if (pubkeyBanCandidate != null)
                lock (pubkeyBanCandidate)
                {
                    pubkeyBanCandidate.IncViolations(violationsPeriodThreshold);
                    if (!shouldBeBanned)
                        shouldBeBanned = ShouldBeBanned(pubkeyBanCandidate);
                }

            return shouldBeBanned;
        }

        #endregion
    }
}
