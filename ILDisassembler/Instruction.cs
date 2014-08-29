//
// Instruction.cs
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
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILDisassembler
{
	internal sealed class Instruction
	{
		private int offset;
		private OpCode opcode;
		private object operand;

		private Instruction previous;
		private Instruction next;

		private static readonly IDictionary<Type, string> typeAliases;
		private static readonly OpCode[] callInstructions;

		static Instruction()
		{
			typeAliases = new Dictionary<Type, string>();
			typeAliases.Add(typeof(String), "string");
			typeAliases.Add(typeof(SByte), "int8");
			typeAliases.Add(typeof(Int16), "int16");
			typeAliases.Add(typeof(Int32), "int32");
			typeAliases.Add(typeof(Int64), "int64");
			typeAliases.Add(typeof(Byte), "uint8");
			typeAliases.Add(typeof(UInt16), "uint16");
			typeAliases.Add(typeof(UInt32), "uint32");
			typeAliases.Add(typeof(UInt64), "uint64");
			typeAliases.Add(typeof(Single), "float32");
			typeAliases.Add(typeof(Double), "float64");
			typeAliases.Add(typeof(void), "void");
			typeAliases.Add(typeof(Object), "object");
			typeAliases.Add(typeof(Boolean), "bool");
			typeAliases.Add(typeof(char), "char");

			callInstructions = new OpCode[]
			{
				OpCodes.Call,
				OpCodes.Calli,
				OpCodes.Callvirt,
			};
		}

		public int Offset
		{
			get { return offset; }
		}

		public OpCode OpCode
		{
			get { return opcode; }
		}

		public object Operand
		{
			get { return operand; }
			internal set { operand = value; }
		}

		public Instruction Previous
		{
			get { return previous; }
			internal set { previous = value; }
		}

		public Instruction Next
		{
			get { return next; }
			internal set { next = value; }
		}

		public int Size
		{
			get
			{
				int size = opcode.Size;

				switch (opcode.OperandType)
				{
					case OperandType.InlineSwitch:
						//size += (1 + ((int[])operand).Length) * 4;
						size += (1 + ((Instruction[])operand).Length) * 4;
						break;
					case OperandType.InlineI8:
					case OperandType.InlineR:
						size += 8;
						break;
					case OperandType.InlineBrTarget:
					case OperandType.InlineField:
					case OperandType.InlineI:
					case OperandType.InlineMethod:
					case OperandType.InlineString:
					case OperandType.InlineTok:
					case OperandType.InlineType:
					case OperandType.ShortInlineR:
						size += 4;
						break;
					case OperandType.InlineVar:
						size += 2;
						break;
					case OperandType.ShortInlineBrTarget:
					case OperandType.ShortInlineI:
					case OperandType.ShortInlineVar:
						size += 1;
						break;
				}

				return size;
			}
		}

		internal Instruction(int offset, OpCode opcode)
		{
			this.offset = offset;
			this.opcode = opcode;
		}

		/// <summary>
		/// Returns the short name for an assembly
		/// </summary>
		/// <param name="assembly">The assembly</param>
		private static string GetAssemblyShortName(Assembly assembly)
		{
			return assembly.FullName.Split(',')[0];
		}

		/// <summary>
		/// Returns the type name for the given type
		/// </summary>
		/// <param name="type">The type</param>
		/// <param name="useAliases">Indicates if to use type aliases</param>
		/// <param name="useAliasOnParams">Indicates if to use type aliases on params if useAliases is false</param>
		private static string GetTypeName(Type type, bool useAliases = false, bool useAliasOnParams = false)
		{
			if (type.IsArray)
			{
				string arrayStr = "[]";
				int arrayRank = type.GetArrayRank();

				if (arrayRank > 1)
				{
					arrayStr = "[";

					for (int i = 0; i < arrayRank; i++)
					{
						if (i != 0)
						{
							arrayStr += ",";
						}

						arrayStr += "0...";
					}

					arrayStr += "]";
				}

				return GetTypeName(type.GetElementType(), useAliases || useAliasOnParams, useAliasOnParams) + arrayStr;
			}

			if (useAliases)
			{
				if (typeAliases.ContainsKey(type))
				{
					return typeAliases[type];
				}
			}

			string assemblyRef = "";

			if (type.Assembly.GlobalAssemblyCache)
			{
				assemblyRef = "[" + GetAssemblyShortName(type.Assembly) + "]";
			}

			if (type.IsGenericType)
			{
				string fullName = type.Name;

				if (!string.IsNullOrEmpty(type.Namespace))
				{
					fullName = type.Namespace + "." + fullName;
				}

				return String.Format(
					"{0}<{1}>",
					assemblyRef + fullName,
					String.Join(",", type.GenericTypeArguments.Select(genParam => GetTypeName(genParam, useAliases || useAliasOnParams, useAliasOnParams))));
			}
			else
			{
				return assemblyRef + type.ToString();
			}
		}

		/// <summary>
		/// Returs the type identifier for the given type
		/// </summary>
		/// <param name="type">The type</param>
		/// <param name="addSpacing">Indicates if to add spacing after the identifier if not empty</param>
		private static string GetTypeIdentifier(Type type, bool addSpacing = false)
		{
			if (type.IsArray)
			{
				return GetTypeIdentifier(type.GetElementType(), addSpacing);
			}

			if ((type.IsClass || type.IsInterface)
				&& type != typeof(object)
				&& type != typeof(string)
				&& type != typeof(void)
				&& type != typeof(ValueType)
				&& !type.IsGenericParameter)
			{
				return "class" + (addSpacing ? " " : "");
			}

			return "";
		}

		/// <summary>
		/// Returns a string representation of the operand
		/// </summary>
		public string OperandToString()
		{
			var opStringBuilder = new StringBuilder();

			//Check if call or newobject
			if (callInstructions.Any(op => op == opcode) || opcode == OpCodes.Newobj)
			{
				var methodBase = (MethodBase)operand;

				if (!methodBase.IsStatic)
				{
					opStringBuilder.Append("instance ");
				}
			}

			switch (opcode.OperandType)
			{
				case OperandType.ShortInlineBrTarget:
				case OperandType.InlineBrTarget:
					AppendLabel(opStringBuilder, (Instruction)operand);
					break;
				case OperandType.InlineSwitch:
					var labels = (Instruction[])operand;
					opStringBuilder.Append("(");
					for (int i = 0; i < labels.Length; i++)
					{
						if (i > 0)
							opStringBuilder.Append(',');

						AppendLabel(opStringBuilder, labels[i]);
					}
					opStringBuilder.Append(")");
					break;
				case OperandType.InlineString:
					opStringBuilder.Append('\"');
					opStringBuilder.Append(operand);
					opStringBuilder.Append('\"');
					break;
				default:
					if (operand is FieldInfo)
					{
						var opField = (FieldInfo)operand;

						string fieldName = opField.Name;
						string typeName = GetTypeName(opField.FieldType, true);

						if (opField.IsCompilerGenerated())
						{
							fieldName = "'" + fieldName + "'";
						}

						opStringBuilder.Append(
							GetTypeIdentifier(opField.FieldType, true) + typeName + " " + opField.DeclaringType.FullName + "::" + fieldName);
						break;
					}

					if (operand is ConstructorInfo)
					{
						var opCons = (ConstructorInfo)operand;
						var declaringType = opCons.DeclaringType;
						var consParams = opCons.GetParameters().Select(param =>
						{
							return GetTypeName(param.ParameterType, true);
						});
						string typeName = GetTypeName(declaringType, useAliasOnParams: true) + "::" + opCons.Name;

						opStringBuilder.Append(String.Format(
							"{0} {1}({2})",
							"void",
							GetTypeIdentifier(declaringType, true) + typeName,
							String.Join(", ", consParams)));
						break;
					}

					if (operand is MethodInfo)
					{
						var opMethod = (MethodInfo)operand;

						var declaringType = opMethod.DeclaringType;
						var args = opMethod.GetParameters().Select(param =>
						{
							return GetTypeName(param.ParameterType, true);
						});

						string methodName = opMethod.Name;

						if (opMethod.IsCompilerGenerated())
						{
							methodName = "'" + methodName + "'";
						}

						string typeName = GetTypeName(declaringType) + "::" + methodName;
						string returnTypeName = GetTypeName(opMethod.ReturnType, true);

						opStringBuilder.Append(
							String.Format("{0} {1}({2})",
							GetTypeIdentifier(opMethod.ReturnType, true) + returnTypeName, typeName,
							String.Join(", ", args)));
						break;
					}

					if (operand is LocalVariableInfo)
					{
						var opLocVar = (LocalVariableInfo)operand;
						opStringBuilder.Append("V_" + opLocVar.LocalIndex);
						break;
					}

					if (operand is Type)
					{
						var opType = (Type)operand;
						opStringBuilder.Append(GetTypeName(opType));
						break;
					}

					if (operand is double)
					{
						var opDouble = (double)operand;
						opStringBuilder.Append(opDouble.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
						break;
					}

					if (operand is float)
					{
						var opFloat = (float)operand;
						opStringBuilder.Append(opFloat.ToString("G9", System.Globalization.CultureInfo.InvariantCulture));
						break;
					}

					opStringBuilder.Append(operand);
					break;
			}

			return opStringBuilder.ToString();
		}

		/// <summary>
		/// Returns a string representation without the operand
		/// </summary>
		public string ToStringWithoutOperand()
		{
			var instruction = new StringBuilder();

			AppendLabel(instruction, this);
			instruction.Append(':');
			instruction.Append(' ');
			instruction.Append(opcode.Name);

			return instruction.ToString();
		}

		private string GenerateChar(char c, int n)
		{
			var builder = new StringBuilder(n);

			for (int i = 0; i < n; i++)
			{
				builder.Append(c);
			}

			return builder.ToString();
		}

		/// <summary>
		/// Returns a string representation of the current instruction with the maxmium amount of spacing between the instruction and operand
		/// </summary>
		/// <param name="maxSpacing">The max spacing</param>
		public string ToString(int maxSpacing = -1)
		{
			var instruction = new StringBuilder();

			AppendLabel(instruction, this);
			instruction.Append(':');
			instruction.Append(' ');
			instruction.Append(opcode.Name);

			if (operand == null)
				return instruction.ToString();

			if (maxSpacing != -1)
			{
				instruction.Append(GenerateChar(' ', maxSpacing - instruction.Length));
				//instruction.Append("\t\t");
			}
			else
			{
				instruction.Append(" ");
			}

			instruction.Append(this.OperandToString());
			return instruction.ToString();
		}

		public override string ToString()
		{
			return this.ToString(-1);
		}

		static void AppendLabel(StringBuilder builder, Instruction instruction)
		{
			builder.Append("IL_");
			builder.Append(instruction.offset.ToString("x4"));
		}
	}

	internal static class ReflectionExtensions
	{
		/// <summary>
		/// Indicates if the current member is compiler generated
		/// </summary>
		/// <param name="member">The member</param>
		internal static bool IsCompilerGenerated(this MemberInfo member)
		{
			return member.CustomAttributes.Any(attr => attr.AttributeType == typeof(CompilerGeneratedAttribute));
		}
	}
}
