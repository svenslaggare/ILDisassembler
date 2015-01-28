using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILDisassembler.Test
{
	public class HelloWorldProgram
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Hello, World!");
		}
	}

	public interface ITalkable
	{
		void talk(string message);
	}

	public interface ICustomList : System.Collections.IList
	{
		
	}
}
