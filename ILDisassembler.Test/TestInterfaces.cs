using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ILDisassembler.Test
{
	/// <summary>
	/// Tests interfaces
	/// </summary>
	[TestClass]
	public class TestInterfaces
	{
		private Disassembler disassembler;
		private Type talkableType;
		private Type customListType;

		[TestInitialize]
		public void Initialize()
		{
			this.disassembler = new Disassembler();
			this.talkableType = typeof(ITalkable);
			this.customListType = typeof(ICustomList);
		}

		[TestMethod]
		public void TestDisassembleTypeHeaderTalkable()
		{
			var expectedTypeHeader = "";
			expectedTypeHeader += ".class interface public abstract auto ansi ILDisassembler.Test.ITalkable\n";
			expectedTypeHeader += "{\n";
			expectedTypeHeader += "}";

			var actualTypeHeader = this.disassembler.DisassembleTypeHeader(this.talkableType);

			GeneratedILComparer.AssertTypeHeaders(expectedTypeHeader, actualTypeHeader);
		}

		[TestMethod]
		public void TestDisassembleTypeHeaderCustomList()
		{
			var expectedTypeHeader = "";
			expectedTypeHeader += ".class interface public abstract auto ansi ILDisassembler.Test.ICustomList\n";
			expectedTypeHeader += "implements [mscorlib]System.Collections.IList, [mscorlib]System.Collections.ICollection, [mscorlib]System.Collections.IEnumerable\n";
			expectedTypeHeader += "{\n";
			expectedTypeHeader += "}";

			var actualTypeHeader = this.disassembler.DisassembleTypeHeader(this.customListType);

			GeneratedILComparer.AssertTypeHeaders(expectedTypeHeader, actualTypeHeader);
		}
	}
}
