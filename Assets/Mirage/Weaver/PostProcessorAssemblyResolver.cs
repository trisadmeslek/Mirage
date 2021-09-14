using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Mono.Cecil;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Mirage.Weaver
{
    public interface IMyAssemblyResolver : IAssemblyResolver
    {
        void AddAssemblyDefinitionBeingOperatedOn(AssemblyDefinition assemblyDefinition);
    }
    class PostProcessorAssemblyResolver
    {
        public static int Version;
        public static IMyAssemblyResolver GetNew(ICompiledAssembly compiledAssembly)
        {
            switch (Version)
            {
                default:
                case 1: return new PostProcessorAssemblyResolver_v1(compiledAssembly);
                case 2: return new PostProcessorAssemblyResolver_v2(compiledAssembly);
                case 3: return new PostProcessorAssemblyResolver_v3(compiledAssembly, false);
                case 4: return new PostProcessorAssemblyResolver_v3(compiledAssembly, true);
            }
        }
    }

    class PostProcessorAssemblyResolver_v1 : IAssemblyResolver, IMyAssemblyResolver
    {
        private readonly string[] _assemblyReferences;
        private readonly string[] _assemblyReferencesFileName;
        private readonly Dictionary<string, AssemblyDefinition> _assemblyCache = new Dictionary<string, AssemblyDefinition>();
        private readonly ICompiledAssembly _compiledAssembly;
        private AssemblyDefinition _selfAssembly;

        public PostProcessorAssemblyResolver_v1(ICompiledAssembly compiledAssembly)
        {
            _compiledAssembly = compiledAssembly;
            _assemblyReferences = compiledAssembly.References;
            // cache paths here so we dont need to call it each time we resolve
            _assemblyReferencesFileName = _assemblyReferences.Select(r => Path.GetFileName(r)).ToArray();
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Cleanup
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters(ReadingMode.Deferred));

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            lock (_assemblyCache)
            {
                if (name.Name == _compiledAssembly.Name)
                    return _selfAssembly;

                string fileName = FindFile(name);
                if (fileName == null)
                    return null;

                DateTime lastWriteTime = File.GetLastWriteTime(fileName);

                string cacheKey = fileName + lastWriteTime;

                if (_assemblyCache.TryGetValue(cacheKey, out AssemblyDefinition result))
                    return result;

                parameters.AssemblyResolver = this;

                MemoryStream ms = MemoryStreamFor(fileName);

                string pdb = fileName + ".pdb";
                if (File.Exists(pdb))
                    parameters.SymbolStream = MemoryStreamFor(pdb);

                var assemblyDefinition = AssemblyDefinition.ReadAssembly(ms, parameters);
                _assemblyCache.Add(cacheKey, assemblyDefinition);
                return assemblyDefinition;
            }
        }

        private string FindFile(AssemblyNameReference name)
        {
            // This method is called a lot, avoid linq

            // first pass, check if we can find dll or exe file
            string dllName = name.Name + ".dll";
            string exeName = name.Name + ".exe";
            for (int i = 0; i < _assemblyReferencesFileName.Length; i++)
            {
                // if filename matches, return full path
                string fileName = _assemblyReferencesFileName[i];
                if (fileName == dllName || fileName == exeName)
                    return _assemblyReferences[i];
            }

            // second pass (only run if first fails), 

            //Unfortunately the current ICompiledAssembly API only provides direct references.
            //It is very much possible that a postprocessor ends up investigating a type in a directly
            //referenced assembly, that contains a field that is not in a directly referenced assembly.
            //if we don't do anything special for that situation, it will fail to resolve.  We should fix this
            //in the ILPostProcessing API. As a workaround, we rely on the fact here that the indirect references
            //are always located next to direct references, so we search in all directories of direct references we
            //got passed, and if we find the file in there, we resolve to it.
            IEnumerable<string> allParentDirectories = _assemblyReferences.Select(Path.GetDirectoryName).Distinct();
            foreach (string parentDir in allParentDirectories)
            {
                string candidate = Path.Combine(parentDir, name.Name + ".dll");
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        static MemoryStream MemoryStreamFor(string fileName)
        {
            return Retry(10, TimeSpan.FromSeconds(1), () =>
            {
                byte[] byteArray;
                using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byteArray = new byte[fs.Length];
                    int readLength = fs.Read(byteArray, 0, (int)fs.Length);
                    if (readLength != fs.Length)
                        throw new InvalidOperationException("File read length is not full length of file.");
                }

                return new MemoryStream(byteArray);
            });
        }

        private static MemoryStream Retry(int retryCount, TimeSpan waitTime, Func<MemoryStream> func)
        {
            try
            {
                return func();
            }
            catch (IOException)
            {
                if (retryCount == 0)
                    throw;
                Console.WriteLine($"Caught IO Exception, trying {retryCount} more times");
                Thread.Sleep(waitTime);
                return Retry(retryCount - 1, waitTime, func);
            }
        }

        public void AddAssemblyDefinitionBeingOperatedOn(AssemblyDefinition assemblyDefinition)
        {
            _selfAssembly = assemblyDefinition;
        }
    }
    class PostProcessorAssemblyResolver_v2 : IAssemblyResolver, IMyAssemblyResolver
    {
        private readonly string[] _assemblyReferences;
        private readonly string[] _assemblyReferencesFileName;
        private readonly Dictionary<string, AssemblyDefinition> _assemblyCache = new Dictionary<string, AssemblyDefinition>();
        private readonly ICompiledAssembly _compiledAssembly;
        private AssemblyDefinition _selfAssembly;

        public PostProcessorAssemblyResolver_v2(ICompiledAssembly compiledAssembly)
        {
            _compiledAssembly = compiledAssembly;
            _assemblyReferences = compiledAssembly.References;
            // cache paths here so we dont need to call it each time we resolve
            _assemblyReferencesFileName = _assemblyReferences.Select(r => Path.GetFileName(r)).ToArray();
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Cleanup
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters(ReadingMode.Deferred));

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            if (name.Name == _compiledAssembly.Name)
                return _selfAssembly;

            lock (_assemblyCache)
            {
                if (GetFileNameAndKey(name, out string fileName, out string cacheKey))

                    if (_assemblyCache.TryGetValue(cacheKey, out AssemblyDefinition result))
                        return result;

                parameters.AssemblyResolver = this;

                MemoryStream ms = MemoryStreamFor(fileName);

                string pdb = fileName + ".pdb";
                if (File.Exists(pdb))
                    parameters.SymbolStream = MemoryStreamFor(pdb);

                var assemblyDefinition = AssemblyDefinition.ReadAssembly(ms, parameters);
                _assemblyCache.Add(cacheKey, assemblyDefinition);
                return assemblyDefinition;
            }
        }

        private bool GetFileNameAndKey(AssemblyNameReference name, out string fileName, out string cacheKey)
        {
            fileName = FindFile(name);
            cacheKey = null;
            if (fileName == null)
                return false;

            DateTime lastWriteTime = File.GetLastWriteTime(fileName);
            cacheKey = cacheKey = fileName + lastWriteTime;
            return true;
        }

        private string FindFile(AssemblyNameReference name)
        {
            // This method is called a lot, avoid linq

            // first pass, check if we can find dll or exe file
            string dllName = name.Name + ".dll";
            string exeName = name.Name + ".exe";
            for (int i = 0; i < _assemblyReferencesFileName.Length; i++)
            {
                // if filename matches, return full path
                string fileName = _assemblyReferencesFileName[i];
                if (fileName == dllName || fileName == exeName)
                    return _assemblyReferences[i];
            }

            // second pass (only run if first fails), 

            //Unfortunately the current ICompiledAssembly API only provides direct references.
            //It is very much possible that a postprocessor ends up investigating a type in a directly
            //referenced assembly, that contains a field that is not in a directly referenced assembly.
            //if we don't do anything special for that situation, it will fail to resolve.  We should fix this
            //in the ILPostProcessing API. As a workaround, we rely on the fact here that the indirect references
            //are always located next to direct references, so we search in all directories of direct references we
            //got passed, and if we find the file in there, we resolve to it.
            IEnumerable<string> allParentDirectories = _assemblyReferences.Select(Path.GetDirectoryName).Distinct();
            foreach (string parentDir in allParentDirectories)
            {
                string candidate = Path.Combine(parentDir, name.Name + ".dll");
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        static MemoryStream MemoryStreamFor(string fileName)
        {
            return Retry(10, TimeSpan.FromSeconds(1), () =>
            {
                byte[] byteArray;
                using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byteArray = new byte[fs.Length];
                    int readLength = fs.Read(byteArray, 0, (int)fs.Length);
                    if (readLength != fs.Length)
                        throw new InvalidOperationException("File read length is not full length of file.");
                }

                return new MemoryStream(byteArray);
            });
        }

        private static MemoryStream Retry(int retryCount, TimeSpan waitTime, Func<MemoryStream> func)
        {
            try
            {
                return func();
            }
            catch (IOException)
            {
                if (retryCount == 0)
                    throw;
                Console.WriteLine($"Caught IO Exception, trying {retryCount} more times");
                Thread.Sleep(waitTime);
                return Retry(retryCount - 1, waitTime, func);
            }
        }

        public void AddAssemblyDefinitionBeingOperatedOn(AssemblyDefinition assemblyDefinition)
        {
            _selfAssembly = assemblyDefinition;
        }
    }
    class PostProcessorAssemblyResolver_v3 : IAssemblyResolver, IMyAssemblyResolver
    {
        private readonly string[] _assemblyReferences;
        private readonly string[] _assemblyReferencesFileName;
        private readonly Dictionary<int, AssemblyDefinition> _assemblyCache = new Dictionary<int, AssemblyDefinition>();
        private readonly ICompiledAssembly _compiledAssembly;
        private AssemblyDefinition _selfAssembly;
        readonly bool logs;


        public PostProcessorAssemblyResolver_v3(ICompiledAssembly compiledAssembly, bool logs)
        {
            this.logs = logs;
            _compiledAssembly = compiledAssembly;
            _assemblyReferences = compiledAssembly.References;
            // cache paths here so we dont need to call it each time we resolve
            _assemblyReferencesFileName = _assemblyReferences.Select(r => Path.GetFileName(r)).ToArray();
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Cleanup
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters(ReadingMode.Deferred));

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            if (name.Name == _compiledAssembly.Name)
                return _selfAssembly;


            lock (_assemblyCache)
            {
                if (!GetFileNameAndKey(name, out string fileName, out int cacheKey))
                    return null;

                if (_assemblyCache.TryGetValue(cacheKey, out AssemblyDefinition result))
                {
                    if (logs)
                        Console.WriteLine($"[PostProcessorAssemblyResolver] Found {cacheKey}");
                    return result;
                }
                else
                {
                    if (logs)
                        Console.WriteLine($"[PostProcessorAssemblyResolver] NotFound {cacheKey} {name.FullName}");
                }

                AssemblyDefinition assemblyDefinition = ReadNewAssembly(parameters, fileName);
                _assemblyCache.Add(cacheKey, assemblyDefinition);
                return assemblyDefinition;
            }
        }

        private bool GetFileNameAndKey(AssemblyNameReference name, out string fileName, out int cacheKey)
        {
            fileName = FindFile(name);
            cacheKey = 0;
            if (fileName == null)
                return false;

            DateTime lastWriteTime = File.GetLastWriteTime(fileName);
            cacheKey = HashCodeHelper.GetCombineHash(fileName, lastWriteTime);
            return true;
        }

        private string FindFile(AssemblyNameReference name)
        {
            // This method is called a lot, avoid linq

            // first pass, check if we can find dll or exe file
            string dllName = name.Name + ".dll";
            string exeName = name.Name + ".exe";
            for (int i = 0; i < _assemblyReferencesFileName.Length; i++)
            {
                // if filename matches, return full path
                string fileName = _assemblyReferencesFileName[i];
                if (fileName == dllName || fileName == exeName)
                    return _assemblyReferences[i];
            }

            // second pass (only run if first fails), 

            //Unfortunately the current ICompiledAssembly API only provides direct references.
            //It is very much possible that a postprocessor ends up investigating a type in a directly
            //referenced assembly, that contains a field that is not in a directly referenced assembly.
            //if we don't do anything special for that situation, it will fail to resolve.  We should fix this
            //in the ILPostProcessing API. As a workaround, we rely on the fact here that the indirect references
            //are always located next to direct references, so we search in all directories of direct references we
            //got passed, and if we find the file in there, we resolve to it.
            IEnumerable<string> allParentDirectories = _assemblyReferences.Select(Path.GetDirectoryName).Distinct();
            foreach (string parentDir in allParentDirectories)
            {
                string candidate = Path.Combine(parentDir, name.Name + ".dll");
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private AssemblyDefinition ReadNewAssembly(ReaderParameters parameters, string fileName)
        {
            parameters.AssemblyResolver = this;

            MemoryStream ms = MemoryStreamFor(fileName);

            string pdb = fileName + ".pdb";
            if (File.Exists(pdb))
                parameters.SymbolStream = MemoryStreamFor(pdb);

            var assemblyDefinition = AssemblyDefinition.ReadAssembly(ms, parameters);
            return assemblyDefinition;
        }

        static MemoryStream MemoryStreamFor(string fileName)
        {
            return Retry(10, TimeSpan.FromSeconds(1), () =>
            {
                byte[] byteArray;
                using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byteArray = new byte[fs.Length];
                    int readLength = fs.Read(byteArray, 0, (int)fs.Length);
                    if (readLength != fs.Length)
                        throw new InvalidOperationException("File read length is not full length of file.");
                }

                return new MemoryStream(byteArray);
            });
        }

        private static MemoryStream Retry(int retryCount, TimeSpan waitTime, Func<MemoryStream> func)
        {
            try
            {
                return func();
            }
            catch (IOException)
            {
                if (retryCount == 0)
                    throw;
                Console.WriteLine($"Caught IO Exception, trying {retryCount} more times");
                Thread.Sleep(waitTime);
                return Retry(retryCount - 1, waitTime, func);
            }
        }

        public void AddAssemblyDefinitionBeingOperatedOn(AssemblyDefinition assemblyDefinition)
        {
            _selfAssembly = assemblyDefinition;
        }
    }
}
