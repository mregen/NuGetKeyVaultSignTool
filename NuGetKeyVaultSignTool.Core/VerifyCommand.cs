﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using NuGet.Protocol;

namespace NuGetKeyVaultSignTool
{
    public class VerifyCommand
    {
        readonly ILogger logger;

        public VerifyCommand(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<bool> VerifyAsync(string file, StringBuilder buffer)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }


            var trustProviders = new ISignatureVerificationProvider[]
            {
                new IntegrityVerificationProvider(),
                new SignatureTrustAndValidityVerificationProvider()
            };
            var verifier = new PackageSignatureVerifier(trustProviders);

            var allPackagesVerified = true;

            try
            {
                var result = 0;
                var packagesToVerify = LocalFolderUtility.ResolvePackageFromPath(file);

                foreach (var packageFile in packagesToVerify)
                {
                    using var package = new PackageArchiveReader(packageFile);
                    var verificationResult = await verifier.VerifySignaturesAsync(package, SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(), CancellationToken.None);

                    if (verificationResult.IsValid)
                    {
                        allPackagesVerified = true;
                    }
                    else
                    {
                        var logMessages = verificationResult.Results.SelectMany(p => p.Issues).Select(p => p.AsRestoreLogMessage()).ToList();
                        foreach (var msg in logMessages)
                        {
                            buffer.AppendLine(msg.Message);
                        }
                        if (logMessages.Any(m => m.Level >= NuGet.Common.LogLevel.Warning))
                        {
                            var errors = logMessages.Where(m => m.Level == NuGet.Common.LogLevel.Error).Count();
                            var warnings = logMessages.Where(m => m.Level == NuGet.Common.LogLevel.Warning).Count();

                            buffer.AppendLine($"Finished with {errors} errors and {warnings} warnings.");

                            result = errors;
                        }
                        allPackagesVerified = false;
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);
                return false;
            }

            return allPackagesVerified;
        }
    }
}
