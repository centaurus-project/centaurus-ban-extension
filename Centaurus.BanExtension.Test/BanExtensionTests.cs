using Centaurus.DAL.Mongo;
using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.Test;
using dotnetstandard_bip32;
using MongoDB.Bson.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;

namespace Centaurus.BanExtension.Test
{
    public class BanExtensionTests
    {
        const int banPeriod = 5;
        const int boostFactor = 1;

        [OneTimeSetUp]
        public void Setup()
        {
            var dbName = "testDB";
            var replicaSet = "centaurusTest";
            var dbPort = 27001;

            MongoDBServerHelper.RunMongoDBServers(new int[] { dbPort }, replicaSet);

            var extensionsPath = ExtensionConfigGenerator.Generate(dbPort, dbName, replicaSet, banPeriod, boostFactor);

            var settings = new AlphaSettings();
            settings.ExtensionsConfigFilePath = Path.GetFullPath(extensionsPath);
            settings.ConnectionString = $"mongodb://localhost:{dbPort}/{dbName}?replicaSet={replicaSet}";
            GlobalInitHelper.SetCommonSettings(settings, TestEnvironment.AlphaKeyPair.SecretSeed);
            settings.Build();

            GlobalInitHelper.Setup(GlobalInitHelper.GetPredefinedClients(), GlobalInitHelper.GetPredefinedAuditors(), settings, new MongoStorage());
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            MongoDBServerHelper.Stop();
            Thread.Sleep(1000);
        }

        [Test]
        public void BannedClientsManagingTest()
        {
            var extensionSettings = Global.ExtensionsManager.Extensions.First().Config;
            var banExtension = new BanExtension();
            banExtension.Init(extensionSettings);

            var banTime = DateTime.UtcNow;
            banTime = banTime.AddTicks(-banTime.Ticks % TimeSpan.TicksPerSecond); //ignore milliseconds because it's rounded on saving
            var source = "testClient";

            //register and permanent banned client
            banExtension.BannedClientsManager.RegisterBan(source, banTime);
            banExtension.BannedClientsManager.UpdateClients();

            banExtension.BannedClientsManager.TryGetBannedClient(source, out var bannedClientRecord);

            //check probation end date
            Assert.AreEqual(bannedClientRecord.Till + new TimeSpan(0, 0, 5), bannedClientRecord.GetProbationEndDate(5, 1));

            //load client from db
            var clients = banExtension.BannedClientsManager.Storage.GetBannedClients();
            Assert.AreEqual(clients.Count, 1);

            //compare loaded from db object with one that stored in memory
            var firstClient = clients.Values.First();
            Assert.AreEqual(firstClient.Source, bannedClientRecord.Source);
            Assert.AreEqual(firstClient.BanCounts, bannedClientRecord.BanCounts);
            Assert.AreEqual(firstClient.Till, bannedClientRecord.Till);
            Assert.AreEqual(firstClient.GetProbationEndDate(banPeriod, boostFactor)
                , bannedClientRecord.GetProbationEndDate(banPeriod, boostFactor));

            //cleanup test
            Thread.Sleep((BannedClientRecord.GetBanPeriod(banPeriod, boostFactor, bannedClientRecord.BanCounts) 
                + BannedClientRecord.GetProbationPeriod(banPeriod, boostFactor, bannedClientRecord.BanCounts)) * 1000); //wait for ban and probation end
            banExtension.BannedClientsManager.CleanUpClients();
            banExtension.BannedClientsManager.UpdateClients();

            banExtension.BannedClientsManager.TryGetBannedClient(source, out bannedClientRecord);

            Assert.AreEqual(bannedClientRecord, null);
            clients = banExtension.BannedClientsManager.Storage.GetBannedClients();
            Assert.AreEqual(clients.Count, 0);

            banExtension.Dispose();

            Console.WriteLine();
        }

        [Test]
        public void TooManyConnectionsTest()
        {
            try
            {
                for (var i = 0; i < 1000; i++)
                    Global.ExtensionsManager.BeforeNewConnection(new ClientWebSocket(), "test");

                Assert.Fail("Client wasn't block after 1000 connections.");
            }
            catch (ConnectionCloseException exc)
            {
                Assert.AreEqual(WebSocketCloseStatus.PolicyViolation, exc.Status);
            }
        }

        [Test]
        public void TooManyConnectionsFromDifferentIpTest()
        {
            try
            {
                var pubKey = (RawPubKey)KeyPair.Random();
                for (var i = 0; i < 1000; i++)
                {
                    var ip = "127.0.0." + i;
                    var webSocket = new FakeWebSocket();
                    Global.ExtensionsManager.BeforeNewConnection(webSocket, ip);
                    var clientConnection = new AlphaWebSocketConnection(webSocket, ip) { ClientPubKey = pubKey };
                    Global.ExtensionsManager.ConnectionValidated(clientConnection);
                }

                Assert.Fail("Client wasn't block after 1000 connections.");
            }
            catch (ConnectionCloseException exc)
            {
                Assert.AreEqual(WebSocketCloseStatus.PolicyViolation, exc.Status);
            }
        }

        [Test]
        public void BlockOnClientExceptionTest()
        {
            try
            {
                var pubKey = (RawPubKey)KeyPair.Random();
                var webSocket = new FakeWebSocket();
                var clientConnection = new AlphaWebSocketConnection(webSocket, "127.0.0.1") { ClientPubKey = pubKey };
                for (var i = 0; i < 1000; i++)
                {
                    Global.ExtensionsManager.HandleMessageFailed(clientConnection, null, new BaseClientException());
                }

                Assert.Fail("Client wasn't block after 1000 connections.");
            }
            catch (ConnectionCloseException exc)
            {
                Assert.AreEqual(WebSocketCloseStatus.PolicyViolation, exc.Status);
            }
        }
    }
}