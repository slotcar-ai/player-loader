using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace PlayerLoaderLocal
{
    class Program
    {
        static DockerClient client;

        static void Main(string[] args)
        {
            // There's some magic going on here!
            // https://github.com/Microsoft/Docker.DotNet/issues/118
            // I would guess this only works on linux with docker installed
            var config = new DockerClientConfiguration(new Uri("unix://var/run/docker.sock"));
            client = config.CreateClient();

            RunRaceTrackAndPlayer().GetAwaiter().GetResult();

            Console.ReadKey();
        }

        static async Task RunRaceTrackAndPlayer()
        {
            // We're trying to replicate the following:
            // docker network create --subnet=172.18.0.0/16 slotcarai-subnet
            // docker run --net slotcarai-subnet --ip 172.18.0.2 -it slotcarai/race-track:34
            // docker run --net slotcarai-subnet --env RACE_TRACK_HOSTNAME = 172.18.0.2 -it slotcarai/player:40
            // Network is not done, but rest works.

            var raceTrack = await CreateRaceTrackContainer();
            var player = await CreatePlayerContainer();

            await Task.WhenAll(
                StartAndWaitAsync(raceTrack),
                StartAndWaitAsync(player));
        }

        static async Task<string> CreateRaceTrackContainer()
        {
            var progressReporter = new Progress<JSONMessage>(m => Console.WriteLine($"{ m.Status }, { m.ID }, { m.ProgressMessage }"));
            
            await client.Images.CreateImageAsync(new ImagesCreateParameters()
            {
                FromImage = "slotcarai/race-track",
                Tag = "34"
            }, null, progressReporter);

            var response = await client.Containers.CreateContainerAsync(new CreateContainerParameters()
            {
                Image = "slotcarai/race-track:34",
                NetworkingConfig = new NetworkingConfig()
                {
                    EndpointsConfig = new Dictionary<string, EndpointSettings>
                    {
                        {"slotcarai-subnet", new EndpointSettings()
                        {
                            IPAddress = "172.18.0.2",
                            NetworkID = "slotcarai-subnet"
                        }}
                    }
                }
            });

            return response.ID;
        }

        static async Task<string> CreatePlayerContainer()
        {
            var progressReporter = new Progress<JSONMessage>(m => Console.WriteLine($"{ m.Status }, { m.ID }, { m.ProgressMessage }"));

            await client.Images.CreateImageAsync(new ImagesCreateParameters()
            {
                FromImage = "slotcarai/player",
                Tag = "40"
            }, null, progressReporter);

            var response = await client.Containers.CreateContainerAsync(new CreateContainerParameters()
            {
                Image = "slotcarai/player:40",
                NetworkingConfig = new NetworkingConfig()
                {
                    EndpointsConfig = new Dictionary<string, EndpointSettings>
                    {
                        {"slotcarai-subnet", new EndpointSettings()
                        {
                            NetworkID = "slotcarai-subnet"
                        }}
                    }
                },
                Env = new List<string> { "RACE_TRACK_HOSTNAME=172.18.0.2" }
            });

            return response.ID;
        }

        static async Task<ContainerWaitResponse> StartAndWaitAsync(string containerId)
        {
            Console.WriteLine($"Started container with ID: { containerId }");
            
            await client.Containers.StartContainerAsync(containerId, new ContainerStartParameters());
            
            return await client.Containers.WaitContainerAsync(containerId);
        }
    }
}
