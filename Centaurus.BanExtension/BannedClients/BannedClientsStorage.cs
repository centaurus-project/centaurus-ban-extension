using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.BanExtension
{
    public class BannedClientsStorage
    {
        private MongoClient client;
        private IMongoDatabase database;
        private IMongoCollection<BannedClientRecord> bannedClientsCollection;

        public BannedClientsStorage(string connectionString)
        {
            var mongoUrl = new MongoUrl(connectionString);

            ConventionRegistry.Register("IgnoreExtraElements", new ConventionPack { new IgnoreExtraElementsConvention(true) }, type => true);

            client = new MongoClient(mongoUrl);
            database = client.GetDatabase(mongoUrl.DatabaseName);
            bannedClientsCollection = database.GetCollection<BannedClientRecord>("bannedClients");
        }

        public Dictionary<string, BannedClientRecord> GetBannedClients()
        {
            return bannedClientsCollection.Find(FilterDefinition<BannedClientRecord>.Empty).ToList().ToDictionary(c => c.Source);
        }

        public void UpdateClients(List<BannedClientRecord> newBannedClientRecords)
        {
            var filter = Builders<BannedClientRecord>.Filter;
            var update = Builders<BannedClientRecord>.Update;

            var updatesLength = newBannedClientRecords.Count;
            var updates = new WriteModel<BannedClientRecord>[updatesLength];

            for (int i = 0; i < updatesLength; i++)
            {
                var banRecord = newBannedClientRecords[i];
                var source = banRecord.Source;
                var currentAccFilter = filter.Eq(a => a.Source, source);
                if (banRecord.BanCounts == 0)
                    updates[i] = new DeleteOneModel<BannedClientRecord>(currentAccFilter);
                else
                    updates[i] = new ReplaceOneModel<BannedClientRecord>(currentAccFilter, banRecord) { IsUpsert = true };
            }
            bannedClientsCollection.BulkWrite(updates);
        }
    }
}