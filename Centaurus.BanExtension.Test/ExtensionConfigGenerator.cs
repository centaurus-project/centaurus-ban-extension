using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Centaurus.BanExtension.Test
{
    public static class ExtensionConfigGenerator
    {
        public static string Generate(int dbPort, string dbName, string replicaSet, int banPeriod, int banPeriodMultiplier)
        {
            var o = new
            {
                extensions = new object[] {
                    new {
                        name = "Centaurus.BanExtension",
                        extensionConfig = new {
                            connectionString = $"mongodb://localhost:{dbPort}/{dbName}?replicaSet={replicaSet}",
                            singleBanPeriod = banPeriod,
                            banPeriodMultiplier = banPeriodMultiplier
                        }
                    }
                }
            };

            var value = Newtonsoft.Json.JsonConvert.SerializeObject(o);
            var path = "test-extesnions.json";
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllText(path, value);
            return path;
        }
    }
}
