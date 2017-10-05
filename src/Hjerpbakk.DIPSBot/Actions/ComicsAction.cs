﻿using System.Threading.Tasks;
using Hjerpbakk.DIPSbot;
using Hjerpbakk.DIPSBot.Clients;
using SlackConnector.Models;

namespace Hjerpbakk.DIPSBot.Actions
{
    public class ComicsAction : IAction
	{
		readonly ISlackIntegration slackIntegration;
        readonly ComicsClient comicsClient;

		public ComicsAction(ISlackIntegration slackIntegration, ComicsClient comicsClient)
		{
			this.slackIntegration = slackIntegration;
            this.comicsClient = comicsClient;
		}

		public async Task Execute(SlackMessage message) =>
            await slackIntegration.SendMessageToChannel(message.ChatHub, $"<{await comicsClient.GetRandomComicAsync()}|Awesome tegneserie \ud83d\ude03 >");
    }
}
