using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TCX.Configuration;
using static OMSamples.SampleStarter;

namespace OMSamples.Samples
{
    [SampleCode("calls")]
    [SampleWarning("")]
    [SampleDescription("Shows how to use CallMonitor")]
    [SampleAction("show", "dns=<dn list> calls={<callid>|all} printall")]
    [SampleAction("monitor", "dns=<dn list> calls={new|all|<callid>} printall")]
    class CallStateMonitor : ISample
    {
        HashSet<int> dnfilter = new HashSet<int>();
        bool PrintAllConnections = false;
        string PrintAll(OMCallCollector.CallStateSnapshot state, PhoneSystem ps)
        {
            var sb = new StringBuilder();
            if (state != null)
            {
                sb.AppendLine($"################# Call.{state.ID}");
                sb.AppendLine($"{state}");
                if (PrintAllConnections)
                {
                    //sb.AppendLine(string.Join(state.TalkTo.Select("")
                    if (state != null)
                    {
                        foreach (var c in ps.GetCallParticipants(state.ID))
                        {
                            if (dnfilter.Count == 0 || dnfilter.Contains(c.DN.ID))
                            {
                                sb.AppendLine($"    @AC.{c.ID}");
                                sb.AppendLine($"    {c}".Replace("\r\n", "\r\n    "));
                                sb.AppendLine($"    @ConnectionState");
                                sb.AppendLine($"        {state.GetConnectionState(c)}".Replace("\r\n", "\r\n        "));
                            }
                        }

                    }
                }
                sb.AppendLine($"-------------------");
            }
            return sb.ToString();
        }
        public void Run(PhoneSystem ps, string action, Dictionary<string, string> args)
        {
            if (args.TryGetValue("dns", out var dnlist))
                dnfilter = new HashSet<int>(dnlist.Split(',').Select(x => ps.GetDNByNumber(x)).Where(x => x != null).Select(x => x.ID));
            PrintAllConnections = args.ContainsKey("printall");
            var omcache = ps.CallStorage;
            {
                switch (action)
                {
                    case "":
                        this.ShowSampleInfo();
                        break;
                    case "show":
                        switch (args["calls"])
                        {
                            case "all":
                                {
                                    foreach (var c in omcache.AllCalls.Where(x=>x.TheState.Participants.Any(x=>dnfilter.Contains(x))))
                                        Console.WriteLine($"{c}");
                                }
                                break;
                            default:
                                Console.WriteLine($"{omcache.GetCall(int.Parse(args["calls"]))}");
                                break;
                        }
                        break;
                    case "monitor":
                        {
                            switch (args["calls"])
                            {
                                case "all": //all calls including existing
                                    omcache.Updated += (id, state) => Console.WriteLine($"Updated - {id} - {PrintAll(state, ps)}");
                                    omcache.Removed += (id, state) => Console.WriteLine($"Ended {id} - {PrintAll(state, ps)}");
                                    break;
                                case "new": //only new calls
                                    {
                                        var excludeIDs = new HashSet<uint>(ps.GetActiveConnectionsByCallID().Keys);
                                        omcache.Updated += (id, state) => { if (!excludeIDs.Contains((uint)id)) Console.WriteLine($"Updated - {id} - {PrintAll(state, ps)}"); };
                                        omcache.Removed += (id, state) => { if (!excludeIDs.Contains((uint)id)) Console.WriteLine($"Removed - {id} - {PrintAll(state, ps)}"); };
                                    }
                                    break;
                                default:
                                    {
                                        var idcall = int.Parse(args["calls"]);
                                        omcache.Updated += (id, state) => { if (id == idcall) Console.WriteLine($"Updated - {id} - {PrintAll(state, ps)}"); };
                                        //we end monitoring when call is finished
                                        omcache.Removed += (id, state) => { if (id == idcall) Console.WriteLine($"Removed - {id} - {PrintAll(state, ps)}"); };
                                    }
                                    break;
                            }
                            Console.ReadKey();
                            break;
                        }
                }
            }
        }
    }
}
