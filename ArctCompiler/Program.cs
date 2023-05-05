using System.ComponentModel;
using System.Diagnostics;
using LLVMSharp;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace ArctCompiler
{
    class ArctCompiler
    {
        class Arct
        {
            public unsafe Arct()
            {
                
                LLVM.InitializeX86TargetMC();
                LLVM.InitializeX86Target();
                LLVM.InitializeX86TargetInfo();
                LLVM.InitializeX86AsmParser();
                LLVM.InitializeX86AsmPrinter();

                String input = File.ReadAllText(@"C:\Users\Redmi\Documents\Programming\C#\CourseCompiler\ArctCompiler\paste.txt");
                ICharStream stream = CharStreams.fromString(input);
                ITokenSource lexer = new arctLexer(stream);
                ITokenStream tokens = new CommonTokenStream(lexer);
                arctParser parser = new arctParser(tokens);
                parser.BuildParseTree = true;
                IParseTree tree = parser.program();

                OverrideListener printer = new OverrideListener();
                ParseTreeWalker.Default.Walk(printer, tree);
                LLVMModuleRef mod = printer.Module;
           
                LLVMBool Success = new LLVMBool(0);
               
                //
                // LLVMPassManagerRef passManager = LLVM.CreateFunctionPassManagerForModule(mod);
                // LLVM.AddPromoteMemoryToRegisterPass(passManager);
                // LLVM.AddDeadStoreEliminationPass(passManager);
                // LLVM.InitializeFunctionPassManager(passManager);
                // LLVMValueRef? function = LLVM.GetFirstFunction(mod);
                // while (function.ToString() != "Printing <null> Value")
                // {
                //    
                //     LLVM.RunFunctionPassManager(passManager,(LLVMValueRef) function);
                //     function = LLVM.GetNextFunction((LLVMValueRef)function);
                // }
                // LLVM.FinalizeFunctionPassManager(passManager);
                // LLVM.DisposePassManager(passManager);
                //
                //
               
               
                
              
                // LLVM.AddConstantMergePass(passManager);
                // LLVM.AddDeadArgEliminationPass(passManager);
                // // LLVM.AddFunctionAttrsPass(passManager);
                // // LLVM.AddFunctionInliningPass(passManager);
                // LLVM.AddGlobalDCEPass(passManager);
                // LLVM.AddGlobalOptimizerPass(passManager);
                // LLVM.AddIPSCCPPass(passManager);
                // LLVM.AddDeadStoreEliminationPass(passManager);
                // LLVM.AddCFGSimplificationPass(passManager);
                // LLVM.AddGVNPass(passManager);
                // LLVM.AddInstructionCombiningPass(passManager);
               
                // LLVM.PassManagerBuilderPopulateFunctionPassManager(passManagerBuilder,passManager);
                // LLVM.RunPassManager(passManager, mod);
                // LLVM.DisposePassManager(passManager);
                // LLVM.DisposeModule(mod);
                
                LLVM.DumpModule(mod);


                
               var passManagerBuilder = LLVM.PassManagerBuilderCreate();
               LLVM.PassManagerBuilderSetOptLevel(passManagerBuilder, 2);
               var passManager = LLVM.CreatePassManager();
               LLVMValueRef? function = LLVM.GetFirstFunction(mod);
               while (function.ToString() != "Printing <null> Value")
               {
                  Console.WriteLine(function.ToString());
                   LLVM.RunFunctionPassManager(passManager,(LLVMValueRef) function);
                   function = LLVM.GetNextFunction((LLVMValueRef)function);
               }
               
               LLVM.FinalizeFunctionPassManager(passManager);
               
               LLVM.AddPromoteMemoryToRegisterPass(passManager);
               LLVM.AddDeadStoreEliminationPass(passManager);
               LLVM.InitializeFunctionPassManager(passManager);
               LLVM.AddConstantMergePass(passManager);
               LLVM.AddDeadArgEliminationPass(passManager);
               LLVM.AddFunctionAttrsPass(passManager);
               LLVM.AddFunctionInliningPass(passManager);
               LLVM.AddGlobalDCEPass(passManager);
               LLVM.AddGlobalOptimizerPass(passManager);
               LLVM.AddIPSCCPPass(passManager);
               LLVM.AddDeadStoreEliminationPass(passManager);
               LLVM.AddCFGSimplificationPass(passManager);
               LLVM.AddGVNPass(passManager);
               LLVM.AddInstructionCombiningPass(passManager);
               LLVM.AddLICMPass(passManager);
               LLVM.AddSCCPPass(passManager);
               LLVM.AddTypeBasedAliasAnalysisPass(passManager);
               LLVM.AddBasicAliasAnalysisPass(passManager);
               
               
               LLVM.PassManagerBuilderPopulateModulePassManager(passManagerBuilder, passManager);
               
               LLVM.PassManagerBuilderDispose(passManagerBuilder);
               LLVM.RunPassManager(passManager, mod);
               
               
               LLVM.DumpModule(mod);
               
               if (LLVM.VerifyModule(mod, LLVMVerifierFailureAction.LLVMPrintMessageAction, out var error) != Success)
               {
                   Console.WriteLine($"Error: {error}");
               }

               
                if (LLVM.GetTargetFromTriple("x86_64-pc-win32", out var target, out error) == Success)
                {
                    var targetMachine = LLVM.CreateTargetMachine(target, "x86_64-pc-windows-msvc", "generic", "",
                        LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault, LLVMRelocMode.LLVMRelocDefault,
                        LLVMCodeModel.LLVMCodeModelDefault);
                    var dl = LLVM.CreateTargetDataLayout(targetMachine);
                    LLVM.SetModuleDataLayout(mod, dl);
                    LLVM.SetTarget(mod, "x86_64-pc-win32");
                    byte[] buffer = System.Text.Encoding.Default.GetBytes("test.o\0");

                    fixed (byte* ptr = buffer)
                    {
                        LLVM.TargetMachineEmitToFile(targetMachine, mod, new IntPtr(ptr),
                            LLVMCodeGenFileType.LLVMObjectFile, out error);
                    }
                }
                // LLVM.DisposePassManager(passManager);
                Process process = new Process();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = "/c " + "clang test.o -o hello.exe ";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                // Start the process and read the output
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                
                
                // Write the output to the console
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = "/c " + "hello";
                
                process.Start();
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                Console.WriteLine("---------------------------------");
                Console.WriteLine(output);
            }
        }

        class acrtLLVM
        {
            public unsafe acrtLLVM()
            {
                LLVMBool Success = new LLVMBool(0);
                LLVMModuleRef mod = LLVM.ModuleCreateWithName("LLVMSharpIntro");

                LLVMTypeRef[] param_types = { LLVM.Int32Type(), LLVM.Int32Type() };
                LLVMTypeRef ret_type = LLVM.FunctionType(LLVM.Int32Type(), param_types, false);
                LLVMValueRef sum = LLVM.AddFunction(mod, "main", ret_type);

                LLVMBasicBlockRef entry = LLVM.AppendBasicBlock(sum, "entry");

                LLVMBuilderRef builder = LLVM.CreateBuilder();
                LLVM.PositionBuilderAtEnd(builder, entry);
                LLVMValueRef tmp = LLVM.BuildAdd(builder, LLVM.GetParam(sum, 0), LLVM.GetParam(sum, 1), "tmp");
                LLVM.BuildRet(builder, tmp);
                LLVM.SetLinkage(sum, LLVMLinkage.LLVMExternalLinkage);
                LLVM.SetDLLStorageClass(sum, LLVMDLLStorageClass.LLVMDLLExportStorageClass);

                if (LLVM.VerifyModule(mod, LLVMVerifierFailureAction.LLVMPrintMessageAction, out var error) != Success)
                {
                    Console.WriteLine($"Error: {error}");
                }

                LLVM.InitializeX86TargetMC();
                LLVM.InitializeX86Target();
                LLVM.InitializeX86TargetInfo();
                LLVM.InitializeX86AsmParser();
                LLVM.InitializeX86AsmPrinter();

                var aa = LLVM.GetLinkage(sum);
                LLVM.DumpModule(mod);
                if (LLVM.GetTargetFromTriple("x86_64-pc-win32", out var target, out error) == Success)
                {
                    var targetMachine = LLVM.CreateTargetMachine(target, "x86_64-pc-windows-msvc", "generic", "",
                        LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault, LLVMRelocMode.LLVMRelocDefault,
                        LLVMCodeModel.LLVMCodeModelDefault);
                    var dl = LLVM.CreateTargetDataLayout(targetMachine);
                    LLVM.SetModuleDataLayout(mod, dl);
                    LLVM.SetTarget(mod, "x86_64-pc-win32");
                    byte[] buffer = System.Text.Encoding.Default.GetBytes("test.o\0");

                    fixed (byte* ptr = buffer)
                    {
                        LLVM.TargetMachineEmitToFile(targetMachine, mod, new IntPtr(ptr),
                            LLVMCodeGenFileType.LLVMObjectFile, out error);
                    }
                }
                
                Process process = new Process();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = "/c " + "clang test.o -o hello.exe ";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                // Start the process and read the output
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                // Write the output to the console
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = "/c " + "hello";
                process.Start();
                Console.WriteLine(output);
            }
        }

    public static void Main(string[] args)
    {
        Arct comp = new Arct();
        //acrtLLVM arctLlvm = new acrtLLVM(); 
    }
}

}