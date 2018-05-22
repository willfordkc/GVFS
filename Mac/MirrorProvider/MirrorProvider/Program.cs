using System;
using System.Linq;
using CommandLine;

namespace MirrorProvider
{
    class Program
    {
        static void Main(string[] args)
        {
            new Parser(
                settings =>
                {
                    settings.CaseSensitive = false;
                    settings.EnableDashDash = true;
                    settings.IgnoreUnknownArguments = false;
                    settings.HelpWriter = Console.Error;
                })
                .ParseArguments(args, typeof(CloneVerb), typeof(MountVerb))
                .WithNotParsed(
                    errors =>
                    {
                        if (errors.Any(error => error is TokenError))
                        {
                            Environment.Exit(1);
                        }
                    })
                .WithParsed<CloneVerb>(clone => clone.Execute())
                .WithParsed<MountVerb>(mount => mount.Execute());
        }
    }
}
