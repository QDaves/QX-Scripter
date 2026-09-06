using System.Globalization;
using System.Text;
using Flazzy.ABC;
using Flazzy.ABC.AVM2.Instructions;

namespace Qx.Headers.Flash;

static class Avm2ReadableCode
{
    public static string Render(
        ASMethod method,
        IReadOnlyList<ASInstruction> code,
        IReadOnlyList<Avm2InstructionInventory> instructions,
        Avm2ControlFlowInventory control_flow)
    {
        var output = new StringBuilder();
        var edges = control_flow.Edges
            .GroupBy(edge => edge.FromBlock)
            .ToDictionary(group => group.Key, group => group.ToList());
        foreach (Avm2BasicBlockInventory block in control_flow.Blocks)
        {
            output.Append("block b").Append(block.Id)
                .Append(" [").Append(block.StartOffset.ToString("x4", CultureInfo.InvariantCulture))
                .Append("..").Append(block.EndOffset.ToString("x4", CultureInfo.InvariantCulture))
                .Append("] stack ").Append(Depth(block.EntryStackDepth))
                .Append(" -> ").Append(Depth(block.ExitStackDepth))
                .Append(" scope ").Append(Depth(block.EntryScopeDepth))
                .Append(" -> ").Append(Depth(block.ExitScopeDepth));
            if (!block.Reachable)
                output.Append(" unreachable");
            output.AppendLine();

            int input_depth = block.EntryStackDepth.GetValueOrDefault();
            var stack = new List<string>(Math.Max(input_depth, 4));
            for (int index = 0; index < input_depth; index++)
                stack.Add($"stack_b{block.Id}_{index}");

            for (int instruction_index = block.FirstInstruction;
                instruction_index <= block.LastInstruction;
                instruction_index++)
            {
                ASInstruction instruction = code[instruction_index];
                Avm2InstructionInventory inventory = instructions[instruction_index];
                string text = RenderInstruction(
                    method,
                    instruction,
                    inventory,
                    stack,
                    edges.GetValueOrDefault(block.Id) ?? []);
                output.Append("  ")
                    .Append(inventory.Offset.ToString("x4", CultureInfo.InvariantCulture))
                    .Append("  ")
                    .AppendLine(text);
            }
            output.AppendLine();
        }
        return output.ToString();
    }

    static string RenderInstruction(
        ASMethod method,
        ASInstruction instruction,
        Avm2InstructionInventory inventory,
        List<string> stack,
        IReadOnlyList<Avm2ControlFlowEdgeInventory> edges)
    {
        if (instruction is Primitive primitive)
        {
            string value = Avm2MethodAnalyzer.LiteralText(primitive.Value);
            stack.Add(value);
            return $"push {value}";
        }
        if (instruction.OP == OPCode.PushUndefined)
        {
            stack.Add("undefined");
            return "push undefined";
        }
        if (instruction is Local local)
            return RenderLocal(method, local, stack);
        if (instruction is Jumper jumper)
            return RenderBranch(jumper, stack, edges);
        if (instruction is LookUpSwitchIns)
        {
            string selector = Pop(stack, inventory.Offset);
            string targets = string.Join(", ", edges
                .Where(edge => edge.Kind is "Case" or "Default")
                .Select(edge => edge.Kind == "Case"
                    ? $"{edge.CaseIndex}: {Block(edge)}"
                    : $"default: {Block(edge)}"));
            return $"switch ({selector}) {{ {targets} }}";
        }

        switch (instruction)
        {
            case GetLexIns lexical:
            {
                string value = Avm2MethodAnalyzer.Qualified(lexical.TypeName);
                stack.Add(value);
                return $"push lexical {value}";
            }
            case FindPropertyIns property when IsStatic(property.PropertyName):
            {
                string value = $"scope({Avm2MethodAnalyzer.Qualified(property.PropertyName)})";
                stack.Add(value);
                return $"push {value}";
            }
            case FindPropStrictIns property when IsStatic(property.PropertyName):
            {
                string value = $"scope_strict({Avm2MethodAnalyzer.Qualified(property.PropertyName)})";
                stack.Add(value);
                return $"push {value}";
            }
            case GetPropertyIns property when IsStatic(property.PropertyName):
            {
                string receiver = Pop(stack, inventory.Offset);
                string value = $"{receiver}.{Name(property.PropertyName)}";
                stack.Add(value);
                return $"push {value}";
            }
            case GetSuperIns property when IsStatic(property.PropertyName):
            {
                string receiver = Pop(stack, inventory.Offset);
                string value = $"super({receiver}).{Name(property.PropertyName)}";
                stack.Add(value);
                return $"push {value}";
            }
            case GetDescendantsIns descendants when IsStatic(descendants.Descendant):
            {
                string receiver = Pop(stack, inventory.Offset);
                string value = $"{receiver}..{Name(descendants.Descendant)}";
                stack.Add(value);
                return $"push {value}";
            }
            case SetPropertyIns property when IsStatic(property.PropertyName):
            {
                string value = Pop(stack, inventory.Offset);
                string receiver = Pop(stack, inventory.Offset);
                return $"{receiver}.{Name(property.PropertyName)} = {value}";
            }
            case InitPropertyIns property when IsStatic(property.PropertyName):
            {
                string value = Pop(stack, inventory.Offset);
                string receiver = Pop(stack, inventory.Offset);
                return $"init {receiver}.{Name(property.PropertyName)} = {value}";
            }
            case SetSuperIns property when IsStatic(property.PropertyName):
            {
                string value = Pop(stack, inventory.Offset);
                string receiver = Pop(stack, inventory.Offset);
                return $"super({receiver}).{Name(property.PropertyName)} = {value}";
            }
            case DeletePropertyIns property when IsStatic(property.PropertyName):
            {
                string receiver = Pop(stack, inventory.Offset);
                string value = $"delete {receiver}.{Name(property.PropertyName)}";
                stack.Add(value);
                return $"push {value}";
            }
            case CallPropertyIns call when IsStatic(call.PropertyName):
                return RenderPropertyCall(call.PropertyName, call.ArgCount, true, false, stack, inventory.Offset);
            case CallPropLexIns call when IsStatic(call.PropertyName):
                return RenderLexicalPropertyCall(call.PropertyName, call.ArgCount, stack, inventory.Offset);
            case CallPropVoidIns call when IsStatic(call.PropertyName):
                return RenderPropertyCall(call.PropertyName, call.ArgCount, false, false, stack, inventory.Offset);
            case CallSuperIns call when IsStatic(call.MethodName):
                return RenderPropertyCall(call.MethodName, call.ArgCount, true, true, stack, inventory.Offset);
            case CallSuperVoidIns call when IsStatic(call.MethodName):
                return RenderPropertyCall(call.MethodName, call.ArgCount, false, true, stack, inventory.Offset);
            case CallStaticIns call:
                return RenderIndexedCall($"method#{call.MethodIndex}", call.ArgCount, true, stack, inventory.Offset);
            case CallMethodIns call:
                return RenderIndexedCall($"dispatch#{call.MethodIndex}", call.ArgCount, true, stack, inventory.Offset);
            case CallIns call:
                return RenderDynamicCall(call.ArgCount, stack, inventory.Offset);
            case ConstructPropIns construct when IsStatic(construct.PropertyName):
                return RenderConstructProperty(construct, stack, inventory.Offset);
            case ConstructIns construct:
                return RenderConstruct(construct.ArgCount, stack, inventory.Offset);
            case ConstructSuperIns construct:
            {
                List<string> arguments = PopArguments(stack, construct.ArgCount, inventory.Offset);
                string receiver = Pop(stack, inventory.Offset);
                return $"super({receiver})({string.Join(", ", arguments)})";
            }
            case NewArrayIns array:
            {
                List<string> values = PopArguments(stack, array.ArgCount, inventory.Offset);
                string value = $"[{string.Join(", ", values)}]";
                stack.Add(value);
                return $"push {value}";
            }
            case NewObjectIns value:
            {
                List<string> values = PopArguments(stack, value.ArgCount * 2, inventory.Offset);
                var pairs = new List<string>(value.ArgCount);
                for (int index = 0; index + 1 < values.Count; index += 2)
                    pairs.Add($"{values[index]}: {values[index + 1]}");
                string expression = $"{{ {string.Join(", ", pairs)} }}";
                stack.Add(expression);
                return $"push {expression}";
            }
            case NewClassIns value:
            {
                string base_type = Pop(stack, inventory.Offset);
                string expression = $"class {ClassName(value)} extends {base_type}";
                stack.Add(expression);
                return $"push {expression}";
            }
            case NewFunctionIns value:
            {
                string expression = $"function#{value.MethodIndex}";
                stack.Add(expression);
                return $"push {expression}";
            }
            case GetSlotIns slot:
            {
                string receiver = Pop(stack, inventory.Offset);
                string value = $"{receiver}.slot#{slot.SlotIndex}";
                stack.Add(value);
                return $"push {value}";
            }
            case SetSlotIns slot:
            {
                string value = Pop(stack, inventory.Offset);
                string receiver = Pop(stack, inventory.Offset);
                return $"{receiver}.slot#{slot.SlotIndex} = {value}";
            }
            case GetGlobalScopeIns:
                stack.Add("global_scope");
                return "push global_scope";
            case GetScopeObjectIns scope:
            {
                string value = $"local_scope[{scope.ScopeIndex}]";
                stack.Add(value);
                return $"push {value}";
            }
            case GetOuterScopeIns scope:
            {
                string value = $"declaring_scope[{scope.ScopeIndex}]";
                stack.Add(value);
                return $"push {value}";
            }
            case PushScopeIns:
            {
                string value = Pop(stack, inventory.Offset);
                return $"scope.push({value})";
            }
            case PushWithIns:
            {
                string value = Pop(stack, inventory.Offset);
                return $"scope.with({value})";
            }
            case PopScopeIns:
                return "scope.pop()";
            case DupIns:
            {
                string value = Pop(stack, inventory.Offset);
                stack.Add(value);
                stack.Add(value);
                return $"dup {value}";
            }
            case SwapIns:
            {
                string right = Pop(stack, inventory.Offset);
                string left = Pop(stack, inventory.Offset);
                stack.Add(right);
                stack.Add(left);
                return $"swap {left}, {right}";
            }
            case PopIns:
                return $"discard {Pop(stack, inventory.Offset)}";
            case ReturnValueIns:
                return $"return {Pop(stack, inventory.Offset)}";
            case ReturnVoidIns:
                return "return";
            case ThrowIns:
                return $"throw {Pop(stack, inventory.Offset)}";
        }

        string? binary_operator = BinaryOperator(instruction.OP);
        if (binary_operator is not null)
        {
            string right = Pop(stack, inventory.Offset);
            string left = Pop(stack, inventory.Offset);
            string value = $"({left} {binary_operator} {right})";
            stack.Add(value);
            return $"push {value}";
        }

        string? unary_operator = UnaryOperator(instruction.OP);
        if (unary_operator is not null)
        {
            string operand = Pop(stack, inventory.Offset);
            string value = unary_operator.Contains("{0}", StringComparison.Ordinal)
                ? string.Format(CultureInfo.InvariantCulture, unary_operator, operand)
                : $"({unary_operator}{operand})";
            stack.Add(value);
            return $"push {value}";
        }

        if (instruction is CoerceIns coerce)
            return RenderConversion($"coerce<{Avm2MethodAnalyzer.Qualified(coerce.TypeName)}>", stack, inventory.Offset);
        if (instruction is AsTypeIns as_type)
            return RenderConversion($"as<{Avm2MethodAnalyzer.Qualified(as_type.TypeName)}>", stack, inventory.Offset);
        if (instruction is ApplyTypeIns apply)
        {
            List<string> types = PopArguments(stack, apply.ParamCount, inventory.Offset);
            string target = Pop(stack, inventory.Offset);
            string value = $"{target}.<{string.Join(", ", types)}>";
            stack.Add(value);
            return $"push {value}";
        }

        return RenderGeneric(instruction, inventory, stack);
    }

    static string RenderLocal(ASMethod method, Local local, List<string> stack)
    {
        string name = LocalName(method, local.Register);
        if (Local.IsGetLocal(local.OP))
        {
            stack.Add(name);
            return $"push {name}";
        }
        if (Local.IsSetLocal(local.OP))
            return $"{name} = {Pop(stack, -1)}";
        return local.OP switch
        {
            OPCode.IncLocal or OPCode.IncLocal_i => $"{name}++",
            OPCode.DecLocal or OPCode.DecLocal_i => $"{name}--",
            OPCode.Kill => $"kill {name}",
            _ => $"{local.OP.ToString().ToLowerInvariant()} {name}"
        };
    }

    static string RenderBranch(
        Jumper jumper,
        List<string> stack,
        IReadOnlyList<Avm2ControlFlowEdgeInventory> edges)
    {
        string target = Block(edges.FirstOrDefault(edge => edge.Kind is "Jump" or "Taken"));
        if (jumper.OP == OPCode.Jump)
            return $"goto {target}";
        if (jumper.OP is OPCode.IfTrue or OPCode.IfFalse)
        {
            string value = Pop(stack, -1);
            string condition = jumper.OP == OPCode.IfTrue ? value : $"!({value})";
            return $"if ({condition}) goto {target}";
        }
        string right = Pop(stack, -1);
        string left = Pop(stack, -1);
        string operation = BranchOperator(jumper.OP);
        return $"if ({left} {operation} {right}) goto {target}";
    }

    static string RenderPropertyCall(
        ASMultiname method,
        int argument_count,
        bool pushes,
        bool super,
        List<string> stack,
        int offset)
    {
        List<string> arguments = PopArguments(stack, argument_count, offset);
        string receiver = Pop(stack, offset);
        string target = super ? $"super({receiver})" : receiver;
        string call = $"{target}.{Name(method)}({string.Join(", ", arguments)})";
        if (pushes)
        {
            stack.Add(call);
            return $"push {call}";
        }
        return call;
    }

    static string RenderIndexedCall(
        string method,
        int argument_count,
        bool pushes,
        List<string> stack,
        int offset)
    {
        List<string> arguments = PopArguments(stack, argument_count, offset);
        string receiver = Pop(stack, offset);
        string call = $"{receiver}.{method}({string.Join(", ", arguments)})";
        if (pushes)
        {
            stack.Add(call);
            return $"push {call}";
        }
        return call;
    }

    static string RenderLexicalPropertyCall(
        ASMultiname method,
        int argument_count,
        List<string> stack,
        int offset)
    {
        List<string> arguments = PopArguments(stack, argument_count, offset);
        string receiver = Pop(stack, offset);
        string suffix = arguments.Count == 0 ? "null" : $"null, {string.Join(", ", arguments)}";
        string call = $"{receiver}.{Name(method)}.call({suffix})";
        stack.Add(call);
        return $"push {call}";
    }

    static string RenderDynamicCall(int argument_count, List<string> stack, int offset)
    {
        List<string> arguments = PopArguments(stack, argument_count, offset);
        string receiver = Pop(stack, offset);
        string method = Pop(stack, offset);
        string call = $"{method}.call({receiver}, {string.Join(", ", arguments)})";
        stack.Add(call);
        return $"push {call}";
    }

    static string RenderConstructProperty(ConstructPropIns construct, List<string> stack, int offset)
    {
        List<string> arguments = PopArguments(stack, construct.ArgCount, offset);
        string receiver = Pop(stack, offset);
        string value = $"new {receiver}.{Name(construct.PropertyName)}({string.Join(", ", arguments)})";
        stack.Add(value);
        return $"push {value}";
    }

    static string RenderConstruct(int argument_count, List<string> stack, int offset)
    {
        List<string> arguments = PopArguments(stack, argument_count, offset);
        string constructor = Pop(stack, offset);
        string value = $"new {constructor}({string.Join(", ", arguments)})";
        stack.Add(value);
        return $"push {value}";
    }

    static string RenderConversion(string operation, List<string> stack, int offset)
    {
        string operand = Pop(stack, offset);
        string value = $"{operation}({operand})";
        stack.Add(value);
        return $"push {value}";
    }

    static string RenderGeneric(
        ASInstruction instruction,
        Avm2InstructionInventory inventory,
        List<string> stack)
    {
        List<string> inputs = PopArguments(stack, inventory.PopCount, inventory.Offset);
        string operation = instruction.OP.ToString().ToLowerInvariant();
        if (inventory.Operands.Count > 0)
        {
            operation += " " + string.Join(", ", inventory.Operands
                .Select(pair => $"{pair.Key}={pair.Value}"));
        }
        if (inventory.PushCount == 0)
            return inputs.Count == 0 ? operation : $"{operation} ({string.Join(", ", inputs)})";
        var outputs = new List<string>(inventory.PushCount);
        for (int index = 0; index < inventory.PushCount; index++)
        {
            string value = inventory.PushCount == 1
                ? $"v_{inventory.Offset:x4}"
                : $"v_{inventory.Offset:x4}_{index}";
            stack.Add(value);
            outputs.Add(value);
        }
        string arguments = inputs.Count == 0 ? "" : $"({string.Join(", ", inputs)})";
        return $"{string.Join(", ", outputs)} = {operation}{arguments}";
    }

    static List<string> PopArguments(List<string> stack, int count, int offset)
    {
        var values = new string[count];
        for (int index = count - 1; index >= 0; index--)
            values[index] = Pop(stack, offset);
        return [.. values];
    }

    static string Pop(List<string> stack, int offset)
    {
        if (stack.Count == 0)
            return offset < 0 ? "stack_unknown" : $"stack_unknown_{offset:x4}";
        string value = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return value;
    }

    static string LocalName(ASMethod method, int register)
    {
        if (register == 0)
            return "this";
        int parameter_index = register - 1;
        if (parameter_index < method.Parameters.Count)
        {
            string? name = method.Parameters[parameter_index].Name;
            return string.IsNullOrWhiteSpace(name) ? $"arg_{register}" : name;
        }
        return $"local_{register}";
    }

    static bool IsStatic(ASMultiname name) => !name.IsNameNeeded && !name.IsNamespaceNeeded;

    static string Name(ASMultiname name)
    {
        string qualified = Avm2MethodAnalyzer.Qualified(name);
        int separator = Math.Max(qualified.LastIndexOf('.'), qualified.LastIndexOf(':'));
        return separator < 0 ? qualified : qualified[(separator + 1)..];
    }

    static string Block(Avm2ControlFlowEdgeInventory? edge)
    {
        if (edge is null)
            return "unresolved";
        return edge.ToBlock.HasValue ? $"b{edge.ToBlock.Value}" : $"exit@{edge.TargetOffset:x4}";
    }

    static string Depth(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "?";

    static string BranchOperator(OPCode op) => op switch
    {
        OPCode.IfEq => "==",
        OPCode.IfNe => "!=",
        OPCode.IfStrictEq => "===",
        OPCode.IfStrictNE => "!==",
        OPCode.IfGe => ">=",
        OPCode.IfGt => ">",
        OPCode.IfLe => "<=",
        OPCode.IfLt => "<",
        OPCode.IfNGe => "!>=",
        OPCode.IfNGt => "!>",
        OPCode.IfNLe => "!<=",
        OPCode.IfNLt => "!<",
        _ => op.ToString()
    };

    static string? BinaryOperator(OPCode op) => op switch
    {
        OPCode.Add or OPCode.Add_i => "+",
        OPCode.Subtract or OPCode.Subtract_i => "-",
        OPCode.Multiply or OPCode.Multiply_i => "*",
        OPCode.Divide => "/",
        OPCode.Modulo => "%",
        OPCode.BitAnd => "&",
        OPCode.BitOr => "|",
        OPCode.BitXor => "^",
        OPCode.LShift => "<<",
        OPCode.RShift => ">>",
        OPCode.URShift => ">>>",
        OPCode.Equals => "==",
        OPCode.StrictEquals => "===",
        OPCode.GreaterEquals => ">=",
        OPCode.GreaterThan => ">",
        OPCode.LessEquals => "<=",
        OPCode.LessThan => "<",
        OPCode.In => "in",
        OPCode.InstanceOf => "instanceof",
        OPCode.IsTypeLate => "is",
        OPCode.AsTypeLate => "as",
        _ => null
    };

    static string? UnaryOperator(OPCode op) => op switch
    {
        OPCode.Not => "!",
        OPCode.BitNot => "~",
        OPCode.Negate or OPCode.Negate_i => "-",
        OPCode.Increment or OPCode.Increment_i => "({0} + 1)",
        OPCode.Decrement or OPCode.Decrement_i => "({0} - 1)",
        OPCode.TypeOf => "typeof ",
        OPCode.Convert_b => "bool({0})",
        OPCode.Convert_d => "double({0})",
        OPCode.Convert_f => "float({0})",
        OPCode.Convert_f4 => "float4({0})",
        OPCode.Convert_i => "int({0})",
        OPCode.Convert_o => "object({0})",
        OPCode.Convert_s => "string({0})",
        OPCode.Convert_u => "uint({0})",
        OPCode.UnPlus => "numeric({0})",
        OPCode.Coerce_a => "any({0})",
        OPCode.Coerce_s => "string({0})",
        OPCode.CheckFilter => "checkfilter({0})",
        OPCode.Esc_XAttr => "esc_attr({0})",
        OPCode.Esc_XElem => "esc_elem({0})",
        _ => null
    };

    static string ClassName(NewClassIns instruction)
    {
        try
        {
            return Avm2MethodAnalyzer.Qualified(instruction.Class.Instance.QName);
        }
        catch (ArgumentOutOfRangeException)
        {
            return $"invalid-class#{instruction.ClassIndex}";
        }
    }
}
