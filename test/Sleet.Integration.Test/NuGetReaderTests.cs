﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;
using Sleet.Test;
using Xunit;

namespace Sleet.Integration.Test
{
    public class NuGetReaderTests
    {
        [Fact]
        public async Task NuGetReader_MultiplePackagesOnRegistration()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var outputRoot = Path.Combine(target.Root, "output");
                var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                var log = new TestLogger();

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", outputRoot, baseUri.AbsoluteUri);

                var sleetConfigPath = Path.Combine(target.Root, "sleet.config");
                JsonUtility.SaveJson(new FileInfo(sleetConfigPath), sleetConfig);

                var exitCode = await Program.MainCore(new[] { "init", "-c", sleetConfigPath, "-s", "local" }, log);

                // push 100 packages
                for (int i = 0; i < 100; i++)
                {
                    var testPackage = new TestPackageContext("packageA", $"1.0.0-alpha.{i}");
                    var zipFile = testPackage.Create(packagesFolder.Root);
                }

                exitCode += await Program.MainCore(new[] { "push", packagesFolder.Root, "-c", sleetConfigPath, "-s", "local" }, log);
                exitCode += await Program.MainCore(new[] { "validate", "-c", sleetConfigPath, "-s", "local" }, log);

                // Act
                // Create a repository abstraction for nuget
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(outputRoot), baseUri);
                var localSource = GetSource(outputRoot, baseUri, fileSystem);

                var resource = await localSource.GetResourceAsync<PackageMetadataResource>();
                var packages = (await resource.GetMetadataAsync("packageA", true, true, log, CancellationToken.None)).ToList();

                // Assert
                Assert.True(0 == exitCode, log.ToString());
                Assert.Equal(100, packages.Count);
            }
        }

        [Fact]
        public async Task NuGetReader_MultiplePackagesOnRegistrationWithRemove()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var outputRoot = Path.Combine(target.Root, "output");
                var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                var log = new TestLogger();

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", outputRoot, baseUri.AbsoluteUri);

                var sleetConfigPath = Path.Combine(target.Root, "sleet.config");
                JsonUtility.SaveJson(new FileInfo(sleetConfigPath), sleetConfig);

                var exitCode = await Program.MainCore(new[] { "init", "-c", sleetConfigPath, "-s", "local" }, log);

                // push 100 packages
                for (int i = 0; i < 100; i++)
                {
                    var testPackage = new TestPackageContext("packageA", $"1.0.0-alpha.{i}");
                    var zipFile = testPackage.Create(packagesFolder.Root);
                }

                exitCode += await Program.MainCore(new[] { "push", packagesFolder.Root, "-c", sleetConfigPath, "-s", "local" }, log);
                exitCode += await Program.MainCore(new[] { "delete", "--id", "packageA", "--version", "1.0.0-alpha.5", "-c", sleetConfigPath, "-s", "local" }, log);
                exitCode += await Program.MainCore(new[] { "validate", "-c", sleetConfigPath, "-s", "local" }, log);

                // Act
                // Create a repository abstraction for nuget
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(outputRoot), baseUri);
                var localSource = GetSource(outputRoot, baseUri, fileSystem);

                var resource = await localSource.GetResourceAsync<PackageMetadataResource>();
                var packages = (await resource.GetMetadataAsync("packageA", true, true, log, CancellationToken.None)).ToList();

                // Assert
                Assert.True(0 == exitCode, log.ToString());
                Assert.Equal(99, packages.Count);
            }
        }

        // Verify latest is found with metadata resource
        [Fact]
        public async Task NuGetReader_FindLatest()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var outputRoot = Path.Combine(target.Root, "output");
                var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                var log = new TestLogger();
                var testPackage = new TestPackageContext("packageA", "1.0.0");

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", outputRoot, baseUri.AbsoluteUri);

                var sleetConfigPath = Path.Combine(target.Root, "sleet.config");
                JsonUtility.SaveJson(new FileInfo(sleetConfigPath), sleetConfig);

                var zipFile = testPackage.Create(packagesFolder.Root);

                // Act
                // Run sleet
                var exitCode = await Program.MainCore(new[] { "init", "-c", sleetConfigPath, "-s", "local" }, log);
                exitCode += await Program.MainCore(new[] { "push", zipFile.FullName, "-c", sleetConfigPath, "-s", "local" }, log);

                // Create a repository abstraction for nuget
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(outputRoot), baseUri);
                var localSource = GetSource(outputRoot, baseUri, fileSystem);

                var resource = await localSource.GetResourceAsync<MetadataResource>();
                var latest = await resource.GetLatestVersion("packageA", true, true, log, CancellationToken.None);

                // Assert
                Assert.True(0 == exitCode, log.ToString());
                Assert.Equal("1.0.0", latest.ToFullVersionString());
            }
        }

        // Verify flat container returns all versions
        [Fact]
        public async Task NuGetReader_FindPackageByIdResource()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var outputRoot = Path.Combine(target.Root, "output");
                var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                var log = new TestLogger();
                var testPackage = new TestPackageContext("packageA", "1.0.0");

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", outputRoot, baseUri.AbsoluteUri);

                var sleetConfigPath = Path.Combine(target.Root, "sleet.config");
                JsonUtility.SaveJson(new FileInfo(sleetConfigPath), sleetConfig);

                var zipFile = testPackage.Create(packagesFolder.Root);

                // Act
                // Run sleet
                var exitCode = await Program.MainCore(new[] { "init", "-c", sleetConfigPath, "-s", "local" }, log);
                exitCode += await Program.MainCore(new[] { "push", zipFile.FullName, "-c", sleetConfigPath, "-s", "local" }, log);

                // Create a repository abstraction for nuget
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(outputRoot), baseUri);
                var localSource = GetSource(outputRoot, baseUri, fileSystem);

                var resource = await localSource.GetResourceAsync<FindPackageByIdResource>();
                resource.Logger = log;
                resource.CacheContext = new SourceCacheContext()
                {
                    NoCache = true
                };

                var versions = await resource.GetAllVersionsAsync("packageA", CancellationToken.None);

                // Assert
                Assert.True(0 == exitCode, log.ToString());
                Assert.Equal("1.0.0", versions.Single().ToFullVersionString());
            }
        }

        // Verify download resource
        [Fact]
        public async Task NuGetReader_DownloadPackage()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var outputRoot = Path.Combine(target.Root, "output");
                var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                var log = new TestLogger();
                var testPackage = new TestPackageContext("packageA", "1.0.0");

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", outputRoot, baseUri.AbsoluteUri);

                var sleetConfigPath = Path.Combine(target.Root, "sleet.config");
                JsonUtility.SaveJson(new FileInfo(sleetConfigPath), sleetConfig);

                var zipFile = testPackage.Create(packagesFolder.Root);

                // Act
                // Run sleet
                var exitCode = await Program.MainCore(new[] { "init", "-c", sleetConfigPath, "-s", "local" }, log);
                exitCode += await Program.MainCore(new[] { "push", zipFile.FullName, "-c", sleetConfigPath, "-s", "local" }, log);

                // Create a repository abstraction for nuget
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(outputRoot), baseUri);
                var localSource = GetSource(outputRoot, baseUri, fileSystem);

                var resource = await localSource.GetResourceAsync<DownloadResource>();
                var result = await resource.GetDownloadResourceResultAsync(new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), NullSettings.Instance, log, CancellationToken.None);

                // Assert
                Assert.True(0 == exitCode, log.ToString());

                Assert.Equal(DownloadResourceResultStatus.Available, result.Status);
                Assert.True(result.PackageStream.Length > 0);
                Assert.Equal(new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), result.PackageReader.GetIdentity());
            }
        }

        // Verify auto complete resource
        [Fact]
        public async Task NuGetReader_AutoComplete()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var outputRoot = Path.Combine(target.Root, "output");
                var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                var log = new TestLogger();
                var testPackage = new TestPackageContext("packageA", "1.0.0");

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", outputRoot, baseUri.AbsoluteUri);

                var sleetConfigPath = Path.Combine(target.Root, "sleet.config");
                JsonUtility.SaveJson(new FileInfo(sleetConfigPath), sleetConfig);

                var zipFile = testPackage.Create(packagesFolder.Root);

                // Act
                // Run sleet
                var exitCode = await Program.MainCore(new[] { "init", "-c", sleetConfigPath, "-s", "local" }, log);
                exitCode += await Program.MainCore(new[] { "push", zipFile.FullName, "-c", sleetConfigPath, "-s", "local" }, log);

                // Create a repository abstraction for nuget
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(outputRoot), baseUri);
                var localSource = GetSource(outputRoot, baseUri, fileSystem);

                var resource = await localSource.GetResourceAsync<AutoCompleteResource>();
                var ids = await resource.IdStartsWith("p", true, log, CancellationToken.None);
                var versions = await resource.VersionStartsWith("packageA", "1", true, log, CancellationToken.None);

                // Assert
                Assert.True(0 == exitCode, log.ToString());

                Assert.Equal("packageA", ids.Single());
                Assert.Equal("1.0.0", versions.Single().ToFullVersionString());
            }
        }

        [Fact]
        public async Task NuGetReader_PackageMetadataResource()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var outputRoot = Path.Combine(target.Root, "output");
                var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                var log = new TestLogger();

                var testPackage = new TestPackageContext()
                {
                    Nuspec = new TestNuspecContext()
                    {
                        Id = "packageA",
                        Version = "1.0.0",
                        Authors = "author",
                        Description = "desc",
                        IconUrl = "http://www.tempuri.org/",
                        Language = "en-us",
                        MinClientVersion = "1.0.0",
                        Title = "title",
                        Tags = "a b d",
                        Summary = "summary",
                        LicenseUrl = "http://www.tempuri.org/lic",
                        ProjectUrl = "http://www.tempuri.org/proj",
                        ReleaseNotes = "notes",
                        Owners = "owners",
                        Copyright = "copyright",
                        RequireLicenseAcceptance = "true"
                    }
                };

                var testPackage2 = new TestPackageContext()
                {
                    Nuspec = new TestNuspecContext()
                    {
                        Id = "packageA",
                        Version = "2.0.0",
                        Authors = "author2",
                        Description = "desc2",
                        IconUrl = "http://www.tempuri2.org/",
                        Language = "en-us",
                        MinClientVersion = "1.0.0",
                        Title = "title2",
                        Tags = "a b c",
                        Summary = "summary2",
                        LicenseUrl = "http://www.tempuri.org/lic2",
                        ProjectUrl = "http://www.tempuri.org/proj2",
                        ReleaseNotes = "notes2",
                        Owners = "owners2",
                        Copyright = "copyright2",
                        RequireLicenseAcceptance = "true"
                    }
                };

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", outputRoot, baseUri.AbsoluteUri);

                var sleetConfigPath = Path.Combine(target.Root, "sleet.config");
                JsonUtility.SaveJson(new FileInfo(sleetConfigPath), sleetConfig);

                var zipFile = testPackage.Create(packagesFolder.Root);
                var zipFile2 = testPackage2.Create(packagesFolder.Root);

                // Act
                // Run sleet
                var exitCode = await Program.MainCore(new[] { "init", "-c", sleetConfigPath, "-s", "local" }, log);
                exitCode += await Program.MainCore(new[] { "push", zipFile2.FullName, "-c", sleetConfigPath, "-s", "local" }, log);
                exitCode += await Program.MainCore(new[] { "push", zipFile.FullName, "-c", sleetConfigPath, "-s", "local" }, log);

                // Create a repository abstraction for nuget
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(outputRoot), baseUri);
                var localSource = GetSource(outputRoot, baseUri, fileSystem);

                var resource = await localSource.GetResourceAsync<PackageMetadataResource>();
                var results = await resource.GetMetadataAsync("packageA", true, true, log, CancellationToken.None);
                var resultArray = results.OrderBy(e => e.Identity.Version).ToArray();

                // Assert
                Assert.True(0 == exitCode, log.ToString());

                Assert.Equal(testPackage.Nuspec.Authors, resultArray[0].Authors);
                Assert.Equal(testPackage.Nuspec.Description, resultArray[0].Description);
                Assert.Null(resultArray[0].DownloadCount);
                Assert.Equal(testPackage.Nuspec.IconUrl, resultArray[0].IconUrl.AbsoluteUri);
                Assert.Equal(testPackage.Nuspec.Id, resultArray[0].Identity.Id);
                Assert.Equal(testPackage.Nuspec.Version.ToString(), resultArray[0].Identity.Version.ToString());
                Assert.Equal(testPackage.Nuspec.LicenseUrl, resultArray[0].LicenseUrl.AbsoluteUri);
                Assert.Null(resultArray[0].Owners);
                Assert.Equal(testPackage.Nuspec.ProjectUrl, resultArray[0].ProjectUrl.AbsoluteUri);
                Assert.Equal(testPackage.Nuspec.Summary, resultArray[0].Summary);
                Assert.Equal("a, b, d", resultArray[0].Tags);
                Assert.Equal(testPackage.Nuspec.Title, resultArray[0].Title);
                Assert.True(resultArray[0].Published.Value.Year == DateTime.UtcNow.Year);
                Assert.Equal("https://localhost:8080/testFeed/", resultArray[0].ReportAbuseUrl.AbsoluteUri);
                Assert.Equal(true, resultArray[0].RequireLicenseAcceptance);

                Assert.Equal(testPackage2.Nuspec.Authors, resultArray[1].Authors);
                Assert.Equal(testPackage2.Nuspec.Description, resultArray[1].Description);
                Assert.Null(resultArray[1].DownloadCount);
                Assert.Equal(testPackage2.Nuspec.IconUrl, resultArray[1].IconUrl.AbsoluteUri);
                Assert.Equal(testPackage2.Nuspec.Id, resultArray[1].Identity.Id);
                Assert.Equal(testPackage2.Nuspec.Version.ToString(), resultArray[1].Identity.Version.ToString());
                Assert.Equal(testPackage2.Nuspec.LicenseUrl, resultArray[1].LicenseUrl.AbsoluteUri);
                Assert.Null(resultArray[1].Owners);
                Assert.Equal(testPackage2.Nuspec.ProjectUrl, resultArray[1].ProjectUrl.AbsoluteUri);
                Assert.Equal(testPackage2.Nuspec.Summary, resultArray[1].Summary);
                Assert.Equal("a, b, c", resultArray[1].Tags);
                Assert.Equal(testPackage2.Nuspec.Title, resultArray[1].Title);
                Assert.True(resultArray[1].Published.Value.Year == DateTime.UtcNow.Year);
                Assert.Equal("https://localhost:8080/testFeed/", resultArray[1].ReportAbuseUrl.AbsoluteUri);
                Assert.Equal(true, resultArray[1].RequireLicenseAcceptance);
            }
        }

        // Verify search
        [Fact]
        public async Task NuGetReader_PackageSearchResource()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var outputRoot = Path.Combine(target.Root, "output");
                var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                var log = new TestLogger();

                var testPackage = new TestPackageContext()
                {
                    Nuspec = new TestNuspecContext()
                    {
                        Id = "packageA",
                        Version = "1.0.0",
                        Authors = "author",
                        Description = "desc",
                        IconUrl = "http://www.tempuri.org",
                        Language = "en-us",
                        MinClientVersion = "1.0.0",
                        Title = "title",
                        Tags = "a b d",
                        Summary = "summary",
                        LicenseUrl = "http://www.tempuri.org/lic",
                        ProjectUrl = "http://www.tempuri.org/proj",
                        ReleaseNotes = "notes",
                        Owners = "owners",
                        Copyright = "copyright",
                        RequireLicenseAcceptance = "true"
                    }
                };

                var testPackage2 = new TestPackageContext()
                {
                    Nuspec = new TestNuspecContext()
                    {
                        Id = "packageA",
                        Version = "2.0.0",
                        Authors = "author2",
                        Description = "desc2",
                        IconUrl = "http://www.tempuri2.org/",
                        Language = "en-us",
                        MinClientVersion = "1.0.0",
                        Title = "title2",
                        Tags = "a b c",
                        Summary = "summary2",
                        LicenseUrl = "http://www.tempuri.org/lic2",
                        ProjectUrl = "http://www.tempuri.org/proj2",
                        ReleaseNotes = "notes2",
                        Owners = "owners2",
                        Copyright = "copyright2",
                        RequireLicenseAcceptance = "true"
                    }
                };

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", outputRoot, baseUri.AbsoluteUri);

                var sleetConfigPath = Path.Combine(target.Root, "sleet.config");
                JsonUtility.SaveJson(new FileInfo(sleetConfigPath), sleetConfig);

                var zipFile = testPackage.Create(packagesFolder.Root);
                var zipFile2 = testPackage2.Create(packagesFolder.Root);

                // Act
                // Run sleet
                var exitCode = await Program.MainCore(new[] { "init", "-c", sleetConfigPath, "-s", "local" }, log);
                exitCode += await Program.MainCore(new[] { "push", zipFile2.FullName, "-c", sleetConfigPath, "-s", "local" }, log);
                exitCode += await Program.MainCore(new[] { "push", zipFile.FullName, "-c", sleetConfigPath, "-s", "local" }, log);

                // Create a repository abstraction for nuget
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(outputRoot), baseUri);
                var localSource = GetSource(outputRoot, baseUri, fileSystem);

                var resource = await localSource.GetResourceAsync<PackageSearchResource>();
                var results = await resource.SearchAsync(string.Empty, new SearchFilter(), 0, 10, log, CancellationToken.None);
                var result = results.Single();

                var versions = await result.GetVersionsAsync();

                // Assert
                Assert.True(0 == exitCode, log.ToString());

                Assert.Equal(testPackage2.Nuspec.Authors, result.Authors);
                Assert.Equal(testPackage2.Nuspec.Description, result.Description);
                Assert.Equal(0, result.DownloadCount);
                Assert.Equal(testPackage2.Nuspec.IconUrl, result.IconUrl.AbsoluteUri);
                Assert.Equal(testPackage2.Nuspec.Id, result.Identity.Id);
                Assert.Equal(testPackage2.Nuspec.Version.ToString(), result.Identity.Version.ToString());
                Assert.Equal(testPackage2.Nuspec.LicenseUrl, result.LicenseUrl.AbsoluteUri);
                Assert.Equal(testPackage2.Nuspec.Owners, result.Owners);
                Assert.Equal(testPackage2.Nuspec.ProjectUrl, result.ProjectUrl.AbsoluteUri);
                Assert.Equal(testPackage2.Nuspec.Summary, result.Summary);
                Assert.Equal("a, b, c", result.Tags);
                Assert.Equal(testPackage2.Nuspec.Title, result.Title);

                Assert.Equal(2, versions.Count());
                Assert.Equal("1.0.0", versions.First().Version.ToString());
                Assert.Equal("2.0.0", versions.Skip(1).First().Version.ToString());
            }
        }

        [Fact]
        public async Task NuGetReader_DependencyInfoResource_DependencyGroups()
        {
            // Arrange
            using (var packagesFolder = new TestFolder())
            using (var target = new TestFolder())
            using (var cache = new LocalCache())
            {
                var outputRoot = Path.Combine(target.Root, "output");
                var baseUri = UriUtility.CreateUri("https://localhost:8080/testFeed/");

                var log = new TestLogger();

                var testPackage = new TestPackageContext()
                {
                    Nuspec = new TestNuspecContext()
                    {
                        Id = "packageA",
                        Version = "1.0.0",
                        Dependencies = new List<PackageDependencyGroup>()
                        {
                            new PackageDependencyGroup(NuGetFramework.Parse("net46"),  new List<PackageDependency>() { }),
                            new PackageDependencyGroup(NuGetFramework.Parse("net45"), new[] { new PackageDependency("packageB", VersionRange.Parse("1.0.0")), new PackageDependency("packageC", VersionRange.Parse("2.0.0")) }),
                            new PackageDependencyGroup(NuGetFramework.Parse("any"), new List<PackageDependency>() { new PackageDependency("packageB", VersionRange.Parse("1.0.0")) })
                        }
                    }
                };

                var sleetConfig = TestUtility.CreateConfigWithLocal("local", outputRoot, baseUri.AbsoluteUri);

                var sleetConfigPath = Path.Combine(target.Root, "sleet.config");
                JsonUtility.SaveJson(new FileInfo(sleetConfigPath), sleetConfig);

                var zipFile = testPackage.Create(packagesFolder.Root);

                // Act
                // Run sleet
                var exitCode = await Program.MainCore(new[] { "init", "-c", sleetConfigPath, "-s", "local" }, log);
                exitCode += await Program.MainCore(new[] { "push", zipFile.FullName, "-c", sleetConfigPath, "-s", "local" }, log);

                // Create a repository abstraction for nuget
                var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(outputRoot), baseUri);
                var localSource = GetSource(outputRoot, baseUri, fileSystem);

                var dependencyInfoResource = await localSource.GetResourceAsync<DependencyInfoResource>();

                var dependencyPackagesNet46 = await dependencyInfoResource.ResolvePackages("packageA", NuGetFramework.Parse("net46"), log, CancellationToken.None);
                var dependencyPackageNet46 = dependencyPackagesNet46.Single();
                var depString46 = string.Join("|", dependencyPackageNet46.Dependencies.Select(d => d.Id + " " + d.VersionRange.ToNormalizedString()));

                var dependencyPackagesNet45 = await dependencyInfoResource.ResolvePackages("packageA", NuGetFramework.Parse("net45"), log, CancellationToken.None);
                var dependencyPackageNet45 = dependencyPackagesNet45.Single();
                var depString45 = string.Join("|", dependencyPackageNet45.Dependencies.Select(d => d.Id + " " + d.VersionRange.ToNormalizedString()));

                var dependencyPackagesNet40 = await dependencyInfoResource.ResolvePackages("packageA", NuGetFramework.Parse("net40"), log, CancellationToken.None);
                var dependencyPackageNet40 = dependencyPackagesNet40.Single();
                var depString40 = string.Join("|", dependencyPackageNet40.Dependencies.Select(d => d.Id + " " + d.VersionRange.ToNormalizedString()));

                // Assert
                Assert.True(0 == exitCode, log.ToString());

                Assert.Equal("https://localhost:8080/testFeed/flatcontainer/packagea/1.0.0/packagea.1.0.0.nupkg", dependencyPackageNet46.DownloadUri.AbsoluteUri);
                Assert.Equal(true, dependencyPackageNet46.Listed);
                Assert.Equal("packageA", dependencyPackageNet46.Id);
                Assert.Equal("1.0.0", dependencyPackageNet46.Version.ToNormalizedString());
                Assert.Equal("", depString46);
                Assert.Equal("packageB [1.0.0, )|packageC [2.0.0, )", depString45);
                Assert.Equal("packageB [1.0.0, )", depString40);
            }
        }

        public static SourceRepository GetSource(string outputRoot, Uri baseUri, PhysicalFileSystem fileSystem)
        {
            var providers = Repository.Provider.GetCoreV3().ToList();

            // HttpSource -> PhysicalFileSystem adapter
            providers.Add(new Lazy<INuGetResourceProvider>(() => new TestHttpSourceResourceProvider(fileSystem)));

            return new SourceRepository(new PackageSource(baseUri.AbsoluteUri + "index.json"), providers);
        }
    }
}
