using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Centaurus.BanExtension.Test
{

    public static class MongoDBServerHelper
    {
        private static Process[] processes;

        public static void RemoveWorkingDirectories()
        {
            if (!Directory.Exists(ConstellationBinFolder))
                return;
            var workingDirectories = Directory.EnumerateDirectories(ConstellationBinFolder, "*WorkingDirectory");
            foreach (var dir in workingDirectories)
                Directory.Delete(dir, true);
        }

        public static string ConstellationBinFolder
        {
            get
            {
                return "ConstellationBin";
            }
        }

        public static void RunMongoDBServers(int[] mongoDbServerPorts, string replicaSet)
        {
            RemoveWorkingDirectories();
            var processes = new Process[mongoDbServerPorts.Length];
            var members = string.Empty;
            for (var i = 0; i < mongoDbServerPorts.Length; i++)
            {
                var workingDir = $"MongoDbServer{i}WorkingDirectory";
                Directory.CreateDirectory(Path.Combine(ConstellationBinFolder, workingDir));

                processes[i] = ProcessHelper.StartNewProcess("mongod",
                    $"--dbpath {workingDir} " +
                    $"--port {mongoDbServerPorts[i]} " +
                    $"--replSet {replicaSet} ", ConstellationBinFolder);

                members += $"{{ _id: {i}, host: 'localhost:{mongoDbServerPorts[i]}' }},";
            }

            //some time for database servers to start
            Thread.Sleep(5000);

            var initScript = "rs.initiate({" +
                        $"_id: '{replicaSet}', " +
                        "version: 1, " +
                        $"members: [{members.TrimEnd(',')}] " +
                    "})";

            ProcessHelper.StartNewProcess("mongo", $"--port {mongoDbServerPorts[0]} --eval \"{initScript}\"", ConstellationBinFolder);

            MongoDBServerHelper.processes = processes;
        }

        public static void Stop()
        {

            foreach (var p in processes)
                try
                {
                    p.Kill();
                }
                catch { }
        }
    }
}
