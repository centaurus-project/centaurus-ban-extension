﻿using Centaurus.Domain;
using Centaurus.Models;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.BanExtension
{
    public class BanExtension : IExtension
    {
        const string connectionStringPropName = "connectionString";
        const string singleBanPeriodPropName = "singleBanPeriod";
        const string banPeriodMultiplierPropName = "banPeriodMultiplier";

        const int violationsCountThreshold = 10;
        const int violationsPeriodThreshold = 1000 * 60; //a minute 

        public BannedClientsManager BannedClientsManager { get; private set; }
        public BanCandidatesManager BanCandidatesManager { get; private set; }

        public int SingleBanPeriod { get; private set; } //in seconds
        public int BanPeriodMultiplier { get; private set; }
        public string ConnectionString { get; private set; }

        public void Init(Dictionary<string, string> settings)
        {

            ConnectionString = GetExtensionConfigValue<string>(settings, connectionStringPropName);
            SingleBanPeriod = GetExtensionConfigValue<int>(settings, singleBanPeriodPropName);
            BanPeriodMultiplier = GetExtensionConfigValue<int>(settings, banPeriodMultiplierPropName);


            BannedClientsManager = new BannedClientsManager(ConnectionString, SingleBanPeriod, BanPeriodMultiplier);
            BanCandidatesManager = new BanCandidatesManager(violationsCountThreshold, violationsPeriodThreshold);

            Global.ExtensionsManager.OnBeforeNewConnection += ExtensionsManager_OnBeforeNewConnection;
            Global.ExtensionsManager.OnConnectionValidated += ExtensionsManager_OnConnectionValidated;
            Global.ExtensionsManager.OnHandleMessageFailed += ExtensionsManager_OnHandleMessageFailed;
        }

        public void Dispose()
        {
            Global.ExtensionsManager.OnBeforeNewConnection -= ExtensionsManager_OnBeforeNewConnection;
            Global.ExtensionsManager.OnConnectionValidated -= ExtensionsManager_OnConnectionValidated;
            Global.ExtensionsManager.OnHandleMessageFailed -= ExtensionsManager_OnHandleMessageFailed;

            BannedClientsManager.UpdateClients();
            BannedClientsManager.Dispose();
        }

        #region private members

        private T GetExtensionConfigValue<T>(Dictionary<string, string> settings, string propName)
        {
            if (!settings.TryGetValue(propName, out string value) || string.IsNullOrWhiteSpace(value))
                throw new ArgumentNullException(propName);
            return (T)Convert.ChangeType(value, typeof(T));
        }

        private void ExtensionsManager_OnHandleMessageFailed(BaseWebSocketConnection connection, MessageEnvelope envelope, Exception exception)
        {
            var currentDate = DateTime.UtcNow;
            if (exception is BaseClientException  //bad requests, forbidden, too many requests etc.
                && BanCandidatesManager.RegisterViolation(connection.Ip, connection.ClientPubKey?.ToString())) //riched max allowed violation count
            {
                BannedClientsManager.RegisterBan(connection.Ip, currentDate);
                BannedClientsManager.RegisterBan(connection.ClientPubKey?.ToString(), currentDate);
                throw new ConnectionCloseException(WebSocketCloseStatus.PolicyViolation, "Too many invalid messages.");
            }
        }

        private void ExtensionsManager_OnConnectionValidated(BaseWebSocketConnection connection)
        {
            var currentDate = DateTime.UtcNow;
            if (BanCandidatesManager.RegisterViolation(connection.ClientPubKey?.ToString()))
            {
                BannedClientsManager.RegisterBan(connection.Ip, currentDate);
                BannedClientsManager.RegisterBan(connection.ClientPubKey?.ToString(), currentDate);
                throw new ConnectionCloseException(WebSocketCloseStatus.PolicyViolation, "Too many connections.");
            }
        }

        private void ExtensionsManager_OnBeforeNewConnection(WebSocket socket, string ip)
        {
            DateTime currentDate = DateTime.UtcNow;
            if (BanCandidatesManager.RegisterViolation(ip))
            {
                BannedClientsManager.RegisterBan(ip, currentDate);
                throw new ConnectionCloseException(WebSocketCloseStatus.PolicyViolation, "Too many connections.");
            }
        }

        #endregion
    }
}
