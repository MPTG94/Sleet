﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace Sleet
{
    public interface IPackagesLookup : IPackageIdLookup
    {
        /// <summary>
        /// Returns all existing packages.
        /// </summary>
        Task<ISet<PackageIdentity>> GetPackages();
    }
}
