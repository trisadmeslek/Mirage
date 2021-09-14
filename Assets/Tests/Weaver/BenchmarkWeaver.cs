using System;
using Mirage.Weaver;
using Mono.Cecil;
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
        public void _Benchmark()
        {
            Run("Default");
        }

        [Test]
        [Performance]
        public void _Benchmark_RunOnce()
        {
            Run("Default", 0, 1);
        }

        [TearDown]
        public void TearDown()
        {
            FieldReferenceComparator.Fast = false;
            PostProcessorAssemblyResolver.Version = 0;
            PostProcessorReflectionImporter.Fast = true;// default true
            Extensions.Fast_IsDerivedFrom = false;
            Extensions.Fast_TryResolve = false;
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

        [Test]
        [Performance]
        public void Benchmark_IsDerivedFrom()
        {
            Extensions.Fast_IsDerivedFrom = false;
            Run("Slow");

            Extensions.Fast_IsDerivedFrom = true;
            Run("Fast");
        }

        [Test]
        [Performance]
        public void Benchmark_TryResolve()
        {
            Extensions.Fast_TryResolve = false;
            Run("Slow");

            Extensions.Fast_TryResolve = true;
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


        static AssemblyDefinition LoadAssembly(string path)
        {
            string fullPath = $"Library/ScriptAssemblies/{path}.dll";
            var assemblyBuilder = new AssemblyBuilder(fullPath, new string[1] { "Assets/Tests/Runtime/MessagePackerTest.cs" })
            {
                referencesOptions = ReferencesOptions.UseEngineModules,
            };

            var compiledAssembly = new CompiledAssembly(fullPath, assemblyBuilder);

            return Mirage.Weaver.Weaver.AssemblyDefinitionFor(compiledAssembly);
        }
        void RunOnEachType(string path, string name, Action<TypeDefinition> action)
        {
            Measure.Method(() =>
            {
                Mono.Collections.Generic.Collection<TypeDefinition> types = OnEach_Assembly.MainModule.Types;
                foreach (TypeDefinition type in types)
                {
                    action.Invoke(type);
                }
            }).SetUp(() =>
            {
                OnEach_Assembly = LoadAssembly(path);
            }).CleanUp(() =>
            {
                OnEach_Assembly?.Dispose();
                OnEach_Assembly = null;
                GC.Collect();
            })
            .SampleGroup($"{name}").WarmupCount(0).MeasurementCount(10).Run();
        }
        AssemblyDefinition OnEach_Assembly;

        [Test]
        [Performance]
        public void OnEach_IsDerivedFrom()
        {
            RunOnEachType("Mirage.Tests.Runtime", "IsDerivedFrom", td => td.IsDerivedFrom<NetworkBehaviour>());
            RunOnEachType("Mirage.Tests.Runtime", "IsNetworkBehaviour", td => IsNetworkBehaviour(td));
            RunOnEachType("Mirage.Tests.Generated.Runtime", "IsDerivedFrom", td => td.IsDerivedFrom<NetworkBehaviour>());
            RunOnEachType("Mirage.Tests.Generated.Runtime", "IsNetworkBehaviour", td => IsNetworkBehaviour(td));
        }

        static bool IsNetworkBehaviour(TypeDefinition td)
        {
            if (!td.IsClass) { return false; }

            TypeReference parent = td.BaseType;
            while (parent != null)
            {
                if (parent.Is<NetworkBehaviour>())
                {
                    return true;
                }

                parent = TryResolve(parent)?.BaseType;
            }
            return false;
        }
        static TypeDefinition TryResolve(TypeReference typeReference)
        {
            try
            {
                return typeReference.Resolve();
            }
            catch
            {
                return null;
            }
        }
    }
}
