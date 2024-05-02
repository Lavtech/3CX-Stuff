using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using TCX.Configuration;

namespace OMSamples.Samples
{
    [SampleCode("ivr")]
    [SampleAction("show", "[dn=dnnumber]")]
    [SampleAction("create", "dn=dnnumber param=value ...")]
    [SampleAction("update", "dn=dnnumber [param=value ...]")]
    [SampleAction("delete", "dn=dnnumber")]
    [SampleDescription("Working with IVR.\n list_of_parameters is sequence of space separated strings (taken in quotes if required):\n"+
        "    prompt=filename|ext<extnumber> - file which is placed in directory specified by IVRPROMPTPATH parameter or extnumber where from to record new fine with random name\n" +
        "    o<digit>=<IVRForwardType>.[<dnnumber>] - assign specific type of destination to option <digit>. <IVRForward> is from enum IVRForwardType <dnnumber> - local number must be proper for specific number\n"+
        "    timeout=<seconds> - number of seconds\n"+
        "    timeout_dest=<IVRForwardType>.[<dnnumber>] - timeout action - same as for options\n"+
        "    name=<ivr name> - name of ivr"+
        "    prop.<NAME>=<value> - set DN property with naem <NAME> to the <value>"
        )
        ]
    class IVRSample : ISample
    {

        public void Run(PhoneSystem ps, string action, Dictionary<string, string> param_set)
        {
            switch (action)
            {
                case "create":
                case "update":
                    {
                        bool isNew = action == "create";
                        var ivr = isNew ? ps.GetTenant().CreateIVR(param_set["dn"]) : (ps.GetDNByNumber(param_set["dn"]) as IVR);
                        if (isNew)
                            ivr.SetProperty("TEST_DEPLOYMENT", "1");
                        if(ivr.GetPropertyValue("TEST_DEPLOYMENT")!="1")
                        {
                            throw new Exception($"{param_set["dn"]} is not a test IVR");
                        }
                        bool assignForward = isNew; //flag which trigger assignmnet of IVRForward collection
                        IEnumerable<IVRForward> ivrForwards = ivr.Forwards;
                        foreach (var paramdata in param_set)
                        {
                            
                            var paramname = paramdata.Key;
                            var paramvalue = paramdata.Value;
                            switch (paramname)
                            {
                                case "prompt":
                                    if (paramvalue.StartsWith("ext"))
                                    {
                                        //record the prompt from the requested extension
                                        ivr.PromptFilename = "OMSamplesTestRecodrdedPromptForIVR" + ivr.Number + ".wav";
                                        var filename = Path.Combine(ps.GetParameterValue("IVRPROMPTPATH"), ivr.PromptFilename);
                                        ivr.PromptFilename = filename;
                                        if (File.Exists(filename))
                                        {
                                            File.Move(filename, filename + ".back");
                                        }
                                        using (var ext = ps.GetDNByNumber(paramvalue.Substring(3)) as Extension)
                                        using (var ev = new AutoResetEvent(false))
                                        using (var listener = new PsTypeEventListener<ActiveConnection>())
                                        {
                                            ActiveConnection activeConnection = null;
                                            listener.SetTypeHandler(
                                                (x) => activeConnection = x, 
                                                (x) => activeConnection = x, 
                                                (x, y) => ev.Set(), 
                                                (x) => x == activeConnection || (activeConnection==null&&x.DN == ext && x.Status == ConnectionStatus.Connected && x.ExternalParty=="RecordFile"), 
                                                (x) => ev.WaitOne(x));
                                            ps.ServiceCall("RecordFile",
                                            new Dictionary<string, string>()
                                            {
                                                { "filename", filename},
                                                { "extension", ext.Number }
                                            });
                                            listener.Wait(60000);//wait a minute until recording call is finished.
                                        }
                                        File.Delete(filename + ".back");
                                        if(!File.Exists(filename))
                                        {
                                            throw new FileNotFoundException($"{filename} is not recorded");
                                        }
                                    }
                                    else
                                        ivr.PromptFilename = paramvalue;
                                    break;
                                case "timeout":
                                    ivr.Timeout = ushort.Parse(paramvalue);
                                    break;
                                case "name":
                                    ivr.Name = paramvalue;
                                    break;
                                default: //props, options and TODEST
                                    {
                                        if (paramname.StartsWith("prop."))
                                        {
                                            ivr.SetProperty(paramname.Substring(5), paramvalue);
                                            break;
                                        }
                                        var data = paramvalue.Split('.');
                                        var number = data[1]; //must be with . at the end
                                        IVRForwardType fwdtype;
                                        var todelete = !Enum.TryParse(data[0], out fwdtype);
                                        if ("timeout_dest" == paramname || (paramname.Length == 2 && paramname[0] == 'o' && paramname.Length == 2 && char.IsDigit(paramname, 1)))
                                        {
                                            var dn = ps.GetDNByNumber(number);
                                            if ("timeout_dest" == paramname)
                                            {
                                                ivr.TimeoutForwardDN = todelete ? null : dn;
                                                ivr.TimeoutForwardType = todelete ? IVRForwardType.EndCall : fwdtype;
                                            }
                                            else
                                            {
                                                var option = (byte)(paramname[1] - '0');
                                                var fwd = ivrForwards.FirstOrDefault(x => x.Number == option);
                                                if (fwd == null)
                                                {
                                                    if (todelete)
                                                        break;
                                                    fwd = ivr.CreateIVRForward();
                                                    ivrForwards = ivrForwards.Union(new IVRForward[] { fwd });
                                                }
                                                if (!todelete)
                                                {
                                                    fwd.Number = option;
                                                    fwd.ForwardDN = dn;
                                                    fwd.ForwardType = fwdtype;
                                                }
                                                else
                                                {
                                                    ivrForwards = ivrForwards.Where(x => x != fwd);
                                                }
                                                assignForward = true;
                                            }
                                        }
                                        else
                                        {
                                            throw new InvalidOperationException($"Unknown patameter{paramname}={paramvalue}");
                                        }
                                    }
                                    break;
                            }

                        }
                        if (assignForward)
                        {
                            ivr.Forwards = ivrForwards.ToArray();
                        }
                        ivr.Save();
                    }
                    break;
                case "delete":
                    {
                        var ivr = ps.GetDNByNumber(param_set["dn"]) as IVR;
                        if (ivr.GetPropertyValue("TEST_DEPLAYMENT") == "1")
                        {
                            ivr.Delete();
                            Console.WriteLine($"Deleted IVR {param_set["dn"]}");
                        }
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
                using (var ivrs = (param_set.ContainsKey("dn") ? [ps.GetDNByNumber(param_set["dn"]) as IVR] : ps.GetAll<IVR>().ToArray()).GetDisposer())
                {
                    var first = ivrs.First(); //exeption if there are no such extension
                    foreach (var ivr in ivrs)
                    {
                        Console.WriteLine($"IVR - {ivr.Number}:");
                        Console.WriteLine($"    name={ivr.Name}");
                        Console.WriteLine($"    prompt={ivr.PromptFilename}");
                        Console.WriteLine($"    timeout={ivr.Timeout}");
                        Console.WriteLine($"    timeout_dest={ivr.TimeoutForwardType}.{ivr.TimeoutForwardDN?.Number}");
                        foreach (var f in ivr.Forwards.OrderBy(x => x.Number))
                        {
                            Console.WriteLine($"        o{f.Number}={f.ForwardType}.{f.ForwardDN?.Number}");
                        }
                        Console.WriteLine("    DNProperties:");
                        foreach (var p in ivr.GetProperties())
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
}
