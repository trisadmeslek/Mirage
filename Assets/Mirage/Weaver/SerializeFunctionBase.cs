using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirage.Weaver
{
    public abstract class SerializeFunctionBase
    {
        protected readonly Dictionary<TypeReference, MethodReference> funcs = new Dictionary<TypeReference, MethodReference>(new TypeReferenceComparer());
        private readonly IWeaverLogger logger;
        protected readonly ModuleDefinition module;

        public int Count => funcs.Count;

        /// <summary>
        /// Type used for logging, eg write or read
        /// </summary>
        protected abstract string FunctionTypeLog { get; }

        /// <summary>
        /// Name for const that will tell other asmdef's that type has already generated function
        /// </summary>
        protected abstract string GeneratedLabel { get; }

        protected SerializeFunctionBase(ModuleDefinition module, IWeaverLogger logger)
        {
            this.logger = logger;
            this.module = module;
        }

        public void Register(TypeReference dataType, MethodReference methodReference)
        {
            if (funcs.ContainsKey(dataType))
            {
                logger.Warning(
                    $"Registering a {FunctionTypeLog} for {dataType.FullName} when one already exists\n" +
                    $"  old:{funcs[dataType].FullName}\n" +
                    $"  new:{methodReference.FullName}",
                    methodReference.Resolve());
            }

            // we need to import type when we Initialize Writers so import here in case it is used anywhere else
            TypeReference imported = module.ImportReference(dataType);
            funcs[imported] = methodReference;

            // mark type as generated,
            MarkAsGenerated(dataType);
        }

        /// <summary>
        /// Marks type as having write/read function if it is in the current module
        /// </summary>
        private void MarkAsGenerated(TypeReference typeReference)
        {
            MarkAsGenerated(typeReference.Resolve());
        }

        /// <summary>
        /// Marks type as having write/read function if it is in the current module
        /// </summary>
        private void MarkAsGenerated(TypeDefinition typeDefinition)
        {
            // if in this module, then mark as generated
            if (typeDefinition.Module == module)
            {
                typeDefinition.SetConst(GeneratedLabel, true);
            }
        }

        /// <summary>
        /// Check if type has a write/read function generated in another module
        /// <para>returns false if type is a member of current module</para>
        /// </summary>
        private bool HasGeneratedFunctionInAnotherModule(TypeReference typeReference)
        {
            TypeDefinition def = typeReference.Resolve();
            // if type is in this module, then we want to generate new function
            if (def.Module == module)
                return false;

            return def.GetConst<bool>(GeneratedLabel);
        }

        /// <summary>
        /// Trys to get writer for type, returns null if not found
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sequencePoint"></param>
        /// <returns>found methohd or null</returns>
        public MethodReference TryGetFunction<T>(SequencePoint sequencePoint) =>
            TryGetFunction(module.ImportReference<T>(), sequencePoint);

        /// <summary>
        /// Trys to get writer for type, returns null if not found
        /// </summary>
        /// <param name="typeReference"></param>
        /// <param name="sequencePoint"></param>
        /// <returns>found methohd or null</returns>
        public MethodReference TryGetFunction(TypeReference typeReference, SequencePoint sequencePoint)
        {
            try
            {
                return GetFunction_Thorws(typeReference);
            }
            catch (SerializeFunctionException e)
            {
                logger.Error(e, sequencePoint);
                return null;
            }
        }

        /// <summary>
        /// checks if function exists for type, if it does not exist it trys to generate it
        /// </summary>
        /// <param name="typeReference"></param>
        /// <param name="sequencePoint"></param>
        /// <returns></returns>
        /// <exception cref="SerializeFunctionException">Throws if unable to find or create function</exception>
        // todo rename this to GetFunction once other classes are able to catch Exception
        public MethodReference GetFunction_Thorws(TypeReference typeReference)
        {
            // if is <T> then  just return generic write./read with T as the generic argument
            if (typeReference.IsGenericParameter)
            {
                return CreateGenericFunction(typeReference);
            }

            // check if there is already a known function for type
            // this will find extention methods within this module
            if (funcs.TryGetValue(typeReference, out MethodReference foundFunc))
            {
                return foundFunc;
            }
            else
            {
                // before generating new function, check if one was generated for type in its own module
                if (HasGeneratedFunctionInAnotherModule(typeReference))
                {
                    return CreateGenericFunction(typeReference);
                }

                return GenerateFunction(module.ImportReference(typeReference));
            }
        }



        private MethodReference GenerateFunction(TypeReference typeReference)
        {
            if (typeReference.IsByReference)
            {
                throw new SerializeFunctionException($"Cannot pass {typeReference.Name} by reference", typeReference);
            }

            // Arrays are special, if we resolve them, we get the element type,
            // eg int[] resolves to int
            // therefore process this before checks below
            if (typeReference.IsArray)
            {
                if (typeReference.IsMultidimensionalArray())
                {
                    throw new SerializeFunctionException($"{typeReference.Name} is an unsupported type. Multidimensional arrays are not supported", typeReference);
                }
                TypeReference elementType = typeReference.GetElementType();
                return GenerateCollectionFunction(typeReference, elementType, ArrayExpression);
            }

            // check for collections
            if (typeReference.Is(typeof(Nullable<>)))
            {
                var genericInstance = (GenericInstanceType)typeReference;
                TypeReference elementType = genericInstance.GenericArguments[0];

                return GenerateCollectionFunction(typeReference, elementType, NullableExpression);
            }
            if (typeReference.Is(typeof(ArraySegment<>)))
            {
                var genericInstance = (GenericInstanceType)typeReference;
                TypeReference elementType = genericInstance.GenericArguments[0];

                return GenerateCollectionFunction(typeReference, elementType, SegmentExpression);
            }
            if (typeReference.Is(typeof(List<>)))
            {
                var genericInstance = (GenericInstanceType)typeReference;
                TypeReference elementType = genericInstance.GenericArguments[0];

                return GenerateCollectionFunction(typeReference, elementType, ListExpression);
            }


            // check for invalid types
            TypeDefinition typeDefinition = typeReference.Resolve();
            if (typeDefinition == null)
            {
                throw ThrowCantGenerate(typeReference);
            }

            if (typeDefinition.IsEnum)
            {
                // serialize enum as their base type
                return GenerateEnumFunction(typeReference);
            }

            if (typeDefinition.IsDerivedFrom<NetworkBehaviour>())
            {
                return GetNetworkBehaviourFunction(typeReference);
            }

            // unity base types are invalid
            if (typeDefinition.IsDerivedFrom<UnityEngine.Component>())
            {
                throw ThrowCantGenerate(typeReference, "component type");
            }
            if (typeReference.Is<UnityEngine.Object>())
            {
                throw ThrowCantGenerate(typeReference);
            }
            if (typeReference.Is<UnityEngine.ScriptableObject>())
            {
                throw ThrowCantGenerate(typeReference);
            }

            // if it is genericInstance, then we can generate writer for it
            if (!typeReference.IsGenericInstance && typeDefinition.HasGenericParameters)
            {
                throw ThrowCantGenerate(typeReference, "generic type");
            }
            if (typeDefinition.IsInterface)
            {
                throw ThrowCantGenerate(typeReference, "interface");
            }
            if (typeDefinition.IsAbstract)
            {
                throw ThrowCantGenerate(typeReference, "abstract class");
            }

            // generate writer for class/struct 
            MethodReference generated = GenerateClassOrStructFunction(typeReference);
            MarkAsGenerated(typeDefinition);

            return generated;
        }

        SerializeFunctionException ThrowCantGenerate(TypeReference typeReference, string typeDescription = null)
        {
            string reasonStr = string.IsNullOrEmpty(typeDescription) ? string.Empty : $"{typeDescription} ";
            return new SerializeFunctionException($"Cannot generate {FunctionTypeLog} for {reasonStr}{typeReference.Name}. Use a supported type or provide a custom {FunctionTypeLog}", typeReference);
        }

        /// <summary>
        /// Creates Generic instance for Write{T} or Read{T} with <paramref name="argument"/> as then generic argument
        /// <para>Can also create Write{int} if real type is given instead of generic argument</para>
        /// </summary>
        /// <param name="argument"></param>
        /// <returns></returns>
        private GenericInstanceMethod CreateGenericFunction(TypeReference argument)
        {
            MethodReference method = GetGenericFunction();

            var generic = new GenericInstanceMethod(method);
            generic.GenericArguments.Add(argument);

            return generic;
        }

        /// <summary>
        /// Gets generic Write{T} or Read{T}
        /// </summary>
        /// <returns></returns>
        protected abstract MethodReference GetGenericFunction();

        protected abstract MethodReference GetNetworkBehaviourFunction(TypeReference typeReference);

        protected abstract MethodReference GenerateEnumFunction(TypeReference typeReference);
        protected abstract MethodReference GenerateCollectionFunction(TypeReference typeReference, TypeReference elementType, Expression<Action> genericExpression);

        protected abstract Expression<Action> ArrayExpression { get; }
        protected abstract Expression<Action> ListExpression { get; }
        protected abstract Expression<Action> SegmentExpression { get; }
        protected abstract Expression<Action> NullableExpression { get; }

        protected abstract MethodReference GenerateClassOrStructFunction(TypeReference typeReference);
    }
}
