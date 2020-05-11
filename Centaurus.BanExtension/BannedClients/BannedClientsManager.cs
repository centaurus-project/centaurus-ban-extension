using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;

namespace Centaurus.BanExtension
{
    public class BannedClientsManager : IDisposable
    {
        public BannedClientsStorage Storage { get; }

        private static Logger logger = LogManager.GetCurrentClassLogger();

        private int singleBanPeriod;
        private int banPeriodMultiplier;
        private Dictionary<string, BannedClientRecord> bannedClients;
        private List<BannedClientRecord> updatedClients;
        private Timer savingTimer;

        public BannedClientsManager(string connectionString, int _singleBanPeriod, int _banPeriodMultiplier)
        {
            singleBanPeriod = _singleBanPeriod;
            banPeriodMultiplier = _banPeriodMultiplier;

            Storage = new BannedClientsStorage(connectionString);

            updatedClients = new List<BannedClientRecord>();

            bannedClients = Storage.GetBannedClients();

            InitTimer();
        }

        public void UpdateClients()
        {
            try
            {
                lock (updatedClients)
                {
                    if (updatedClients.Count < 1)
                        return;
                    Storage.UpdateClients(updatedClients);
                    updatedClients = new List<BannedClientRecord>();
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Exception on updating banned clients.");
                throw;
            }
        }

        /// <summary>
        /// Looks for a currently banned client
        /// </summary>
        public bool TryGetBannedClient(string source, DateTime currentDate, out BannedClientRecord bannedClientRecord)
        {
            bannedClientRecord = null;
            if (string.IsNullOrEmpty(source))
                return false;

            lock (bannedClients)
            {
                if (bannedClients.TryGetValue(source, out bannedClientRecord) && bannedClientRecord.IsBanActive(currentDate))
                    return true;
                else
                {
                    bannedClientRecord = null;
                    return false;
                }
            }
        }

        public BannedClientRecord RegisterBan(string source, DateTime currentDate)
        {
            if (string.IsNullOrEmpty(source))
                return null;

            BannedClientRecord bannedClient;
            lock (bannedClients)
            {
                if (!bannedClients.ContainsKey(source))
                {
                    bannedClient = new BannedClientRecord { Source = source };
                    bannedClients[source] = bannedClient;
                }
                else
                    bannedClient = bannedClients[source];
            }
            lock (bannedClient)
            {
                bannedClient.BanCounts++;
                bannedClient.SetTillDate(currentDate, singleBanPeriod, banPeriodMultiplier);
            }
            lock (updatedClients)
                updatedClients.Add(bannedClient);
            return bannedClient;
        }

        public void Dispose()
        {
            savingTimer.Stop();
            savingTimer.Elapsed -= SavingTimer_Elapsed;
            savingTimer.Dispose();
        }

        #region private members

        void InitTimer()
        {
            savingTimer = new Timer();
            savingTimer.Elapsed += SavingTimer_Elapsed;
            savingTimer.AutoReset = false;
            savingTimer.Interval = 5 * 1000;// * 60;
            savingTimer.Start();
        }

        void SavingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                UpdateClients();
            }
            finally
            {
                savingTimer.Start();
            }
        }

        #endregion
    }
}
