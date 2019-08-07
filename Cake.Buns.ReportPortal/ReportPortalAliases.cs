using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.Diagnostics;
using ReportPortal.Buns.Clean;
using ReportPortal.Buns.Merge;
using ReportPortal.Buns.Merge.Smart;
using ReportPortal.Client;
using ReportPortal.Client.Filtering;
using ReportPortal.Client.Models;
using System.Threading.Tasks;

using LogLevel = Cake.Core.Diagnostics.LogLevel;

namespace Cake.Buns.ReportPortal
{
    [CakeNamespaceImport("ReportPortal.Buns.Clean")]
    [CakeNamespaceImport("ReportPortal.Buns.Merge")]
    [CakeNamespaceImport("ReportPortal.Buns.Merge.Smart")]
    [CakeNamespaceImport("ReportPortal.Client")]
    [CakeNamespaceImport("ReportPortal.Client.Filtering")]
    [CakeNamespaceImport("ReportPortal.Client.Models")]
    public static class ReportPortalAliases
    {
        [CakeMethodAlias]
        public static async Task<Launch> CleanLaunchAsync(
            this ICakeContext context,
            Service service,
            Launch launch,
            CleanOptions options)
        {
            var message = $"Before cleaning total tests in launch with id = {launch.Id} equals {launch.Statistics.Executions.Total}";
            context.Log.Write(Verbosity.Diagnostic, LogLevel.Debug, message);

            var afterClean = await new LaunchCleaner(service, options).CleanAsync(launch).ConfigureAwait(false);

            message = $"Cleaning launch with id = {launch.Id} successfully completed." +
             $" Total tests =  {afterClean.Statistics.Executions.Total}";

            context.Log.Write(Verbosity.Diagnostic, LogLevel.Debug, message);
            return afterClean;
        }

        [CakeMethodAlias]
        public static async Task<Launch> WithForciblyTerminatingCleanLaunchAsync(
            this ICakeContext context,
            Service service,
            Launch launch,
            CleanOptions options)
        {
            var message = $"Before cleaning total tests in launch with id = {launch.Id} equals {launch.Statistics.Executions.Total}";
            context.Log.Write(Verbosity.Diagnostic, LogLevel.Debug, message);

            var decorated = new LaunchCleaner(service, options);

            var afterClean = await new ForciblyTerminatingLaunchCleaner(decorated, service)
                .CleanAsync(launch)
                .ConfigureAwait(false);

            message = $"Cleaning launch with forcibly terminating with id = {launch.Id} successfully completed." +
                $" Total tests =  {afterClean.Statistics.Executions.Total}";

            context.Log.Write(Verbosity.Diagnostic, LogLevel.Debug, message);

            return afterClean;
        }

        [CakeMethodAlias]
        public static async Task<Launch> MergeLaunchesAsync(
            this ICakeContext context,
            Service service,
            Launch first,
            Launch second,
            MergeOptions options = null)
        {
            var launch = await new LaunchMerger(service, options)
                .MergeAsync(first, second)
                .ConfigureAwait(false);

            var message = $"Merging launches with id = {first.Id} and id = {second.Id} successfully completed";
            context.Log.Write(Verbosity.Diagnostic, LogLevel.Debug, message);

            return launch;
        }

        [CakeMethodAlias]
        public static async Task<Launch> MergeLaunchesWithCleaningAsync(
            this ICakeContext context,
            Service service,
            Launch cleanable,
            Launch notCleanable,
            CleanOptions cleanOptions,
            MergeOptions mergeOptions = null)
        {
            var cleaner = new ForciblyTerminatingLaunchCleaner(
                new LaunchCleaner(service, cleanOptions),
                service);

            var launch = await new CleanableLaunchMerger(new LaunchMerger(service, mergeOptions), cleaner)
                .MergeAsync(cleanable, notCleanable)
                .ConfigureAwait(false);

            var message = $"Merging launches with cleaning with id = {cleanable.Id} and id = {notCleanable.Id} successfully completed";
            context.Log.Write(Verbosity.Diagnostic, LogLevel.Debug, message);

            return launch;
        }

        [CakeMethodAlias]
        public static async Task<Launch> SmartMergeLaunchesAsync(
            this ICakeContext context,
            Service service,
            FilterOption filter,
            CleanOptions cleanOptions,
            MergeOptions mergeOptions = null,
            bool debug = true)
        {
            var cleaner = new ForciblyTerminatingLaunchCleaner(
                 new LaunchCleaner(service, cleanOptions),
                 service);

            var merger = new CleanableLaunchMerger(new LaunchMerger(service, mergeOptions), cleaner);

            var launch = await new SmartLaunchMerger(merger, service, debug)
                .MergeAsync(filter)
                .ConfigureAwait(false);

            var message = $"Smart merging launches successfully completed.";
            context.Log.Write(Verbosity.Diagnostic, LogLevel.Debug, message);

            return launch;
        }
    }
}
