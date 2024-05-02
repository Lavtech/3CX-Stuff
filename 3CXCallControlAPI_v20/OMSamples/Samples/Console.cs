using System;
using System.Linq;
using TCX.Configuration;

namespace OMSamples
{
    /// <summary>
    /// it is formulated as ISample
    /// </summary>
    class SamplesConsole
    {
        public void Run(PhoneSystem ps)
        {
            while (!Program.Stop)
            {
                try
                {
                    Console.Write("\n>");
                    Console.Out.Flush();
                    var input = Console.ReadLine();
                    try
                    {
                        var commandLine = new SampleCommadLine(input);
                        Console.WriteLine($"Executing {commandLine}");
                        try
                        {
                            SampleStarter.StartSample(ps, commandLine.sample, commandLine.action, commandLine.parameters);
                        }
                        catch(Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Sample '{commandLine}' execution failed {ex}");
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.WriteLine(ex);
                        }
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Invalid command line:\n{input}");
                        Console.ResetColor();
                        continue;
                    }
                    finally
                    {
                        Console.ResetColor();
                    }
                }
                catch
                {
                    break;
                }
            }
            Console.WriteLine("Exit console");
        }
    }
}
