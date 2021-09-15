using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Mirage.Weaver
{
    // todo add docs for what this type does
    public class PropertySiteProcessor
    {
        // setter functions that replace [SyncVar] member variable references. dict<field, replacement>
        public Dictionary<FieldReference, MethodDefinition> Setters = new Dictionary<FieldReference, MethodDefinition>(new FieldReferenceComparator());
        // getter functions that replace [SyncVar] member variable references. dict<field, replacement>
        public Dictionary<FieldReference, MethodDefinition> Getters = new Dictionary<FieldReference, MethodDefinition>(new FieldReferenceComparator());

        [System.Obsolete("Called inside AllInstructionsChecker instead", true)]
        public void Process(ModuleDefinition moduleDef)
        {
            // replace all field access with property access for syncvars
            //CodePass.ForEachInstruction(moduleDef, WeavedMethods, ProcessInstruction);
        }

        private static bool WeavedMethods(MethodDefinition md) =>
                        md.Name != ".cctor" &&
                        md.Name != NetworkBehaviourProcessor.ProcessedFunctionName &&
                        !md.Name.StartsWith(RpcProcessor.InvokeRpcPrefix) &&
                        !md.IsConstructor;

        // replaces syncvar write access with the NetworkXYZ.get property calls
        void ProcessInstructionSetterField(Instruction i, FieldReference opField)
        {
            // does it set a field that we replaced?
            if (Setters.TryGetValue(opField, out MethodDefinition replacement))
            {
                if (opField.DeclaringType.IsGenericInstance || opField.DeclaringType.HasGenericParameters) // We're calling to a generic class
                {
                    var genericType = (GenericInstanceType)opField.DeclaringType;
                    i.OpCode = OpCodes.Callvirt;
                    i.Operand = replacement.MakeHostInstanceGeneric(genericType);
                }
                else
                {
                    //replace with property
                    i.OpCode = OpCodes.Call;
                    i.Operand = replacement;
                }
            }
        }

        // replaces syncvar read access with the NetworkXYZ.get property calls
        void ProcessInstructionGetterField(Instruction i, FieldReference opField)
        {
            // does it set a field that we replaced?
            if (Getters.TryGetValue(opField, out MethodDefinition replacement))
            {
                if (opField.DeclaringType.IsGenericInstance || opField.DeclaringType.HasGenericParameters) // We're calling to a generic class
                {
                    var genericType = (GenericInstanceType)opField.DeclaringType;
                    i.OpCode = OpCodes.Callvirt;
                    i.Operand = replacement.MakeHostInstanceGeneric(genericType);
                }
                else
                {
                    //replace with property
                    i.OpCode = OpCodes.Call;
                    i.Operand = replacement;
                }
            }
        }

        public void ProcessInstruction(MethodDefinition md, ref Instruction instr)
        {
            if (instr.OpCode == OpCodes.Stfld && instr.Operand is FieldReference opFieldst)
            {
                // this instruction sets the value of a field. cache the field reference.
                ProcessInstructionSetterField(instr, opFieldst);
            }

            if (instr.OpCode == OpCodes.Ldfld && instr.Operand is FieldReference opFieldld)
            {
                // this instruction gets the value of a field. cache the field reference.
                ProcessInstructionGetterField(instr, opFieldld);
            }

            if (instr.OpCode == OpCodes.Ldflda && instr.Operand is FieldReference opFieldlda)
            {
                // loading a field by reference,  watch out for initobj instruction
                // see https://github.com/vis2k/Mirror/issues/696
                ProcessInstructionLoadAddress(md, ref instr, opFieldlda);
            }
        }

        void ProcessInstructionLoadAddress(MethodDefinition md, ref Instruction instr, FieldReference opField)
        {
            // does it set a field that we replaced?
            if (Setters.TryGetValue(opField, out MethodDefinition replacement))
            {
                // we have a replacement for this property
                // is the next instruction a initobj?
                Instruction nextInstr = instr.Next;

                if (nextInstr.OpCode == OpCodes.Initobj)
                {
                    // we need to replace this code with:
                    //     var tmp = new MyStruct();
                    //     this.set_Networkxxxx(tmp);
                    ILProcessor worker = md.Body.GetILProcessor();
                    VariableDefinition tmpVariable = md.AddLocal(opField.FieldType);

                    worker.InsertBefore(instr, worker.Create(OpCodes.Ldloca, tmpVariable));
                    worker.InsertBefore(instr, worker.Create(OpCodes.Initobj, opField.FieldType));
                    worker.InsertBefore(instr, worker.Create(OpCodes.Ldloc, tmpVariable));
                    Instruction newInstr = worker.Create(OpCodes.Call, replacement);
                    worker.InsertBefore(instr, newInstr);

                    worker.Remove(instr);
                    worker.Remove(nextInstr);

                    instr = newInstr;
                }
            }
        }
    }
}
