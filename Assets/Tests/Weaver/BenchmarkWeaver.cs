using Mirage.Weaver;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor.Compilation;

namespace Mirage.Tests.Weaver
{
    public class BenchmarkWeaver
    {
        [Test]
        [Performance]
        public void Benchmark()
        {
            Measure.Method(RunWeaver)
               .WarmupCount(1)
               .MeasurementCount(10)
               .Run();
        }

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
        }
    }
}
