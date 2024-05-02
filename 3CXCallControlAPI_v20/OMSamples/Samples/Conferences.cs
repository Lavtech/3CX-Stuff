using System;
using System.Collections.Generic;
using System.Linq;
using TCX.Configuration;
using OMSamples;
using static TCX.PBXAPI.CallControlAPI;
using TCX.PBXAPI;
using TCX.Configuration.Interop;
using System.Threading;
using System.ComponentModel;
namespace OMSamples.Samples
{
    [SampleCode("conference")]
    [SampleWarning("Sample does NOT protect existing schedules when removal is requested]")]
    [SampleDescription("Commands:\n" +
        "    active - list of active audio conferences (including joined)\n" +
        "    scheduled - all scheduled meetings\n" +
        "    removeschedule - deleted schedule of conference. Active conference will continue as ad-hoc audio conference.\n" +
        "    dropall - drop all calls in active audio conference. Scheduled conference will be left available until end of schedule. ad-hoc will be terminated\n" +
        "    destroy - terminate all calls, delete schedule (if defined). Active conference will become unavailable\n" +
        "    add - adds participant by calling specific number\n" +
        "    hold - put member call on hold\n" +
        "    resume - resume member's call\n" +
        "    mute - mute incoming stream from member\n" +
        "    unmute - remove mute from incoming stream of member\n" +
        "    drop - disconenct member of audio conference\n"
        )]
    [SampleAction("show", "[id=<active_id>|pin=<conference_pin>]")]
    [SampleAction("removeschedule", "id=<active_id>|pin=<conference_pin>")]
    [SampleAction("dropall", "id=<active_id>|pin=<conference_pin>")]
    [SampleAction("destroy", "id=<active_id>|pin=<conference_pin>")]
    [SampleAction("add", "id=<active_id>|pin=<conference_pin> callto=<number>")]
    [SampleAction("hold", "id=<active_id>|pin=<conference_pin> participant=<participant_id>")]
    [SampleAction("resume", "id=<active_id>|pin=<conference_pin> participant=<participant_id>")]
    [SampleAction("mute", "id=<active_id>|pin=<conference_pin> participant=<participant_id>")]
    [SampleAction("unmute", "id=<active_id>|pin=<conference_pin> participant=<participant_id>")]
    [SampleAction("drop", "id=<active_id>|pin=<conference_pin> participant=<participant_id>")]
    class Conferences : ISample
    {
        void PrintStat(Statistics s, int max = int.MaxValue)
        {
            Console.WriteLine($"ID={s.ID}:");
            foreach (var a in s.Content)
            {
                Console.WriteLine($"\t{a.Key}={new string(a.Value.Take(max).ToArray())}");
            }
        }

        void PrintParticipants(Statistics s)
        {
            Console.WriteLine($"\tCurrent participants:");
            foreach (var a in s.GetArray("participants"))
            {
                Console.WriteLine($"\t\t{a}");
            }
            Console.WriteLine($"\tLeft Conference:");
            foreach (var a in s.GetArray("disconnected"))
            {
                Console.WriteLine($"\t\t{a}");
            }
        }
        public void Run(PhoneSystem ps, string command, Dictionary<string, string> args)
        {
            if (command == "")
            {
                this.ShowSampleInfo();
                return;
            }
            args.TryGetValue("id", out var idstr);
            int.TryParse(idstr, out var id);
            args.TryGetValue("pin", out var pin);
            var conf = ps.GetByID("S_CONFERENCESTATE", id) ?? ps.CreateStatistics("S_CONFERENCESTATE", pin);
            if (conf.ID==0) //no such record
            {
                Console.WriteLine("Not found");
                return;
            }
            pin = conf["pin"];
            var shedule = int.TryParse(conf["sheduleid"], out var schedule_id) ? ps.GetByID("S_SCHEDULEDCONF", schedule_id) : null;
            switch (command)
            {
                case "show":
                    {
                        PrintParticipants(conf);
                        if(shedule!=null)
                            PrintStat(ps.GetByID("S_SCHEDULEDCONF", schedule_id));
                        else
                            Console.WriteLine("SCHEDULE is not defined");
                    }
                    break;
                case "removeschedule":
                    {
                        shedule?.Delete();
                    }
                    break;
                default:
                    {
                        using (var confExt = ps.GetAll<ConferencePlaceExtension>().GetDisposer().Single())
                        {
                            var parameters = new RPCParameters{
                                { "pin", pin },
                                { "method", command switch
                                            {
                                                "destroy" or "dropall" => "delete",
                                                "hold" or "resume" => "hold",
                                                "mute"or "unmute" => "mute",
                                                "drop" or "add" =>command,
                                                _=>throw new ArgumentException("Invalid Action specified"),
                                            }}
                            };
                            parameters.Get("method", out string tocall);
                            if(args.TryGetValue("participant", out var participantid))
                            {
                                parameters.Add("member", participantid);
                            }
                            switch (tocall)
                            {
                                case "hold":
                                    parameters.Add("hold", tocall == command ? "1" : "0");
                                    break;
                                case "mute":
                                    parameters.Add("mute", tocall == command ? "1" : "0");
                                    break;
                            }
                            confExt.ServiceCallAsync(parameters);
                        }
                    }
                    break;
            }
        }
    }
}
