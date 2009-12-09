To build ObjectCloud, build the entire Server/ObjectCloud.sln solution.

Note:  In MonoDevelop, make sure the build (and run) ObjectCloud.CodeGenerator.exe first

Regarding the unit tests:
The "UnitTests" project collects all of the unit tests into a single location for easy running in NUnit.
There are occasional failures with sending a large binary HTTP payload.  This will be addressed in an upcoming release