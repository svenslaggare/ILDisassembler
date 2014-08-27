ILDisassembler
==============

An IL disassembler written using the .NET reflection API
which allows the library to be embedded in .NET applications.
The method body disassembler is based on [Mono.Reflection](https://github.com/jbevain/mono.reflection/).

<h2>Difference from ildasm.exe</h2>
* Instead of raw data for custom attribute constructors, uses the "real" value.
* Generics are handled different, for example the generic argument position with "!" are omitted.

<h2>Limitations</h2>
Because the library is written using reflection, some features available in *ildasm.exe*
is not available.
* Unmanaged code.
* Raw constructor data.
