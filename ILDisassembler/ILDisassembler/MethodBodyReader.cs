//
// MethodBodyReader.cs
//
// Author:
//   Jb Evain (jbevain@novell.com)
//
// (C) 2009 - 2010 Novell, Inc. (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace ILDisassembler
{
	internal class MethodBodyReader
	{
		private static readonly OpCode[] oneByteOpCodes;
		private static readonly OpCode[] twoByteOpCodes;

		private readonly MethodBase method;
		private readonly MethodBody body;
		private readonly Module module;
		private readonly Type[] typeArguments;
		private readonly Type[] methodArguments;
		private readonly ByteBuffer ilBuffer;
		private readonly ParameterInfo[] parameters;
		private readonly IList<LocalVariableInfo> locals;
		private readonly List<Instruction> instructions;

		static MethodBodyReader()
		{
			oneByteOpCodes = new OpCode[0xe1];
			twoByteOpCodes = new OpCode[0x1f];

			var fields = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);

			foreach (var field in fields)
			{
				var opcode = (OpCode)field.GetValue(null);
				if (opcode.OpCodeType == OpCodeType.Nternal)
				{
					continue;
				}

				if (opcode.Size == 1)
				{
					oneByteOpCodes[opcode.Value] = opcode;
				}
				else
				{
					twoByteOpCodes[opcode.Value & 0xff] = opcode;
				}
			}
		}

		private MethodBodyReader(MethodBase method)
		{
			this.method = method;

			this.body = method.GetMethodBody();
			if (this.body == null)
				throw new ArgumentException("Method has no body");

			var bytes = body.GetILAsByteArray();

			if (bytes == null)
			{
				throw new ArgumentException("Can not get the body of the method");
			}

			if (!(method is ConstructorInfo))
			{
				methodArguments = method.GetGenericArguments();
			}

			if (method.DeclaringType != null)
			{
				typeArguments = method.DeclaringType.GetGenericArguments();
			}

			this.parameters = method.GetParameters();
			this.locals = body.LocalVariables;
			this.module = method.Module;
			this.ilBuffer = new ByteBuffer(bytes);
			this.instructions = new List<Instruction>((bytes.Length + 1) / 2);
		}

		void ReadInstructions()
		{
			Instruction previous = null;

			while (ilBuffer.position < ilBuffer.buffer.Length)
			{
				var instruction = new Instruction(ilBuffer.position, ReadOpCode());

				ReadOperand(instruction);

				if (previous != null)
				{
					instruction.Previous = previous;
					previous.Next = instruction;
				}

				instructions.Add(instruction);
				previous = instruction;
			}

			ResolveBranches();
		}

		void ReadOperand(Instruction instruction)
		{
			switch (instruction.OpCode.OperandType)
			{
				case OperandType.InlineNone:
					break;
				case OperandType.InlineSwitch:
					int length = ilBuffer.ReadInt32();
					int base_offset = ilBuffer.position + (4 * length);
					int[] branches = new int[length];

					for (int i = 0; i < length; i++)
					{
						branches[i] = ilBuffer.ReadInt32() + base_offset;
					}

					instruction.Operand = branches;
					break;
				case OperandType.ShortInlineBrTarget:
					instruction.Operand = (((sbyte)ilBuffer.ReadByte()) + ilBuffer.position);
					break;
				case OperandType.InlineBrTarget:
					instruction.Operand = ilBuffer.ReadInt32() + ilBuffer.position;
					break;
				case OperandType.ShortInlineI:
					if (instruction.OpCode == OpCodes.Ldc_I4_S)
					{
						instruction.Operand = (sbyte)ilBuffer.ReadByte();
					}
					else
					{
						instruction.Operand = ilBuffer.ReadByte();
					}
					break;
				case OperandType.InlineI:
					instruction.Operand = ilBuffer.ReadInt32();
					break;
				case OperandType.ShortInlineR:
					instruction.Operand = ilBuffer.ReadSingle();
					break;
				case OperandType.InlineR:
					instruction.Operand = ilBuffer.ReadDouble();
					break;
				case OperandType.InlineI8:
					instruction.Operand = ilBuffer.ReadInt64();
					break;
				case OperandType.InlineSig:
					instruction.Operand = module.ResolveSignature(ilBuffer.ReadInt32());
					break;
				case OperandType.InlineString:
					instruction.Operand = module.ResolveString(ilBuffer.ReadInt32());
					break;
				case OperandType.InlineTok:
				case OperandType.InlineType:
				case OperandType.InlineMethod:
				case OperandType.InlineField:
					instruction.Operand = module.ResolveMember(ilBuffer.ReadInt32(), typeArguments, methodArguments);
					break;
				case OperandType.ShortInlineVar:
					instruction.Operand = GetVariable(instruction, ilBuffer.ReadByte());
					break;
				case OperandType.InlineVar:
					instruction.Operand = GetVariable(instruction, ilBuffer.ReadInt16());
					break;
				default:
					throw new NotSupportedException();
			}
		}

		void ResolveBranches()
		{
			foreach (var instruction in instructions)
			{
				switch (instruction.OpCode.OperandType)
				{
					case OperandType.ShortInlineBrTarget:
					case OperandType.InlineBrTarget:
						instruction.Operand = GetInstruction(instructions, (int)instruction.Operand);
						break;
					case OperandType.InlineSwitch:
						var offsets = (int[])instruction.Operand;
						var branches = new Instruction[offsets.Length];

						for (int j = 0; j < offsets.Length; j++)
						{
							branches[j] = GetInstruction(instructions, offsets[j]);
						}

						instruction.Operand = branches;
						break;
				}
			}
		}

		static Instruction GetInstruction(List<Instruction> instructions, int offset)
		{
			var size = instructions.Count;

			if (offset < 0 || offset > instructions[size - 1].Offset)
			{
				return null;
			}

			int min = 0;
			int max = size - 1;
			while (min <= max)
			{
				int mid = min + ((max - min) / 2);
				var instruction = instructions[mid];
				var instruction_offset = instruction.Offset;

				if (offset == instruction_offset)
				{
					return instruction;
				}

				if (offset < instruction_offset)
				{
					max = mid - 1;
				}
				else
				{
					min = mid + 1;
				}
			}
			return null;
		}

		object GetVariable(Instruction instruction, int index)
		{
			return TargetsLocalVariable(instruction.OpCode)
				? (object)GetLocalVariable(index)
				: (object)GetParameter(index);
		}

		static bool TargetsLocalVariable(OpCode opcode)
		{
			return opcode.Name.Contains("loc");
		}

		LocalVariableInfo GetLocalVariable(int index)
		{
			return locals[index];
		}

		ParameterInfo GetParameter(int index)
		{
			return parameters[method.IsStatic ? index : index - 1];
		}

		OpCode ReadOpCode()
		{
			byte op = ilBuffer.ReadByte();
			return op != 0xfe
				? oneByteOpCodes[op]
				: twoByteOpCodes[ilBuffer.ReadByte()];
		}

		/// <summary>
		/// Returns the instruction for the given method
		/// </summary>
		/// <param name="method">The method</param>
		public static IList<Instruction> GetInstructions(MethodBase method)
		{
			var reader = new MethodBodyReader(method);
			reader.ReadInstructions();
			return reader.instructions;
		}
	}
}