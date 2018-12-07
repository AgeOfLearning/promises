msbuild /p:Configuration=Debug ./Promises/Promises.sln 
dotnet vstest ./Promises/Promises.Tests/bin/Debug/Promises.Tests.dll /InIsolation /logger:trx