﻿using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using SlackConnector.Models;
using Hjerpbakk.DIPSbot;
using Hjerpbakk.DIPSBot.Predicates;

namespace Hjerpbakk.DIPSBot.Actions
{
    class ListCommandsAction : IAction
    {
        readonly ISlackIntegration slackIntegration;
        readonly IEnumerable<IPredicate> availableActions;

        public ListCommandsAction(ISlackIntegration slackIntegration, IEnumerable<IPredicate> availableActions)
        {
            this.slackIntegration = slackIntegration;
            this.availableActions = availableActions;
        }

        // TODO: Dersom boten blir tagget i en tråd og han ikke forstår, kommer det en tom listing av kommandoer
        // TODO: Boten forstår ikke meldinger i en dm-kanal med flere...
        public async Task Execute(SlackMessage message)
        {
            var commands = string.Join("", availableActions.Where(predicate => !string.IsNullOrEmpty(predicate.CommandText)).Select(predicate => "- " + predicate.CommandText + "\n"));
            var availableCommands = "*Available commands*\n" + commands;
            await slackIntegration.SendDirectMessage(message.User, availableCommands);
        }
    }
}
