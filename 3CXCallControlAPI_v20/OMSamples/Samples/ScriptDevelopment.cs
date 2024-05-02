using System;
using System.Linq;
using TCX.Configuration;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using TCX.PBXAPI;
using System.Text.RegularExpressions;
using System.Data.Common;

namespace OMSamples.Samples
{
    /// <summary>
    /// This sample may require adjustments for concrete version of PBX.
    /// it demonstrates manipulations which are available for RoutPoint object
    /// there is special form of ServiceCallAsync for RoutePoint object which
    /// allows to trigger call to some number.
    /// The action itself cannot be monitored
    /// Script on RoutePoint is always start RoutePoint script.
    /// The script itslf should distingush the type of call.
    /// </summary>
    [SampleCode("scriptdev")]
    [SampleAction("online", "online \"folder=<script subfolder>\"")]
    [SampleAction("delete", "rp=<list of route point dn numbers>")]
    [SampleAction("call", "rp=<dn number of route point> from=<extension/device>|to=<phonenumber>")]
    [SampleAction("check", "rp=<list of dn numbers>")]
    [SampleAction("showfailed", "")]
    [SampleAction("deployall", "\"folder=<folder which contains script subfolders (each with .cs files)>\"")]
    [SampleWarning("Some actions of this sample may override existing configuration. DON'T use it on production environment")]
    class ScriptDevelopment : ISample
    {
        bool UpdateOrCreate(PhoneSystem ps, string CallFlowName, string script)
        {
            try
            {
                var rp = ps.GetDNByNumber(CallFlowName) as RoutePoint ?? ps.GetTenant().CreateRoutePoint(CallFlowName, script);
                if (rp.ID != 0)
                {
                    if (rp.GetPropertyValue("OMSAMPLES_SCRIPT") != "1")
                    {
                        Console.WriteLine($"CallFlow route point {CallFlowName} is not configured for test purposes (dn property OMSAMPLES_SCRIPT is not set to 1)");
                        return false;
                    }
                    //this part is just "make modification to enforce recompilation"
                    if (rp.ScriptCode == script && rp.GetPropertyValue("COMPILATION_SUCCEEDED") != "1")
                    {
                        if (script.EndsWith(" "))
                            script = script.Substring(0, script.Length - 1);
                        else
                            script += " ";
                    }
                    rp.ScriptCode = script;
                }
                else
                    rp.SetProperty("OMSAMPLES_SCRIPT", "1");
                if (rp.GetPropertyValue("AUTOANSWER") != "0")
                    rp.SetProperty("AUTOANSWER", "0");
                rp.Save();
                Console.WriteLine($"{CallFlowName} has been updated");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{CallFlowName} update failed\n{ex}");
            }
            return false;
        }
        readonly Regex regExprFolder = new("^#?[%a-zA-Z0-9]*$");
        const string FOLDER = "folder";
        const string RP = "rp";
        const string FROM = "from";
        const string TO = "to";
        public void Run(PhoneSystem ps, string action, Dictionary<string, string> parameters)
        {
            var result = Task.Run(async () =>
            {
                switch (action)
                {
                    //tries to create callflow routing points according to folder content.
                    //runs FileSystem watcher which synchronize filesystem content with 3CX configuration
                    //runs 3CX Configuration listener which provides reports each time when CallFlow route points created by this procedure
                    //will be updated
                    //procedure is tracking only one folder with single ".cs" file. (all other files in folder are ignored)
                    //the folder name defines DN.Number of the RP.
                    //virtually, DN.Number of RP can be any "token" of following structure /^[\#]?[A-Za-z0-9\*]+$/
                    //if phones don't allow to enter alpha characters then- /^[\#]?[0-9\*]+$/ is preferred
                    //if phones don't allow to enter # - /^[0-9\*]+$/
                    //and most supported is /^[0-9]+$/
                    //example:
                    //C:/Scripts/%101
                    //             |.cs
                    //OMSamplesCore scriptdev online "folder=C:/Scripts/%101"
                    //will created route point with *101 with script from .cs file located in the specified folder
                    //will show compilation result (if any)
                    //the routepoint can be caller to check the result
                    case "online":
                        {
                            if (regExprFolder.IsMatch(parameters[FOLDER]))
                            {
                                var CallFlowName = Path.GetFileName(parameters[FOLDER]).Replace('%', '*');
                                var filename = Path.Combine(parameters[FOLDER], ".cs");
                                Console.WriteLine($"Deploying {CallFlowName} from {filename}");
                                UpdateOrCreate(ps, CallFlowName, File.ReadAllText(filename));
                                var RoutePointTrack = new PsTypeEventListener<RoutePoint>("DN");
                                RoutePointTrack.SetTypeHandler(x => ShowState(x), x => ShowState(x), (x, y) => Deleted(x, y), x => x.Number == CallFlowName);

                                var lastWriteTime = File.GetLastWriteTime(filename);
                                while (!Console.KeyAvailable) {
                                    await Task.Delay(5000).ContinueWith(x => File.GetLastWriteTime(filename) > lastWriteTime).ContinueWith(
                                        x =>
                                        {
                                            if (x.Result)
                                            {
                                                lastWriteTime = File.GetLastWriteTime(filename);
                                                UpdateOrCreate(ps, CallFlowName, File.ReadAllText(filename));
                                            }
                                        }
                                        , TaskContinuationOptions.OnlyOnRanToCompletion);
                                }
                                while (Console.KeyAvailable)
                                    Console.ReadKey();
                            }
                        }
                        break;
                    //initating test call. see CallControlAPI
                    case "call":
                        {
                            if (ps.GetDNByNumber(parameters[RP]) is RoutePoint rp)
                            {
                                CallControlResult theresult = null;
                                if (parameters.TryGetValue(TO, out var destination_number))
                                {
                                    //init outgoing call from routepoint
                                    theresult = await rp.ServiceCallAsync(new TCX.Configuration.Interop.RPCParameters { { "method", "initiate_call" }, { "destination", parameters[TO] } });
                                }
                                else if (parameters.TryGetValue(FROM, out var source_of_call))
                                {
                                    var source = ps.GetDNByNumber(source_of_call);
                                    theresult = await source.GetRegistrarContactsEx().First().MakeCallAsync(rp.Number);
                                }
                                else
                                {
                                    throw new Exception("Malformed 'call' request");
                                }
                            }
                        }
                        break;
                    //just prints full state of the requested CallFlow scripts deployed on server.
                    case "check":
                        {
                            var selected = parameters.TryGetValue(RP, out var rps_dns) ?
                            rps_dns.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet() : [];
                            using (var rps = ps.GetRoutePoints().Extract(x => !selected.Any() || selected.Contains(x.Number))
                                .ToArray().GetDisposer())
                            {
                                foreach (var rp in rps)
                                {
                                    ShowState(rp);
                                }
                            }
                        }
                        break;
                    //allows to delete CallFlow script which was deployed using this OM sample
                    case "delete":
                        {
                            using (var rp = ps.GetDNByNumber(parameters[RP]) as RoutePoint)
                            {
                                if (rp == null)
                                    Console.WriteLine($"RoutePoint {parameters[RP]} does not exist");
                                else if (rp.GetPropertyValue("OMSAMPLES_SCRIPT") != "1")
                                    Console.WriteLine($"RoutePoint {parameters[RP]} is not marked for test deployment script (TEST_DEPLOYMENT!=1)");
                                else
                                {
                                    Console.Write($"Are you sure want to delete {parameters[RP]}(y/N)?");
                                    if (Console.ReadLine().ToUpperInvariant() == "Y")
                                        rp.Delete();
                                    else
                                    {
                                        Console.WriteLine($"Cancelled");
                                    }
                                }
                            }
                        }
                        break;
                    //Floder with scripts should have following structure
                    //C:/Scripts
                    //         |
                    //         +%101
                    //         |.cs
                    //         +#2
                    //         |.cs
                    //         ....
                    //OMSamplesCore scriptdev online "folder=C:/Scripts"
                    //will update or create *101 and #2 taken from .cs file in the folder
                    case "deployall":

                        foreach (var a in Directory.EnumerateDirectories(parameters[FOLDER]).Where(x => regExprFolder.IsMatch(Path.GetFileName(x)) && File.Exists(Path.Combine(x, ".cs"))))
                        {
                            var CallFlowName = Path.GetFileName(a).Replace('%', '*');
                            var filename = Path.Combine(a, ".cs");
                            Console.WriteLine($"Updating {CallFlowName} from {filename}");
                            UpdateOrCreate(ps, CallFlowName, File.ReadAllText(filename));
                        }
                        break;
                    //shows all problematic CallFlow scripts where last try to update/compile script was not successful.
                    case "showfailed":
                        {
                            var selected = parameters.TryGetValue(RP, out var rps_dns) ?
                            rps_dns.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet() : new();
                            using (var rps = ps.GetRoutePoints().Extract(x => !selected.Any() || selected.Contains(x.Number))
                                .Where(x => x.GetPropertyValue("INVALID_SCRIPT") == "1").ToArray().GetDisposer())
                            {
                                foreach (var rp in rps)
                                {
                                    ShowState(rp);
                                }
                            }
                        }
                        break;
                    default:
                        throw new InvalidOperationException($"{action} - action is not defined");
                }
                return true;
            }).Result;
        }

        private void Deleted(RoutePoint x, int id)
        {
            Console.WriteLine($"Deleted {id}");
        }

        struct ColoredSpan
        {
            public ConsoleColor color;
            public KeyValuePair<int, int> location;
        }
        void ShowCompilationResult(string code, string Report)
        {
            var foreground = Console.ForegroundColor;
            try
            {
                var allspans = Report.Split('\n').Where(x => x.StartsWith(":["))
                    .Select(x => x.Split(':', StringSplitOptions.RemoveEmptyEntries))
                    .Select(x => x[0].Last() == ')' ? (x[0] + x[2].Trim().First()) : x[0]) //add 'E' if not specified
                    .Select(
                        x =>
                            new string(x.Skip(1).Take(x.Length - 3).ToArray()).Split('.', StringSplitOptions.RemoveEmptyEntries)
                            .Append(new string(x.Last(), 1)).ToArray())
                    .Select(x => new ColoredSpan()
                    {
                        location = KeyValuePair.Create(int.Parse(x[0]), int.Parse(x[1])),
                        color = x[2] == "H" ? ConsoleColor.DarkGray : x[2] == "W" ? ConsoleColor.Yellow : x[2] == "E" ? ConsoleColor.Red : foreground
                    }).Append(new ColoredSpan() { color = foreground, location = KeyValuePair.Create(code.Length + 1, code.Length + 1) }).OrderBy(x => x.location.Key).ThenBy(x => x.location.Value).ToArray();
                int currentlocation = 1;
                foreach (var span in allspans)
                {
                    if (currentlocation < span.location.Key)
                    {
                        Console.ForegroundColor = foreground;
                        Console.Write(new string(code.Skip(currentlocation - 1).Take(span.location.Key - currentlocation).ToArray()));
                        currentlocation += span.location.Key - currentlocation;
                    }
                    if (currentlocation == span.location.Key)
                    {
                        Console.ForegroundColor = span.color;
                        Console.Write(new string(code.Skip(currentlocation - 1).Take(span.location.Value - span.location.Key).ToArray()));
                        currentlocation += span.location.Value - span.location.Key;
                    }
                    if (currentlocation < span.location.Value)
                    {
                        Console.ForegroundColor = span.color;
                        Console.Write(new string(code.Skip(currentlocation - 1).Take(span.location.Value - currentlocation).ToArray()));
                        currentlocation += span.location.Value - currentlocation;
                    }
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Malformed Report {Report}");
            }
            finally
            {
                Console.ForegroundColor = foreground;
            }
        }
        private void ShowState(RoutePoint rp)
        {
            Console.WriteLine($"------ Report for {rp}--------");
            var foreground = Console.ForegroundColor;
            try
            {
                var rejected_code = rp.GetPropertyValue("REJECTED_CODE");
                var compilation_result = rp.GetPropertyValue("COMPILATION_RESULT");
                bool compilation_succeeded = rp.GetPropertyValue("COMPILATION_SUCCEEDED") == "1";
                bool invalid_script = rp.GetPropertyValue("INVALID_SCRIPT") == "1";
                if (compilation_succeeded)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Code successfully applied");
                }
                else
                {
                    if (invalid_script)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Route point code compilation failed.");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("RoutePoint works with old code. Last try tp update code has been rejected:");
                    }
                }
                Console.ForegroundColor = foreground;
                Console.WriteLine($"\nCompilation result:\n{compilation_result}");
                ShowCompilationResult(compilation_succeeded ? rp.ScriptCode : rejected_code, compilation_result);
                Console.WriteLine($"------ End of Report for {rp}--------");
            }
            finally
            {
                Console.ForegroundColor = foreground;
            }
        }
    }
}
