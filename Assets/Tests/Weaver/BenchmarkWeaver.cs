using System;
using Mirage.Weaver;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Measurements;
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
            Run(Measure.Method(RunWeaver));
        }


        void Run(MethodMeasurement method) => method.WarmupCount(1).MeasurementCount(10).CleanUp(() => GC.Collect()).Run();

        public void RunWeaver()
        {
            var logger = new WeaverLogger();
            var weaver = new Mirage.Weaver.Weaver(logger);
            string path = "Library/ScriptAssemblies/Mirage.Tests.Runtime.dll";
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
