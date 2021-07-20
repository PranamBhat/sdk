﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks;
using NuGet.RuntimeModel;

namespace Microsoft.DotNet.PackageValidation
{
    public class ValidatePackage : TaskBase
    {
        [Required]
        public string PackageTargetPath { get; set; }
        
        public string RoslynPath { get; set; } 

        public string RuntimeGraph { get; set; }

        public string NoWarn { get; set; }

        public bool RunApiCompat { get; set; }

        public bool EnableStrictModeForCompatibleTfms { get; set; }

        public bool EnableStrictModeForCompatibleFrameworksInPackage { get; set; }

        public string BaselinePackageTargetPath { get; set; }

        public bool DisablePackageBaselineValidation { get; set; }

        public bool GenerateCompatibilitySuppressionFile { get; set; }

        public string CompatibilitySuppressionFilePath { get; set; }

        public override bool Execute()
        {
            if (!string.IsNullOrEmpty(RoslynPath))
              AppDomain.CurrentDomain.AssemblyResolve += ResolverForRoslyn;

            try
            {
                return base.Execute();
            }
            finally
            {
                if (!string.IsNullOrEmpty(RoslynPath))
                  AppDomain.CurrentDomain.AssemblyResolve -= ResolverForRoslyn;
            }
        }
        
        private static Assembly ResolverForRoslyn(object sender, ResolveEventArgs args)
        {
            AssemblyName name = new(args.Name);
            return name.Name switch
            {
                "Microsoft.CodeAnalysis" or "Microsoft.CodeAnalysis.CSharp" =>
                    Assembly.LoadFrom(Path.Combine(RoslynPath, $"{name.Name}.dll")),
                _ => null,
            };
        }
        
        protected override void ExecuteCore()
        {
            RuntimeGraph runtimeGraph = null;
            if (!string.IsNullOrEmpty(RuntimeGraph))
            {
                runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(RuntimeGraph);
            }

            Package package = NupkgParser.CreatePackage(PackageTargetPath, runtimeGraph);
            PackageValidationLogger logger = new(Log, CompatibilitySuppressionFilePath, GenerateCompatibilitySuppressionFile);

            new CompatibleTfmValidator(NoWarn, null, RunApiCompat, EnableStrictModeForCompatibleTfms, logger).Validate(package);
            new CompatibleFrameworkInPackageValidator(NoWarn, null, EnableStrictModeForCompatibleFrameworksInPackage, logger).Validate(package);

            if (!DisablePackageBaselineValidation && !string.IsNullOrEmpty(BaselinePackageTargetPath))
            {
                Package baselinePackage = NupkgParser.CreatePackage(BaselinePackageTargetPath, runtimeGraph);
                new BaselinePackageValidator(baselinePackage, NoWarn, null, RunApiCompat, logger).Validate(package);
            }

            if (GenerateCompatibilitySuppressionFile)
            {
                logger.GenerateSuppressionsFile(CompatibilitySuppressionFilePath);
            }
        }
    }
}
