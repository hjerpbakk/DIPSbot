﻿using LightInject;
using System;
using System.Threading.Tasks;
using Hjerpbakk.DIPSbot.Services;
using SlackConnector;
using Hjerpbakk.DIPSBot;
using System.Threading;
using System.Net.Http;
using Hjerpbakk.DIPSBot.Clients;
using Hjerpbakk.DIPSBot.Actions;
using Hjerpbakk.DIPSBot.MessageHandlers;
using Hjerpbakk.DIPSBot.Configuration;
using Hjerpbakk.ServiceDiscovery.Client;
using Hjerpbakk.DIPSBot.Services;
using Microsoft.ApplicationInsights;
using System.Collections.Generic;

namespace Hjerpbakk.DIPSbot.Runner
{
    class DIPSbotHost
	{
        static readonly ManualResetEvent manualResetEvent;

        readonly TelemetryClient telemetryClient;

        int restartCount;
        DIPSbotImplementation DIPSbot;

        static DIPSbotHost() {
            manualResetEvent = new ManualResetEvent(false);
        }

        public DIPSbotHost(TelemetryClient telemetryClient)
        {
            this.telemetryClient = telemetryClient;
        }

        public async Task<string> Start(AppConfiguration configuration)
		{
            try
            {
				configuration.FatalExceptionHandler = RestartBot;
				while (true)
				{
                    Console.WriteLine("Starting DIPSbot...");
                    IServiceContainer serviceContainer;
					try
					{
						serviceContainer = await CompositionRoot(configuration);
						DIPSbot = serviceContainer.GetInstance<DIPSbotImplementation>();
						await DIPSbot.Connect();
					}
					catch (Exception e)
					{
						Console.WriteLine($"Error connecting to Slack or other services: {e}");
                        var properties = new Dictionary<string, string> { { "Error connecting to Slack ", restartCount.ToString() } };
                        telemetryClient.TrackException(e);
						return "";
					}

                    Console.WriteLine("DIPSbot started.");
					manualResetEvent.WaitOne();

					Console.WriteLine("Stopping before restart...");
					var res = Stop().GetAwaiter().GetResult();
					serviceContainer.Dispose();

					manualResetEvent.Reset();
				}
			}
            catch (Exception e)
            {
                var properties = new Dictionary<string, string> { { "Died unexpectedly ", restartCount.ToString() } };
                telemetryClient.TrackException(e);
                return e.ToString();
            }
        }

        public async Task<string> Stop()
		{
            try {
				Console.WriteLine("Disconnecting...");
				await DIPSbot.Close();
				DIPSbot = null;
                Console.WriteLine("DIPSbot stopped.");
            } catch (Exception e) {
                return e.ToString();
            }

            return "";
		}

        void RestartBot(Exception exception) {
            Interlocked.Increment(ref restartCount);
            manualResetEvent.Set();
            Console.WriteLine("Trying to restart. Cause of death:");
            Console.WriteLine(exception);

            var properties = new Dictionary<string, string> { { "Bot died during message handling, trying to restart ", restartCount.ToString() } };
            telemetryClient.TrackException(exception);
        }

		async Task<IServiceContainer> CompositionRoot(AppConfiguration configuration)
		{
			var serviceContainer = new ServiceContainer();
			serviceContainer.RegisterInstance<IServiceContainer>(serviceContainer);

            serviceContainer.RegisterInstance(telemetryClient);

            var httpClient = new HttpClient();
            serviceContainer.RegisterInstance(httpClient);
            var serviceDiscoveryClient = new ServiceDiscoveryClient(httpClient, configuration.ServiceDiscoveryServerName);

            // TODO: Smoothify
            var kitchenServiceTask = serviceDiscoveryClient.GetServiceURL(configuration.KitchenResponsibleServiceName);
            var comicsServiceTask = serviceDiscoveryClient.GetServiceURL(configuration.ComicsServiceName);
            await Task.WhenAll(kitchenServiceTask, comicsServiceTask);
            configuration.KitchenServiceURL = kitchenServiceTask.Result;
            configuration.ComicsServiceURL = comicsServiceTask.Result;

            serviceContainer.RegisterInstance(configuration);
            serviceContainer.RegisterInstance<IReadOnlyAppConfiguration>(configuration);
            serviceContainer.RegisterInstance(serviceDiscoveryClient);

			serviceContainer.Register<ISlackConnector, SlackConnector.SlackConnector>(new PerContainerLifetime());
			serviceContainer.Register<ISlackIntegration, SlackIntegration>(new PerContainerLifetime());

            serviceContainer.Register<ComicsClient>(new PerContainerLifetime());
            serviceContainer.Register<IKitchenResponsibleClient, KitchenResponsibleClient>(new PerContainerLifetime());
			serviceContainer.Register<IOrganizationService, FileOrganizationService>(new PerContainerLifetime());
            serviceContainer.Register<IDebuggingService, DebuggingService>(new PerContainerLifetime());

            serviceContainer.Register<AdminMessageHandler>(new PerContainerLifetime());
            serviceContainer.Register<ChannelMessageHandler>(new PerContainerLifetime());
            serviceContainer.Register<MessageHandler>(new PerContainerLifetime());
            serviceContainer.Register<RegularUserMessageHandler>(new PerContainerLifetime());
            serviceContainer.Register<TrondheimMessageHandler>(new PerContainerLifetime());

            // TODO: Smoothify
            serviceContainer.Register<AddDevelopersToUtviklingChannelAction>(new PerContainerLifetime());
            serviceContainer.Register<KitchenResponsibleAction>(new PerContainerLifetime());
            serviceContainer.Register<AddEmployeeAction>(new PerContainerLifetime());
            serviceContainer.Register<ThanksAction>(new PerContainerLifetime());
            serviceContainer.Register<NegativeAction>(new PerContainerLifetime());
            serviceContainer.Register<WeekAction>(new PerContainerLifetime());
            serviceContainer.Register<RemoveEmployeeAction>(new PerContainerLifetime());
            serviceContainer.Register<ComicsAction>(new PerContainerLifetime());
            serviceContainer.Register<VersionAction>(new PerContainerLifetime());
			
			serviceContainer.Register<DIPSbotImplementation>(new PerContainerLifetime());

			return serviceContainer;
		}
	}
}