﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;
using NuGet.Packaging;

namespace Sleet
{
    internal static class PushCommand
    {
        public static void Register(CommandLineApplication cmdApp, ILogger log)
        {
            cmdApp.Command("push", (cmd) => Run(cmd, log), throwOnUnexpectedArg: true);
        }

        private static void Run(CommandLineApplication cmd, ILogger log)
        {
            cmd.Description = "Push a package to a feed.";

            var optionConfigFile = cmd.Option("-c|--config", "sleet.json file to read sources and settings from.",
                CommandOptionType.SingleValue);

            var sourceName = cmd.Option("-s|--source", "Source name from sleet.json.",
                            CommandOptionType.SingleValue);

            var forceName = cmd.Option("-f|--force", "Overwrite existing packages.",
                            CommandOptionType.NoValue);

            var argRoot = cmd.Argument(
                "[root]",
                "Paths to individual packages or directories containing packages.",
                multipleValues: true);

            cmd.HelpOption("-?|-h|--help");

            var required = new List<CommandOption>()
            {
                sourceName
            };

            cmd.OnExecute(async () =>
            {
                try
                {
                    cmd.ShowRootCommandFullNameAndVersion();

                    // Validate parameters
                    foreach (var requiredOption in required)
                    {
                        if (!requiredOption.HasValue())
                        {
                            throw new ArgumentException($"Missing required parameter --{requiredOption.LongName}.");
                        }
                    }

                    var settings = LocalSettings.Load(optionConfigFile.Value());

                    using (var cache = new LocalCache())
                    {
                        var fileSystem = FileSystemFactory.CreateFileSystem(settings, cache, sourceName.Value());

                        if (fileSystem == null)
                        {
                            throw new InvalidOperationException("Unable to find source. Verify that the --source parameter is correct and that sleet.json contains the named source.");
                        }

                        return await RunCore(settings, fileSystem, argRoot.Values.ToList(), forceName.HasValue(), log);
                    }
                }
                catch (Exception ex)
                {
                    log.LogError(ex.Message);
                    log.LogDebug(ex.ToString());
                }

                return 1;
            });
        }

        public static async Task<int> RunCore(LocalSettings settings, ISleetFileSystem source, List<string> inputs, bool force, ILogger log)
        {
            var exitCode = 0;

            var token = CancellationToken.None;
            var now = DateTimeOffset.UtcNow;

            // Get packages
            var packages = GetPackageInputs(inputs, now, log);

            // Check if already initialized
            using (var feedLock = await SourceUtility.VerifyInitAndLock(source, log, token))
            {

                // Validate source
                await UpgradeUtility.UpgradeIfNeeded(source, log, token);

                // Get sleet.settings.json
                var sourceSettings = new SourceSettings();

                // Settings context used for all operations
                var context = new SleetContext()
                {
                    LocalSettings = settings,
                    SourceSettings = sourceSettings,
                    Log = log,
                    Source = source,
                    Token = token
                };

                var packageIndex = new PackageIndex(context);

                foreach (var package in packages)
                {
                    if (await packageIndex.Exists(package.Identity))
                    {
                        if (force)
                        {
                            log.LogInformation($"Package already exists, removing {package.ToString()}");
                            await SleetUtility.RemovePackage(context, package.Identity);
                        }
                        else
                        {
                            throw new InvalidOperationException($"Package already exists: '{package.Identity}'.");
                        }
                    }

                    log.LogInformation($"Adding {package.Identity.ToString()}");
                    await SleetUtility.AddPackage(context, package);
                }

                // Save all
                await source.Commit(log, token);
            }

            return exitCode;
        }

        /// <summary>
        /// Parse input arguments for nupkg paths.
        /// </summary>
        public static List<PackageInput> GetPackageInputs(List<string> inputs, DateTimeOffset now, ILogger log)
        {
            var packages = new List<PackageInput>();

            // Check inputs
            if (inputs.Count < 1)
            {
                throw new ArgumentException("No packages found.");
            }

            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var input in inputs)
            {
                var inputFile = Path.GetFullPath(input);

                if (File.Exists(inputFile))
                {
                    files.Add(inputFile);
                }
                else if (Directory.Exists(inputFile))
                {
                    var directoryFiles = Directory.GetFiles(inputFile, "*.nupkg", SearchOption.AllDirectories)
                        .Where(file => file.IndexOf(".symbols.nupkg") < 0)
                        .ToList();

                    if (directoryFiles.Count < 1)
                    {
                        throw new FileNotFoundException($"Unable to find nupkgs in '{inputFile}'.");
                    }

                    files.UnionWith(directoryFiles);
                }
                else
                {
                    throw new FileNotFoundException($"Unable to find '{inputFile}'.");
                }
            }

            if (files.Any(file => file.IndexOf(".symbols.nupkg") > -1))
            {
                throw new ArgumentException("Symbol packages are not supported.");
            }

            foreach (var file in files)
            {
                // Validate package
                PackageInput packageInput = null;

                log.LogInformation($"Reading {file}");

                try
                {
                    var zip = new ZipArchive(File.OpenRead(file), ZipArchiveMode.Read, leaveOpen: false);

                    var package = new PackageArchiveReader(zip);

                    packageInput = new PackageInput()
                    {
                        PackagePath = file,
                        Identity = package.GetIdentity(),
                        Package = package,
                        Zip = zip,
                    };
                }
                catch
                {
                    log.LogError($"Invalid package '{file}'.");
                    throw;
                }

                // Display a message for non-normalized packages
                if (packageInput.Identity.Version.ToString() != packageInput.Identity.Version.ToNormalizedString())
                {
                    var message = $"Package '{packageInput.PackagePath}' does not contain a normalized version. Normalized: '{packageInput.Identity.Version.ToNormalizedString()}' Nuspec version: '{packageInput.Identity.Version.ToString()}'. See https://semver.org/ for details.";
                    log.LogVerbose(message);
                }

                // Check for correct nuspec name
                var nuspecName = packageInput.Identity.Id + ".nuspec";
                if (packageInput.Zip.GetEntry(nuspecName) == null)
                {
                    throw new InvalidDataException($"'{packageInput.PackagePath}' does not contain '{nuspecName}'.");
                }

                // Check for multiple nuspec files
                if (packageInput.Zip.Entries.Where(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).Count() > 1)
                {
                    throw new InvalidDataException($"'{packageInput.PackagePath}' contains multiple nuspecs and cannot be consumed.");
                }

                // Check for duplicates
                if (packages.Any(package => package.Identity.Equals(packageInput.Identity)))
                {
                    throw new InvalidOperationException($"Duplicate packages detected for '{packageInput.Identity}'.");
                }

                packages.Add(packageInput);
            }

            return packages;
        }
    }

    public static class PushCommandTestHook
    {
        public static Task<int> RunCore(LocalSettings settings, ISleetFileSystem source, List<string> inputs, bool force, ILogger log)
        {
            return PushCommand.RunCore(settings, source, inputs, force, log);
        }
    }
}
