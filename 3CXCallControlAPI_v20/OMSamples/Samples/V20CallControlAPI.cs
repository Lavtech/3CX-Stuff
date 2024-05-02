using System;
using System.Linq;
using TCX.Configuration;
using static TCX.PBXAPI.CallControlAPI;
using TCX.PBXAPI;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using TCX.Configuration.Interop;

namespace OMSamples.Samples
{
    [SampleCode("callcontrol")]
    [SampleAction("info", "[dn=<dn_number>]|[sipdevice=<sipurl>]")]
    [SampleAction("makecall", "[dn=<dn_number>]|[sipdevice=<sipurl>] destination=<number or destination_string>")]
    [SampleAction("answer", "[dn=<dn_number>]|[sipdevice=<sipurl>]")]
    [SampleAction("pickup", "[dn=<dn_number>]|[sipdevice=<sipurl>] dnfrom=<ringing dn>")]
    [SampleAction("divert", "[dn=<dn_number>]|[sipdevice=<sipurl>] destination=<number or destination_string>")]
    [SampleAction("bxfer", "[dn=<dn_number>]|[sipdevice=<sipurl>] [destination=<number or destination_string>]")]
    [SampleAction("routeto", "[dn=<dn_number>]|[sipdevice=<sipurl>] destination=<number or destination_string> [timeout=<in seconts>]")]
    [SampleAction("drop", "[dn=<dn_number>]|[sipdevice=<sipurl>]")]
    [SampleAction("attachcallerdata", "[dn=<dn_number>]|[sipdevice=<sipurl>] [name=value...]")]
    [SampleAction("attachdata", "[dn=<dn_number>]|[sipdevice=<sipurl>] [name=value...]")]
    [SampleDescription("demonstrates CORE LEVEL Call Cantrol API available in v20. See comments in the code for further explanations")]
    public class CallControl : ISample
    {

        //EXAMPLES:

        //CommandLine:
        //OMSamplesCore makecall dn=100 destination=200
        //
        //the request will be redirected to MakeCall helper.
        //such request is always reported as successful.

        //CommandLine:
        //OMSamplesCore callcontrol makecall sipdevice=sip:100@1.1.1.1:5070 destination=200
        //
        //initiate call from the specific device. This request is reported as successful when the device is
        //started ringing (emulation) or the device has initiated call.
        //CallControl result reports corresponding source connection which was initiated.
        //please pay attention that the connection may be dropped at any time

        //CommandLine:
        //OMSamplesCore callcontrol pickup dn=100 dnfrom=102
        //
        //if 102 has a ringing call (and 100 is allowed to pickup calls from 102)
        //the all registered devices of externaion 100 will ring.
        //add sipdevice=<device contact url> to specify exact device which should puckup the call

        //command line parameters
        //makecall - source of call
        //pickup - redirect to
        //divert - currently ringing
        //join - calls are connected with the device
        //routeto - replace connection currently connected with
        //attachdata - attach data to the connection of this DN
        //servicecall - name of service DN
        //MANDATORY
        const string P_DN = "dn";

        //OPTIONAL
        //if DN has only one registered device, the device is assumed as specified for the action
        //if DN has multiple registered device, the action will run on "DN" if device is not explicitly specified
        const string P_DEVICE = "sipdevice";

        //makecall - destination of the call
        //divert - the new destination
        //routeto - destination of reroute
        const string P_DESTINATION = "destination";

        const string P_DESTDEVICE = "destdevice";

        //pickup - ringing dn
        const string P_DNFROM = "dnfrom";
        //specifies timeout of operation (if applicable)
        const string P_TIMEOUT = "timeout";

        public void Run(PhoneSystem ps, string action, Dictionary<string, string> parameters)
        {
            if (action == "")
            {
                this.ShowSampleInfo();
                return;
            }
            var result = Task.Run(async () =>
            {
                var timeout = int.Parse(parameters.TryGetValue(P_TIMEOUT, out var timeoutstr)?timeoutstr : "180");
                var the_dn = parameters.TryGetValue(P_DN, out var dn) ? (ps.GetDNByNumber(dn) ?? throw new Exception("DN Not Found")) : null;
                //is set if dn is explicitrly specified
                var allDevices = the_dn?.GetRegistrarContactsEx();
                //if dn is specified, the sipdavice should be one of that DN
                var the_device = parameters.TryGetValue(P_DEVICE, out var contact) ?
                    (allDevices??ps.GetAll<RegistrarRecord>()).FirstOrDefault(x => x.Contact == contact)??throw new Exception("Device Not Found") :
                    allDevices.Length==0? allDevices[0] : null;
                the_dn ??= the_device?.DN; //if dn is not specified, it will be taken from device definition
                if(the_dn==null)
                {
                    throw new Exception("dn number or sipdevice url is not specified");
                }
                ActiveConnection[] connections = null;
                if (the_device != null)
                {
                    //work with concrete device
                    connections = the_dn.GetActiveConnections().Extract(x => the_device.Contact == x["devcontact"]).ToArray();
                }
                else
                {
                    //work with any device
                    connections = the_dn.GetActiveConnections();
                }
                var the_dn_from = parameters.TryGetValue(P_DNFROM, out var dn_from) ? ps.GetDNByNumber(dn_from) : null;
                var destination_device = parameters.TryGetValue(P_DESTDEVICE, out var destdevicecontact) ?
                    (ps.GetAll<RegistrarRecord>().ExtractFirstOrDefault(x => x.Contact == destdevicecontact) ?? throw new Exception("Destination device is not found")) : null;

                DestinationStruct structured_destination = new DestinationStruct();
                var use_structured_destination =
                    parameters.TryGetValue(P_DESTINATION, out var destination_string)
                    &&
                    DestinationStruct.TryParse(destination_string, out structured_destination);
                var infostring =
                      $"\nDN={the_dn.Number}-{the_dn}\n-----DEVICE={string.Join("\n-----DEVICE=",
                            the_dn.GetRegistrarContactsEx().Select(
                                x => $"{x.Contact}\n{string.Join("\n", the_dn.GetActiveConnections().Extract(y => y["devcontact"] == x.Contact))}"
                            ))}";
                try
                {
                    var callcontrol_result = action switch
                    {
                        "info" =>
                            await Task.Run(() =>
                            Console.WriteLine(infostring))
                            .ContinueWith(_ => (CallControlResult)null),
                        //request RoutePoint to initiate the call
                        "servicecall" when (the_dn is RoutePoint || the_dn is Queue || the_dn is IVR) =>
                            await the_dn.ServiceCallAsync(new RPCParameters { { "method", "initiate_call" }, { "destination", destination_string} }),

                        //making call from the specific device (executed also, if DN has only one device regustered)
                        "makecall" when the_device is not null =>
                            await the_device.MakeCallAsync(destination_string),
                        //if dn has single device, use it as the dource of makecall;
                        "makecall" when allDevices?.Length == 1 =>
                            await allDevices[0].MakeCallAsync(destination_string),

                        //making call - MakeCallHelper [many registrations on DN]
                        "makecall" when the_device is null =>
                            await the_dn.MakeCallAsync(destination_string),

                        //answer on the directly controlled device.
                        "answer" when the_device is not null =>
                            //answer action is available ONLY if it is with the device which supports Direct Call Control
                            await connections.ExtractFirstOrDefault(x => x.Status == ConnectionStatus.Ringing && x["directcallctrl"] == "1")?.AnswerAsync(),

                        //Pickup callback to DN (all deivices ring)
                        "pickup" when the_device is null =>
                            await the_dn.PickupCallbackAsync(
                                the_dn_from.GetActiveConnections().ExtractFirstOrDefault(x => x.Status == ConnectionStatus.Ringing)
                            ),

                        //pickup on the specific device
                        "pickup" when the_device is not null =>
                            await the_device.PickupCallbackAsync(
                                the_dn_from.GetActiveConnections().ExtractFirstOrDefault(x => x.Status == ConnectionStatus.Ringing)
                            ),

                        //replaces ringing connection on dn/sipdevice with new routes based on specified number
                        "divert" when !use_structured_destination =>
                            await connections.ExtractFirstOrDefault(x => x.Status == ConnectionStatus.Ringing).DivertAsync(destination_string, false),

                        //replaces ringing connection on dn/sipdevice with new route to specified destination
                        "divert" when use_structured_destination =>
                            await connections.ExtractFirstOrDefault(x => x.Status == ConnectionStatus.Ringing).DivertAsync(structured_destination),

                        //successful only if route to specified destination has been answered.
                        //route can be answered by othe destination if the destination is subject for further routing

                        //route to number
                        "bxfer" when !use_structured_destination =>
                            await connections.ExtractFirstOrDefault(x => x.Status == ConnectionStatus.Connected)
                            .ReplaceWithAsync(destination_string),

                        //exact destination [known issue. Does not suport ProceedWithNoException]
                        "bxfer" when use_structured_destination =>
                            await connections.ExtractFirstOrDefault(x => x.Status == ConnectionStatus.Connected)
                            .ReplaceWithAsync(structured_destination),

                        //destination is device. [fails. not implemented yet]
                        "bxfer" when destination_device != null =>
                            await connections.ExtractFirstOrDefault(x => x.Status == ConnectionStatus.Connected)
                            .ReplaceWithAsync(destination_device),

                        //success when transfer procedure started.
                        //the connation which initiates transfer will leave the call once transfer will be finished
                        "bxferinit" when !use_structured_destination =>
                            await connections.ExtractFirstOrDefault(x => x.Status == ConnectionStatus.Connected)
                            .ReplaceWithInitOnlyAsync(destination_string),

                        //TODO: palnned to add
                        //"bxferinit" when use_structured_destination =>
                        //    await connections.ExtractFirstOrDefault(x => x.Status == ConnectionStatus.Connected)
                        //    .ReplaceWithInitOnlyAsync(structured_destination),

                        //joins call on the specific device.
                        "join" when the_device is not null && connections.Length > 2 =>
                             await connections[0].ReplaceWithPartyOfAsync(connections[1]),

                        //routeto is working in Connected and Ringing states, so there is no necessity to check the connection state.
                        //if connection is ringing, the route will fail if other routes(if any) will answer the call
                        //there is more advanced verion which receives RouteRequest
                        "routeto" when use_structured_destination =>
                             await connections.ExtractFirstOrDefault().RouteToAsync(structured_destination, timeout),
                        "routeto" when !use_structured_destination =>
                             await connections.ExtractFirstOrDefault().RouteToAsync(
                                 new RouteRequest { Destination = destination_string, TimeOut = TimeSpan.FromSeconds(timeout) }
                                 ),
                        //drops the connection. call will continue according to routing configuration
                        "drop" =>
                            await connections.ExtractFirstOrDefault().DropAsync(),

                        //attaches data to the party of the connection.
                        "attachcallerdata" =>
                            await connections.ExtractFirstOrDefault().GetPartyConnection().AttachConnectionDataAsync(parameters.Where(x => x.Key.StartsWith("public_")).ToDictionary()),
                        //attaches data to the first connection on specified dn or device.
                        "attachdata" =>
                            await connections.ExtractFirstOrDefault().AttachConnectionDataAsync(parameters.Where(x => x.Key.StartsWith("public_")).ToDictionary()),

                        var action => throw new Exception($"Unknown action({action}) or it is not applicable (at the moment) on {the_dn}\n")
                    };
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Operation Completed");
                    Console.ResetColor();
                    Console.WriteLine($"{callcontrol_result}");
                }
                catch (PBXIsNotConnected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Action has been failed. PBX is disconnected");
                    throw;
                }
                catch (OperationFailed ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Action has been failed: Operation Failed");
                    Console.ResetColor();
                    Console.WriteLine(ex);
                    throw;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Action has been failed: Unexpected exception");
                    Console.ResetColor();
                    Console.WriteLine(ex);
                    throw;
                }
                finally
                {
                    Console.ResetColor();
                }
                return true;
            }).Result;
        }
    }
}
