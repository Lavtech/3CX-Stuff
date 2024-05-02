using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCX.Configuration;

namespace OMSamples.Samples
{
    [SampleCode("queue")]
    [SampleAction("show", "[dn=<queue_number>]")]
    [SampleAction("create", "dn=<queue_number> param=value ...")]
    [SampleAction("update", "dn=<queue_number> [param=value ...]")]
    [SampleAction("delete", "dn=<queue_number>")]
    [SampleDescription("Working with Queues.\n parameters is sequence of space separated strings (taken in quotes if required):\n" +
        "    name=<queue name> - name of the queue\n" +
        "    pstrategy=<Queue.PollingStrategyType> - polling strategy as named in Queue.PollingStrategyType\n" +
        "    pollingtime=<seconds> - ringing time for polling callss\n" +
        "    intro=filename - intro prompt of the queue - the file which is loacted in the directory specified by IVRPROMPTPATH parameter.\n" +
        "    moh=filename - Music On Hold for calls which are waiting in the queue\n" +
        "    agents=<dnnumber>[,<dnnumber>] - list of queue agents\n" +
        "    managers=<dnnumber>[,<dnnumber>] - list of queue managers\n" +
        "    maxwait=<seconds> - maximal time of waiting in the queue.\n" +
        "    noanswer=<DestinationType>.[<dnnumber>].[<externalnumber>] - timeout action - same as for options\n" +
        "    login=<dnnumber>[,<dnnumber>] - Agents to login into the queue\n" +
        "    logout=<dnnumber>[,<dnnumber>] - Agents to logout from the queue\n" +
        "    prop.<NAME>=<value> - set DN property with naem <NAME> to the <value>")
        ]
    class QueueSample : ISample
    {
        void ExtractModifications(PhoneSystem ps, string thelist, out Dictionary<int, Extension> to_add, out Dictionary<int, Extension> to_remove)
        {
            var set = thelist.Split(',');
            to_add = set.Where(x => x.StartsWith("+")).Select(x => x.TrimStart('+')).SelectMany(
                x =>
                {
                    var thedn = ps.GetDNByNumber(x) as Extension;
                    if (thedn != null)
                        return new[] { thedn };
                    if (x.StartsWith("*"))
                    {
                        var endswith = x.TrimStart('*');
                        return ps.GetAll<Extension>().Where(extension => extension.Number.EndsWith(endswith));
                    }
                    else if (x.EndsWith("*"))
                    {
                        var startswith = x.TrimEnd('*');
                        return ps.GetAll<Extension>().Where(extension => extension.Number.StartsWith(startswith));
                    }
                    return new Extension[0];
                }).ToDictionary(x => x.ID);
            to_remove = set.Where(x => x.StartsWith("-")).Select(x => x.TrimStart('-')).SelectMany(
                x =>
                {
                    var thedn = ps.GetDNByNumber(x) as Extension;
                    if (thedn != null)
                        return new[] { thedn };
                    if (x.StartsWith("*"))
                    {
                        var endswith = x.TrimStart('*');
                        return ps.GetAll<Extension>().Where(extension => extension.Number.EndsWith(endswith));
                    }
                    else if (x.EndsWith("*"))
                    {
                        var startswith = x.TrimEnd('*');
                        return ps.GetAll<Extension>().Where(extension => extension.Number.StartsWith(startswith));
                    }
                    return new Extension[0];
                }).ToDictionary(x => x.ID);
            var ambigousActionsOn = to_remove.Keys.Intersect(to_add.Keys).ToHashSet();
            if (ambigousActionsOn.Any())
            {
                throw new Exception($"Ambiguous add/remove action for {{ {string.Join(",", ambigousActionsOn.Select(x=>ps.GetByID<DN>(x)?.Number))} }}");
            }
        }

        public void Run(PhoneSystem ps, string action, Dictionary<string, string> param_set)
        {
            if (action == "")
            {
                this.ShowSampleInfo();
                return;
            }
            var queue = action == "create" ? ps.GetTenant().CreateQueue(param_set["dn"]) : ps.GetDNByNumber(param_set["dn"]) as Queue;
            if (queue == null)
            {
                Console.WriteLine($"Quueue {param_set["dn"]} not found");
                return;
            }
            var sw = Stopwatch.StartNew();
            var elapsed = sw.Elapsed.TotalMilliseconds;
            switch (action)
            {
                case "create":
                case "update":
                    {
                        bool created = action == "create";
                        Console.WriteLine($"take queue object data {sw.Elapsed.TotalMilliseconds}ms");

                        foreach (var param in param_set)
                        {
                            switch (param.Key)
                            {
                                case "name":
                                    queue.Name = param.Value;
                                    break;
                                case "pstrategy":
                                    {
                                        PollingStrategyType strategyType;
                                        if (Enum.TryParse(param.Value, out strategyType))
                                        {
                                            queue.PollingStrategy = strategyType;
                                        }
                                        else
                                            throw new InvalidCastException("Undefined Polling strategy type");
                                    }
                                    break;
                                case "pollingtime":
                                    {
                                        queue.RingTimeout = ushort.Parse(param.Value);
                                    }
                                    break;
                                case "intro":
                                    queue.IntroFile = param.Value;
                                    break;
                                case "moh":
                                    queue.OnHoldFile = param.Value;
                                    break;
                                case "agents":
                                case "managers":
                                    {
                                        ExtractModifications(ps, param.Value, out var to_add, out var to_remove);
                                        if (param.Key == "agents")
                                        {
                                            var hashset = new HashSet<int>(queue.QueueAgents.Select(x => x.DNRef.ID));
                                            //as far as array of agents is sorted by priority we cannot use "bulk" update in case of "insert"
                                            //so we need to reassign agent array
                                            //queue.AttachOnSave(to_add.Where(x => !hashset.Contains(x.Key)).Select(x => queue.CreateAgent(x.Value)));
                                            //queue.DeleteOnSave(queueAgents.Where(x => hashset.Contains(x.DNRef.ID) && to_remove.ContainsKey(x.DNRef.ID)));
                                            queue.QueueAgents =
                                                queue.QueueAgents.Where(x => hashset.Contains(x.DNRef.ID) && !to_remove.ContainsKey(x.DNRef.ID))
                                                .Concat(to_add.Where(x => !hashset.Contains(x.Key)).Select(x => queue.CreateAgent(x.Value)))
                                                .ToArray();
                                        }
                                        else
                                        {
                                            //managers can be updated using "bulk" transaction.
                                            var hashset = new HashSet<int>(queue.QueueManagers.Select(x => x.DNRef.ID));
                                            queue.QueueManagers =
                                                queue.QueueManagers.Where(x => hashset.Contains(x.DNRef.ID) && !to_remove.ContainsKey(x.DNRef.ID))
                                                .Concat(to_add.Where(x => !hashset.Contains(x.Key)).Select(x => queue.CreateManager(x.Value)))
                                                .ToArray();
                                        }
                                    }
                                    break;
                                case "login":
                                case "logout":
                                    {
                                        var expectedStatus = param.Key == "login" ? QueueStatusType.LoggedIn : QueueStatusType.LoggedOut;
                                        var hs = new HashSet<string>(param.Value.Split(','));
                                        foreach (var agent in queue.QueueAgents)
                                        {
                                            if(hs.Contains(agent.DNRef.Number) && agent.QueueStatus != expectedStatus)
                                                agent.QueueStatus = expectedStatus;
                                        }                                        
                                    }
                                    break;
                                case "maxwait":
                                    queue.MasterTimeout = ushort.Parse(param.Value);
                                    break;
                                case "noanswer":
                                    {
                                        var data = param.Value.Split('.');
                                        if(DestinationStruct.TryParse(param.Value, out var dest))
                                        {
                                           dest.CopyTo(queue.ForwardNoAnswer);
                                        }
                                        else
                                            throw new InvalidCastException("Unknown NoAnswer destination type");
                                    }
                                    break;
                                default:
                                    {
                                        if (param.Key.StartsWith("prop."))
                                        {
                                            queue.SetProperty(param.Key.Substring(5), param.Value);
                                        }
                                        else
                                            throw new InvalidOperationException($"Unknown parameter {param.Key}={param.Value}");
                                        break;
                                    }
                            }
                        }
                        Console.WriteLine($"Update queue object {sw.Elapsed.TotalMilliseconds - elapsed}ms");
                        elapsed = sw.Elapsed.TotalMilliseconds;
                        queue.Save();
                        Console.WriteLine($"Saving queue object {sw.Elapsed.TotalMilliseconds - elapsed}ms");
                        elapsed = sw.Elapsed.TotalMilliseconds;
                    }
                    break;
                case "delete":
                    {
                        queue.Delete();
                        Console.WriteLine($"Deleted Queue {queue}");
                        return;
                    }
                case "show":
                    //simply display results
                    break;
                default:
                    throw new ArgumentException("Invalid action name");
            }
            //show result
            {
                using (var queues = (queue == null ? ps.GetAll<Queue>().ToArray() : [queue]).GetDisposer())
                {
                    var first = queues.First(); //exeption is there are no such extension
                    foreach (var q in queues)
                    {
                        Console.WriteLine($"Queue - {q.Number}:");
                        Console.WriteLine($"    name={q.Name}");
                        Console.WriteLine($"    pstrategy={q.PollingStrategy}");
                        Console.WriteLine($"    pollingtime={q.RingTimeout}");
                        Console.WriteLine($"    intro={q.IntroFile}");
                        Console.WriteLine($"    moh={q.OnHoldFile}");
                        Console.WriteLine($"    agents={string.Join(",", q.QueueAgents.Select(x => x.DNRef.Number))}");
                        Console.WriteLine($"    managers={string.Join(",", q.QueueManagers.Select(x => x.DNRef.Number))}");
                        Console.WriteLine($"    maxwait={q.MasterTimeout}");
                        Console.WriteLine($"    noanswer={q.ForwardNoAnswer}");
                        var loggedin = string.Join(",",
                            q.QueueAgents.Where(x => x.QueueStatus == QueueStatusType.LoggedIn && (x.DN as Extension)?.QueueStatus == QueueStatusType.LoggedIn).Select(x => x.DNRef.Number));
                        var loggedout = string.Join(",",
                            q.QueueAgents.Where(x => x.QueueStatus == QueueStatusType.LoggedOut || (x.DN as Extension)?.QueueStatus == QueueStatusType.LoggedOut).Select(x => x.DNRef.Number));
                        Console.WriteLine($"    login={loggedin}");
                        Console.WriteLine($"    logout={loggedout}");
                        Console.WriteLine($"    DNProperties:");
                        foreach (var p in q.GetProperties())
                        {
                            var name = p.Name;
                            var value = p.Value.Length > 50 ? new string(p.Value.Take(50).ToArray()) + "..." : p.Value;
                            Console.WriteLine($"        prop.{name}={value}");
                        }
                    }
                }
                sw.Stop();
                Console.WriteLine($"Printing results {sw.Elapsed.TotalMilliseconds - elapsed}ms");
                elapsed = sw.Elapsed.TotalMilliseconds;
            }
        }
    }
}
