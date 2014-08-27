using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILDisassembler
{
	/// <summary>
	/// Represents a disassembled type
	/// </summary>
	public sealed class DisassembledType
	{
		/// <summary>
		/// The type
		/// </summary>
		public Type Type { get; private set; }

		/// <summary>
		/// The type header
		/// </summary>
		public string TypeHeader { get; private set; }

		/// <summary>
		/// Returns the fields
		/// </summary>
		public IImmutableList<string> Fields { get; private set; }

		/// <summary>
		/// Returns the properties
		/// </summary>
		public IImmutableList<string> Properties { get; private set; }

		/// <summary>
		/// Returns the events
		/// </summary>
		public IImmutableList<string> Events { get; private set; }

		/// <summary>
		/// Returns the methods
		/// </summary>
		public IImmutableList<string> Methods { get; private set; }

		/// <summary>
		/// Creates a new disassembled type
		/// </summary>
		/// <param name="type">The type</param>
		/// <param name="typeHeader">The type header</param>
		/// <param name="fields">The fields</param>
		/// <param name="properties">The properties</param>
		/// <param name="events">The events</param>
		/// <param name="methods">The methods</param>
		public DisassembledType(Type type, string typeHeader,
			IImmutableList<string> fields, IImmutableList<string> properties, IImmutableList<string> events, IImmutableList<string> methods)
		{
			this.Type = type;
			this.TypeHeader = typeHeader;
			this.Fields = fields;
			this.Properties = properties;
			this.Events = events;
			this.Methods = methods;
		}

		/// <summary>
		/// Returns the disassembled type as a string
		/// </summary>
		public override string ToString()
		{
			var disassembledType = new StringBuilder();

			disassembledType.Append(this.TypeHeader);
			disassembledType.AppendLine("\r\n");

			foreach (var field in this.Fields)
			{
				disassembledType.Append(field);
				disassembledType.AppendLine("\r\n");
			}

			foreach (var property in this.Properties)
			{
				disassembledType.Append(property);
				disassembledType.AppendLine("\r\n");
			}

			foreach (var eventRef in this.Events)
			{
				disassembledType.Append(eventRef);
				disassembledType.AppendLine("\r\n");
			}

			bool isFirst = true;

			foreach (var method in this.Methods)
			{
				if (!isFirst)
				{
					disassembledType.AppendLine("\r\n");
				}

				disassembledType.Append(method);
				isFirst = false;
			}

			return disassembledType.ToString();
		}
	}
}
