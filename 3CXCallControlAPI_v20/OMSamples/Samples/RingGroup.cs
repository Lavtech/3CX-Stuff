using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using TCX.Configuration;

namespace OMSamples.Samples
{
    [SampleCode("ringgroup")]
    [SampleAction("show", "[dn=<dn number>]")]
    [SampleAction("create", "dn=<dn number> name=<riggroup name> strategy=<RingGroup.StrategyType> ringtime=<in seconds> agents=<dn list> noanswer=<DestinationString> [prop.<NAME>=<value> ...]")]
    [SampleAction("update", "dn=<dn number> [name=<riggroup name>] [strategy=<RingGroup.StrategyType>] [agents=<dn list>] [noanswer=<DestinationString>] [prop.<NAME>=<value> ...]")]
    [SampleAction("delete", "dn=<dn number>")]
    [SampleDescription("Working with RingGroup.\n list_of_parameters is sequence of space separated strings (taken in quotes if required):\n" +
        "    name=<queue name> - name of the queue\n" +
        "    strategy=<RingGroup.StrategyType> - polling strategy as named in Queue.PollingStrategyType\n" +
        "    agents=<dnnumber>[,<dnnumber>] - new list of agents\n" +
        "    ringtime=<seconds> - ring timeout.\n" +
        "    noanswer=<DestinationType>.[<dnnumber>].[<externalnumber>] - timeout action - same as for options\n" +
        "    prop.<NAME>=<value> - set DN property with naem <NAME> to the <value>\n\n"+
        "    NOTE: RingGroup with Paging strategy can be configured to use multicast transport instead of making calls to each of members.\n"+
        "          To set/reset usage of multicast, set/reset following DN properties of the Paging ringgroup:\n"+
        "            MULTICASTADDR=<multicatIP>\n" +
        "            MULTICASTPORT=<multicastport>\n" +
        "            MULTICASTCODEC=<multicastcodec>\n" +
        "            MULTICASTPTIME=<codecptime>\n")
        ]
    class RingGroupSampel : ISample
    {
        public void Run(PhoneSystem ps, string action, Dictionary<string, string> args)
        {
            if(action=="")
            {
                this.ShowSampleInfo();
                return;
            }
            var subject = action=="create"?ps.GetTenant().CreateRingGroup(args["dn"]) : ps.GetDNByNumber(args["dn"]) as RingGroup;
            if(subject == null)
            {
                Console.WriteLine("RingGroup not found");
                return;
            }

            foreach(var arg in args)
            {
                switch(arg.Key)
                {
                    case "dn":
                        break;
                    case "name":
                        subject.Name = arg.Value;
                        break;
                    case "strategy":
                        subject.RingStrategy = Enum.Parse<StrategyType>(arg.Value);
                        break;
                    case "agents":
                        subject.Members = arg.Value.Split(',').Select(x => ps.GetDNByNumber(x) as Extension).Where(x => x != null).ToArray();
                        break;
                    case "timeout":
                        subject.RingTime = ushort.Parse(arg.Value);
                        break;
                    case "noanswer":
                        {
                            if(DestinationStruct.TryParse(arg.Value, out var noanswerdest))
                            {
                                subject.ForwardNoAnswer.CopyFrom(noanswerdest);
                            }
                            break;
                        }
                    default:
                        if(arg.Key.StartsWith("prop."))
                        {
                            subject.SetProperty(arg.Key.Substring(5), arg.Value);
                        }
                        break;
                }
            }
            subject?.Save();
            using (var ringgroups = subject==null?ps.GetAll<RingGroup>().ToArray().GetDisposer(): new RingGroup[] { subject }.GetDisposer())
            {
                var first = ringgroups.First(); //exeption is there are no such extension
                foreach (var rg in ringgroups)
                {
                    Console.WriteLine($"RingGroup - {rg.Number}:");
                    Console.WriteLine($"    name={rg.Name}");
                    Console.WriteLine($"    strategy={rg.RingStrategy}");
                    Console.WriteLine($"    agents={string.Join(",", rg.Members.Select(x => x.Number))}");
                    Console.WriteLine($"    ringtime={rg.RingTime}");
                    Console.WriteLine($"    noanswer={rg.ForwardNoAnswer.To}.{rg.ForwardNoAnswer.Internal?.Number ?? rg.ForwardNoAnswer.External}");
                    Console.WriteLine($"    DNProperties:");
                    foreach (var p in rg.GetProperties())
                    {
                        var name = p.Name;
                        var value = p.Value.Length > 50 ? new string(p.Value.Take(50).ToArray()) + "..." : p.Value;
                        Console.WriteLine($"        prop.{name}={value}");
                    }
                }
            }
        }
    }
}

