using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ILDisassembler
{
	/// <summary>
	/// Represents an IL dissasembler
	/// </summary>
	public sealed class Disassembler
	{

		#region Fields
		private static readonly IList<string> attrIgnoreList;
		private static readonly IDictionary<string, string> typeAliases;
		#endregion

		#region Help classes
		private enum ExceptionHandlingClauseType { Try, Catch, FilterCatch, Filter, Finally };
		private enum BlockPart { Begin, End };
		private class ExceptionHandlingClause : IEquatable<ExceptionHandlingClause>
		{
			public ExceptionHandlingClauseType Type { get; set; }
			public BlockPart BlockPart { get; set; }
			public Type CatchType { get; set; }

			public bool Equals(ExceptionHandlingClause other)
			{
				return this.Type == other.Type
					&& this.BlockPart == other.BlockPart
					&& this.CatchType == other.CatchType;
			}
		}
		#endregion

		#region Constructors
		static Disassembler()
		{
			attrIgnoreList = new List<string>();
			attrIgnoreList.Add("privatescope");
			attrIgnoreList.Add("vtablelayoutmask");

			typeAliases = new Dictionary<string, string>();
			typeAliases.Add("System.SByte", "int8");
			typeAliases.Add("System.Int16", "int16");
			typeAliases.Add("System.Int32", "int32");
			typeAliases.Add("System.Int64", "int64");
			typeAliases.Add("System.Byte", "uint8");
			typeAliases.Add("System.UInt16", "uint16");
			typeAliases.Add("System.UInt32", "uint32");
			typeAliases.Add("System.UInt64", "uint64");
			typeAliases.Add("System.Single", "float32");
			typeAliases.Add("System.Double", "float64");
			typeAliases.Add("System.String", "string");
			typeAliases.Add("System.Char", "char");
			typeAliases.Add("System.Boolean", "bool");
			typeAliases.Add("System.Void", "void");
			typeAliases.Add("System.Object", "object");

			foreach (var current in new Dictionary<string, string>(typeAliases))
			{
				typeAliases.Add(current.Key + "&", current.Value + "&");
			}
		}
		#endregion

		#region Properties

		#endregion

		#region Methods

		#region Helper Methods
		/// <summary>
		/// Returns the name for the given parameter
		/// </summary>
		/// <param name="parameter">The parameter</param>
		private static string GetParameterName(ParameterInfo parameter)
		{
			switch(parameter.Name)
			{
				case "object":
					return "'object'";
				case "value":
					return "'value'";
				case "method":
					return "'method'";
				default:
					return parameter.Name;
			}
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
		/// <param name="currentAssembly">Current assembly</param>
		/// <param name="type">The type</param>
		/// <param name="useAliases">Indicates if to use type aliases</param>
		/// <param name="useAliasOnParams">Indicates if to use type aliases on params if useAliases is false</param>
		private static string GetTypeName(Assembly currentAssembly, Type type, bool useAliases = false, bool useAliasOnParams = false)
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

				return GetTypeName(currentAssembly, type.GetElementType(), useAliases || useAliasOnParams, useAliasOnParams) + arrayStr;
			}

			if (useAliases)
			{
				if (typeAliases.ContainsKey(type.ToString()))
				{
					return typeAliases[type.ToString()];
				}
			}

			string assemblyRef = "";

			if (currentAssembly != type.Assembly)
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
					String.Join(",", type.GenericTypeArguments.Select(genParam => 
						GetTypeName(currentAssembly, genParam, useAliases || useAliasOnParams, useAliasOnParams))));
			}
			else
			{
				return assemblyRef + type.ToString();
			}
		}

		/// <summary>
		/// Returns the given custom attribute as a string
		/// </summary>
		/// <param name="currentAssembly">The current assembly</param>
		/// <param name="customAttribute">The custom attribute</param>
		private static string GetCustomAttribute(Assembly currentAssembly, CustomAttributeData customAttribute)
		{
			var attrParams = customAttribute.Constructor
				.GetParameters()
				.Select(type => GetTypeName(currentAssembly, type.ParameterType, true));

			var consAttrStr = String.Join(
				",",
				customAttribute.ConstructorArguments.Select(arg => arg.ToString())
				.Concat(customAttribute.NamedArguments.Select(arg => arg.ToString())));

			if (String.IsNullOrEmpty(consAttrStr))
			{
				//consAttrStr = "01 00 00 00";
				consAttrStr = "";
			}

			return String.Format(".custom instance {0} {1}::.ctor({2}) = ({3})",
				"void",
				GetTypeName(null, customAttribute.AttributeType),
				String.Join(",", attrParams),
				consAttrStr);
		}

		/// <summary>
		/// Emites a block part to the given output writer
		/// </summary>
		/// <param name="outputWriter">The output writer</param>
		/// <param name="blockPart">The block part</param>
		/// <param name="headerString">The header string</param>
		private void EmitBlockPart(OutputWriter outputWriter, BlockPart blockPart, string headerString = "")
		{
			if (blockPart == BlockPart.Begin)
			{
				if (headerString != "")
				{
					outputWriter.AppendLine(headerString);
				}
				
				outputWriter.AppendLine("{");
				outputWriter.Indent();
			}
			else
			{
				outputWriter.Unindent();
				outputWriter.AppendLine("}");
			}
		}

		/// <summary>
		/// Returns the generic parameters
		/// </summary>
		/// <param name="currentAssembly">The current assembly</param>
		/// <param name="genericParameters">The generic parameters</param>
		private static string GetGenericParameters(Assembly currentAssembly, Type[] genericParameters)
		{
			return String.Join(", ", genericParameters.Select(param =>
			{
				var genericConstraints = param.GetGenericParameterConstraints();

				var genericParamAttr = new List<string>();
				Func<GenericParameterAttributes, bool> hasGenParamAttr = attr =>
					param.GenericParameterAttributes == attr || param.GenericParameterAttributes.HasFlag(attr);

				if (hasGenParamAttr(GenericParameterAttributes.DefaultConstructorConstraint))
				{
					genericParamAttr.Add(".ctor");
				}

				if (hasGenParamAttr(GenericParameterAttributes.NotNullableValueTypeConstraint))
				{
					genericParamAttr.Add("valuetype");
				}

				if (hasGenParamAttr(GenericParameterAttributes.ReferenceTypeConstraint))
				{
					genericParamAttr.Add("class");

				}
				if (hasGenParamAttr(GenericParameterAttributes.Covariant))
				{
					genericParamAttr.Add("+");
				}

				if (hasGenParamAttr(GenericParameterAttributes.Contravariant))
				{
					genericParamAttr.Add("-");
				}

				if (genericConstraints.Length > 0)
				{
					return String.Format(
						"{0}({1}) {2}",
						genericParamAttr.Count > 0 ? String.Join(" ", genericParamAttr) + " " : "",
						String.Join(", ", genericConstraints.Select(cons =>
							GetTypeIdentifier(currentAssembly, cons, addSpacing: true) + GetTypeName(currentAssembly, cons))),
						param.ToString());
				}
				else
				{
					return (genericParamAttr.Count > 0 ? String.Join(" ", genericParamAttr) + " " : "") + param.ToString();
				}
			}));
		}

		/// <summary>
		/// Returs the type identifier for the given type
		/// </summary>
		/// <param name="currentAssembly">The current assembly</param>
		/// <param name="type">The type</param>
		/// <param name="addSpacing">Indicates if to add spacing after the identifier if not empty</param>
		/// <param name="isParameter">Indicates if the type is a parameter</param>
		private static string GetTypeIdentifier(Assembly currentAssembly, Type type, bool addSpacing = false, bool isParameter = false)
		{
			if (type.IsArray)
			{
				return GetTypeIdentifier(currentAssembly, type.GetElementType(), addSpacing, isParameter);
			}

			if ((type.IsClass || type.IsInterface)
				&& type != typeof(object)
				&& type != typeof(string)
				&& type != typeof(void)
				&& type != typeof(ValueType)
				&& !type.IsGenericParameter
				&& !(!isParameter && type.Assembly == currentAssembly))
			{
				return "class" + (addSpacing ? " ": "");
			}

			return "";
		}

		/// <summary>
		/// Returns the method parameters for the given method
		/// </summary>
		/// <param name="method">The method</param>
		private static string GetMethodParameters(MethodBase method)
		{
			return String.Join(", ", method.GetParameters().Select(arg =>
			{
				var paramAttrs = new List<string>();

				if (arg.HasDefaultValue)
				{
					paramAttrs.Add("[opt]");
				}

				if (arg.IsOut)
				{
					paramAttrs.Add("[out]");
				}

				string argTypeName = GetTypeName(method.DeclaringType.Assembly, arg.ParameterType, true);
				string attrStr = "";

				if (paramAttrs.Count > 0)
				{
					attrStr = String.Join(" ", paramAttrs) + " ";
				}

				string typeIdentifier = GetTypeIdentifier(method.DeclaringType.Assembly, arg.ParameterType, addSpacing: true, isParameter: true);

				return typeIdentifier + attrStr + argTypeName + " " + GetParameterName(arg);
			}));
		}
		#endregion

		#region Disassembler Methods
		/// <summary>
		/// Disassembles the given type header
		/// </summary>
		/// <param name="type">The type header</param>
		public string DisassembleTypeHeader(Type type)
		{
			var outputWriter = new OutputWriter(7);
			
			var attrs = new List<string>();

			#region Attributes
			attrs.Add(".class");
			
			if (type.IsEnum)
			{
				attrs.Add("enum");
			}
			else
			{
				attrs.Add(type.IsValueType ? "value" : "");
			}

			attrs.Add(type.IsInterface ? "interface" : "");

			attrs.Add(type.IsPublic ? "public" : "private");

			attrs.Add(type.IsAutoLayout ? "auto" : "");
			attrs.Add(type.IsLayoutSequential ? "sequential" : "");
			attrs.Add(type.IsExplicitLayout ? "explicit" : "");

			if (type.IsAnsiClass)
			{
				attrs.Add("ansi");
			}

			attrs.Add(type.IsAbstract ? "abstract" : "");
			attrs.Add(type.IsSealed ? "sealed" : "");

			attrs.Add(type.Attributes.HasFlag(TypeAttributes.BeforeFieldInit) ? "beforefieldinit" : "");			
			#endregion

			outputWriter.Append(String.Join(" ", attrs.Where(attr => !String.IsNullOrEmpty(attr))));
			outputWriter.Append(" " + type.FullName);

			if (type.IsGenericType)
			{
				outputWriter.Append("<");
				outputWriter.Append(GetGenericParameters(type.Assembly, type.GetGenericArguments()));
				outputWriter.Append(">");
			}

			outputWriter.Indent();
			outputWriter.AppendLine();

			if (!type.IsInterface)
			{
				outputWriter.AppendLine(
					String.Format("extends {0}",
					GetTypeName(type.Assembly, type.BaseType, useAliasOnParams: true)));
			}

			var interfaces = type.GetInterfaces().ToList();

			if (interfaces.Count > 0)
			{
				outputWriter.AppendLine("implements " + String.Join(", ", interfaces.Select(i => GetTypeName(type.Assembly, i))));
			}

			outputWriter.Unindent();
			outputWriter.AppendLine("{");
			outputWriter.AppendLine("}");

			return outputWriter.ToString();
		}

		/// <summary>
		/// Disassembles the given method body
		/// </summary>
		private void DisassembleMethodBody(OutputWriter outputWriter, MethodBase method, MethodBody methodBody)
		{
			var methodInstructions = MethodBodyReader.GetInstructions(method);

			//Calculate the maximum spacing between the instruction and operands and code size
			int maxSpacing = int.MinValue;
			int codeSize = 0;
			int maxIndentationLevel = int.MinValue;

			foreach (var inst in methodInstructions)
			{
				var length = inst.ToStringWithoutOperand().Length;

				if (length > maxSpacing)
				{
					maxSpacing = length;
				}

				codeSize += inst.Size;
			}

			//Output default values
			foreach (var param in method.GetParameters())
			{
				if (param.HasDefaultValue)
				{
					string defaultValue = "";

					if (param.ParameterType == typeof(string))
					{
						defaultValue = "\"" + param.DefaultValue + "\"";
					}
					else if (param.ParameterType.IsPrimitive)
					{
						#region Primitive default values
						string primValue = "";

						if (param.ParameterType == typeof(sbyte))
						{
							primValue = "0x" + ((sbyte)param.DefaultValue).ToString("X2");
						}
						else if (param.ParameterType == typeof(short))
						{
							primValue = "0x" + ((short)param.DefaultValue).ToString("X4");
						}
						else if (param.ParameterType == typeof(int))
						{
							primValue = "0x" + ((int)param.DefaultValue).ToString("X8");
						}
						else if (param.ParameterType == typeof(long))
						{
							primValue = "0x" + ((long)param.DefaultValue).ToString("X16");
						}
						else if (param.ParameterType == typeof(byte))
						{
							primValue = "0x" + ((byte)param.DefaultValue).ToString("X2");
						}
						else if (param.ParameterType == typeof(ushort))
						{
							primValue = "0x" + ((ushort)param.DefaultValue).ToString("X4");
						}
						else if (param.ParameterType == typeof(uint))
						{
							primValue = "0x" + ((uint)param.DefaultValue).ToString("X8");
						}
						else if (param.ParameterType == typeof(ulong))
						{
							primValue = "0x" + ((ulong)param.DefaultValue).ToString("X16");
						}
						else if (param.ParameterType == typeof(float))
						{
							primValue = ((float)param.DefaultValue).ToString("G9", System.Globalization.CultureInfo.InvariantCulture);
						}
						else if (param.ParameterType == typeof(double))
						{
							primValue = ((double)param.DefaultValue).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
						}
						else
						{
							primValue = param.DefaultValue.ToString();
						}

						defaultValue = String.Format(
							"{0}({1})",
							param.ParameterType.Name.ToString().ToLower(),
							primValue);
						#endregion
					}
					else
					{
						if (param.DefaultValue == null)
						{
							defaultValue = "nullref";
						}
						else
						{
							defaultValue = param.DefaultValue.ToString();
						}
					}

					outputWriter.AppendLine(String.Format(".param [{0}] = {1}", param.Position + 1, defaultValue));
				}
			}
			   
			//Output the locals, stack size and code size
			outputWriter.AppendLine(String.Format("// Code size       {0} (0x{1})", codeSize, codeSize.ToString("x")));
			outputWriter.AppendLine(".maxstack " + methodBody.MaxStackSize);

			if (methodBody.LocalVariables.Count > 0)
			{
				outputWriter.AppendLine(
					String.Format(".locals init ({0})",
					String.Join(", ", methodBody.LocalVariables.Select(local =>
						GetTypeName(method.DeclaringType.Assembly, local.LocalType, true) + " V_" + local.LocalIndex))));
			}

			//Extract the try, catch and finally blocks
			var exceptionHandlingClauses = new Dictionary<int, IList<ExceptionHandlingClause>>();

			foreach (var currentClause in methodBody.ExceptionHandlingClauses)
			{
				if (currentClause.Flags == ExceptionHandlingClauseOptions.Clause)
				{
					#region Try-Catch
					//Try
					exceptionHandlingClauses.AddMulti(
						currentClause.TryOffset,
						new ExceptionHandlingClause() { Type = ExceptionHandlingClauseType.Try, BlockPart = BlockPart.Begin });

					exceptionHandlingClauses.AddMulti(
						currentClause.TryOffset + currentClause.TryLength,
						new ExceptionHandlingClause() { Type = ExceptionHandlingClauseType.Try, BlockPart = BlockPart.End });

					//Handler
					exceptionHandlingClauses.AddMulti(
						currentClause.HandlerOffset,
						new ExceptionHandlingClause()
						{
							Type = ExceptionHandlingClauseType.Catch,
							BlockPart = BlockPart.Begin,
							CatchType = currentClause.CatchType
						});

					exceptionHandlingClauses.AddMulti(
						currentClause.HandlerOffset + currentClause.HandlerLength,
						new ExceptionHandlingClause() { Type = ExceptionHandlingClauseType.Catch, BlockPart = BlockPart.End });
					#endregion
				}

				if (currentClause.Flags == ExceptionHandlingClauseOptions.Filter)
				{
					#region Filter
					var filterTryStart = new ExceptionHandlingClause() { Type = ExceptionHandlingClauseType.Try, BlockPart = BlockPart.Begin };
					var filterTryEnd = new ExceptionHandlingClause() { Type = ExceptionHandlingClauseType.Try, BlockPart = BlockPart.End };

					//Check if this filter is the first or in the chain of other filters
					bool isFirstFilter = false;

					if (exceptionHandlingClauses.ContainsKey(currentClause.TryOffset)
						&& exceptionHandlingClauses.ContainsKey(currentClause.TryOffset + currentClause.TryLength))
					{
						isFirstFilter = exceptionHandlingClauses[currentClause.TryOffset].Any(clause => clause.Equals((filterTryStart)))
							&& exceptionHandlingClauses[currentClause.TryOffset + currentClause.TryLength].Any(clause => clause.Equals((filterTryEnd)));
					}

					if (!isFirstFilter)
					{
						//Try
						exceptionHandlingClauses.AddMulti(
							currentClause.TryOffset,
							filterTryStart);

						exceptionHandlingClauses.AddMulti(
							currentClause.TryOffset + currentClause.TryLength,
							filterTryEnd);
					}

					//Filter
					exceptionHandlingClauses.AddMulti(
						currentClause.FilterOffset,
						new ExceptionHandlingClause() { Type = ExceptionHandlingClauseType.Filter, BlockPart = BlockPart.Begin });

					exceptionHandlingClauses.AddMulti(
						currentClause.HandlerOffset,
						new ExceptionHandlingClause() { Type = ExceptionHandlingClauseType.Filter, BlockPart = BlockPart.End });

					//Handler
					exceptionHandlingClauses.AddMulti(
						currentClause.HandlerOffset,
						new ExceptionHandlingClause() { Type = ExceptionHandlingClauseType.FilterCatch, BlockPart = BlockPart.Begin });

					exceptionHandlingClauses.AddMulti(
						currentClause.HandlerOffset + currentClause.HandlerLength,
						new ExceptionHandlingClause() { Type = ExceptionHandlingClauseType.FilterCatch, BlockPart = BlockPart.End });
					#endregion
				}

				if (currentClause.Flags == ExceptionHandlingClauseOptions.Finally)
				{
					#region Finally
					//Try
					exceptionHandlingClauses.AddMulti(
						currentClause.TryOffset,
						new ExceptionHandlingClause() { Type = ExceptionHandlingClauseType.Try, BlockPart = BlockPart.Begin });

					exceptionHandlingClauses.AddMulti(
						currentClause.TryOffset + currentClause.TryLength,
						new ExceptionHandlingClause() { Type = ExceptionHandlingClauseType.Try, BlockPart = BlockPart.End });

					//Handler
					exceptionHandlingClauses.AddMulti(
						currentClause.HandlerOffset,
						new ExceptionHandlingClause() { Type = ExceptionHandlingClauseType.Finally, BlockPart = BlockPart.Begin });

					exceptionHandlingClauses.AddMulti(
						currentClause.HandlerOffset + currentClause.HandlerLength,
						new ExceptionHandlingClause() { Type = ExceptionHandlingClauseType.Finally, BlockPart = BlockPart.End });
					#endregion
				}
			}

			//Disassemble the method body
			foreach (var inst in methodInstructions)
			{
				//Handle exception clauses
				#region Exception Clauses
				if (exceptionHandlingClauses.ContainsKey(inst.Offset))
				{
					foreach (var clause in exceptionHandlingClauses[inst.Offset])
					{
						switch (clause.Type)
						{
							case ExceptionHandlingClauseType.Try:
								this.EmitBlockPart(outputWriter, clause.BlockPart, ".try");
								break;
							case ExceptionHandlingClauseType.Catch:
								var catchException = clause.CatchType != null ? GetAssemblyShortName(clause.CatchType.Assembly) : "";
								this.EmitBlockPart(outputWriter, clause.BlockPart, "catch " + "[" + catchException + "]" + clause.CatchType);
								break;
							case ExceptionHandlingClauseType.Filter:
								this.EmitBlockPart(outputWriter, clause.BlockPart, "filter");
								break;
							case ExceptionHandlingClauseType.FilterCatch:
								this.EmitBlockPart(outputWriter, clause.BlockPart);
								break;
							case ExceptionHandlingClauseType.Finally:
								this.EmitBlockPart(outputWriter, clause.BlockPart, "finally");
								break;
						}
					}
				}
				#endregion

				maxIndentationLevel = Math.Max(outputWriter.IndentationLevel, maxIndentationLevel);
				//int spacing = maxSpacing + 3 + ((maxIndentationLevel - outputWriter.IndentationLevel) * outputWriter.IndentationSize);
				int spacing = maxSpacing + 3;
				outputWriter.AppendLine(inst.ToString(spacing));
			}
		}

		/// <summary>
		/// Disassembles the given method or constructor
		/// </summary>
		/// <param name="method">The method</param>
		public string DisassembleMethod(MethodBase method)
		{
			var disassembledMethod = new OutputWriter(4);

			//Build the method signature
			disassembledMethod.Append(".method ");

			//The attributes for the method
			var attrs = Regex.Split(method.Attributes.ToString(), ", ")
				.Select(attr => attr.ToLower())
				.Where(attr => !attrIgnoreList.Contains(attr)).ToList();

			if (!method.IsStatic)
			{
				attrs.Add("instance");
			}

			if (method.IsVirtual)
			{
				attrs.Add("newslot");
			}
			
			disassembledMethod.Append(String.Join(" ", attrs) + " ");

			if (!method.IsConstructor && method.MemberType != MemberTypes.Constructor)
			{
				var retType = ((MethodInfo)method).ReturnType;
				var typeName = GetTypeName(method.DeclaringType.Assembly, retType, true);
				disassembledMethod.Append(GetTypeIdentifier(method.DeclaringType.Assembly, retType, addSpacing: true) + typeName + " ");
			}
			else
			{
				disassembledMethod.Append("void ");
			}

			disassembledMethod.Append(method.Name);

			//Add generic parameters
			if (method.IsGenericMethod)
			{		
				disassembledMethod.Append("<");
				disassembledMethod.Append(GetGenericParameters(method.DeclaringType.Assembly, method.GetGenericArguments()));
				disassembledMethod.Append(">");
			}
			
			//Add method parameters
			disassembledMethod.Append("(");
			disassembledMethod.Append(GetMethodParameters(method));

			var impFlags = new List<string>();

			if (method.MethodImplementationFlags == MethodImplAttributes.IL)
			{
				impFlags.Add("cil");
			}

			if (method.MethodImplementationFlags == MethodImplAttributes.Runtime)
			{
				impFlags.Add("runtime");
			}

			if (method.MethodImplementationFlags.HasFlag(MethodImplAttributes.Managed))
			{
				impFlags.Add("managed");
			}

			disassembledMethod.Append(")");
			disassembledMethod.Append(" " + String.Join(" ", impFlags));
			disassembledMethod.AppendLine();

			disassembledMethod.AppendLine("{");
			disassembledMethod.Indent();

			//Output the custom attributes
			foreach (var customAttr in method.CustomAttributes)
			{
				disassembledMethod.AppendLine(GetCustomAttribute(method.DeclaringType.Assembly, customAttr));
			}

			var methodBody = method.GetMethodBody();

			//Some methods doesn't have bodies
			if (methodBody != null)
			{
				this.DisassembleMethodBody(disassembledMethod, method, methodBody);
			}

			disassembledMethod.Unindent();
			disassembledMethod.AppendLine("}");

			return disassembledMethod.ToString();
		}

		/// <summary>
		/// Disasembles the given field
		/// </summary>
		/// <param name="field">The field</param>
		public string DissassembleField(FieldInfo field)
		{
			var disassembledField = new StringBuilder();
		
			var attrs = Regex.Split(field.Attributes.ToString(), ", ")
				.Select(attr => attr.ToLower())
				.Where(attr => !attrIgnoreList.Contains(attr)).ToList();

			if (field.DeclaringType.IsValueType)
			{
				attrs.Add("valuetype");
			}

			disassembledField.Append(".field");
			disassembledField.Append(" ");
			disassembledField.Append(String.Join(" ", attrs));
			disassembledField.Append(" ");
			disassembledField.Append(GetTypeName(field.DeclaringType.Assembly, field.FieldType, true));
			disassembledField.Append(" ");

			//Check if compiler generated
			if (!field.CustomAttributes.Any(attr => attr.AttributeType == typeof(CompilerGeneratedAttribute)))
			{
				disassembledField.Append(field.Name);
			}
			else
			{
				disassembledField.Append("'" + field.Name + "'");
			}

			if (field.IsLiteral)
			{
				if (field.DeclaringType.IsEnum)
				{
					var rawConstantValue = field.GetRawConstantValue();
					var enumType = rawConstantValue.GetType();
					disassembledField.Append(String.Format(" = {0}({1})", enumType.Name.ToLower(), rawConstantValue.ToString()));
				}
				else
				{
					disassembledField.Append(String.Format(" = {0}", field.GetRawConstantValue().ToString()));
				}
			}

			if (field.CustomAttributes.Count() > 0)
			{
				disassembledField.AppendLine();
			}

			//Output the custom attributes
			bool isFirst = true;
			foreach (var customAttr in field.CustomAttributes)
			{
				if (!isFirst)
				{
					disassembledField.AppendLine();
				}
				else
				{
					isFirst = false;
				}

				disassembledField.Append(GetCustomAttribute(field.DeclaringType.Assembly, customAttr));
			}

			return disassembledField.ToString();
		}

		/// <summary>
		/// Disassembles the given property
		/// </summary>
		/// <param name="property">The property</param>
		public string DissassembleProperty(PropertyInfo property)
		{
			var outputWriter = new OutputWriter(4);

			bool isStatic = property.CanRead ? property.GetMethod.IsStatic : property.SetMethod.IsStatic;
			bool isClassReturn = property.CanRead ? property.GetMethod.ReturnType.IsClass : false;
			List<string> attrs = new List<string>();

			if (!isStatic)
			{
				attrs.Add("instance");
			}

			if (isClassReturn)
			{
				attrs.Add("class");
			}

			string headerStr = " " + String.Join(" ", attrs) + " ";

			outputWriter.AppendLine(String.Format(".property{0}{1} {2}()",
				headerStr,
				GetTypeName(property.DeclaringType.Assembly, property.PropertyType, true),
				property.Name));

			outputWriter.AppendLine("{");
			outputWriter.Indent();

			//Output the custom attributes
			foreach (var customAttr in property.CustomAttributes)
			{
				outputWriter.AppendLine(GetCustomAttribute(property.DeclaringType.Assembly, customAttr));
			}

			if (property.CanRead)
			{
				outputWriter.AppendLine(String.Format(".get{0}{1} {2}()",
					headerStr,
					GetTypeName(property.DeclaringType.Assembly, property.GetMethod.ReturnType, true),
					property.GetMethod.DeclaringType + "::" + property.GetMethod.Name));
			}

			if (property.CanWrite)
			{
				outputWriter.AppendLine(String.Format(".set{0}{1} {2}({3})",
					headerStr,
					GetTypeName(property.DeclaringType.Assembly, property.SetMethod.ReturnType, true),
					property.SetMethod.DeclaringType + "::" + property.SetMethod.Name,
					String.Join(", ", property.SetMethod.GetParameters().Select(arg =>
						GetTypeName(property.DeclaringType.Assembly, arg.ParameterType, true)))));
			}

			outputWriter.Unindent();
			outputWriter.AppendLine("}");

			return outputWriter.ToString();
		}

		/// <summary>
		/// Disassembles the given event
		/// </summary>
		/// <param name="eventInfo">The event</param>
		public string DissassembleEvent(EventInfo eventInfo)
		{
			var outputWriter = new OutputWriter(4);

			List<string> attrs = new List<string>();
			
			if (!eventInfo.AddMethod.IsStatic)
			{
				attrs.Add("instance");
			}

			outputWriter.AppendLine(String.Format(".event {0} {1}",
				GetTypeName(eventInfo.DeclaringType.Assembly, eventInfo.EventHandlerType, true),
				eventInfo.Name));

			outputWriter.AppendLine("{");
			outputWriter.Indent();

			//Output the custom attributes
			foreach (var customAttr in eventInfo.CustomAttributes)
			{
				outputWriter.AppendLine(GetCustomAttribute(eventInfo.DeclaringType.Assembly, customAttr));
			}

			outputWriter.AppendLine(String.Format(".addon{0}void {1}({2})",
				(attrs.Count > 0 ? " ": "") + String.Join(" ", attrs) + " ",
				eventInfo.DeclaringType.Name + "::" + eventInfo.AddMethod.Name,
				GetTypeName(eventInfo.DeclaringType.Assembly, eventInfo.EventHandlerType, true)));

			outputWriter.AppendLine(String.Format(".removeon{0}void {1}({2})",
				(attrs.Count > 0 ? " " : "") + String.Join(" ", attrs) + " ",
				eventInfo.DeclaringType.Name + "::" + eventInfo.RemoveMethod.Name,
				GetTypeName(eventInfo.DeclaringType.Assembly, eventInfo.EventHandlerType, true)));

			outputWriter.Unindent();
			outputWriter.AppendLine("}");

			return outputWriter.ToString();
		}

		/// <summary>
		/// Disassembles the given type
		/// </summary>
		/// <param name="type">The type</param>
		/// <returns>The disassembled type</returns>
		public DisassembledType Disassemble(Type type)
		{
			var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
			var typeConstructors = type.GetConstructors(bindingFlags).Cast<MethodBase>();
			var typeFields = type.GetFields(bindingFlags);
			var typeProperties = type.GetProperties(bindingFlags);
			var typeEvents = type.GetEvents(bindingFlags);
			var typeMethods = type.GetMethods(bindingFlags);

			var disassembledFields = new List<string>();
			var disassembledProperties = new List<string>();
			var disassembledEvents = new List<string>();
			var disassembledMethods = new List<string>();
			
			foreach (var field in typeFields)
			{
				disassembledFields.Add(this.DissassembleField(field));
			}

			foreach (var property in typeProperties)
			{
				disassembledProperties.Add(this.DissassembleProperty(property));
			}

			foreach (var eventRef in typeEvents)
			{
				disassembledEvents.Add(this.DissassembleEvent(eventRef));
			}

			foreach (var method in typeMethods.Concat(typeConstructors))
			{
				bool correctMethodImplementation = method.MethodImplementationFlags == MethodImplAttributes.IL
					|| method.MethodImplementationFlags == MethodImplAttributes.Runtime;

				if (correctMethodImplementation && method.DeclaringType == type)
				{
					disassembledMethods.Add(this.DisassembleMethod(method));
				}
			}

			string typeHeader = this.DisassembleTypeHeader(type);

			return new DisassembledType(
				type, 
				typeHeader,
				ImmutableList.CreateRange(disassembledFields),
				ImmutableList.CreateRange(disassembledProperties),
				ImmutableList.CreateRange(disassembledEvents),
				ImmutableList.CreateRange(disassembledMethods));
		}
		#endregion

		#endregion

	}
}
