using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ILDisassembler.Test
{
	/// <summary>
	/// Tests the ILDisassembler class using the HelloWorldProgram class
	/// </summary>
	[TestClass]
	public class HelloWorldProgramTests
	{
		private Disassembler disassembler;
		private Type helloWorldProgramType;

		[TestInitialize]
		public void Initialize()
		{
			this.disassembler = new Disassembler();
			this.helloWorldProgramType = typeof(HelloWorldProgram);
		}

		[TestMethod]
		public void TestDisassembleTypeHeader()
		{
			var expectedTypeHeader = "";
			expectedTypeHeader += ".class public auto ansi beforefieldinit ILDisassembler.Test.HelloWorldProgram\n";
			expectedTypeHeader += "extends [mscorlib]System.Object\n";
			expectedTypeHeader += "{\n";
			expectedTypeHeader += "}";

			var actualTypeHeader = this.disassembler.DisassembleTypeHeader(this.helloWorldProgramType);

			GeneratedILComparer.AssertTypeHeaders(expectedTypeHeader, actualTypeHeader);
		}
	}
}
