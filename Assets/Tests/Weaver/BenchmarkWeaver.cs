using System;
using Mirage.Weaver;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor.Compilation;
using UnityEngine;

namespace Mirage.Tests.Weaver
{
    public class BenchmarkWeaver
    {
        [Test]
        [Performance]
        public void Benchmark()
        {
            Run("Default");
        }

        [Test]
        [Performance]
        public void Benchmark_RunOnce()
        {
            Run("Default", 0, 1);
        }

        [TearDown]
        public void TearDown()
        {
            FieldReferenceComparator.Fast = false;
            PostProcessorAssemblyResolver.Version = 0;
            PostProcessorReflectionImporter.Fast = false;
        }

        [Test]
        [Performance]
        public void Benchmark_FieldReferenceComparator()
        {
            FieldReferenceComparator.Fast = false;
            Run("Slow");

            FieldReferenceComparator.Fast = true;
            Run("Fast");
        }

        [Test]
        [Performance]
        public void Benchmark_PostProcessorAssemblyResolver()
        {
            for (int i = 0; i < 4; i++)
            {
                PostProcessorAssemblyResolver.Version = 1 + i;
                Run($"Version_{PostProcessorAssemblyResolver.Version}");
            }
        }

        [Test]
        [Performance]
        public void Benchmark_PostProcessorReflectionImporter()
        {
            PostProcessorReflectionImporter.Fast = false;
            Run("Slow");

            PostProcessorReflectionImporter.Fast = true;
            Run("Fast");
        }

        void Run(string name, int warmup = 1, int measure = 6)
        {
            Measure.Method(() => RunWeaver("Library/ScriptAssemblies/Mirage.Tests.Runtime.dll")).SampleGroup($"Runtime_{name}").WarmupCount(warmup).MeasurementCount(measure).CleanUp(() => GC.Collect()).Run();
            Measure.Method(() => RunWeaver("Library/ScriptAssemblies/Mirage.Tests.Generated.Runtime.dll")).SampleGroup($"Generated_{name}").WarmupCount(warmup).MeasurementCount(measure).CleanUp(() => GC.Collect()).Run();
        }

        public void RunWeaver(string path)
        {
            Debug.Log($"Weaving {path}");

            var logger = new WeaverLogger();
            var weaver = new Mirage.Weaver.Weaver(logger);
            var assemblyBuilder = new AssemblyBuilder(path, new string[1] { "Assets/Tests/Runtime/MessagePackerTest.cs" })
            {
                referencesOptions = ReferencesOptions.UseEngineModules,
            };

            var compiledAssembly = new CompiledAssembly(path, assemblyBuilder);
            weaver.Weave(compiledAssembly);

            Assert.That(logger.Diagnostics.Count == 0, "Had errors");
            foreach (Unity.CompilationPipeline.Common.Diagnostics.DiagnosticMessage message in logger.Diagnostics)
            {
                if (message.DiagnosticType == Unity.CompilationPipeline.Common.Diagnostics.DiagnosticType.Error)
                    Debug.LogError(message.MessageData);
                else
                    Debug.LogWarning(message.MessageData);
            }
        }
    }
}
