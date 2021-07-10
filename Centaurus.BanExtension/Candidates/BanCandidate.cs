using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.BanExtension
{
    public class BanCandidate
    {
        public BanCandidate(string source)
        {
            Source = source;
        }

        public string Source { get; set; }

        public DateTime LastViolationDate { get; private set; }

        public int ViolationsCount { get; private set; }

        public void IncViolations(TimeSpan violationsPeriodThreshold)
        {
            var currentDate = DateTime.UtcNow;
            TryClearViolations(currentDate, violationsPeriodThreshold);
            ViolationsCount++;
            LastViolationDate = currentDate;
        }

        void TryClearViolations(DateTime currentDate, TimeSpan violationsPeriodThreshold)
        {
            if (currentDate - LastViolationDate > violationsPeriodThreshold)
                ViolationsCount = 0;
        }
    }
}
