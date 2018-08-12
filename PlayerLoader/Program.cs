using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using k8s;
using k8s.Models;

namespace PlayerLoader
{
    public class Program
    {
        public static IQueueClient queue;
        public static IKubernetes k8s;

        public static void Main(string[] args)
        {
            Console.WriteLine("Starting player-loader...");

            Console.WriteLine("Configuring Kubernetes client...");
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
            k8s = new Kubernetes(config);

            Console.WriteLine("Connecting to Azure Service Bus...");
            var serviceBusConnectionString = Environment.GetEnvironmentVariable("SLOTCAR_AI_SERVICEBUS_KEY_SENDLISTEN");
            var queueName = "player-loader";
            var queueClient = new QueueClient(serviceBusConnectionString, queueName);
            var options = new MessageHandlerOptions(ExceptionReceivedHandler)
            {
                MaxConcurrentCalls = 1,
                AutoComplete = true
            };
            
            Console.WriteLine($"Setting up message handler...");
            queueClient.RegisterMessageHandler(ProcessMessagesAsync, options);

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

        static async Task ProcessMessagesAsync(Message message, CancellationToken token)
        {
            var messageBody = Encoding.UTF8.GetString(message.Body);
            Console.WriteLine($"Received message: SequenceNumber:{ message.SystemProperties.SequenceNumber } Body:{ messageBody }");

            Console.WriteLine("Loading new race-track and player...");
            await DeployRaceTrackAndGivenPlayerAsync(messageBody);

            Console.WriteLine($"slotcarai/player:{ messageBody } loaded");
        }

        static Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            Console.WriteLine($"Message handler encountered an exception {exceptionReceivedEventArgs.Exception}.");
            var context = exceptionReceivedEventArgs.ExceptionReceivedContext;
            Console.WriteLine("Exception context for troubleshooting:");
            Console.WriteLine($"- Endpoint: {context.Endpoint}");
            Console.WriteLine($"- Entity Path: {context.EntityPath}");
            Console.WriteLine($"- Executing Action: {context.Action}");
            return Task.CompletedTask;
        }

        static async Task DeployRaceTrackAndGivenPlayerAsync(string playerTag)
        {
            var deployments = await k8s.ListNamespacedDeploymentAsync("default");

            var raceTrackDeployment = RaceTrackDeployment();
            if (deployments.Items.Any(d => d.Metadata.Name == "race-track"))
            {
                await k8s.ReplaceNamespacedDeploymentAsync(raceTrackDeployment, "race-track", "default");
            }
            else
            {
                await k8s.CreateNamespacedDeploymentAsync(raceTrackDeployment, "default");
            }

            var raceTrackService = RaceTrackService();
            // TODO: This is probably worth doing? raceTrackService.Validate();
            var services = k8s.ListNamespacedService("default");
            if (!services.Items.Any(s => s.Metadata.Name == "race-track"))
            {
                await k8s.CreateNamespacedServiceAsync(raceTrackService, "default");
            }

            var playerDeployment = PlayerDeployment(playerTag);
            if (deployments.Items.Any(d => d.Metadata.Name == "player"))
            {
                await k8s.ReplaceNamespacedDeploymentAsync(playerDeployment, "player", "default");
            }
            else
            {
                await k8s.CreateNamespacedDeploymentAsync(playerDeployment, "default");
            }
        }

        public static V1Deployment PlayerDeployment(string playerTag)
        {
            return new V1Deployment
            {
                Metadata = new V1ObjectMeta
                {
                    Name = "player",
                    Labels = new Dictionary<string, string>
                    {
                        {"app", "slotcarai"}
                    }
                },
                Spec = new V1DeploymentSpec
                {
                    Replicas = 1,
                    Selector = new V1LabelSelector
                    {
                        MatchLabels = new Dictionary<string, string>
                        {
                            {"app", "player"}
                        }
                    },
                    Template = new V1PodTemplateSpec
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Labels = new Dictionary<string, string>
                            {
                                {"app", "player"}
                            }
                        },
                        Spec = new V1PodSpec
                        {
                            Containers = new List<V1Container>
                            {
                                new V1Container
                                {
                                    Name = "player",
                                    Image = $"slotcarai/player:{ playerTag }",
                                    Ports = new List<V1ContainerPort>
                                    {
                                        new V1ContainerPort
                                        {
                                            ContainerPort = 11000
                                        }
                                    },
                                    Env = new List<V1EnvVar>
                                    {
                                        new V1EnvVar
                                        {
                                            Name = "RACE_TRACK_HOSTNAME",
                                            Value = "race-track"
                                        },
                                        new V1EnvVar
                                        {
                                            Name = "RACE_TRACK_PORT",
                                            Value = "11000"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        public static V1Deployment RaceTrackDeployment()
        {
            return new V1Deployment
            {
                Metadata = new V1ObjectMeta
                {
                    Name = "race-track",
                    Labels = new Dictionary<string, string>
                    {
                        {"app", "slotcarai"}
                    }
                },
                Spec = new V1DeploymentSpec
                {
                    Replicas = 1,
                    Selector = new V1LabelSelector
                    {
                        MatchLabels = new Dictionary<string, string>
                        {
                            {"app", "race-track"}
                        }
                    },
                    Template = new V1PodTemplateSpec
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Labels = new Dictionary<string, string>
                            {
                                {"app", "race-track"}
                            }
                        },
                        Spec = new V1PodSpec
                        {
                            Containers = new List<V1Container>
                            {
                                new V1Container
                                {
                                    Name = "race-track",
                                    Image = "slotcarai/race-track:25",
                                    Ports = new List<V1ContainerPort>
                                    {
                                        new V1ContainerPort
                                        {
                                            ContainerPort = 11000
                                        }
                                    },
                                    Env = new List<V1EnvVar>
                                    {
                                        new V1EnvVar
                                        {
                                            Name = "RACE_TRACK_PORT",
                                            Value = "11000"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        public static V1Service RaceTrackService()
        {
            return new V1Service
            {
                Metadata = new V1ObjectMeta
                {
                    Name = "race-track"
                },
                Spec = new V1ServiceSpec
                {
                    Selector = new Dictionary<string, string>
                    {
                        {"app", "race-track"}
                    },
                    Ports = new List<V1ServicePort>
                    {
                        new V1ServicePort
                        {
                            Name = "slotcarai-race-track",
                            Port = 11000
                        }
                    }
                }
            };
        }
    }
}
