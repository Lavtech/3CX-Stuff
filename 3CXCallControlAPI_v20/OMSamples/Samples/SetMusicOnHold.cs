using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TCX.Configuration;
using System.IO;
namespace OMSamples.Samples
{
    [SampleCode("musiconhold")]
    [SampleAction("show", "")]
    [SampleAction("update", "<entity>=<source>")]
    [SampleWarning("modifies configuration. Do not use in production environment")]
    [SampleDescription("")]
    class MusicOnHold : ISample
    {
        string[] MOHParameters =
        {
        "MUSICONHOLDFILE",
        "MUSICONHOLDFILE1",
        "MUSICONHOLDFILE2",
        "MUSICONHOLDFILE3",
        "MUSICONHOLDFILE4",
        "MUSICONHOLDFILE5",
        "MUSICONHOLDFILE6",
        "MUSICONHOLDFILE7",
        "MUSICONHOLDFILE8",
        "MUSICONHOLDFILE9",
        "CONFPLACE_MOH_SOURCE",
        "IVR_MOH_SOURCE",
        "PARK_MOH_SOURCE"
        };

        public void Run(PhoneSystem ps, string action, Dictionary<string, string> args)
        {
            if (action != "update" && action != "show")
            {
                this.ShowSampleInfo();
                return;
            }

            if (action == "update")
            {
                var FilesFolder = ps.GetParameterValue("IVRPROMPTPATH"); //base folder for files
                var PlaylistFolder = Path.Combine(FilesFolder, "Playlist"); //base folder for playlists
                foreach (var a in args)
                {
                    var name = a.Key;
                    var value = a.Value; //can be folder of configured playlist or the path to the file
                    if (value != string.Empty)
                    {
                        if (!File.Exists(Path.Combine(FilesFolder, value)))
                        {
                            AudioFeed playlist = null;
                            if (!Directory.Exists(Path.Combine(PlaylistFolder, value)) || (playlist = ps.GetAllAudioFeeds().GetDisposer(x => x.Source == value).FirstOrDefault()) == null) //not found even playlist
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"Source {value} is not found to update {name}");
                                Console.ResetColor();
                                continue;
                            }
                            //we need to set pipe reference
                            Console.WriteLine($"Source for {name} is the playlist {value} ({playlist.Name})");
                            value = @"\\.\pipe\" + playlist.Name;
                        }
                        else
                        {
                            value = Path.Combine(FilesFolder, value);
                        }
                    }
                    var q = ps.GetDNByNumber(name) as Queue;
                    var p = ps.GetParameterValue(name);
                    if (p != null && MOHParameters.Contains(name)) //parameter
                    {
                        ps.SetParameter(name, value);
                        Console.WriteLine($"updated PARAM.{name}={value}");
                    }
                    else if (q != null && p == null)
                    {
                        try
                        {
                            q.OnHoldFile = value;
                            q.Save();
                            Console.WriteLine($"updated queue {q}\nQUEUE.{name}={value}");
                        }
                        catch
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Failed to update music on hold on QUEUE.{name}");
                            Console.ResetColor();
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Invalid entity {name}. Should be name of a custom parameter or queue number");
                        Console.ResetColor();
                    }
                }
            }
            else
            {
                Console.WriteLine("All MOH users:");
                var allMOHSources = MOHParameters.Select(x => new KeyValuePair<string, string>("PARAM." + x, ps.GetParameterValue(x))).Where(z => z.Value != null)
                    .Concat(ps.GetQueues().Select(y => new KeyValuePair<string, string>("QUEUE." + y.Number, y.OnHoldFile)));
                foreach (var a in allMOHSources)
                {
                    if (a.Value.StartsWith(@"\\.\pipe\")) // it should be AudioFeed reference
                    {
                        var res = ps.GetAllAudioFeeds().FirstOrDefault(x => x.Name == a.Value.Substring(9));
                        if (res == null) //not found
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"{a.Key}={a.Value} UNDEFINED PLAYLIST REFERENCE");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"{a.Key}=PL[{res.Source}]({res.Name})");
                        }
                    }
                    else
                    {
                        bool exists = File.Exists(a.Value);
                        if (!exists)
                            Console.ForegroundColor = ConsoleColor.Red;
                        else
                            Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"{a.Key}=FILE[{a.Value}]" + (exists ? "" : " NOT EXIST"));
                    }
                    Console.ResetColor();
                }
            }
        }
    }
}