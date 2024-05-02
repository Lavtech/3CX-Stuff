using System;
using System.Collections.Generic;
using System.Linq;
using TCX.Configuration;

/// <summary>
///Previously, this sample used "LOGGED_IN_QUEUES" DN property of the extension.
///Nowadays(since v16) login/logout state of the agent is explicitly defined as a property of QueueAgent object.
///So, this sample is modified to reflect actual way to work with per queue login for of the extension.
///Old code is left and commented out to provide hints how to modify code which was relying on the value of "LOGGEN_IN_QUEUES" DN property
/// </summary>
namespace OMSamples.Samples
{
    [SampleCode("qlogin")]
    [SampleAction("login", "agent=<extension number> [workingset=<queue dn list>]")]
    [SampleAction("logout", "agent=<extension number> [workingset=<queue dn list>]")]
    [SampleAction("status", "agent=<extension number>")]
    [SampleDescription("shows how to change status of the agent in the queue. ")]
    class QueueLogin : ISample
    {
        public void Run(PhoneSystem ps, string action, Dictionary<string, string> args)
        {
            if (action == "")
            {
                this.ShowSampleInfo();
                return;
            }

            var agent = ps.GetDNByNumber(args["agent"]) as Extension;
            if (agent == null)
            {
                Console.WriteLine($"{args["agent"]} is not an agent");
                return;
            }

            if (!agent.QueueMembership.Any())
            {
                Console.WriteLine($"Extension {agent.Number} is not an agent of the queues");
                return;
            }

            if (args.TryGetValue("workingset", out var working_set))
            {
                var newset = working_set.Split(',').ToHashSet();
                foreach (var membership in agent.QueueMembership)
                {
                    bool should_be_logged_in = newset.Contains(membership.Queue.Number);
                    bool logged_in = membership.QueueStatus == QueueStatusType.LoggedIn;
                    if (should_be_logged_in && !logged_in)
                    {
                        membership.QueueStatus = QueueStatusType.LoggedIn;
                    }
                    else if (!should_be_logged_in && logged_in)
                    {
                        membership.QueueStatus = QueueStatusType.LoggedOut;
                    }
                }
            }
            switch (action)
            {
                case "login":
                    agent.QueueStatus = QueueStatusType.LoggedIn;
                    break;
                case "logout":
                    agent.QueueStatus = QueueStatusType.LoggedIn;
                    break;
                default:
                    Console.WriteLine($"Undefined action '{action}'");
                    return;
            }
            agent.Save();
            Console.WriteLine($"{agent.Number} - {agent.FirstName} {agent.FirstName}:");
            foreach (var qa in agent.QueueMembership) //(0)
            {
                var ExtensionStatus = agent.QueueStatus;//(1)
                var ProfileAllowsLogin = (agent.IsOverrideActiveNow ? agent.CurrentProfileOverride : agent.CurrentProfile)
                    .ForceQueueStatus != (int)QueueStatusType.LoggedOut;//(2)
                var QueueAgentStatus = qa.QueueStatus;//(3)

                var cumulativeStatus =
                    ExtensionStatus == QueueStatusType.LoggedIn
                    &&
                    ProfileAllowsLogin
                    &&
                    QueueAgentStatus == QueueStatusType.LoggedIn
                    ?
                    QueueStatusType.LoggedIn
                    :
                    QueueStatusType.LoggedOut;
                Console.WriteLine($"    Queue {qa.Queue.Number} - {cumulativeStatus} (Extension:{agent.QueueStatus}, ProfileAllows={ProfileAllowsLogin} and QueueAgent:{QueueAgentStatus})");
            }
        }
    }
}
