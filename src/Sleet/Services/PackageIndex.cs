﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Sleet
{
    /// <summary>
    /// sleet.packageindex.json is a simple json index of all ids and versions contained in the feed.
    /// </summary>
    public class PackageIndex : ISleetService, IPackagesLookup
    {
        private readonly SleetContext _context;

        public string Name { get; } = nameof(PackageIndex);

        public PackageIndex(SleetContext context)
        {
            _context = context;
        }

        public async Task AddPackage(PackageInput packageInput)
        {
            // Load existing index
            var index = await GetPackageIndex();

            // Add package
            ISet<NuGetVersion> versions;
            if (!index.TryGetValue(packageInput.Identity.Id, out versions))
            {
                versions = new HashSet<NuGetVersion>();
                index.Add(packageInput.Identity.Id, versions);
            }

            versions.Add(packageInput.Identity.Version);

            // Create updated index
            var json = CreateJson(index);
            var file = Index;

            await file.Write(json, _context.Log, _context.Token);
        }

        public async Task RemovePackage(PackageIdentity package)
        {
            // Load existing index
            var index = await GetPackageIndex();

            // Remove package
            ISet<NuGetVersion> versions;
            if (index.TryGetValue(package.Id, out versions) && versions.Remove(package.Version))
            {
                // Create updated index
                var json = CreateJson(index);
                var file = Index;

                await file.Write(json, _context.Log, _context.Token);
            }
        }

        /// <summary>
        /// Creates a set of all indexed packages
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackages()
        {
            var result = new HashSet<PackageIdentity>();

            var packages = await GetPackageIndex();

            foreach (var pair in packages)
            {
                foreach (var version in pair.Value)
                {
                    result.Add(new PackageIdentity(pair.Key, version));
                }
            }

            return result;
        }

        /// <summary>
        /// Returns all packages in the feed.
        /// Id -> Version
        /// </summary>
        public async Task<IDictionary<string, ISet<NuGetVersion>>> GetPackageIndex()
        {
            var index = new Dictionary<string, ISet<NuGetVersion>>(StringComparer.OrdinalIgnoreCase);

            var json = await GetJson();

            var packagesNode = json["packages"] as JObject;
            
            if (packagesNode == null)
            {
                throw new InvalidDataException("Packages node missing from sleet.packageindex.json");
            }

            foreach (var property in packagesNode.Properties())
            {
                var versions = (JArray)property.Value;

                foreach (var versionEntry in versions)
                {
                    var packageVersion = NuGetVersion.Parse(versionEntry.ToObject<string>());
                    var id = property.Name;

                    ISet<NuGetVersion> packageVersions;
                    if (!index.TryGetValue(id, out packageVersions))
                    {
                        packageVersions = new HashSet<NuGetVersion>();
                        index.Add(id, packageVersions);
                    }

                    packageVersions.Add(packageVersion);
                }
            }

            return index;
        }

        /// <summary>
        /// Find all versions of a package.
        /// </summary>
        public async Task<ISet<PackageIdentity>> GetPackagesById(string packageId)
        {
            var results = new HashSet<PackageIdentity>();
            var versions = await GetPackageVersions(packageId);

            foreach (var version in versions)
            {
                results.Add(new PackageIdentity(packageId, version));
            }

            return results;
        }

        /// <summary>
        /// Find all versions of a package.
        /// </summary>
        public async Task<ISet<NuGetVersion>> GetPackageVersions(string packageId)
        {
            var index = await GetPackageIndex();

            ISet<NuGetVersion> versions;
            if (!index.TryGetValue(packageId, out versions))
            {
                versions = new HashSet<NuGetVersion>();
            }

            return versions;
        }

        /// <summary>
        /// True if the package exists in the index.
        /// </summary>
        public async Task<bool> Exists(string packageId, NuGetVersion version)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            var byId = await GetPackagesById(packageId);

            return byId.Contains(new PackageIdentity(packageId, version));
        }

        public Task<bool> Exists(PackageIdentity package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            return Exists(package.Id, package.Version);
        }

        private ISleetFile Index
        {
            get
            {
                return _context.Source.Get("/sleet.packageindex.json");
            }
        }

        private async Task<JObject> GetJson()
        {
            var file = Index;

            return await file.GetJson(_context.Log, _context.Token);
        }

        private static JObject CreateJson(IDictionary<string, ISet<NuGetVersion>> index)
        {
            var json = new JObject();

            var packages = new JObject();

            json.Add("packages", packages);

            foreach (var id in index.Keys.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                var versionArray = new JArray(index[id].Select(v => v.ToNormalizedString()));

                if (versionArray.Count > 0)
                {
                    packages.Add(id, versionArray);
                }
            }

            return json;
        }
    }
}
