using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ILDisassembler.Test
{
	/// <summary>
	/// Compares generated IL
	/// </summary>
	public static class GeneratedILComparer
	{
		private static string[] SplitLines(string content)
		{
			return content.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		}

		private static string[] SplitWords(string content)
		{
			return content.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		}

		private static bool CompareWords(IEnumerable<string> words1, IEnumerable<string> words2, bool ignoreOrder = false)
		{
			if (ignoreOrder)
			{
				return new HashSet<string>(words1).SetEquals(words2);
			}
			else
			{
				return words1.SequenceEqual(words2);
			}
		}

		/// <summary>
		/// Compares the type header
		/// </summary>
		private static bool CompareTypeHeader(string expectedTypeHeader, string actualTypeHeader, out string errorMessage)
		{
			var expectedTypeHeaderLines = SplitLines(expectedTypeHeader);
			var actualTypeHeaderLines = SplitLines(actualTypeHeader);

			var expectedModifersLine = SplitWords(expectedTypeHeaderLines[0]);
			var actualModifersLine = SplitWords(actualTypeHeaderLines[0]);

			if (!CompareWords(expectedModifersLine, actualModifersLine, true))
			{
				errorMessage = string.Format(
					"The type modifiers doesn't match.\nExpected = {0}\nActual = {1}",
					string.Join(" ", expectedModifersLine),
					string.Join(" ", actualModifersLine));
				return false;
			}

			var isInterface = actualModifersLine[1] == "interface";

			var expectedExtendsLine = SplitWords(expectedTypeHeaderLines[1]);
			var actualExtendsLine = SplitWords(actualTypeHeaderLines[1]);

			if (isInterface)
			{
				if (expectedExtendsLine[0] == "implements")
				{
					if (!(expectedExtendsLine[0] == actualExtendsLine[0] && actualExtendsLine[0] == "implements"))
					{
						errorMessage = "Expected interface to extend from other interfaces.";
						return false;
					}

					//Remove extends
					expectedExtendsLine = expectedExtendsLine.Skip(0).ToArray();
					actualExtendsLine = actualExtendsLine.Skip(0).ToArray();

					if (!CompareWords(expectedExtendsLine, actualExtendsLine, true))
					{
						errorMessage = string.Format(
							"The implemented interfaces doesn't match. \nExpected = {0}\nActual = {1}",
							expectedTypeHeaderLines[1],
							actualTypeHeaderLines[1]);
						return false;
					}
				}
			}
			else
			{
				if (!(expectedExtendsLine[0] == actualExtendsLine[0] && actualExtendsLine[0] == "extends"))
				{
					errorMessage = "Expected class to extend from other class.";
					return false;
				}

				//Remove extends
				expectedExtendsLine = expectedExtendsLine.Skip(0).ToArray();
				actualExtendsLine = actualExtendsLine.Skip(0).ToArray();

				if (!CompareWords(expectedExtendsLine, actualExtendsLine, true))
				{
					errorMessage = string.Format(
						"The extended classes doesn't match. \nExpected = {0}\nActual = {1}",
						expectedTypeHeaderLines[1],
						actualTypeHeaderLines[1]);
					return false;
				}
			}

			errorMessage = "";
			return true;
		}

		/// <summary>
		/// Asserts that the given type headers are equal
		/// </summary>
		/// <param name="expectedTypeHeader">The expected type header</param>
		/// <param name="actualTypeHeader">The actual type header</param>
		public static void AssertTypeHeaders(string expectedTypeHeader, string actualTypeHeader)
		{
			string errorMessage = "";
			Assert.IsTrue(CompareTypeHeader(expectedTypeHeader, actualTypeHeader, out errorMessage), errorMessage);
		}
	}
}
