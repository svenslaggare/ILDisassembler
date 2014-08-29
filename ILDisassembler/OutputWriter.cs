using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILDisassembler
{
	/// <summary>
	/// Represents an output writer
	/// </summary>
	internal sealed class OutputWriter
	{
		private StringBuilder stringBuilder;
		private int indentationSize;
		private string indendationStr = "";

		/// <summary>
		/// Creates a new output writer
		/// </summary>
		/// <param name="indentationSize">The indentation size</param>
		public OutputWriter(int indentationSize)
		{
			this.stringBuilder = new StringBuilder();
			this.indentationSize = indentationSize;
		}

		/// <summary>
		/// Increases the indentation one level
		/// </summary>
		public void Indent()
		{
			for (int i = 0; i < this.indentationSize; i++)
			{
				indendationStr += " ";
			}
		}

		/// <summary>
		/// Decreases the indentation one level
		/// </summary>
		public void Unindent()
		{
			indendationStr = indendationStr.Substring(0, this.indendationStr.Length - this.indentationSize);
		}

		/// <summary>
		/// Appends a new line with indentation to the buffer
		/// </summary>
		/// <param name="str">The string to append</param>
		public void AppendLine(string str = "")
		{
			this.stringBuilder.AppendLine(this.indendationStr + str);
		}

		/// <summary>
		/// Appends indentation to the buffer
		/// </summary>
		public void AppendIndentation()
		{
			this.stringBuilder.Append(this.indendationStr);
		}

		/// <summary>
		/// Appends the given string to the buffer
		/// </summary>
		/// <param name="str">The string to append</param>
		/// <remarks>The string is appended without indentation</remarks>
		public void Append(string str)
		{
			this.stringBuilder.Append(str);
		}

		/// <summary>
		/// Returns the built string
		/// </summary>
		public override string ToString()
		{
			return this.stringBuilder.ToString().TrimEnd('\r', '\n');
			//return this.stringBuilder.ToString();
		}
	}

}
