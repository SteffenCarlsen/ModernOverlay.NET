using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using BenchmarkDotNet.Running;

bool dryRun = args.Any(static arg => string.Equals(arg, "Dry", StringComparison.OrdinalIgnoreCase));
string[] benchmarkArgs = RemoveDryJobArgument(args);
Job job = dryRun ? Job.Dry : Job.Default;

IConfig config = DefaultConfig.Instance.AddJob(job.WithToolchain(InProcessEmitToolchain.Instance));
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(benchmarkArgs, config);

static string[] RemoveDryJobArgument(string[] args)
{
    var filtered = new List<string>(args.Length);
    for (int i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], "--job", StringComparison.OrdinalIgnoreCase)
            && i + 1 < args.Length
            && string.Equals(args[i + 1], "Dry", StringComparison.OrdinalIgnoreCase))
        {
            i++;
            continue;
        }

        filtered.Add(args[i]);
    }

    return [.. filtered];
}
