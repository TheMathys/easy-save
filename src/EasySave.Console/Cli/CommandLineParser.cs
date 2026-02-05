using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace EasySave.Console.Cli
{

    /// <summary>
    /// CommandLineParse guess what job is to be done depending on the list of args from the prompt.
    /// The Parse method must accept an arg type string and must return a list of int between one and five (jobs ids).
    /// Wrong id's are excluded.
    /// </summary>
    static class CommandLineParser
    {
        public static IReadOnlyList<int> Parse(string[] args)
        {
            IReadOnlyList<int> jobIds = new List<int>();
            foreach (var arg in args)
            {
                int min = 1;
                int max = 5;

                if (int.TryParse(arg, out int jobId) && jobId >= min && jobId <= max)
                {
                    ((List<int>)jobIds).Add(jobId);
                }
            }
            return jobIds.ToImmutableList<int>();
        }
    }
}
