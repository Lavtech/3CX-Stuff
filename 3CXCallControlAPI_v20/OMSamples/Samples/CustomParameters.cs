using System;
using System.Collections.Generic;
using System.Linq;
using TCX.Configuration;
namespace OMSamples.Samples
{
    [SampleCode("parameters")]
    [SampleAction("show","contains=<partialname>")]
    [SampleAction("set", "<paramname>=<paramvalue> ...")]
    [SampleAction("delete", "paramname ...")]
    [SampleAction("notify", "<paramname>[=<paramvalue>] ... ")]
    [SampleDescription("Commands:\n" +
        "    show - show parameters which are contain specified string in name\n" +
        "    set - sets parameters\n" +
        "    delete - removes list of parameters\n" +
        "    notify - Notifies change of parameter and optinally updates it")]

    class CustomParameters : ISample
    {
        public void Run(PhoneSystem ps, string action, Dictionary<string, string> args)
        {
            if(action=="")
            {
                this.ShowSampleInfo();
                return;
            }
            switch (action)
            {
                case "set":
                    {
                        var validchars = "ABCDEFGHIGKLMNOPQRSTUVWXYZ_".ToHashSet();
                        var transaction = ps.CreateBatch("Update parameters");
                        foreach(var arg in args.Where(x=>x.Key.All(x => validchars.Contains(x))))
                        {
                            ps.SetParameter(arg.Key, arg.Value, transaction);    
                        }
                        transaction.Commit();
                    }
                    break;
                case "delete":
                    {
                        ps.DeleteParameters(args.Keys);
                    }
                    break;
                case "notify":
                    {
                        foreach(var arg in args)
                        {
                            ps.NotifyParameterUpdate(arg.Key, arg.Value);
                        }
                    }
                    break;
                case "show":
                    {
                        args.TryGetValue("constains", out var contains);
                        using (var paramset = ps.GetParameters().GetDisposer(x => contains == null || x.Name.Contains(contains)))
                        {
                            foreach (var p in paramset)
                                Console.WriteLine($"{p.Name}={p.Value} \n    DESCRIPTION:{new string(p.Description.Take(50).ToArray())}");
                        }
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unknown action {action}");
            }
        }
    }
}
