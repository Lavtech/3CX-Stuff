using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TCX.Configuration;
using System.Diagnostics;

namespace OMSamples
{
    struct SampleCommadLine
    {
        public string sample="?";
        public string action="";
        private string actionstr = "";
        public bool no_action_specified = true;
        public Dictionary<string, string> parameters = [];
        public string original_input="";
        public SampleCommadLine(string input)
        {
            original_input = input;
            string[] tokens = input.Split(' ');
            sample = tokens.Take(1).First();
            action = tokens.Skip(1).FirstOrDefault()??"";
            var trimDoubleQuotes = new List<string>();
            foreach (var token in tokens.Skip(2))
            {
                if (trimDoubleQuotes.Count == 0)
                {
                    trimDoubleQuotes.Add(token);
                }
                else
                {
                    if (trimDoubleQuotes[^1].StartsWith("\"") && !trimDoubleQuotes[^1].EndsWith("\""))
                    {
                        trimDoubleQuotes[^1] += trimDoubleQuotes[^1] + $" {token}";
                    }
                    else
                    {
                        trimDoubleQuotes[^1] = trimDoubleQuotes[^1];
                        trimDoubleQuotes.Add(token);
                    }
                }
            }
            if (!string.IsNullOrEmpty(action))
                actionstr = $".{action}";
            parameters = trimDoubleQuotes.Select(x=>x.Trim('"').Split('=')).ToDictionary(x => x[0], y => string.Join("=", y.Skip(1)));
        }
        public override string ToString()
        {
            return $"{sample}{actionstr}({string.Join(", ", parameters.Select(x => $"{x.Key}={x.Value}"))})";
        }
    }

    public interface ISample
    {
        void Run(PhoneSystem ps, string action, Dictionary<string, string> additional_parameters);
    }
    public static class SampleStarter
    {
        private static readonly Dictionary<string, ISample> samples = new Dictionary<string, ISample>();

        static SampleStarter()
        {
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (Type type in types)
            {
                if (typeof(ISample).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    object[] attrs = type.GetCustomAttributes(typeof(SampleCodeAttribute), true);
                    if (attrs.Length == 0)
                        continue;
                    SampleCodeAttribute code = attrs[0] as SampleCodeAttribute;
                    samples[code.Code] = (ISample)Activator.CreateInstance(type);
                }
            }
        }

        private static string GetDescription(Type type)
        {
            object[] objs = type.GetCustomAttributes(typeof(SampleDescriptionAttribute), true);
            if (objs == null || objs.Length == 0)
                return "no description ";
            return ((SampleDescriptionAttribute)objs[0]).ToString();
        }

        private static string GetWarning(Type type)
        {
            object[] objs = type.GetCustomAttributes(typeof(SampleWarningAttribute), true);
            if (objs == null || objs.Length == 0)
                return "";
            return ((SampleWarningAttribute)objs[0]).ToString();
        }

        private static int CompareParams(SampleActionAttribute x, SampleActionAttribute y)
        {
            return x.Name.CompareTo(y.Name);
        }

        private static string GetParameters(Type type)
        {
            return $"Parameters:\n    {string.Join("\n   ", type.GetCustomAttributes(typeof(SampleActionAttribute), true).Cast<SampleActionAttribute>().OrderBy(x => x.Name))}";
        }
        public static bool ShowAllSamples()
        {
            Console.WriteLine(string.Join("\n", samples.Keys.AsEnumerable()));
            return true;
        }
        public static bool ShowSampleInfo(this ISample sample)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{GetWarning(sample.GetType())}");
            Console.ResetColor();
            Console.WriteLine($"{GetDescription(sample.GetType())}");
            Console.WriteLine($"{GetParameters(sample.GetType())}");
            return true;
        }
        static bool ExecuteSample(this ISample sample, PhoneSystem ps, string action, Dictionary<string, string> additional_parameters)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                sample.Run(ps, action, additional_parameters);
                return true;
            }
            finally
            {
                sw.Stop();
                Console.WriteLine($"Sample execution time - {sw.Elapsed}");
            }
        }
        public static bool StartSample(PhoneSystem ps, string sample_name, string action_name, Dictionary<string, string> additional_parameters)
        {
            //we intercept '?' here.
            return sample_name switch
            {
                "?" => ShowAllSamples(),
                _ when samples.TryGetValue(sample_name, out var sample) && action_name=="?" => sample.ShowSampleInfo(),
                _ when samples.TryGetValue(sample_name, out var sample) => sample.ExecuteSample(ps, action_name, additional_parameters),
                _ => throw new EntryPointNotFoundException($"Sample '{sample_name}' is not found")
            };

        }
    }
    public class SampleCodeAttribute : Attribute
    {
        private string code;

        public SampleCodeAttribute(string code)
        {
            this.code = code;
        }

        public string Code
        {
            get
            {
                return code;
            }
        }
    }

    public class SampleDescriptionAttribute : Attribute
    {
        private string desc;

        public SampleDescriptionAttribute(string description)
        {
            this.desc = description;
        }

        public string Description
        {
            get
            {
                return desc;
            }
        }

        public override string ToString()
        {
            if (desc == null)
                return "";
            return "Description: " + desc;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class SampleActionAttribute : Attribute
    {
        private string parameters;
        private string name;

        public SampleActionAttribute(string name, string parameters)
        {
            this.parameters = parameters;
            this.name = name;
        }

        public string Description
        {
            get
            {
                return parameters;
            }
        }
        public string Name
        {
            get
            {
                return name;
            }
        }
        public override string ToString()
        {
            return $"{name} {parameters}";
        }
    }

    public class SampleWarningAttribute : Attribute
    {
        private string warning;

        public SampleWarningAttribute(string warning)
        {
            this.warning = warning;
        }

        public string Warning
        {
            get
            {
                return warning;
            }
        }

        public override string ToString()
        {
            if (warning == null)
                return "";
            return "WARNING: " + warning;
        }
    }
}

