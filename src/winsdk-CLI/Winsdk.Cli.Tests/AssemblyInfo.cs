using Microsoft.VisualStudio.TestTools.UnitTesting;

// Configure test parallelization at assembly level
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
