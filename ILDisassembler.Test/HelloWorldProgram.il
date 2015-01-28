.class public auto ansi beforefieldinit ILDisassembler.Test.HelloWorldProgram
       extends [mscorlib]System.Object
{
} // end of class ILDisassembler.Test.HelloWorldProgram

.method public hidebysig specialname rtspecialname 
        instance void  .ctor() cil managed
{
  // Code size       7 (0x7)
  .maxstack  8
  IL_0000:  ldarg.0
  IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
  IL_0006:  ret
} // end of method HelloWorldProgram::.ctor

.method private hidebysig static void  Main(string[] args) cil managed
{
  // Code size       13 (0xd)
  .maxstack  8
  IL_0000:  nop
  IL_0001:  ldstr      "Hello, World!"
  IL_0006:  call       void [mscorlib]System.Console::WriteLine(string)
  IL_000b:  nop
  IL_000c:  ret
} // end of method HelloWorldProgram::Main