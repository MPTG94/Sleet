﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace Sleet
{
    public interface IPackageIdLookup
    {
        /// <summary>
        /// Find all existing packages with the given id.
        /// </summary>
        Task<ISet<PackageIdentity>> GetPackagesById(string packageId);
    }
}
