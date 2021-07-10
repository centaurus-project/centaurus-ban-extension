using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace Centaurus.BanExtension
{
    public class BannedClientsManager : IDisposable
    {
        public BannedClientsStorage Storage { get; }

        private static Logger logger = LogManager.GetCurrentClassLogger();

        private object updatedClientsSyncRoot = new { };

        private int singleBanPeriod;
        private int boostFactor;
        private Dictionary<string, BannedClientRecord> bannedClients;
        private Dictionary<string, BannedClientRecord> updatedClients;
        private Timer savingTimer;
        private Timer cleanUpTimer;

        public BannedClientsManager(string connectionString, int _singleBanPeriod, int _banPeriodMultiplier)
        {
            singleBanPeriod = _singleBanPeriod;
            boostFactor = _banPeriodMultiplier;

            Storage = new BannedClientsStorage(connectionString);

            updatedClients = new Dictionary<string, BannedClientRecord>();

            bannedClients = Storage.GetBannedClients();

            InitTimers();
        }


        public void CleanUpClients()
        {
            var currentDate = DateTime.UtcNow;
            var allBannedClients = bannedClients.Values.ToArray();
            foreach (var bannedClient in allBannedClients)
            {
                lock (bannedClient)
                    if (!bannedClient.IsOnProbation(currentDate, singleBanPeriod, boostFactor))
                        bannedClient.BanCounts = 0;

                lock (updatedClientsSyncRoot)
                    if (!updatedClients.ContainsKey(bannedClient.Source))
                        updatedClients[bannedClient.Source] = bannedClient;
            }
        }


        public void UpdateClients()
        {
            List<BannedClientRecord> currentUpdates;
            lock (updatedClientsSyncRoot)
            {
                if (updatedClients.Count < 1)
                    return;
                currentUpdates = updatedClients.Values.ToList();
                updatedClients = new Dictionary<string, BannedClientRecord>();
            }

            try
            {
                Storage.UpdateClients(currentUpdates);
            }
            catch (Exception exc)
            {
                //make sure updates are not lost
                foreach (var bannedClient in currentUpdates)
                {
                    lock (updatedClientsSyncRoot)
                    {
                        if (!updatedClients.ContainsKey(bannedClient.Source))
                            updatedClients[bannedClient.Source] = bannedClient;
                    }
                }
                logger.Error(exc, "Exception on updating banned clients.");
                throw;
            }

            try
            {
                //make sure updates are not lost
                foreach (var bannedClient in currentUpdates)
                {
                    if (bannedClient.BanCounts > 0)
                        continue;
                    lock (bannedClients)
                    {
                        if (bannedClients.ContainsKey(bannedClient.Source)
                            && bannedClients[bannedClient.Source].BanCounts == 0) //check that value is 0, because new ban could be registered during update
                            bannedClients.Remove(bannedClient.Source);
                    }
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Exception on unbanned clients cleanup.");
                throw;
            }
        }

        public bool TryGetBannedClient(string source, out BannedClientRecord bannedClientRecord)
        {
            lock (bannedClients)
                return bannedClients.TryGetValue(source, out bannedClientRecord);
        }

        public bool IsClientBanned(string source, DateTime currentDate)
        {
            lock (bannedClients)
                return TryGetBannedClient(source, out BannedClientRecord bannedClientRecord) && bannedClientRecord.IsBanActive(currentDate);
        }


        public BannedClientRecord RegisterBan(string source, DateTime currentDate)
        {
            if (string.IsNullOrEmpty(source))
                return null;

            BannedClientRecord bannedClient;
            //get ban record or register new one
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
            //update ban counts
            lock (bannedClient)
            {
                if (bannedClient.BanCounts > 0
                    && !bannedClient.IsOnProbation(currentDate, singleBanPeriod, boostFactor)) //if probation period is over, but bans count weren't reset
                    bannedClient.BanCounts = 0;

                bannedClient.BanCounts++;
                bannedClient.SetTillDate(currentDate, singleBanPeriod, boostFactor);
            }
            //add ban record to update list
            lock (updatedClientsSyncRoot)
            {
                if (!updatedClients.ContainsKey(bannedClient.Source))
                    updatedClients[bannedClient.Source] = bannedClient;
            }
            return bannedClient;
        }

        public void Dispose()
        {
            savingTimer.Stop();
            savingTimer.Elapsed -= SavingTimer_Elapsed;
            savingTimer.Dispose();

            cleanUpTimer.Stop();
            cleanUpTimer.Elapsed -= CleanUpTimer_Elapsed;
            cleanUpTimer.Dispose();
        }

        #region private members

        void InitTimers()
        {
            savingTimer = new Timer();
            savingTimer.Elapsed += SavingTimer_Elapsed;
            savingTimer.AutoReset = false;
            savingTimer.Interval = new TimeSpan(0, 10, 0).TotalMilliseconds;
            savingTimer.Start();

            cleanUpTimer = new Timer();
            cleanUpTimer.Elapsed += CleanUpTimer_Elapsed;
            cleanUpTimer.AutoReset = false;
            cleanUpTimer.Interval = new TimeSpan(1, 0, 0).TotalMilliseconds;
            cleanUpTimer.Start();
        }

        void CleanUpTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                CleanUpClients();
            }
            finally
            {
                cleanUpTimer.Start();
            }
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
